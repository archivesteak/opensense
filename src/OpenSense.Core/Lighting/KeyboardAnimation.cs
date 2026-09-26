using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

/// <summary>
/// The embedded controller's zoned keyboard lighting, run by run, so a preview moves exactly as the keyboard does (traced
/// from the AN515-57's controller firmware). The controller runs the effect once per <see cref="TickLength"/> and sends the
/// four zones to the keyboard after every <see cref="RunsPerFrame"/>th run; <see cref="Zones"/> is the last frame sent.
/// All of it is the controller's own integer arithmetic: truncating divisions, 8- and 16-bit wrap-around, and zones that
/// keep the last value written to them when an effect stops updating them. An effect starts over only when the effect
/// byte or the direction byte changes (even for an effect that ignores the direction); speed, colour and brightness are
/// taken up as they come. Starting over sends the zones black at once, but keeps the frame counter and Twinkling's
/// counter and on-time, as the controller does.
/// </summary>
public sealed class KeyboardAnimation
{
    public const int ZoneCount = 4;

    public const int RunsPerFrame = 4;

    /// <summary>
    /// How often the controller runs the effect. It is meant to be 10 ms (its backlight timeout counts down in these runs:
    /// 3000 of them for 30 s); the exact clock of the controller's timer isn't known.
    /// </summary>
    public static TimeSpan TickLength { get; } = TimeSpan.FromMilliseconds(10);

    // Breathing and Neon: a phase that climbs by the speed each run; bright at the top, and the run that passes the end is
    // dark and starts the next pulse.
    private const int PulseTop = 200;
    private const int PulseEnd = 2 * PulseTop + 1;

    // Shifting and Zoom: a timer that climbs by a multiple of the speed each run; the run that takes it past this moves the
    // pattern one step and starts the timer again.
    private const int StepAfter = 200;

    // Meteor's flags: a zone's bit (zone 1 lowest) while it is lit, and that bit shifted up four while it fades.
    private const int MeteorFading = 4;

    // Wave's flags per zone: bits 0-2 while red, green or blue fades, bits 3-5 while it takes part.
    private const int WaveOn = 3;

    // Twinkling's on-time for each selector 1-5 (the payload's byte 8): 3/10, 2/5, 1/2, 3/5, 7/10 of the blink.
    private static readonly (int Times, int Over)[] TwinkleShares = [(3, 10), (2, 5), (1, 2), (3, 5), (7, 10)];

    // Wave starts every zone part way through its colours, one zone after the other.
    private static readonly int[][] WaveStart = [[0, 128, 128], [0, 255, 0], [128, 128, 0], [255, 0, 0]];
    private static readonly int[] WaveFlagsForward = [0x32, 0x32, 0x19, 0x19];
    private static readonly int[] WaveFlagsBackward = [0x34, 0x1A, 0x1A, 0x29];

    // What the controller was sent: its bytes, as it keeps them. -1 until the first payload.
    private int _effect = -1;
    private int _direction = -1;
    private int _speed;
    private int _brightness;
    private int _twinkleSelector;
    private RgbColor _color;
    private readonly (bool On, RgbColor Color)[] _staticZones = new (bool, RgbColor)[ZoneCount];

    // The zones as the effect last wrote them (zone 1 first, R G B), and as the keyboard last got them.
    private readonly int[] _output = new int[ZoneCount * 3];
    private readonly RgbColor[] _sent = new RgbColor[ZoneCount];

    // Effect state, set up again when the effect starts over.
    private int _phase;
    private int _neonChannel;
    private int _stepFlags;
    private int _mask;
    private readonly int[] _waveLevels = new int[ZoneCount * 3];
    private readonly int[] _waveFlags = new int[ZoneCount];
    private readonly int[] _meteorLevels = new int[ZoneCount];
    private int _meteorFlags;
    private bool _meteorReturning;

    // Kept when the effect starts over.
    private int _frameCount;
    private int _twinkleCount;
    private int _twinkleOn;

    /// <summary>The four zones as the keyboard last got them (zone 1 first).</summary>
    public IReadOnlyList<RgbColor> Zones => _sent;

    /// <summary>The effect byte last received, or null before any.</summary>
    public KeyboardEffect? Effect => _effect < 0 ? null : (KeyboardEffect)_effect;

    /// <summary>Static's zone switches and colours (<c>SetGamingLED</c>, <c>SetGamingRgbKb</c>), zone 1 first; zones left out are dark.</summary>
    public void SetStaticZones(IReadOnlyList<(bool On, RgbColor Color)> zones)
    {
        for (var z = 0; z < ZoneCount; z++)
            _staticZones[z] = z < zones.Count ? zones[z] : (false, default);
    }

    /// <summary>
    /// Takes a <c>SetGamingKBBacklight</c> payload (<see cref="KeyboardProtocol.BacklightPayload"/>): effect, speed,
    /// brightness, direction and colour from the first record, Twinkling's on-time selector from byte 8.
    /// </summary>
    public void Receive(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new ArgumentException("A backlight payload has at least 8 bytes.", nameof(payload));
        _speed = payload[1];
        // The controller works in 0..100; Acer's software and OpenSense never send more.
        _brightness = Math.Min((int)payload[2], 100);
        _color = new RgbColor(payload[5], payload[6], payload[7]);
        _twinkleSelector = payload.Length > 8 ? payload[8] : 0;
        if (payload[0] != _effect || payload[4] != _direction)
        {
            _effect = payload[0];
            _direction = payload[4];
            StartOver();
        }
    }

    /// <summary>
    /// Takes <paramref name="lighting"/> as OpenSense sends it to a zoned keyboard (<see cref="EcKeyboardBackend"/>) with
    /// <paramref name="zones"/> zones. The colours are as chosen: the per-model correction only evens out the LEDs.
    /// </summary>
    public void Show(LightingSettings lighting, int zones)
    {
        if (EcKeyboardBackend.BacklightPayload(lighting) is not { } payload)
            return;
        SetStaticZones([.. Enumerable.Range(0, Math.Min(zones, ZoneCount)).Select(i => (lighting.Zone(i).On, RgbColor.FromHex(lighting.Zone(i).Color)))]);
        Receive(payload);
    }

    /// <summary>Runs the effect <paramref name="runs"/> times.</summary>
    public void Advance(int runs)
    {
        for (var i = 0; i < runs; i++)
            Run();
    }

    /// <summary>A zone as the effect last wrote it, which the keyboard gets with the next frame.</summary>
    internal RgbColor Pending(int zone) => new((byte)_output[zone * 3], (byte)_output[zone * 3 + 1], (byte)_output[zone * 3 + 2]);

    private void StartOver()
    {
        _phase = 0;
        _neonChannel = 0;
        _stepFlags = 1;
        _mask = 0;
        var flags = _direction == 1 ? WaveFlagsForward : WaveFlagsBackward;
        for (var z = 0; z < ZoneCount; z++)
        {
            WaveStart[z].CopyTo(_waveLevels, z * 3);
            _waveFlags[z] = flags[z];
        }
        Array.Clear(_meteorLevels);
        _meteorFlags = 1;
        _meteorReturning = false;
        Array.Clear(_output);
        Array.Clear(_sent);
    }

    private void Run()
    {
        if (_effect < 0)
            return;
        if (_effect == (int)KeyboardEffect.Static)
        {
            // Static goes out as soon as it changes, not on the frame rhythm.
            RunStatic();
            Send();
            return;
        }
        switch ((KeyboardEffect)_effect)
        {
            case KeyboardEffect.Breathing: RunBreathing(); break;
            case KeyboardEffect.Neon: RunNeon(); break;
            case KeyboardEffect.Wave: RunWave(); break;
            case KeyboardEffect.Shifting: RunShifting(); break;
            case KeyboardEffect.Zoom: RunZoom(); break;
            case KeyboardEffect.Meteor: RunMeteor(); break;
            case KeyboardEffect.Twinkling: RunTwinkling(); break;
            default: break; // an effect the controller doesn't know: the zones stay as they are, the frames still go out
        }
        // The counter is tested before it counts: 0, 1, 2, 3, then a frame and 0 again.
        var count = _frameCount;
        _frameCount = (count + 1) & 0xFF;
        if (count >= RunsPerFrame - 1)
        {
            _frameCount = 0;
            Send();
        }
    }

    private void Send()
    {
        for (var z = 0; z < ZoneCount; z++)
            _sent[z] = Pending(z);
    }

    private int Dim(int value) => value * _brightness / 100;

    private void Write(int zone, RgbColor color)
    {
        _output[zone * 3] = color.R;
        _output[zone * 3 + 1] = color.G;
        _output[zone * 3 + 2] = color.B;
    }

    private void Fill(RgbColor color)
    {
        for (var z = 0; z < ZoneCount; z++)
            Write(z, color);
    }

    /// <summary>The effect colour times <paramref name="level"/>/<paramref name="full"/>, then at the brightness.</summary>
    private RgbColor Scaled(int level, int full) =>
        new((byte)Dim(_color.R * level / full), (byte)Dim(_color.G * level / full), (byte)Dim(_color.B * level / full));

    private void RunStatic()
    {
        for (var z = 0; z < ZoneCount; z++)
        {
            var (on, c) = _staticZones[z];
            Write(z, on ? new RgbColor((byte)Dim(c.R), (byte)Dim(c.G), (byte)Dim(c.B)) : default);
        }
    }

    /// <summary>Breathing's and Neon's pulse: 0..200 and back down, or null on the dark run between two pulses.</summary>
    private int? Pulse()
    {
        if (_phase < 0xFFFF)
            _phase = (_phase + _speed) & 0xFFFF;
        if (_phase >= PulseEnd)
        {
            _phase = 0;
            return null;
        }
        return _phase <= PulseTop ? _phase : PulseEnd - _phase;
    }

    private void RunBreathing() => Fill(Pulse() is { } level ? Scaled(level, PulseTop) : default);

    /// <summary>Red, then green, then blue: each one pulse, the other channels left as they are.</summary>
    private void RunNeon()
    {
        if (Pulse() is not { } level)
        {
            for (var z = 0; z < ZoneCount; z++)
                _output[z * 3 + _neonChannel] = 0;
            _neonChannel = (_neonChannel + 1) % 3;
            return;
        }
        var value = Dim(255 * level / PulseTop);
        for (var z = 0; z < ZoneCount; z++)
            _output[z * 3 + _neonChannel] = value;
    }

    /// <summary>
    /// Each zone's red, green and blue rise and fall in turn, handing over at the top: red to green to blue with direction
    /// 1, red to blue to green otherwise. Zones and channels are done in order, so a channel handed to one not yet done
    /// moves in the same run.
    /// </summary>
    private void RunWave()
    {
        var forward = _direction == 1;
        for (var z = 0; z < ZoneCount; z++)
        {
            for (var c = 0; c < 3; c++)
            {
                var fading = 1 << c;
                if ((_waveFlags[z] & (1 << (c + WaveOn))) == 0)
                    continue;
                var i = z * 3 + c;
                if (_waveLevels[i] < 255 && (_waveFlags[z] & fading) == 0)
                {
                    if (_waveLevels[i] < 255 - _speed)
                    {
                        _waveLevels[i] += _speed;
                        _output[i] = Dim(_waveLevels[i]);
                    }
                    else
                    {
                        // At the top: start fading, bring the next channel in and drop the one before (its output as it was).
                        _waveLevels[i] = 255;
                        _waveFlags[z] |= fading;
                        var next = forward ? (c + 1) % 3 : (c + 2) % 3;
                        var before = forward ? (c + 2) % 3 : (c + 1) % 3;
                        _waveLevels[z * 3 + before] = 0;
                        _waveFlags[z] &= ~(1 << (before + WaveOn));
                        _waveFlags[z] |= 1 << (next + WaveOn);
                    }
                }
                if ((_waveFlags[z] & fading) != 0)
                {
                    if (_waveLevels[i] > _speed)
                    {
                        _waveLevels[i] -= _speed;
                        _output[i] = Dim(_waveLevels[i]);
                    }
                    else
                    {
                        // Out: the channel keeps its last output.
                        _waveLevels[i] = 0;
                        _waveFlags[z] &= ~fading;
                    }
                }
            }
        }
    }

    /// <summary>
    /// A pulse of the colour runs from zone 1 to zone 4 and back: each zone rises, and at the top starts fading and lights
    /// the next. The end zones pulse twice, once on the way there and once turning back.
    /// </summary>
    private void RunMeteor()
    {
        for (var z = 0; z < ZoneCount; z++)
        {
            var lit = 1 << z;
            var fading = lit << MeteorFading;
            if ((_meteorFlags & lit) == 0)
                continue;
            if (_meteorLevels[z] < 255 && (_meteorFlags & fading) == 0)
            {
                if (_meteorLevels[z] < 255 - _speed)
                {
                    _meteorLevels[z] += _speed;
                    Write(z, Scaled(_meteorLevels[z], 255));
                }
                else
                {
                    _meteorLevels[z] = 255;
                    _meteorFlags |= fading;
                    var next = z + (_meteorReturning ? -1 : 1);
                    if (next is >= 0 and < ZoneCount)
                        _meteorFlags |= 1 << next;
                }
            }
            if ((_meteorFlags & fading) != 0)
            {
                if (_meteorLevels[z] > _speed)
                {
                    _meteorLevels[z] -= _speed;
                    Write(z, Scaled(_meteorLevels[z], 255));
                }
                else
                {
                    // Out, keeping its last output. An end zone lights again to turn the pulse round.
                    _meteorLevels[z] = 0;
                    _meteorFlags &= ~(lit | fading);
                    if (z == ZoneCount - 1 && !_meteorReturning)
                    {
                        _meteorFlags |= lit;
                        _meteorReturning = true;
                    }
                    else if (z == 0 && _meteorReturning)
                    {
                        _meteorFlags |= lit;
                        _meteorReturning = false;
                    }
                }
            }
        }
    }

    /// <summary>The pattern moves one step when the timer, climbing by <paramref name="rate"/> times the speed, passes 200.</summary>
    private bool StepDue(int rate)
    {
        _phase = (_phase + rate * _speed) & 0xFFFF;
        if (_phase <= StepAfter)
            return false;
        _phase = 0;
        return true;
    }

    /// <summary>
    /// The zones fill with the colour one by one from one end, then empty one by one from the same end: from zone 1 with
    /// direction 1, from zone 4 otherwise. It starts dark.
    /// </summary>
    private void RunShifting()
    {
        if (StepDue(4))
        {
            var filling = (_stepFlags & 1) != 0;
            _mask = _direction == 1
                ? ((_mask << 2) | (filling ? 0b11 : 0)) & 0xFF
                : (_mask >> 2) | (filling ? 0b1100_0000 : 0);
            if (filling && _mask == 0xFF)
                _stepFlags = (_stepFlags & ~1) | 2;
            else if (_mask == 0 && (_stepFlags & 2) != 0)
                _stepFlags = (_stepFlags & ~2) | 1;
        }
        ShowMask();
    }

    /// <summary>The middle zones, then all four, then the middle ones, then none; it starts dark.</summary>
    private void RunZoom()
    {
        if (StepDue(2))
        {
            (_stepFlags, _mask) = _stepFlags switch
            {
                1 => (2, 0b0011_1100),
                2 => (4, 0xFF),
                4 => (8, 0b0011_1100),
                8 => (1, 0),
                _ => (_stepFlags, _mask),
            };
        }
        ShowMask();
    }

    /// <summary>Shifting's and Zoom's zones: two bits each, zone 1 lowest; lit in the colour when either is set.</summary>
    private void ShowMask()
    {
        var color = Scaled(1, 1);
        for (var z = 0; z < ZoneCount; z++)
            Write(z, ((_mask >> (z * 2)) & 0b11) != 0 ? color : default);
    }

    /// <summary>
    /// The whole keyboard blinks: on for the on-time, then off to the end of the blink. The blink lasts 150 - 10 × speed
    /// runs (16-bit, so speeds over 15 wrap round to long ones); an unknown selector keeps the on-time there was.
    /// </summary>
    private void RunTwinkling()
    {
        _twinkleCount = (_twinkleCount + 1) & 0xFFFF;
        var period = (150 - 10 * _speed) & 0xFFFF;
        _phase = period;
        if (_twinkleSelector is >= 1 and <= 5)
        {
            var (times, over) = TwinkleShares[_twinkleSelector - 1];
            _twinkleOn = ((period * times) & 0xFFFF) / over;
        }
        if (_twinkleCount <= _twinkleOn)
        {
            Fill(Scaled(1, 1));
        }
        else
        {
            Fill(default);
            if (_twinkleCount >= period)
                _twinkleCount = 0;
        }
    }
}
