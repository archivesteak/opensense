using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Tests;

public sealed class AcerServiceKeyboardTests
{
    [Fact]
    public void Describes_a_four_zone_keyboard_with_the_zoned_effects()
    {
        var light = AcerServiceKeyboardBackend.Describe();

        Assert.Equal(LightingLocation.Keyboard, light.Location);
        Assert.Equal(LightingBackendKind.AcerServiceKeyboard, light.Backend);
        Assert.Equal(4, light.Zones);
        Assert.True(light.ZoneSwitches);
        Assert.Equal(KeyboardProtocol.ZonedEffects.Count, light.Effects.Count);
        Assert.True(light.Offers(LightingEffect.Static));
        Assert.True(light.Offers(LightingEffect.Wave));
        Assert.False(light.Offers(LightingEffect.PerKey));
    }

    [Fact]
    public void Shares_its_id_with_the_native_keyboard_so_settings_carry_over()
    {
        Assert.Equal(EcKeyboardBackend.Id, AcerServiceKeyboardBackend.Describe().Id);
    }
}
