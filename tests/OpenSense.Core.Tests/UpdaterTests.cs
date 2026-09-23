using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Updates;

namespace OpenSense.Core.Tests;

public sealed class UpdaterTests : IDisposable
{
    private static readonly byte[] SetupBytes = Encoding.UTF8.GetBytes("pretend installer");
    private static readonly string SetupDigest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(SetupBytes));

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("v1.2.3", true, "1.2.3")]
    [InlineData("0.4.0", true, "0.4.0")]
    [InlineData("v1.2", false, null)]
    [InlineData("v1.2.3-beta", false, null)]
    [InlineData("nightly", false, null)]
    public void Tags_parse_as_three_part_versions(string tag, bool ok, string? expected)
    {
        Assert.Equal(ok, GitHubUpdater.TryParseVersion(tag, out var version));
        if (ok)
            Assert.Equal(Version.Parse(expected!), version);
    }

    [Fact]
    public async Task Newer_release_offers_the_installer_for_installed_copies()
    {
        var update = await Updater(Release("v0.2.0")).CheckAsync(InstallKind.Installer, TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 2, 0), update.Version);
        Assert.Equal("OpenSense-0.2.0-Setup-x64.exe", update.Asset!.Name);
    }

    [Fact]
    public async Task Portable_copies_get_the_zip()
    {
        var update = await Updater(Release("v0.2.0")).CheckAsync(InstallKind.Portable, TestContext.Current.CancellationToken);

        Assert.Equal("OpenSense-0.2.0-Portable-x64.zip", update!.Asset!.Name);
    }

    [Theory]
    [InlineData("v0.1.0")] // same version
    [InlineData("v0.0.9")] // older
    public async Task Same_or_older_release_is_not_an_update(string tag) =>
        Assert.Null(await Updater(Release(tag)).CheckAsync(InstallKind.Installer, TestContext.Current.CancellationToken));

    [Fact]
    public async Task Repository_without_releases_is_not_an_error() =>
        Assert.Null(await Updater(null).CheckAsync(InstallKind.Installer, TestContext.Current.CancellationToken));

    [Fact]
    public void Arm64_machines_pick_arm64_files()
    {
        GitHubAsset[] assets =
        [
            Asset("OpenSense-0.2.0-Setup-x64.exe", null),
            Asset("OpenSense-0.2.0-Setup-arm64.exe", null),
        ];
        Assert.Equal("OpenSense-0.2.0-Setup-arm64.exe", GitHubUpdater.SelectAsset(assets, InstallKind.Installer, Architecture.Arm64)!.Name);
    }

    [Fact]
    public async Task Download_is_verified_against_the_published_digest()
    {
        var updater = Updater(Release("v0.2.0"));
        var update = await updater.CheckAsync(InstallKind.Installer, TestContext.Current.CancellationToken);
        var path = Path.Combine(_directory, "setup.exe");
        var progress = new List<double>();

        await updater.DownloadAsync(update!.Asset!, path, new SynchronousProgress(progress.Add), TestContext.Current.CancellationToken);

        Assert.Equal(SetupBytes, File.ReadAllBytes(path));
        Assert.Equal(1.0, progress[^1]);
    }

    [Fact]
    public async Task Tampered_download_is_rejected_and_deleted()
    {
        var updater = Updater(Release("v0.2.0", setupDigest: "sha256:" + new string('0', 64)));
        var update = await updater.CheckAsync(InstallKind.Installer, TestContext.Current.CancellationToken);
        var path = Path.Combine(_directory, "setup.exe");

        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            updater.DownloadAsync(update!.Asset!, path, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Missing_digest_is_fetched_from_the_asset()
    {
        var updater = Updater(Release("v0.2.0", setupDigest: null));
        var update = await updater.CheckAsync(InstallKind.Installer, TestContext.Current.CancellationToken);

        await updater.DownloadAsync(update!.Asset!, Path.Combine(_directory, "setup.exe"), cancellationToken: TestContext.Current.CancellationToken);
    }

    private static GitHubUpdater Updater(string? releaseJson) =>
        new(new HttpClient(new FakeGitHub(releaseJson)), new UpdaterOptions("owner/opensense", new Version(0, 1, 0)), NullLogger<GitHubUpdater>.Instance);

    private static GitHubAsset Asset(string name, string? digest) =>
        new(name, new Uri($"https://api.github.com/assets/{name}"), new Uri($"https://github.com/download/{name}"), SetupBytes.Length, digest);

    private static string Release(string tag, string? setupDigest = "published") => $$"""
        {
          "tag_name": "{{tag}}", "name": "OpenSense {{tag}}", "html_url": "https://github.com/owner/opensense/releases/tag/{{tag}}",
          "draft": false, "prerelease": false, "published_at": "2026-10-01T12:00:00Z",
          "assets": [
            { "name": "OpenSense-{{tag.TrimStart('v')}}-Setup-x64.exe", "url": "https://api.github.com/assets/setup",
              "browser_download_url": "https://github.com/download/setup", "size": {{SetupBytes.Length}},
              "digest": {{(setupDigest is null ? "null" : $"\"{(setupDigest == "published" ? SetupDigest : setupDigest)}\"")}} },
            { "name": "OpenSense-{{tag.TrimStart('v')}}-Portable-x64.zip", "url": "https://api.github.com/assets/zip",
              "browser_download_url": "https://github.com/download/zip", "size": 1, "digest": "sha256:00" }
          ]
        }
        """;

    /// <summary>Answers the three URLs the updater uses; 404 for a repository without releases.</summary>
    private sealed class FakeGitHub(string? releaseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            HttpResponseMessage response = url switch
            {
                _ when url.EndsWith("/releases/latest", StringComparison.Ordinal) && releaseJson is null => new(HttpStatusCode.NotFound),
                _ when url.EndsWith("/releases/latest", StringComparison.Ordinal) => Json(releaseJson!),
                "https://api.github.com/assets/setup" => Json($$"""{ "name": "setup", "url": "{{url}}", "browser_download_url": "https://github.com/download/setup", "size": 1, "digest": "{{SetupDigest}}" }"""),
                "https://github.com/download/setup" => new(HttpStatusCode.OK) { Content = new ByteArrayContent(SetupBytes) },
                _ => new(HttpStatusCode.NotFound),
            };
            return Task.FromResult(response);
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    /// <summary>Progress&lt;T&gt; posts to the thread pool; tests want reports in order, now.</summary>
    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
