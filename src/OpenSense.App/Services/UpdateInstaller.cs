using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Win32;
using OpenSense.Core.Updates;

namespace OpenSense.App.Services;

/// <summary>
/// Applies a downloaded update. Installed copies run the next setup silently (it updates in place and
/// reopens the app); portable copies are updated in two steps, as Prism Launcher does: the new version,
/// unpacked to a temporary folder, waits for this one to exit and then replaces its files.
/// </summary>
public static class UpdateInstaller
{
    private const int ErrorCancelled = 1223; // the user declined the UAC prompt
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OpenSense";

    public static string DownloadDirectory { get; } = Path.Combine(Path.GetTempPath(), "OpenSense-update");

    private static string AppDirectory => Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    /// <summary>Installed by setup (this is the folder setup registered), or a portable copy.</summary>
    public static InstallKind DetectKind()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(UninstallKey);
        return key?.GetValue("InstallLocation") is string location &&
               string.Equals(Path.TrimEndingDirectorySeparator(location), AppDirectory, StringComparison.OrdinalIgnoreCase)
            ? InstallKind.Installer
            : InstallKind.Portable;
    }

    /// <summary>Starts the downloaded setup silently; it closes this app and opens the new one. False if UAC was declined.</summary>
    public static bool StartSetup(string setupPath)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(setupPath, "/S /Relaunch") { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    /// <summary>Unpacks the portable zip and starts its OpenSense.exe to replace this copy once it exits.</summary>
    public static void StartPortableUpdate(string zipPath)
    {
        var staging = Path.Combine(DownloadDirectory, Path.GetFileNameWithoutExtension(zipPath));
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        ZipFile.ExtractToDirectory(zipPath, staging);

        // The zip holds an OpenSense folder; find its app wherever it is.
        var newApp = Directory.EnumerateFiles(staging, "OpenSense.exe", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidDataException("The portable update does not contain OpenSense.exe.");

        // Started directly (not through the shell) so it keeps this copy's rights: the portable app runs elevated.
        var start = new ProcessStartInfo(newApp) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(newApp)! };
        start.ArgumentList.Add("--apply-update");
        start.ArgumentList.Add(AppDirectory);
        start.ArgumentList.Add("--wait");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var _ = Process.Start(start);
    }

    /// <summary>
    /// Runs in the new version's staging folder: waits for the old app to exit, copies itself over
    /// <paramref name="target"/> (backing up what it replaces, restoring it on failure), then starts it.
    /// </summary>
    public static void ApplyPortableUpdate(string target, int? waitForProcess, Action<string> log)
    {
        if (waitForProcess is { } pid)
        {
            try
            {
                using var old = Process.GetProcessById(pid);
                if (!old.WaitForExit(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("The running OpenSense did not close.");
            }
            catch (ArgumentException)
            {
                // already gone
            }
        }

        var source = AppDirectory;
        var backup = Path.Combine(DownloadDirectory, "backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        var replaced = new List<string>();
        var added = new List<string>();
        log($"Updating {target} from {source}");
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination))
                {
                    var saved = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Copy(destination, saved, overwrite: true);
                    replaced.Add(relative);
                }
                else
                {
                    added.Add(relative);
                }
                File.Copy(file, destination, overwrite: true);
            }
            log($"Replaced {replaced.Count} files, added {added.Count}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"Update failed, restoring the previous version: {ex}");
            foreach (var relative in replaced)
                File.Copy(Path.Combine(backup, relative), Path.Combine(target, relative), overwrite: true);
            foreach (var relative in added)
                File.Delete(Path.Combine(target, relative));
            throw;
        }
        finally
        {
            Process.Start(new ProcessStartInfo(Path.Combine(target, "OpenSense.exe")) { UseShellExecute = true, WorkingDirectory = target })?.Dispose();
        }
    }
}
