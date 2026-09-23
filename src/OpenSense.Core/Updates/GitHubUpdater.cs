using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace OpenSense.Core.Updates;

/// <summary>How this copy of OpenSense was deployed, which decides what an update downloads.</summary>
public enum InstallKind
{
    /// <summary>Installed by setup: updated by running the next setup.</summary>
    Installer,

    /// <summary>Extracted from the portable zip: updated by replacing its files.</summary>
    Portable,
}

/// <param name="Repository">GitHub "owner/name".</param>
/// <param name="CurrentVersion">The running version (major.minor.patch).</param>
public sealed record UpdaterOptions(string Repository, Version CurrentVersion);

public sealed record ReleaseAsset(string Name, Uri DownloadUrl, Uri ApiUrl, long Size, string? Digest);

/// <summary>A release newer than the running version, and the file this copy updates from (null if it has none).</summary>
public sealed record AvailableUpdate(Version Version, string Tag, string Name, Uri ReleasePage, DateTimeOffset? PublishedAt, ReleaseAsset? Asset);

/// <summary>The downloaded file does not match the SHA-256 digest GitHub published for it.</summary>
public sealed class UpdateVerificationException(string message) : Exception(message);

/// <summary>
/// Finds newer OpenSense releases on GitHub and downloads the matching installer or portable zip, verified
/// against the SHA-256 digest GitHub publishes for every release asset.
/// </summary>
public sealed partial class GitHubUpdater(HttpClient http, UpdaterOptions options, ILogger<GitHubUpdater> log)
{
    private const int BufferSize = 81920;

    public Version CurrentVersion => options.CurrentVersion;

    /// <summary>The latest stable release if it is newer than this one; drafts and pre-releases are ignored.</summary>
    public async Task<AvailableUpdate?> CheckAsync(InstallKind kind, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(new Uri($"https://api.github.com/repos/{options.Repository}/releases/latest"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            LogNoReleases(options.Repository);
            return null;
        }
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync(GitHubJson.Default.GitHubRelease, cancellationToken).ConfigureAwait(false);
        if (release is null || release.Draft || release.Prerelease || !TryParseVersion(release.TagName, out var version))
            return null;
        LogLatest(release.TagName, options.CurrentVersion);
        if (version <= options.CurrentVersion)
            return null;

        return new AvailableUpdate(version, release.TagName, string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            release.HtmlUrl, release.PublishedAt, SelectAsset(release.Assets, kind, RuntimeInformation.ProcessArchitecture));
    }

    /// <summary>Downloads <paramref name="asset"/> to <paramref name="path"/> and checks its SHA-256 digest.</summary>
    /// <exception cref="UpdateVerificationException">No digest was published, or the file does not match it.</exception>
    public async Task DownloadAsync(ReleaseAsset asset, string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Older API responses may omit the digest on the release listing; the asset itself always has it.
        var digest = asset.Digest
            ?? (await http.GetFromJsonAsync(asset.ApiUrl, GitHubJson.Default.GitHubAsset, cancellationToken).ConfigureAwait(false))?.Digest;
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            throw new UpdateVerificationException($"GitHub published no SHA-256 digest for {asset.Name}, so it cannot be verified.");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var response = await http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? asset.Size;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            var buffer = new byte[BufferSize];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                received += read;
                if (total > 0)
                    progress?.Report(Math.Min(1, (double)received / total));
            }
        }

        var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
        var expected = digest["sha256:".Length..].ToLowerInvariant();
        if (actual != expected)
        {
            File.Delete(path);
            throw new UpdateVerificationException($"{asset.Name} does not match its published SHA-256 digest (expected {expected}, got {actual}).");
        }
        LogDownloaded(asset.Name, path);
    }

    /// <summary>The installer or portable zip for this architecture, by the names CI gives them.</summary>
    public static ReleaseAsset? SelectAsset(IEnumerable<GitHubAsset> assets, InstallKind kind, Architecture architecture)
    {
        var arch = architecture == Architecture.Arm64 ? "arm64" : "x64";
        var suffix = kind == InstallKind.Installer ? $"-Setup-{arch}.exe" : $"-Portable-{arch}.zip";
        var asset = assets.FirstOrDefault(a => a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        return asset is null ? null : new ReleaseAsset(asset.Name, asset.BrowserDownloadUrl, asset.Url, asset.Size, asset.Digest);
    }

    /// <summary>"v1.2.3" or "1.2.3" (no pre-release suffix) as a three-part version.</summary>
    public static bool TryParseVersion(string tag, out Version version)
    {
        version = new Version();
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed) || parsed.Build < 0 || parsed.Revision > 0)
            return false;
        version = new Version(parsed.Major, parsed.Minor, parsed.Build);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No releases published yet for {Repository}")]
    private partial void LogNoReleases(string repository);

    [LoggerMessage(Level = LogLevel.Information, Message = "Latest release is {Tag}; running {Current}")]
    private partial void LogLatest(string tag, Version current);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded and verified {Asset} to {Path}")]
    private partial void LogDownloaded(string asset, string path);
}

public sealed record GitHubRelease(
    string TagName,
    string? Name,
    Uri HtmlUrl,
    bool Draft,
    bool Prerelease,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubAsset> Assets);

public sealed record GitHubAsset(string Name, Uri Url, Uri BrowserDownloadUrl, long Size, string? Digest);

/// <summary>GitHub REST API models (snake_case), source-generated.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
internal sealed partial class GitHubJson : JsonSerializerContext;
