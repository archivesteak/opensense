using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Engine;

public sealed partial class OpenSenseEngine
{
    private ISystemPower? _systemPower;
    private PowerService? _powerService;

    public Task SetPowerAsync(PowerSettings power, CancellationToken cancellationToken = default)
    {
        UpdateSettings(s => s with { Power = power });
        return _powerService?.ApplyAsync(power) ?? Task.CompletedTask;
    }

    public Task<CalibrationResult> StartBatteryCalibrationAsync(CancellationToken cancellationToken = default) =>
        _powerService?.StartCalibrationAsync() ?? Task.FromResult(CalibrationResult.Unsupported);

    public Task StopBatteryCalibrationAsync(CancellationToken cancellationToken = default) =>
        _powerService?.StopCalibrationAsync() ?? Task.CompletedTask;

    public Task<BatteryHealth?> ReadBatteryHealthAsync(CancellationToken cancellationToken = default) =>
        Task.Run(_machine.ReadBatteryHealth, cancellationToken);

    private void StartPower(MachineSettings settings)
    {
        _systemPower ??= _machine.OpenSystemPower();
        _powerService = new PowerService(_controller!, _capabilities, _power!, _systemPower, Runtime.Calibration, SaveCalibration);
        _powerService.Notice += OnNotice;
        _ = _powerService.StartAsync(settings.Power);
    }

    private void StopPower()
    {
        if (_powerService is not { } power)
            return;
        power.Notice -= OnNotice;
        power.Dispose();
        _powerService = null;
    }

    private void SaveCalibration(CalibrationRecord? calibration) => UpdateRuntime(r => r with { Calibration = calibration });
}
