using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;
using OpenSense.Core.Monitoring;

namespace OpenSense.App.Localization;

/// <summary>Words, in the app's language, for the values the engine reports.</summary>
public static class Names
{
    /// <summary>Notices shown as a toast that replaces the one before (tag "opmode"), not as a banner in the window.</summary>
    public static bool IsModeToast(NoticeKind kind) =>
        kind is NoticeKind.OperatingModeSwitchedByKey or NoticeKind.OperatingModeChangedByPower or NoticeKind.TurboUnavailable;

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

    public static string Effect(LightingEffect effect) => effect switch
    {
        LightingEffect.Breathing => Strings.Get("Effect_Breathing"),
        LightingEffect.Neon => Strings.Get("Effect_Neon"),
        LightingEffect.Wave => Strings.Get("Effect_Wave"),
        LightingEffect.Shifting => Strings.Get("Effect_Shifting"),
        LightingEffect.Zoom => Strings.Get("Effect_Zoom"),
        LightingEffect.Meteor => Strings.Get("Effect_Meteor"),
        LightingEffect.Twinkling => Strings.Get("Effect_Twinkling"),
        LightingEffect.Snake => Strings.Get("Effect_Snake"),
        LightingEffect.Lightning => Strings.Get("Effect_Lightning"),
        LightingEffect.Stack => Strings.Get("Effect_Stack"),
        LightingEffect.MotionPoint => Strings.Get("Effect_MotionPoint"),
        LightingEffect.ZoomIn => Strings.Get("Effect_Zoom"),
        LightingEffect.Ripple => Strings.Get("Effect_Ripple"),
        LightingEffect.Raindrop => Strings.Get("Effect_Raindrop"),
        LightingEffect.Fireball => Strings.Get("Effect_Fireball"),
        LightingEffect.Snow => Strings.Get("Effect_Snow"),
        LightingEffect.Heartbeat => Strings.Get("Effect_Heartbeat"),
        LightingEffect.Dazzling => Strings.Get("Effect_Dazzling"),
        LightingEffect.Matrix => Strings.Get("Effect_Matrix"),
        LightingEffect.Swiping => Strings.Get("Effect_Swiping"),
        LightingEffect.RowWave => Strings.Get("Effect_RowWave"),
        LightingEffect.Racing => Strings.Get("Effect_Racing"),
        LightingEffect.Sprouting => Strings.Get("Effect_Sprouting"),
        LightingEffect.Disco => Strings.Get("Effect_Disco"),
        LightingEffect.PingPong => Strings.Get("Effect_PingPong"),
        LightingEffect.LightShow => Strings.Get("Effect_LightShow"),
        LightingEffect.FollowOperatingMode => Strings.Get("Effect_FollowOperatingMode"),
        LightingEffect.Combo => Strings.Get("Effect_Combo"),
        LightingEffect.Rainbow => Strings.Get("Effect_Rainbow"),
        LightingEffect.Slash => Strings.Get("Effect_Slash"),
        LightingEffect.Star => Strings.Get("Effect_Star"),
        LightingEffect.Blasting => Strings.Get("Effect_Blasting"),
        _ => effect.ToString(),
    };

    /// <summary>
    /// What the effect looks like on this light (Snake in one colour on the keyboards, in the rainbow's on the light bars;
    /// the embedded controller's zoned keyboard runs Neon, Meteor and Twinkling its own way).
    /// </summary>
    public static string EffectDescription(EffectTraits traits, LightingBackendKind backend) => traits.Effect switch
    {
        LightingEffect.Breathing => Strings.Get("Effect_Breathing_Description"),
        LightingEffect.Neon when backend == LightingBackendKind.EcKeyboard => Strings.Get("Effect_NeonKeyboard_Description"),
        LightingEffect.Neon => Strings.Get("Effect_Neon_Description"),
        LightingEffect.Wave => Strings.Get("Effect_Wave_Description"),
        LightingEffect.Shifting => Strings.Get("Effect_Shifting_Description"),
        LightingEffect.Zoom => Strings.Get("Effect_Zoom_Description"),
        LightingEffect.Meteor when backend == LightingBackendKind.EcKeyboard => Strings.Get("Effect_MeteorKeyboard_Description"),
        LightingEffect.Meteor => Strings.Get("Effect_Meteor_Description"),
        LightingEffect.Twinkling when backend == LightingBackendKind.EcKeyboard => Strings.Get("Effect_TwinklingKeyboard_Description"),
        LightingEffect.Twinkling => Strings.Get("Effect_Twinkling_Description"),
        LightingEffect.Snake when traits.Color => Strings.Get("Effect_SnakeColor_Description"),
        LightingEffect.Snake => Strings.Get("Effect_Snake_Description"),
        LightingEffect.Lightning => Strings.Get("Effect_Lightning_Description"),
        LightingEffect.Stack => Strings.Get("Effect_Stack_Description"),
        LightingEffect.MotionPoint => Strings.Get("Effect_MotionPoint_Description"),
        LightingEffect.ZoomIn => Strings.Get("Effect_ZoomIn_Description"),
        LightingEffect.Ripple => Strings.Get("Effect_Ripple_Description"),
        LightingEffect.Raindrop => Strings.Get("Effect_Raindrop_Description"),
        LightingEffect.Fireball => Strings.Get("Effect_Fireball_Description"),
        LightingEffect.Snow => Strings.Get("Effect_Snow_Description"),
        LightingEffect.Heartbeat => Strings.Get("Effect_Heartbeat_Description"),
        LightingEffect.Dazzling => Strings.Get("Effect_Dazzling_Description"),
        LightingEffect.Matrix => Strings.Get("Effect_Matrix_Description"),
        LightingEffect.Swiping => Strings.Get("Effect_Swiping_Description"),
        LightingEffect.RowWave => Strings.Get("Effect_RowWave_Description"),
        LightingEffect.Racing => Strings.Get("Effect_Racing_Description"),
        LightingEffect.Sprouting => Strings.Get("Effect_Sprouting_Description"),
        LightingEffect.Disco => Strings.Get("Effect_Disco_Description"),
        LightingEffect.PingPong => Strings.Get("Effect_PingPong_Description"),
        LightingEffect.LightShow => Strings.Get("Effect_LightShow_Description"),
        LightingEffect.FollowOperatingMode => Strings.Get("Effect_FollowOperatingMode_Description"),
        LightingEffect.Combo => Strings.Get("Effect_Combo_Description"),
        LightingEffect.Rainbow => Strings.Get("Effect_Rainbow_Description"),
        LightingEffect.Slash => Strings.Get("Effect_Slash_Description"),
        LightingEffect.Star => Strings.Get("Effect_Star_Description"),
        LightingEffect.Blasting => Strings.Get("Effect_Blasting_Description"),
        _ => "",
    };

    /// <summary>"US (ANSI)", "UK and Europe (ISO)", "Japanese (JIS)".</summary>
    public static string Layout(KeyboardLayout layout) => layout switch
    {
        KeyboardLayout.Iso => Strings.Get("Layout_Iso"),
        KeyboardLayout.Jis => Strings.Get("Layout_Jis"),
        _ => Strings.Get("Layout_Ansi"),
    };

    public static string Direction(LightingDirection direction) => direction switch
    {
        LightingDirection.Left => Strings.Get("Direction_Left"),
        LightingDirection.Up => Strings.Get("Direction_Up"),
        LightingDirection.Down => Strings.Get("Direction_Down"),
        _ => Strings.Get("Direction_Right"),
    };

    /// <summary>"Keyboard", "Light bar", "Logo", "Cover logo", ….</summary>
    public static string Light(LightingLocation light) => light switch
    {
        LightingLocation.LightBar => Strings.Get("Light_LightBar"),
        LightingLocation.InfinityMirror => Strings.Get("Light_InfinityMirror"),
        LightingLocation.Logo => Strings.Get("Light_Logo"),
        LightingLocation.FrontLightBar => Strings.Get("Light_FrontLightBar"),
        LightingLocation.LeftLightBar => Strings.Get("Light_LeftLightBar"),
        LightingLocation.RightLightBar => Strings.Get("Light_RightLightBar"),
        LightingLocation.RearLightBar => Strings.Get("Light_RearLightBar"),
        LightingLocation.TurboKey => Strings.Get("Light_TurboKey"),
        LightingLocation.ModeKey => Strings.Get("Light_ModeKey"),
        LightingLocation.CoverLogo => Strings.Get("Light_CoverLogo"),
        LightingLocation.BaseLogo => Strings.Get("Light_BaseLogo"),
        LightingLocation.MagKey => Strings.Get("Light_MagKey"),
        LightingLocation.InfiniteRing => Strings.Get("Light_InfiniteRing"),
        _ => Strings.Get("Light_Keyboard"),
    };

    /// <summary>"CPU" or "GPU".</summary>
    public static string Chip(FanChip chip) => chip switch
    {
        FanChip.Gpu => Strings.Get("Chip_Gpu"),
        FanChip.System => Strings.Get("Chip_System"),
        _ => Strings.Get("Chip_Cpu"),
    };

    /// <summary>
    /// "CPU fan", "GPU fan", or "GPU fan 1" and "GPU fan 2" on a laptop with two (as Acer's software names them).
    /// </summary>
    /// <param name="fans">The laptop's fans, which tell whether a fan needs its number.</param>
    public static string Fan(FanId fan, IReadOnlyCollection<FanChannel> fans) => fan switch
    {
        FanId.Gpu when Has(fans, FanId.Gpu2) => Strings.Get("Fan_Gpu1"),
        FanId.Gpu => Strings.Get("Fan_Gpu"),
        FanId.Gpu2 => Strings.Get("Fan_Gpu2"),
        FanId.System when Has(fans, FanId.System2) => Strings.Get("Fan_System1"),
        FanId.System => Strings.Get("Fan_System"),
        FanId.System2 => Strings.Get("Fan_System2"),
        _ => Strings.Get("Fan_Cpu"),
    };

    /// <summary>The fan's short name for tight spaces (the tray): "CPU", "GPU", "GPU 2".</summary>
    public static string FanShort(FanId fan, IReadOnlyCollection<FanChannel> fans) => fan switch
    {
        FanId.Gpu when Has(fans, FanId.Gpu2) => Strings.Get("FanShort_Gpu1"),
        FanId.Gpu => Strings.Get("Chip_Gpu"),
        FanId.Gpu2 => Strings.Get("FanShort_Gpu2"),
        FanId.System when Has(fans, FanId.System2) => Strings.Get("FanShort_System1"),
        FanId.System => Strings.Get("Chip_System"),
        FanId.System2 => Strings.Get("FanShort_System2"),
        _ => Strings.Get("Chip_Cpu"),
    };

    private static bool Has(IReadOnlyCollection<FanChannel> fans, FanId id) => fans.Any(f => f.Id == id);

    public static string FanLock(FanLock reason) => reason switch
    {
        Core.Control.FanLock.QuietMode => Strings.Get("FanLock_QuietMode"),
        Core.Control.FanLock.EcoMode => Strings.Get("FanLock_EcoMode"),
        Core.Control.FanLock.DustDefender => Strings.Get("FanLock_DustDefender"),
        _ => reason.ToString(),
    };

    public static (string Title, string Message) Notice(ControlNotice notice) => notice.Kind switch
    {
        NoticeKind.FirmwareAccessDenied => (FanControl, Strings.Get("Notice_FirmwareAccessDenied")),
        NoticeKind.OperatingModeRejected => (FanControl,
            Strings.Format("Notice_OperatingModeRejected", notice.OperatingMode is { } mode ? OperatingMode(mode) : "")),
        NoticeKind.CoolBoostRejected => (FanControl, Strings.Get("Notice_CoolBoostRejected")),
        NoticeKind.FanTableRejected => (FanControl, Strings.Get("Notice_FanTableRejected")),
        NoticeKind.FansHandedBack => (FanControl, Strings.Get("Notice_FansHandedBack")),
        NoticeKind.LightingRejected => notice.Light switch
        {
            LightingLocation.LightBar or LightingLocation.InfinityMirror => (Lighting, Strings.Get("Notice_LightBarRejected")),
            LightingLocation.Logo => (Lighting, Strings.Get("Notice_LogoRejected")),
            null or LightingLocation.Keyboard => (Keyboard, Strings.Get("Notice_LightingRejected")),
            { } light => (Lighting, Strings.Format("Notice_LightRejected", Light(light))),
        },
        NoticeKind.BacklightTimeoutRejected => (Keyboard, Strings.Get("Notice_BacklightTimeoutRejected")),
        NoticeKind.WindowsKeyRejected => (Keyboard, Strings.Get("Notice_WindowsKeyRejected")),
        NoticeKind.LcdOverdriveRejected => (Strings.Get("Notice_Display_Title"), Strings.Get("Notice_LcdOverdriveRejected")),
        NoticeKind.ChargeLimitRejected => (Battery, Strings.Get("Notice_ChargeLimitRejected")),
        NoticeKind.UsbChargingRejected => (Battery, Strings.Get("Notice_UsbChargingRejected")),
        NoticeKind.CalibrationFinished => (Battery, Strings.Get("Notice_CalibrationFinished")),
        NoticeKind.CalibrationInterrupted => (Battery, Strings.Get("Notice_CalibrationInterrupted")),
        NoticeKind.CalibrationStoppedUnplugged => (Battery, Strings.Get("Notice_CalibrationStoppedUnplugged")),
        NoticeKind.GpuClocksRejected => (Strings.Get("Notice_GpuClocks_Title"), Strings.Get("Notice_GpuClocksRejected")),
        NoticeKind.OperatingModeSwitchedByKey or NoticeKind.OperatingModeChangedByPower =>
            (Strings.Get("Notice_OperatingMode_Title"), ModeInForce(notice)),
        NoticeKind.TurboUnavailable => (Strings.Get("Notice_OperatingMode_Title"), notice.PowerLimit == PowerLimit.Adapter
            ? Strings.Get("Notice_TurboNeedsAdapter")
            : Strings.Get("Notice_TurboUnavailable")),
        _ => (FanControl, Strings.Format("Notice_ControlLoopError", notice.Detail ?? notice.Kind.ToString())),
    };

    /// <summary>
    /// "Performance", "On battery: Eco", "Plugged in: Turbo", "Battery low: Balanced until it charges" or
    /// "Held back by the adapter: Balanced".
    /// </summary>
    private static string ModeInForce(ControlNotice notice)
    {
        var mode = notice.OperatingMode is { } m ? OperatingMode(m) : "";
        return notice.PowerLimit switch
        {
            PowerLimit.Battery => Strings.Format("Notice_ModeOnBattery", mode),
            PowerLimit.LowBattery => Strings.Format("Notice_ModeLowBattery", mode),
            PowerLimit.Adapter => Strings.Format("Notice_ModeAdapter", mode),
            _ when notice.Kind == NoticeKind.OperatingModeChangedByPower => Strings.Format("Notice_ModePluggedIn", mode),
            _ => mode,
        };
    }

    private static string FanControl => Strings.Get("Notice_FanControl_Title");

    private static string Keyboard => Strings.Get("Notice_Keyboard_Title");

    private static string Lighting => Strings.Get("Notice_Lighting_Title");

    private static string Battery => Strings.Get("Notice_Battery_Title");

    /// <summary>Where a temperature comes from, e.g. "Intel package sensor (TjMax 100 °C)" or "Embedded controller (PawnIO is not installed)".</summary>
    public static string TemperatureSource(SensorStatus status) => status.Sensor switch
    {
        ChipSensor.IntelPackage => Strings.Format("Sensor_IntelPackage", status.TjMax),
        ChipSensor.AmdTctl => Strings.Get("Sensor_AmdTctl"),
        ChipSensor.NvidiaDriver => Strings.Get("Sensor_NvidiaDriver"),
        ChipSensor.GraphicsDriver => Strings.Get("Sensor_GraphicsDriver"),
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
        SensorProblem.NoDiscreteGpu => Strings.Get("SensorProblem_NoDiscreteGpu"),
        SensorProblem.GpuAdapterUnavailable => Strings.Format("SensorProblem_GpuAdapterUnavailable", status.Detail),
        SensorProblem.PawnIONotInstalled => Strings.Get("SensorProblem_PawnIONotInstalled"),
        SensorProblem.PawnIOUnavailable => Strings.Format("SensorProblem_PawnIOUnavailable", status.Detail),
        SensorProblem.PawnIOModuleRefused => Strings.Format("SensorProblem_PawnIOModuleRefused", status.Detail),
        _ => status.Problem.ToString(),
    };
}
