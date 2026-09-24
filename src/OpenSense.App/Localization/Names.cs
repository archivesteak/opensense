using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.App.Localization;

/// <summary>Words, in the app's language, for the values the engine reports.</summary>
public static class Names
{
    public static string FanMode(FanControlMode mode) => mode switch
    {
        FanControlMode.Max => Strings.Get("FanMode_Max"),
        FanControlMode.Custom => Strings.Get("FanMode_Custom"),
        _ => Strings.Get("FanMode_Auto"),
    };

    public static string FanModeDescription(FanControlMode mode) => mode switch
    {
        FanControlMode.Max => Strings.Get("FanMode_Max_Description"),
        FanControlMode.Custom => Strings.Get("FanMode_Custom_Description"),
        _ => Strings.Get("FanMode_Auto_Description"),
    };

    public static string OperatingMode(OperatingMode mode) => mode switch
    {
        Core.Hardware.OperatingMode.Eco => Strings.Get("OperatingMode_Eco"),
        Core.Hardware.OperatingMode.Quiet => Strings.Get("OperatingMode_Quiet"),
        Core.Hardware.OperatingMode.Performance => Strings.Get("OperatingMode_Performance"),
        Core.Hardware.OperatingMode.Turbo => Strings.Get("OperatingMode_Turbo"),
        _ => Strings.Get("OperatingMode_Balanced"),
    };

    public static string OperatingModeDescription(OperatingMode mode) => mode switch
    {
        Core.Hardware.OperatingMode.Eco => Strings.Get("OperatingMode_Eco_Description"),
        Core.Hardware.OperatingMode.Quiet => Strings.Get("OperatingMode_Quiet_Description"),
        Core.Hardware.OperatingMode.Performance => Strings.Get("OperatingMode_Performance_Description"),
        Core.Hardware.OperatingMode.Turbo => Strings.Get("OperatingMode_Turbo_Description"),
        _ => Strings.Get("OperatingMode_Balanced_Description"),
    };

    /// <summary>"CPU" or "GPU".</summary>
    public static string Chip(FanId fan) => fan == FanId.Gpu ? Strings.Get("Chip_Gpu") : Strings.Get("Chip_Cpu");

    /// <summary>"CPU fan" or "GPU fan".</summary>
    public static string Fan(FanId fan) => fan == FanId.Gpu ? Strings.Get("Fan_Gpu") : Strings.Get("Fan_Cpu");

    public static string FanLock(FanLock reason) => reason switch
    {
        Core.Control.FanLock.QuietMode => Strings.Get("FanLock_QuietMode"),
        _ => reason.ToString(),
    };

    public static (string Title, string Message) Notice(ControlNotice notice) => notice.Kind switch
    {
        NoticeKind.FirmwareAccessDenied => (FanControl, Strings.Get("Notice_FirmwareAccessDenied")),
        NoticeKind.OperatingModeRejected => (FanControl,
            Strings.Format("Notice_OperatingModeRejected", notice.OperatingMode is { } mode ? OperatingMode(mode) : "")),
        NoticeKind.CoolBoostRejected => (FanControl, Strings.Get("Notice_CoolBoostRejected")),
        NoticeKind.FansHandedBack => (FanControl, Strings.Get("Notice_FansHandedBack")),
        NoticeKind.LightingRejected => (Keyboard, Strings.Get("Notice_LightingRejected")),
        NoticeKind.BacklightTimeoutRejected => (Keyboard, Strings.Get("Notice_BacklightTimeoutRejected")),
        NoticeKind.WindowsKeyRejected => (Keyboard, Strings.Get("Notice_WindowsKeyRejected")),
        NoticeKind.LcdOverdriveRejected => (Strings.Get("Notice_Display_Title"), Strings.Get("Notice_LcdOverdriveRejected")),
        _ => (FanControl, Strings.Format("Notice_ControlLoopError", notice.Detail ?? notice.Kind.ToString())),
    };

    private static string FanControl => Strings.Get("Notice_FanControl_Title");

    private static string Keyboard => Strings.Get("Notice_Keyboard_Title");

    /// <summary>Where a temperature comes from, e.g. "Intel package sensor (TjMax 100 °C)" or "Embedded controller (PawnIO is not installed)".</summary>
    public static string TemperatureSource(SensorStatus status) => status.Sensor switch
    {
        ChipSensor.IntelPackage => Strings.Format("Sensor_IntelPackage", status.TjMax),
        ChipSensor.AmdTctl => Strings.Get("Sensor_AmdTctl"),
        ChipSensor.NvidiaDriver => Strings.Get("Sensor_NvidiaDriver"),
        _ when status.Problem == SensorProblem.None => Strings.Get("Sensor_EmbeddedController"),
        _ => Strings.Format("Sensor_EmbeddedControllerBecause", Reason(status)),
    };

    private static string Reason(SensorStatus status) => status.Problem switch
    {
        SensorProblem.NotX86Cpu => Strings.Get("SensorProblem_NotX86Cpu"),
        SensorProblem.UnsupportedCpuVendor => Strings.Format("SensorProblem_UnsupportedCpuVendor", status.Detail),
        SensorProblem.NoPackageSensor => Strings.Get("SensorProblem_NoPackageSensor"),
        SensorProblem.ImplausibleTjMax => Strings.Format("SensorProblem_ImplausibleTjMax", status.Detail),
        SensorProblem.NoReading => Strings.Get("SensorProblem_NoReading"),
        SensorProblem.PciLockUnavailable => Strings.Format("SensorProblem_PciLockUnavailable", status.Detail),
        SensorProblem.NoNvidiaDriver => Strings.Get("SensorProblem_NoNvidiaDriver"),
        SensorProblem.NvmlTooOld => Strings.Get("SensorProblem_NvmlTooOld"),
        SensorProblem.PawnIONotInstalled => Strings.Get("SensorProblem_PawnIONotInstalled"),
        SensorProblem.PawnIOUnavailable => Strings.Format("SensorProblem_PawnIOUnavailable", status.Detail),
        SensorProblem.PawnIOModuleRefused => Strings.Format("SensorProblem_PawnIOModuleRefused", status.Detail),
        _ => status.Problem.ToString(),
    };
}
