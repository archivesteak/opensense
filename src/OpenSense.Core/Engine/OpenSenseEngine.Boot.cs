using System.Text;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;

namespace OpenSense.Core.Engine;

public sealed partial class OpenSenseEngine
{
    private readonly SemaphoreSlim _bootLogoGate = new(1, 1);
    private IBootLogoStore? _bootLogo;

    public async Task<bool> SetBootAnimationAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (_controller is not { } controller || !_capabilities.BootAnimation)
            return false;
        var ok = await controller.InvokeAsync(d => d.SetBootAnimation(enabled)).ConfigureAwait(false);
        if (ok && _firmware is { } firmware)
            _firmware = firmware with { BootAnimation = enabled };
        LogBootAnimation(enabled, ok);
        return ok;
    }

    public async Task<BootLogoState> GetBootLogoAsync(CancellationToken cancellationToken = default)
    {
        if (_bootLogo is not { } store || !_capabilities.CustomBootLogo)
            return BootLogoState.None;
        await _bootLogoGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                var screen = _machine.ReadInternalScreen();
                try
                {
                    return store.Read() is { } current
                        ? new BootLogoState(true, current.Format, current.Data, screen)
                        : new BootLogoState(false, null, null, screen);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogBootLogoFailed(ex);
                    return new BootLogoState(false, null, null, screen);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _bootLogoGate.Release();
        }
    }

    public async Task<BootLogoResult> SetBootLogoAsync(byte[] image, CancellationToken cancellationToken = default)
    {
        if (_bootLogo is not { } store || !_capabilities.CustomBootLogo)
            return BootLogoResult.Unsupported;
        await _bootLogoGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await Task.Run(() =>
            {
                var screen = _machine.ReadInternalScreen();
                var problem = BootLogoRules.Check(image, screen, out var parsed);
                if (problem != BootLogoProblem.None)
                    return Result(problem);
                return screen is null ? BootLogoResult.ScreenUnknown : store.Write(parsed!, image);
            }, cancellationToken).ConfigureAwait(false);
            LogBootLogo(image.Length, result);
            // The BIOS setup's switch for the picture: on by default, but the picture stays hidden while it is off.
            if (result == BootLogoResult.Done && _capabilities.CustomBootLogoSwitch && _controller is { } controller)
            {
                var shown = await controller.InvokeAsync(d => d.GetCustomBootLogo() != false || d.SetCustomBootLogo(true)).ConfigureAwait(false);
                LogBootLogoSwitch(shown);
            }
            return result;
        }
        finally
        {
            _bootLogoGate.Release();
        }
    }

    public async Task<bool> RestoreBootLogoAsync(CancellationToken cancellationToken = default)
    {
        if (_bootLogo is not { } store || !_capabilities.CustomBootLogo)
            return false;
        await _bootLogoGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ok = await Task.Run(store.Restore, cancellationToken).ConfigureAwait(false);
            LogBootLogoRestored(ok);
            return ok;
        }
        finally
        {
            _bootLogoGate.Release();
        }
    }

    /// <summary>
    /// A custom boot logo needs the firmware's support (SMBIOS record 0x0D, or the BIOS setup's switch for it) and an EFI
    /// system partition to put it on.
    /// </summary>
    private DeviceCapabilities DetectBootLogo(DeviceCapabilities caps)
    {
        if (caps.Smbios.Gaming(GamingRecord.CustomBootLogo) != 1 && !caps.CustomBootLogoSwitch)
            return caps;
        var log = new StringBuilder();
        _bootLogo = _machine.OpenBootLogo(line => log.AppendLine(line));
        log.Append("=> custom boot logo: ").Append(_bootLogo is not null).AppendLine();
        return caps with { CustomBootLogo = _bootLogo is not null, Diagnostics = caps.Diagnostics + log };
    }

    private static BootLogoResult Result(BootLogoProblem problem) => problem switch
    {
        BootLogoProblem.FileTooLarge => BootLogoResult.FileTooLarge,
        BootLogoProblem.TooManyPixels => BootLogoResult.TooManyPixels,
        BootLogoProblem.ColorDepth => BootLogoResult.ColorDepth,
        BootLogoProblem.NotBaseline => BootLogoResult.NotBaseline,
        _ => BootLogoResult.NotGifOrJpeg,
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Boot animation {Enabled} requested, accepted: {Accepted}")]
    private partial void LogBootAnimation(bool enabled, bool accepted);

    [LoggerMessage(Level = LogLevel.Information, Message = "Boot logo of {Bytes} bytes: {Result}")]
    private partial void LogBootLogo(int bytes, BootLogoResult result);

    [LoggerMessage(Level = LogLevel.Information, Message = "The firmware shows the custom boot logo: {Shown}")]
    private partial void LogBootLogoSwitch(bool shown);

    [LoggerMessage(Level = LogLevel.Information, Message = "Boot logo restored to Acer's: {Ok}")]
    private partial void LogBootLogoRestored(bool ok);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the boot logo")]
    private partial void LogBootLogoFailed(Exception ex);
}
