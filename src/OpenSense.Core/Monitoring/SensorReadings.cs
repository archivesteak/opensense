namespace OpenSense.Core.Monitoring;

/// <summary>Malformed sensor readings must not become temperatures or poison the control step.</summary>
internal static class SensorReadings
{
    public static double? Temperature(double? value) =>
        value is > 0 and <= 150 && double.IsFinite(value.Value) ? value : null;
}
