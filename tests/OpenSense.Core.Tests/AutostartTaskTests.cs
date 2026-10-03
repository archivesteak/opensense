using System.Xml.Linq;
using OpenSense.App.Services;

namespace OpenSense.Core.Tests;

public class AutostartTaskTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";

    private const string Here = @"D:\New place\OpenSense.exe";
    private const string OldPlace = @"D:\Old place\OpenSense.exe"; // gone: the copy was moved from here
    private const string OtherCopy = @"D:\Other copy\OpenSense.exe"; // still on disk

    [Fact]
    public void The_task_starts_at_sign_in_with_full_rights_and_keeps_running_on_battery()
    {
        var task = XDocument.Parse(AutostartTask.BuildXml(@"D:\Apps\OpenSense\OpenSense.exe", Sid)).Root!;
        string Value(params string[] path) => path.Aggregate(task, (element, name) => element.Element(task.Name.Namespace + name)!).Value;

        Assert.Equal(Sid, Value("Triggers", "LogonTrigger", "UserId"));
        Assert.Equal("HighestAvailable", Value("Principals", "Principal", "RunLevel"));
        Assert.Equal("--autostart", Value("Actions", "Exec", "Arguments"));
        // Task Scheduler's defaults would not start it on battery, stop it when the charger comes out, and end it after 72 hours.
        Assert.Equal("false", Value("Settings", "DisallowStartIfOnBatteries"));
        Assert.Equal("false", Value("Settings", "StopIfGoingOnBatteries"));
        Assert.Equal("PT0S", Value("Settings", "ExecutionTimeLimit"));
    }

    [Theory]
    [InlineData(@"D:\Apps\OpenSense\OpenSense.exe")]
    [InlineData(@"C:\Program Files\Open Sense\OpenSense.exe")]
    [InlineData(@"E:\R&D 'beta'\Ünïcode\OpenSense.exe")]
    public void The_program_comes_back_the_way_it_went_in(string exe) =>
        Assert.Equal(exe, AutostartTask.ReadTarget(AutostartTask.BuildXml(exe, Sid)));

    [Fact]
    public void A_task_switched_off_in_Task_Scheduler_counts_as_no_task()
    {
        var xml = AutostartTask.BuildXml(Here, Sid).Replace("<Priority>4</Priority>", "<Priority>4</Priority><Enabled>false</Enabled>");
        Assert.Null(AutostartTask.ReadTarget(xml));
    }

    [Theory]
    [InlineData(null, false, false)] // nothing set up
    [InlineData(Here, false, false)] // the task already starts this copy
    [InlineData(@"d:\NEW place\opensense.EXE", false, false)] // the same file, differently cased
    [InlineData(OldPlace, false, true)] // this copy was moved: the task names a place that is gone, so it follows
    [InlineData(OtherCopy, false, false)] // another copy that still exists keeps its task
    [InlineData(null, true, true)] // an old Run entry (from versions before the task) moves into it
    [InlineData(Here, true, true)] // ...and is cleaned up when the task is already there
    [InlineData(OtherCopy, true, false)] // ...but does not take another copy's task
    public void A_copy_keeps_its_task_pointed_at_itself(string? taskTarget, bool runEntryIsThisCopy, bool expected) =>
        Assert.Equal(expected, AutostartTask.NeedsWrite(taskTarget, runEntryIsThisCopy, Here, path => path == OtherCopy));
}
