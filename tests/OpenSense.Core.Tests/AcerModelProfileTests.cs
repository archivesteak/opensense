using System.Globalization;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

public sealed class AcerModelProfileTests
{
    [Theory]
    [InlineData("Nitro AN515-57-R86A", "AN515-57", false)]
    [InlineData("Acer Nitro AN515-58", "AN515-58", true)]
    [InlineData("Nitro AN517-43-R6SH", "AN517-43", true)]
    [InlineData("Nitro AN515-51s", "AN515-51S", false)]
    public void Full_dmi_names_match_their_owned_model_defaults(string model, string code, bool modes)
    {
        var profile = AcerModelProfile.For(model);

        Assert.Equal(code, profile.ModelCode);
        Assert.Equal(modes, profile.OperatingModes);
        Assert.True(profile.CpuFan);
        Assert.True(profile.GpuFan);
        Assert.False(profile.SystemFan);
        Assert.Equal(KeyboardPayloadLayout.NitroSense, profile.KeyboardLayout);
        Assert.Equal([1d, 1d, 1d], profile.KeyboardColorScale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nitro AN515-580")]
    [InlineData("XAN515-58")]
    [InlineData("Nitro AN515-58X")]
    [InlineData("Nitro AN515-51")]
    [InlineData("Unreviewed ANV16-41")]
    public void Unreviewed_or_partial_model_names_do_not_inherit_defaults(string? model)
    {
        var profile = AcerModelProfile.For(model);

        Assert.Null(profile.ModelCode);
        Assert.Null(profile.OperatingModes);
        Assert.Null(profile.CpuFan);
        Assert.Null(profile.KeyboardLayout);
    }

    [Fact]
    public void Feature_only_older_models_keep_missing_fan_and_setting_metadata_unknown()
    {
        var profile = AcerModelProfile.For("AN515-52");

        Assert.False(profile.OperatingModes);
        Assert.Null(profile.CpuFan);
        Assert.Null(profile.GpuFan);
        Assert.Null(profile.SystemFan);
        Assert.Null(profile.WindowsKeyLock);
        Assert.Null(profile.LcdOverdrive);
        Assert.Equal(KeyboardPayloadLayout.NitroSense, profile.KeyboardLayout);
    }

    [Theory]
    [InlineData("AN515-58")]
    [InlineData("AN517-55")]
    public void Per_unit_keyboard_properties_are_not_fabricated_from_generic_settings(string model)
    {
        var profile = AcerModelProfile.For(model);

        Assert.Null(profile.RgbKeyboard);
        Assert.Null(profile.KeyboardZones);
        Assert.Null(profile.KeyboardTimeoutHotkey);
        Assert.True(profile.WindowsKeyLock);
        Assert.True(profile.LcdOverdrive);
    }

    [Fact]
    public void Measured_legacy_timeout_candidate_and_two_gpu_fan_model_are_preserved()
    {
        Assert.Equal((byte)0x84, AcerModelProfile.For("AN515-57").KeyboardTimeoutHotkey);
        Assert.Null(AcerModelProfile.For("AN515-55").KeyboardTimeoutHotkey);
        Assert.Null(AcerModelProfile.For("AN515-57").RgbKeyboard);
        Assert.Null(AcerModelProfile.For("AN515-57").KeyboardZones);
        var dualGpu = AcerModelProfile.For("Predator PTX17-71");
        Assert.Equal(1, dualGpu.CpuFanCount);
        Assert.Equal(2, dualGpu.GpuFanCount);
        Assert.Equal(0, dualGpu.SystemFanCount);
        Assert.Null(dualGpu.KeyboardLayout);
    }

    [Fact]
    public void Matching_is_independent_of_current_culture()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("AN515-51S", AcerModelProfile.For("nitro anv16-41; an515-51s").ModelCode);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
