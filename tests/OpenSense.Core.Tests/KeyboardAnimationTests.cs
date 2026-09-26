using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Tests;

/// <summary>The zoned keyboard's effects as the AN515-57's controller runs them, with values worked out by hand.</summary>
public class KeyboardAnimationTests
{
    private static readonly RgbColor Pink = new(255, 45, 85);

    private static KeyboardAnimation Start(KeyboardEffect effect, int speed, int brightness = 100, int direction = 1, RgbColor? color = null, int selector = 3)
    {
        var animation = new KeyboardAnimation();
        animation.Receive(Payload(effect, speed, brightness, direction, color ?? Pink, selector));
        return animation;
    }

    private static byte[] Payload(KeyboardEffect effect, int speed, int brightness, int direction, RgbColor color, int selector = 3)
    {
        var payload = new byte[16];
        payload[0] = (byte)effect;
        payload[1] = (byte)speed;
        payload[2] = (byte)brightness;
        payload[4] = (byte)direction;
        payload[5] = color.R;
        payload[6] = color.G;
        payload[7] = color.B;
        payload[8] = (byte)selector;
        return payload;
    }

    private static RgbColor[] Pending(KeyboardAnimation animation) => [.. Enumerable.Range(0, KeyboardAnimation.ZoneCount).Select(animation.Pending)];

    private static RgbColor[] All(RgbColor color) => [color, color, color, color];

    private static RgbColor[] Dark => All(default);

    [Fact]
    public void A_frame_goes_out_after_every_fourth_run()
    {
        var animation = Start(KeyboardEffect.Breathing, 1);
        List<int> sent = [];
        var previous = animation.Zones.ToArray();
        for (var run = 1; run <= 12; run++)
        {
            animation.Advance(1);
            if (!animation.Zones.SequenceEqual(previous))
                sent.Add(run);
            previous = [.. animation.Zones];
        }
        Assert.Equal([4, 8, 12], sent);
        // Run 12's pulse level: 255 × 12 / 200 = 15, 45 × 12 / 200 = 2, 85 × 12 / 200 = 5.
        Assert.Equal(All(new RgbColor(15, 2, 5)), animation.Zones);
    }

    [Fact]
    public void Starting_over_sends_black_at_once_but_keeps_the_frame_rhythm()
    {
        var animation = Start(KeyboardEffect.Breathing, 50);
        animation.Advance(6);
        Assert.NotEqual(Dark, animation.Zones);

        animation.Receive(Payload(KeyboardEffect.Neon, 50, 100, 0, default));
        Assert.Equal(Dark, animation.Zones);
        // Two runs into the second frame when Neon started: its first frame comes after two more runs, not four.
        animation.Advance(1);
        Assert.Equal(Dark, animation.Zones);
        animation.Advance(1);
        Assert.Equal(All(new RgbColor(127, 0, 0)), animation.Zones);
    }

    [Fact]
    public void Speed_colour_and_brightness_go_on_without_starting_over_but_a_new_direction_byte_starts_over()
    {
        var animation = Start(KeyboardEffect.Meteor, 5, direction: 0);
        animation.Advance(40);
        animation.Receive(Payload(KeyboardEffect.Meteor, 9, 50, 0, new RgbColor(0, 255, 0)));
        Assert.NotEqual(Dark, animation.Zones);
        animation.Advance(1);
        // The level went on climbing from 200, now by 9, in the new colour at half brightness.
        Assert.Equal(new RgbColor(0, 104, 0), animation.Pending(0));

        // Meteor ignores the direction, but the controller still starts over when its byte changes.
        animation.Receive(Payload(KeyboardEffect.Meteor, 9, 50, 1, new RgbColor(0, 255, 0)));
        Assert.Equal(Dark, animation.Zones);
        Assert.Equal(Dark, Pending(animation));
    }

    [Fact]
    public void Breathing_is_full_for_two_runs_at_the_top_and_dark_for_one_between_pulses()
    {
        var animation = Start(KeyboardEffect.Breathing, 1);
        Dictionary<int, RgbColor> level = [];
        for (var run = 1; run <= 402; run++)
        {
            animation.Advance(1);
            level[run] = animation.Pending(0);
        }
        Assert.Equal(new RgbColor(1, 0, 0), level[1]);
        Assert.Equal(Pink, level[200]);
        Assert.Equal(Pink, level[201]);
        Assert.Equal(new RgbColor(1, 0, 0), level[400]);
        Assert.Equal(default, level[401]);
        Assert.Equal(new RgbColor(1, 0, 0), level[402]);
    }

    [Fact]
    public void Brightness_divides_after_the_pulse_both_truncating()
    {
        var animation = Start(KeyboardEffect.Breathing, 1, brightness: 50);
        animation.Advance(200);
        Assert.Equal(new RgbColor(127, 22, 42), animation.Pending(0));
    }

    [Fact]
    public void Neon_pulses_red_then_green_then_blue()
    {
        var animation = Start(KeyboardEffect.Neon, 1, color: default);
        animation.Advance(200);
        Assert.Equal(All(new RgbColor(255, 0, 0)), Pending(animation));
        animation.Advance(201);
        Assert.Equal(Dark, Pending(animation));
        animation.Advance(1);
        Assert.Equal(All(new RgbColor(0, 1, 0)), Pending(animation));
        animation.Advance(401 + 200);
        Assert.Equal(All(new RgbColor(0, 0, 255)), Pending(animation));
    }

    [Theory]
    [InlineData(1, new byte[] { 0, 127, 129, 0, 254, 1, 127, 129, 0, 254, 1, 0 })]
    [InlineData(2, new byte[] { 0, 129, 127, 1, 254, 0, 129, 127, 0, 254, 0, 1 })]
    public void Wave_starts_each_zone_part_way_through_its_colours(int direction, byte[] expected)
    {
        var animation = Start(KeyboardEffect.Wave, 1, direction: direction, color: default);
        animation.Advance(1);
        Assert.Equal(expected, Pending(animation).SelectMany(c => new[] { c.R, c.G, c.B }));
    }

    [Theory]
    [InlineData(1, 0, 1, 254)]
    [InlineData(2, 0, 254, 2)]
    public void Wave_leaves_a_finished_channel_at_its_last_output(int direction, byte r, byte g, byte b)
    {
        var animation = Start(KeyboardEffect.Wave, 1, direction: direction, color: default);
        animation.Advance(127);
        Assert.Equal(new RgbColor(r, g, b), animation.Pending(0));
    }

    [Fact]
    public void Meteor_runs_to_zone_four_and_back_leaving_faint_tails()
    {
        var animation = Start(KeyboardEffect.Meteor, 5);
        var peak = new RgbColor(250, 44, 83);
        var tail = new RgbColor(5, 0, 1);
        animation.Advance(50);
        Assert.Equal([peak, default, default, default], Pending(animation));
        // At the top zone 1 turns to fade and zone 2 starts in the same run.
        animation.Advance(1);
        Assert.Equal([peak, tail, default, default], Pending(animation));
        // Zone 1 has gone out but keeps its last, faint output.
        animation.Advance(50);
        Assert.Equal([tail, peak, tail, default], Pending(animation));
        animation.Advance(100);
        Assert.Equal([tail, tail, tail, peak], Pending(animation));
        // Zone 4 pulses again to turn round; the pulse is back in zone 1 at run 455.
        animation.Advance(254);
        Assert.Equal([peak, tail, tail, tail], Pending(animation));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 3)]
    public void Shifting_starts_dark_and_fills_from_the_end_the_direction_names(int direction, int first)
    {
        var animation = Start(KeyboardEffect.Shifting, 1, direction: direction);
        animation.Advance(50);
        Assert.Equal(Dark, Pending(animation));
        animation.Advance(1);
        Assert.Equal(Enumerable.Range(0, 4).Select(z => z == first ? Pink : default), Pending(animation));
        animation.Advance(51);
        var second = direction == 1 ? 1 : 2;
        Assert.Equal(Enumerable.Range(0, 4).Select(z => z == first || z == second ? Pink : default), Pending(animation));
    }

    [Fact]
    public void Zoom_lights_the_middle_then_all_then_the_middle_then_none()
    {
        var animation = Start(KeyboardEffect.Zoom, 1);
        animation.Advance(100);
        Assert.Equal(Dark, Pending(animation));
        animation.Advance(1);
        Assert.Equal([default, Pink, Pink, default], Pending(animation));
        animation.Advance(101);
        Assert.Equal(All(Pink), Pending(animation));
        animation.Advance(101);
        Assert.Equal([default, Pink, Pink, default], Pending(animation));
        animation.Advance(101);
        Assert.Equal(Dark, Pending(animation));
    }

    [Fact]
    public void Twinkling_blinks_the_whole_keyboard_on_for_half_of_each_blink()
    {
        // Speed 5: a blink of 150 - 50 = 100 runs; selector 3 lights the first half.
        var animation = Start(KeyboardEffect.Twinkling, 5);
        animation.Advance(50);
        Assert.Equal(All(Pink), Pending(animation));
        animation.Advance(1);
        Assert.Equal(Dark, Pending(animation));
        animation.Advance(49);
        Assert.Equal(Dark, Pending(animation));
        animation.Advance(1);
        Assert.Equal(All(Pink), Pending(animation));
    }

    [Fact]
    public void Twinkling_in_nitrosenses_layout_stays_dark()
    {
        // NitroSense repeats the record, so byte 8 is the effect (7): no selector, and the on-time is still none.
        var animation = Start(KeyboardEffect.Twinkling, 5, selector: 7);
        for (var run = 0; run < 300; run++)
        {
            animation.Advance(1);
            Assert.Equal(Dark, Pending(animation));
        }
    }

    [Fact]
    public void Twinkling_keeps_its_count_when_it_starts_over()
    {
        var animation = Start(KeyboardEffect.Twinkling, 5);
        animation.Advance(75);
        animation.Receive(Payload(KeyboardEffect.Breathing, 5, 100, 0, Pink));
        animation.Receive(Payload(KeyboardEffect.Twinkling, 5, 100, 0, Pink));
        // Run 76 of the blink, past its on-time: the keyboard stays dark until the blink ends.
        animation.Advance(1);
        Assert.Equal(Dark, Pending(animation));
        animation.Advance(24);
        animation.Advance(1);
        Assert.Equal(All(Pink), Pending(animation));
    }

    [Fact]
    public void Static_lights_the_zones_that_are_on_at_once()
    {
        var animation = new KeyboardAnimation();
        animation.SetStaticZones([(true, new RgbColor(255, 0, 0)), (false, new RgbColor(0, 255, 0)), (true, new RgbColor(0, 0, 255)), (true, new RgbColor(101, 3, 200))]);
        animation.Receive(KeyboardProtocol.BacklightPayload(KeyboardEffect.Static, 0, 50, KeyboardDirection.Right, default));
        animation.Advance(1);
        Assert.Equal([new RgbColor(127, 0, 0), default, new RgbColor(0, 0, 127), new RgbColor(50, 1, 100)], animation.Zones);
    }

    [Fact]
    public void Settings_go_to_the_model_as_the_backend_sends_them()
    {
        var animation = new KeyboardAnimation();
        animation.Show(new LightingSettings { Effect = LightingEffect.Twinkling, Speed = 5, Brightness = 100, EffectColor = "#FF2D55" }, 4);
        Assert.Equal(KeyboardEffect.Twinkling, animation.Effect);
        // PredatorSense's layout carries the selector (3), so it blinks.
        animation.Advance(4);
        Assert.Equal(All(Pink), animation.Zones);

        animation.Show(new LightingSettings
        {
            Effect = LightingEffect.Static,
            Brightness = 100,
            Zones = [new(true, "#010203"), new(false, "#FFFFFF"), new(true, "#FFFFFF")],
        }, 3);
        animation.Advance(1);
        Assert.Equal([new RgbColor(1, 2, 3), default, RgbColor.White, default], animation.Zones);
    }

    [Theory]
    [InlineData(LightingEffect.Static, new byte[] { 0, 0, 75, 0, 0, 0, 0, 0, 0, 1 })]
    [InlineData(LightingEffect.Wave, new byte[] { 3, 9, 75, 0x08, 2, 0, 0, 0, 3, 1 })]
    [InlineData(LightingEffect.Meteor, new byte[] { 6, 9, 75, 0, 0, 0x10, 0x20, 0x30, 3, 1 })]
    public void The_backends_payload_clamps_the_speed_and_keeps_the_chosen_colour(LightingEffect effect, byte[] expected)
    {
        var lighting = new LightingSettings { Effect = effect, Speed = 12, Brightness = 75, Direction = LightingDirection.Left, EffectColor = "#102030" };
        Assert.Equal(expected, EcKeyboardBackend.BacklightPayload(lighting)![..10]);
        Assert.Null(EcKeyboardBackend.BacklightPayload(lighting with { Effect = LightingEffect.Ripple }));
    }
}
