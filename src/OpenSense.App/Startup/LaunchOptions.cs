using System.Globalization;

namespace OpenSense.App.Startup;

public enum MaintenanceCommand
{
    None,

    /// <summary>Ask the running OpenSense window to quit and wait until it has.</summary>
    Exit,

    EnableAutostart,
    DisableAutostart,

    /// <summary>Start the OpenSense service (run elevated by the app when the service is stopped).</summary>
    StartService,

    /// <summary>Portable update, second step: copy this (new) version over <see cref="LaunchOptions.UpdateTarget"/>.</summary>
    ApplyUpdate,
}

/// <summary>Command-line switches.</summary>
public sealed record LaunchOptions
{
    /// <summary>Started at sign-in: stay in the notification area.</summary>
    public bool Autostart { get; init; }

    /// <summary>Open on this page (dashboard, fans, lighting, system, settings).</summary>
    public string? Page { get; init; }

    /// <summary>Installer/maintenance commands that run without UI and exit.</summary>
    public MaintenanceCommand Command { get; init; }

    /// <summary>The portable folder <see cref="MaintenanceCommand.ApplyUpdate"/> updates.</summary>
    public string? UpdateTarget { get; init; }

    /// <summary>A process to wait for before starting: the old copy, when updating or restarting.</summary>
    public int? WaitForProcess { get; init; }

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var options = new LaunchOptions();
        for (var i = 0; i < args.Count; i++)
        {
            options = args[i].ToLowerInvariant() switch
            {
                "--autostart" => options with { Autostart = true },
                "--page" when i + 1 < args.Count => options with { Page = args[++i] },
                "--exit" => options with { Command = MaintenanceCommand.Exit },
                "--enable-autostart" => options with { Command = MaintenanceCommand.EnableAutostart },
                "--disable-autostart" => options with { Command = MaintenanceCommand.DisableAutostart },
                "--start-service" => options with { Command = MaintenanceCommand.StartService },
                "--apply-update" when i + 1 < args.Count => options with { Command = MaintenanceCommand.ApplyUpdate, UpdateTarget = args[++i] },
                "--wait" when i + 1 < args.Count => options with
                {
                    WaitForProcess = int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ? pid : null,
                },
                _ => options,
            };
        }
        return options;
    }

    /// <summary>Arguments to pass on when relaunching (e.g. elevated).</summary>
    public string ToArguments()
    {
        var parts = new List<string>();
        if (Autostart) parts.Add("--autostart");
        if (Page is not null) parts.Add($"--page {Page}");
        if (WaitForProcess is { } pid) parts.Add(string.Create(CultureInfo.InvariantCulture, $"--wait {pid}"));
        return string.Join(' ', parts);
    }
}
