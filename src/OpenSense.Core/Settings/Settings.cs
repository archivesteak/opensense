using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Settings;

/// <summary>How the app looks and behaves for one Windows user.</summary>
public sealed record UiSettings
{
    public bool CloseToTray { get; init; } = true;
    public bool UseFahrenheit { get; init; }

    /// <summary>The laptop's NitroSense key opens OpenSense, as it opened NitroSense.</summary>
    public bool OpenWithNitroSenseKey { get; init; } = true;

    /// <summary>Another key or combination that opens OpenSense from anywhere; null for none.</summary>
    public KeyShortcut? OpenShortcut { get; init; }

    /// <summary>0 = follow Windows, 1 = light, 2 = dark.</summary>
    public int Theme { get; init; }

    /// <summary>The app's language (a Strings folder name such as "de-DE"), or null to follow Windows.</summary>
    public string? Language { get; init; }
}

/// <summary>A key (Windows virtual-key code) and the modifier keys held with it, as RegisterHotKey takes them.</summary>
public sealed record KeyShortcut(int Key, bool Control = false, bool Alt = false, bool Shift = false, bool Windows = false);

/// <summary>Update checks against GitHub Releases, per user.</summary>
public sealed record UpdateSettings
{
    /// <summary>Check once a day.</summary>
    public bool CheckAutomatically { get; init; } = true;

    public DateTimeOffset? LastCheck { get; init; }

    /// <summary>A release tag the user chose to skip; automatic checks stay quiet about it.</summary>
    public string? SkippedVersion { get; init; }
}

/// <summary>Per-user settings, kept by the app in %APPDATA%\OpenSense.</summary>
public sealed record UserSettings
{
    public int Version { get; init; } = 1;
    public UiSettings Ui { get; init; } = new();
    public UpdateSettings Updates { get; init; } = new();
}

/// <summary>
/// What the laptop should do, for every user: kept by the OpenSense service (or the portable app)
/// in %ProgramData%\OpenSense and applied from boot, before anyone signs in.
/// </summary>
public sealed record MachineSettings
{
    public int Version { get; init; } = 1;
    public ControlProfile Profile { get; init; } = new();
    public KeyboardSettings Keyboard { get; init; } = new();
    public CapabilityOverrides Overrides { get; init; } = new();
}

public static class SettingsPaths
{
    public static string User { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenSense", "settings.json");

    public static string MachineDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenSense");

    public static string Machine { get; } = Path.Combine(MachineDirectory, "settings.json");

    /// <summary>
    /// Creates <see cref="MachineDirectory"/> readable by everyone but writable only by SYSTEM and
    /// administrators, and resets those permissions if it already exists: %ProgramData% lets any user
    /// create folders, so one could otherwise plant settings for the service. Needs administrator rights.
    /// </summary>
    public static void SecureMachineDirectory()
    {
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));

        var directory = new DirectoryInfo(MachineDirectory);
        if (directory.Exists)
            directory.SetAccessControl(security);
        else
            directory.Create(security);
    }
}

public static class SettingsJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>Loads and saves settings of type <typeparamref name="T"/> as a JSON file.</summary>
public sealed class SettingsStore<T>(string path) where T : new()
{
    public string Path { get; } = path;

    public bool Exists => File.Exists(Path);

    public T Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(Path), SettingsJson.Options) ?? new T();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt file must not stop OpenSense; keep a copy for inspection and start fresh.
            TryBackup();
        }
        return new T();
    }

    public void Save(T settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJson.Options));
        File.Move(temp, Path, overwrite: true);
    }

    private void TryBackup()
    {
        try
        {
            File.Copy(Path, Path + ".bad", overwrite: true);
        }
        catch (Exception)
        {
            // best effort
        }
    }
}
