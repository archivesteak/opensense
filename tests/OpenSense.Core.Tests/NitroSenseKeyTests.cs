using Microsoft.Extensions.Time.Testing;
using OpenSense.App.Services;

namespace OpenSense.Core.Tests;

public class NitroSenseKeyTests
{
    [Fact]
    public void Keyboard_press_fires_once_until_break_even_while_held()
    {
        using var tracker = new NitroSenseKeyTracker();

        Assert.True(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: false));
        Assert.False(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: false));
        Assert.False(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: true));
        Assert.True(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: false));
    }

    [Fact]
    public void Keyboard_ignores_other_scan_codes_and_non_extended_keys()
    {
        using var tracker = new NitroSenseKeyTracker();

        Assert.False(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: false, isKeyUp: false));
        Assert.False(tracker.HandleKeyboardInput(0x74, isExtendedE0: true, isKeyUp: false));
        Assert.True(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: false));
    }

    [Fact]
    public void ResetKeyState_recovers_after_missed_break_event()
    {
        using var tracker = new NitroSenseKeyTracker();

        Assert.True(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: false));
        Assert.False(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: false));

        tracker.ResetKeyState();

        Assert.True(tracker.HandleKeyboardInput(NitroSenseKeyTracker.KeyboardScanCode, isExtendedE0: true, isKeyUp: false));
    }

    [Fact]
    public void Hid_matches_acer_signature_and_rejects_non_matching_reports()
    {
        byte[] hit = [0x00, 0x81, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00];
        byte[] wrongByte = [0x01, 0x81, 0xFE, 0x00];
        byte[] shiftedOffset = [0x01, 0x00, 0x81, 0xFF];

        Assert.True(NitroSenseKeyTracker.MatchesHidReports(hit, reportSize: hit.Length, reportCount: 1));
        Assert.False(NitroSenseKeyTracker.MatchesHidReports(wrongByte, reportSize: wrongByte.Length, reportCount: 1));
        Assert.False(NitroSenseKeyTracker.MatchesHidReports(shiftedOffset, reportSize: shiftedOffset.Length, reportCount: 1));
    }

    [Fact]
    public void Hid_finds_signature_in_later_batched_report()
    {
        byte[] batched =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x02, 0x81, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00,
        ];

        Assert.True(NitroSenseKeyTracker.MatchesHidReports(batched, reportSize: 8, reportCount: 2));
    }

    [Fact]
    public void Hid_handles_short_and_extreme_bounds_without_overflow()
    {
        byte[] tooShort = [0x00, 0x81];
        byte[] validBytes = [0x00, 0x81, 0xFF, 0x00];
        byte[] nonMatching = [0x00, 0x00, 0x00, 0x00];

        Assert.False(NitroSenseKeyTracker.MatchesHidReports(ReadOnlySpan<byte>.Empty, reportSize: 8, reportCount: 1));
        Assert.False(NitroSenseKeyTracker.MatchesHidReports(tooShort, reportSize: 8, reportCount: 1));
        Assert.False(NitroSenseKeyTracker.MatchesHidReports(validBytes, reportSize: 2, reportCount: 1));
        Assert.False(NitroSenseKeyTracker.MatchesHidReports(validBytes, reportSize: 4, reportCount: 0));
        Assert.False(NitroSenseKeyTracker.MatchesHidReports(nonMatching, reportSize: int.MaxValue, reportCount: 4));
    }

    [Fact]
    public void Retry_recovers_after_transient_startup_failure()
    {
        var time = new FakeTimeProvider();
        using var tracker = new NitroSenseKeyTracker(time);
        var attempts = 0;

        void Attempt()
        {
            attempts++;
            if (attempts < 2)
                tracker.ScheduleRetry(Attempt);
            else
                tracker.CancelRetry();
        }

        tracker.Enable();
        Assert.Equal(NitroSenseKeyTracker.DefaultRetryDelays[0], tracker.ScheduleRetry(Attempt));
        Assert.True(tracker.HasPendingRetry);

        time.Advance(TimeSpan.FromMilliseconds(900));
        Assert.Equal(0, attempts);

        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, attempts);
        Assert.True(tracker.HasPendingRetry);

        time.Advance(NitroSenseKeyTracker.DefaultRetryDelays[1]);
        Assert.Equal(2, attempts);
        Assert.False(tracker.HasPendingRetry);

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void Retry_stops_after_budget_and_resets_for_unlock_recovery()
    {
        var time = new FakeTimeProvider();
        using var tracker = new NitroSenseKeyTracker(time);
        var attempts = 0;

        tracker.Enable();
        foreach (var expectedDelay in NitroSenseKeyTracker.DefaultRetryDelays)
        {
            Assert.Equal(expectedDelay, tracker.ScheduleRetry(() => attempts++));
            time.Advance(expectedDelay);
        }

        Assert.Equal(NitroSenseKeyTracker.DefaultRetryDelays.Count, attempts);
        Assert.Null(tracker.ScheduleRetry(() => attempts++));
        Assert.False(tracker.HasPendingRetry);

        tracker.ResetRetries();
        Assert.Equal(NitroSenseKeyTracker.DefaultRetryDelays[0], tracker.ScheduleRetry(() => attempts++));
    }

    [Fact]
    public void Disable_drops_pending_and_already_queued_retry_callbacks()
    {
        var time = new FakeTimeProvider();
        var queued = new Queue<Action>();
        using var tracker = new NitroSenseKeyTracker(time, dispatch: queued.Enqueue);
        var attempts = 0;

        tracker.Enable();
        Assert.NotNull(tracker.ScheduleRetry(() => attempts++));
        time.Advance(NitroSenseKeyTracker.DefaultRetryDelays[0]);
        Assert.Single(queued);

        tracker.Disable();
        Assert.False(tracker.HasPendingRetry);

        while (queued.TryDequeue(out var work))
            work();

        Assert.Equal(0, attempts);
    }
}
