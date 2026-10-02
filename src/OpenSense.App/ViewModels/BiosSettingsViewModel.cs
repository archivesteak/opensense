using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Hardware;

namespace OpenSense.App.ViewModels;

/// <summary>
/// The BIOS's own settings (Acer's settings interface, from the 2025 Predators on), on the System page once the user has
/// allowed changing them in Settings. The engine keeps and checks that permission; this only shows what it will take.
/// </summary>
public sealed partial class BiosSettingsViewModel(DeviceSession session, NotificationService notifications) : ObservableObject
{
    /// <summary>The BIOS supervisor password, once the BIOS asked for it: kept only while the app runs.</summary>
    private string _password = "";

    public ObservableCollection<BiosSettingViewModel> Rows { get; } = [];

    /// <summary>The BIOS has the interface and the user allowed BIOS settings (in Settings).</summary>
    [ObservableProperty]
    public partial bool Available { get; set; }

    /// <summary>The dangerous settings and loading defaults can be used too.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultsAvailable))]
    public partial bool DangerousAllowed { get; set; }

    /// <summary>The expander is open: the settings are read from the BIOS each time it opens.</summary>
    [ObservableProperty]
    public partial bool Expanded { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = Strings.Get("Bios_StatusIdle");

    /// <summary>A change is waiting for the laptop to start again.</summary>
    [ObservableProperty]
    public partial bool RestartPending { get; set; }

    /// <summary>The BIOS is being read or written.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Idle), nameof(DefaultsAvailable))]
    public partial bool Busy { get; set; }

    public bool Idle => !Busy;

    /// <summary>Loading defaults changes everything at once, so it belongs to the dangerous settings.</summary>
    public bool DefaultsAvailable => DangerousAllowed && !Busy;

    /// <summary>Asks for the BIOS supervisor password (true: the last one was wrong); null when the user gives up.</summary>
    public Func<bool, Task<string?>>? AskPassword { get; set; }

    /// <summary>Asks for a new password for the named setting, of at most the given length, typed twice; null when the user gives up.</summary>
    public Func<string, int, Task<string?>>? AskNewPassword { get; set; }

    /// <summary>Asks before a new BIOS password (for the named setting) is saved.</summary>
    public Func<string, Task<bool>>? ConfirmPassword { get; set; }

    /// <summary>Asks before a dangerous setting (name, value) changes.</summary>
    public Func<string, string, Task<bool>>? ConfirmDangerous { get; set; }

    /// <summary>Asks before defaults load (true: the user's own, saved in the BIOS setup).</summary>
    public Func<bool, Task<bool>>? ConfirmDefaults { get; set; }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        var access = session.Settings.Bios;
        Available = session.Capabilities.BiosSettings && access.Enabled;
        DangerousAllowed = access.Dangerous;
        if (!Available)
        {
            Rows.Clear();
            Expanded = false;
            _password = "";
            Status = Strings.Get("Bios_StatusIdle");
        }
        Refresh();
    }

    partial void OnExpandedChanged(bool value)
    {
        if (value)
            _ = ReadAsync();
    }

    partial void OnBusyChanged(bool value) => Refresh();

    [RelayCommand]
    private static void RestartNow() => WindowsRestart.RestartNow();

    [RelayCommand]
    private Task LoadDefaultsAsync() => RunDefaultsAsync(user: false);

    [RelayCommand]
    private Task LoadUserDefaultsAsync() => RunDefaultsAsync(user: true);

    internal bool Allows(BiosRisk risk) => session.Settings.Bios.Allows(risk);

    private void Refresh()
    {
        foreach (var row in Rows)
            row.Refresh();
    }

    private async Task ReadAsync()
    {
        if (!Available || Busy)
            return;
        Busy = true;
        Status = Strings.Get("Bios_StatusReading");
        try
        {
            Show(await session.ReadBiosSettingsAsync());
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Puts <paramref name="settings"/> on show: the rows stay as they are (and where they are) when nothing but values changed.</summary>
    private void Show(IReadOnlyList<BiosSetting> settings)
    {
        if (Rows.Count == settings.Count && Rows.Zip(settings).All(pair => pair.First.Matches(pair.Second)))
        {
            foreach (var (row, setting) in Rows.Zip(settings))
                row.Update(setting);
        }
        else
        {
            Rows.Clear();
            foreach (var setting in settings)
                Rows.Add(new BiosSettingViewModel(this, setting));
        }
        Status = settings.Count == 0 ? Strings.Get("Bios_StatusNone") : Strings.Format("Bios_StatusCount", settings.Count);
    }

    /// <summary>Sets <paramref name="row"/> to <paramref name="value"/>; the row goes back to what the BIOS holds if that does not work.</summary>
    internal async Task ChangeAsync(BiosSettingViewModel row, string value)
    {
        if (Busy)
        {
            row.Revert();
            return;
        }
        Busy = true;
        try
        {
            if (row.Risk == BiosRisk.Dangerous && !(ConfirmDangerous is { } confirm && await confirm(row.Name, value)))
            {
                row.Revert();
                return;
            }
            var result = await SendAsync(password => session.SetBiosSettingAsync(row.Name, value, password));
            if (BiosProtocol.Succeeded(result))
            {
                row.Commit(value);
                RestartPending = true;
                await ReloadAsync();
            }
            else
            {
                row.Revert();
                Report(result);
            }
        }
        finally
        {
            Busy = false;
        }
    }

    internal async Task SetPasswordAsync(BiosSettingViewModel row)
    {
        if (Busy || AskNewPassword is not { } ask || await ask(row.Name, session.Capabilities.BiosPasswordMaxLength) is not { } newPassword)
            return;
        // Typing it twice is not saving it: nothing goes to the BIOS until the user says so.
        if (ConfirmPassword is not { } confirm || !await confirm(row.Name))
            return;
        Busy = true;
        try
        {
            var result = await SendAsync(password => session.SetBiosSettingAsync(row.Name, newPassword, password));
            if (!BiosProtocol.Succeeded(result))
            {
                Report(result);
                return;
            }
            // The supervisor password is the one every later change has to give.
            if (row.Name.Equals(BiosProtocol.SupervisorPassword, StringComparison.OrdinalIgnoreCase))
                _password = newPassword;
            RestartPending = true;
            notifications.Show(Strings.Get("Notice_Bios_Title"), Strings.Get("Notice_BiosPasswordSet"), InfoBarSeverity.Success);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Takes a BIOS password away by setting it to nothing, without a question of its own. The BIOS doesn't say whether one
    /// is set, so this is offered either way. It goes out with the password the user typed earlier (the one just set, or the
    /// one given for an earlier change); only when the BIOS wants another, as after a restart, is the user asked for it.
    /// </summary>
    internal async Task RemovePasswordAsync(BiosSettingViewModel row)
    {
        if (Busy)
            return;
        Busy = true;
        try
        {
            var result = await SendAsync(password => session.SetBiosSettingAsync(row.Name, "", password));
            if (!BiosProtocol.Succeeded(result))
            {
                Report(result);
                return;
            }
            if (row.Name.Equals(BiosProtocol.SupervisorPassword, StringComparison.OrdinalIgnoreCase))
                _password = "";
            RestartPending = true;
            notifications.Show(Strings.Get("Notice_Bios_Title"), Strings.Get("Notice_BiosPasswordRemoved"), InfoBarSeverity.Success);
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task RunDefaultsAsync(bool user)
    {
        if (Busy || !DangerousAllowed || ConfirmDefaults is not { } confirm || !await confirm(user))
            return;
        Busy = true;
        try
        {
            var result = await SendAsync(password => user ? session.LoadBiosUserDefaultsAsync(password) : session.LoadBiosDefaultsAsync(password));
            if (BiosProtocol.Succeeded(result))
            {
                RestartPending = true;
                await ReloadAsync();
            }
            else
            {
                Report(result);
            }
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>What the BIOS holds after a change; the rows stay as they are when it can't be read.</summary>
    private async Task ReloadAsync()
    {
        if (await session.ReadBiosSettingsAsync() is { Count: > 0 } settings)
            Show(settings);
    }

    /// <summary>
    /// Sends with the password the BIOS last accepted; where it wants another, asks until the user gives up. A password the
    /// BIOS turns down is forgotten.
    /// </summary>
    private async Task<BiosChangeResult> SendAsync(Func<string, Task<BiosChangeResult>> send)
    {
        var result = await send(_password);
        var wrong = false;
        while (result == BiosChangeResult.WrongPassword && AskPassword is { } ask && await ask(wrong) is { } entered)
        {
            _password = entered;
            wrong = true;
            result = await send(_password);
        }
        if (result == BiosChangeResult.WrongPassword)
            _password = "";
        return result;
    }

    private void Report(BiosChangeResult result)
    {
        // Turning down the password prompt is an answer, not a failure.
        if (Names.BiosProblem(result) is { Length: > 0 } message)
            notifications.Show(Strings.Get("Notice_Bios_Title"), message, InfoBarSeverity.Error);
    }
}

/// <summary>One BIOS setting: a switch, a choice between the values the BIOS lists, or a password to set.</summary>
public sealed partial class BiosSettingViewModel : ObservableObject
{
    private readonly BiosSettingsViewModel _owner;
    private string _current;
    private bool _silent;

    public BiosSettingViewModel(BiosSettingsViewModel owner, BiosSetting setting)
    {
        _owner = owner;
        _current = setting.Current;
        Name = setting.Name;
        Risk = setting.Risk;
        Options = setting.Options;
        IsPassword = setting.Kind == BiosSettingKind.Password;
        IsSwitch = !IsPassword && setting.Options.Count == 2 && setting.Options.Contains("Disabled") && setting.Options.Contains("Enabled");
        Update(setting);
        Refresh();
    }

    /// <summary>The name as the BIOS gives it.</summary>
    public string Name { get; }

    public BiosRisk Risk { get; }

    public IReadOnlyList<string> Options { get; }

    public bool IsPassword { get; }

    /// <summary>Disabled or Enabled: a switch, as the BIOS setup's own "Disabled / Enabled" choices are.</summary>
    public bool IsSwitch { get; }

    public bool IsChoice => !IsPassword && !IsSwitch;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial string? SelectedOption { get; set; }

    /// <summary>The user's switches allow this setting, and the BIOS is not busy.</summary>
    [ObservableProperty]
    public partial bool CanChange { get; set; }

    /// <summary>Why it is set apart, when it is a dangerous one.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } = "";

    /// <summary>The same setting with the same values to pick from (only its value may differ).</summary>
    internal bool Matches(BiosSetting setting) =>
        Name == setting.Name && IsPassword == (setting.Kind == BiosSettingKind.Password) && Options.SequenceEqual(setting.Options);

    /// <summary>Shows the value the BIOS holds, without it counting as the user's change.</summary>
    internal void Update(BiosSetting setting)
    {
        _current = setting.Current;
        Revert();
    }

    /// <summary>The BIOS took <paramref name="value"/>.</summary>
    internal void Commit(string value) => _current = value;

    /// <summary>Shows what the BIOS holds again: the change did not go through.</summary>
    internal void Revert()
    {
        _silent = true;
        IsOn = _current == "Enabled";
        SelectedOption = Options.Contains(_current) ? _current : null;
        _silent = false;
    }

    internal void Refresh()
    {
        var allowed = _owner.Allows(Risk);
        CanChange = allowed && _owner.Idle;
        Description = Risk == BiosRisk.Dangerous ? Strings.Get(allowed ? "Bios_Dangerous" : "Bios_DangerousLocked") : "";
    }

    partial void OnIsOnChanged(bool value)
    {
        if (!_silent && IsSwitch)
            _ = _owner.ChangeAsync(this, value ? "Enabled" : "Disabled");
    }

    partial void OnSelectedOptionChanged(string? value)
    {
        if (!_silent && IsChoice && value is not null)
            _ = _owner.ChangeAsync(this, value);
    }

    [RelayCommand]
    private Task SetPasswordAsync() => _owner.SetPasswordAsync(this);

    [RelayCommand]
    private Task RemovePasswordAsync() => _owner.RemovePasswordAsync(this);
}
