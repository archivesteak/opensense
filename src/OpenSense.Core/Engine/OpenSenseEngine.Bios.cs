using Microsoft.Extensions.Logging;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Engine;

public sealed partial class OpenSenseEngine
{
    public Task SetBiosAccessAsync(BiosAccess access, CancellationToken cancellationToken = default)
    {
        // The dangerous settings only follow the switch for BIOS settings as a whole.
        var allowed = access with { Dangerous = access.Enabled && access.Dangerous };
        UpdateSettings(s => s with { Bios = allowed });
        LogBiosAccess(allowed.Enabled, allowed.Dangerous);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<BiosSetting>> ReadBiosSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_controller is not { } controller || !_capabilities.BiosSettings)
            return [];
        try
        {
            return await controller.InvokeAsync(d => d.GetBiosSettings()).ConfigureAwait(false);
        }
        catch (AcerWmiException ex) when (ex is not AcerWmiAccessDeniedException)
        {
            LogBiosFailed(ex);
            return [];
        }
    }

    public async Task<BiosChangeResult> SetBiosSettingAsync(string name, string value, string password, CancellationToken cancellationToken = default)
    {
        var risk = BiosProtocol.RiskOf(name);
        // An empty new password takes the password away; every other value has to be text the BIOS can take.
        var isPassword = BiosProtocol.IsPassword(name);
        IReadOnlyList<string> texts = isPassword && value.Length == 0 ? [name] : [name, value];
        var result = await ChangeBiosAsync(risk, password, texts, device => device.SetBiosSetting(name, value, password),
            isPassword ? value : null).ConfigureAwait(false);
        // A password is never logged, nor the one being set.
        LogBiosSetting(name, BiosProtocol.IsPassword(name) ? "" : value, risk, result);
        return result;
    }

    public async Task<BiosChangeResult> LoadBiosDefaultsAsync(string password, CancellationToken cancellationToken = default)
    {
        var result = await ChangeBiosAsync(BiosRisk.Dangerous, password, [], device => device.LoadBiosDefaults(password)).ConfigureAwait(false);
        LogBiosDefaults("BIOS", result);
        return result;
    }

    public async Task<BiosChangeResult> LoadBiosUserDefaultsAsync(string password, CancellationToken cancellationToken = default)
    {
        var result = await ChangeBiosAsync(BiosRisk.Dangerous, password, [], device => device.LoadBiosUserDefaults(password)).ConfigureAwait(false);
        LogBiosDefaults("user", result);
        return result;
    }

    /// <summary>
    /// Checks the user's switches for BIOS settings (here, not in the app: any client could ask), then that the BIOS has the
    /// interface and that what goes into it (the password, and the <paramref name="texts"/> that name and give the change) is
    /// text it can take, then makes the change on the control thread. A <paramref name="newPassword"/> longer than the BIOS
    /// keeps whole isn't sent: what it kept could not be typed at the next start.
    /// </summary>
    private async Task<BiosChangeResult> ChangeBiosAsync(BiosRisk risk, string password, IReadOnlyList<string> texts,
        Func<AcerDevice, BiosChangeResult> change, string? newPassword = null)
    {
        if (!Current.Bios.Allows(risk))
            return BiosChangeResult.NotAllowed;
        if (_controller is not { } controller || !_capabilities.BiosSettings)
            return BiosChangeResult.Unavailable;
        if (!BiosProtocol.IsPlainText(password, allowEmpty: true) || !texts.All(t => BiosProtocol.IsPlainText(t))
            || newPassword?.Length > _capabilities.BiosPasswordMaxLength)
            return BiosChangeResult.Rejected;
        try
        {
            return await controller.InvokeAsync(change).ConfigureAwait(false);
        }
        catch (AcerWmiException ex) when (ex is not AcerWmiAccessDeniedException)
        {
            LogBiosFailed(ex);
            return BiosChangeResult.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "BIOS settings allowed: {Enabled}, dangerous ones too: {Dangerous}")]
    private partial void LogBiosAccess(bool enabled, bool dangerous);

    [LoggerMessage(Level = LogLevel.Information, Message = "BIOS setting {Name} = '{Value}' ({Risk}): {Result}")]
    private partial void LogBiosSetting(string name, string value, BiosRisk risk, BiosChangeResult result);

    [LoggerMessage(Level = LogLevel.Information, Message = "BIOS {Which} defaults loaded: {Result}")]
    private partial void LogBiosDefaults(string which, BiosChangeResult result);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The BIOS settings interface failed")]
    private partial void LogBiosFailed(Exception ex);
}
