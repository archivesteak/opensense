using System.Xml;
using System.Xml.Linq;

namespace OpenSense.App.Services;

/// <summary>
/// The Task Scheduler task that starts a portable copy when its user signs in, and when a copy has to rewrite it. A portable
/// copy runs elevated, so a Run entry would make Windows ask about it at every sign-in; a task that runs with the highest
/// rights available starts it without asking. Text and paths only (no Windows calls), so the tests can run it.
/// </summary>
internal static class AutostartTask
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private const string Description =
        "Starts OpenSense when you sign in. Made by its Start with Windows setting; switch that off to remove this task.";

    /// <summary>Task XML (schema 1.2) that starts <paramref name="exePath"/> in the notification area when <paramref name="userSid"/> signs in.</summary>
    public static string BuildXml(string exePath, string userSid)
    {
        var task = new XElement(Ns + "Task", new XAttribute("version", "1.2"),
            new XElement(Ns + "RegistrationInfo",
                new XElement(Ns + "Author", "OpenSense"),
                new XElement(Ns + "Description", Description)),
            new XElement(Ns + "Triggers",
                new XElement(Ns + "LogonTrigger",
                    new XElement(Ns + "UserId", userSid))),
            new XElement(Ns + "Principals",
                new XElement(Ns + "Principal", new XAttribute("id", "Author"),
                    new XElement(Ns + "UserId", userSid),
                    new XElement(Ns + "LogonType", "InteractiveToken"),
                    new XElement(Ns + "RunLevel", "HighestAvailable"))),
            // Task Scheduler's defaults suit a background job, not a laptop's tray app: they would not start it on battery,
            // stop it when the charger comes out, end it after 72 hours and run it at below-normal priority.
            new XElement(Ns + "Settings",
                new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(Ns + "DisallowStartIfOnBatteries", "false"),
                new XElement(Ns + "StopIfGoingOnBatteries", "false"),
                new XElement(Ns + "ExecutionTimeLimit", "PT0S"),
                new XElement(Ns + "Priority", 4)),
            new XElement(Ns + "Actions", new XAttribute("Context", "Author"),
                new XElement(Ns + "Exec",
                    new XElement(Ns + "Command", $"\"{exePath}\""),
                    new XElement(Ns + "Arguments", "--autostart"),
                    new XElement(Ns + "WorkingDirectory", Path.GetDirectoryName(exePath)))));

        using var writer = new StringWriter(); // UTF-16, which is what the Task Scheduler API takes
        new XDocument(new XDeclaration("1.0", "UTF-16", null), task).Save(writer);
        return writer.ToString();
    }

    /// <summary>
    /// The program an enabled task starts. Null when the task is disabled (the user switched it off in Task Scheduler:
    /// that counts as no task) or its XML has no program.
    /// </summary>
    public static string? ReadTarget(string xml)
    {
        try
        {
            var task = XDocument.Parse(xml).Root;
            if (string.Equals((string?)task?.Element(Ns + "Settings")?.Element(Ns + "Enabled"), "false", StringComparison.OrdinalIgnoreCase))
                return null;
            var command = ((string?)task?.Element(Ns + "Actions")?.Element(Ns + "Exec")?.Element(Ns + "Command"))?.Trim().Trim('"');
            return string.IsNullOrEmpty(command) ? null : Environment.ExpandEnvironmentVariables(command);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a portable copy should write the task for itself when it starts. A task whose program is gone means the copy
    /// was moved (or replaced by one in another folder), so it follows the copy. A task that points to another copy that
    /// still exists stays with that copy. A Run entry for this copy, from versions that had no task, moves into the task.
    /// </summary>
    /// <param name="taskTarget">What the task starts now; null when there is no task.</param>
    /// <param name="runEntryIsThisCopy">The per-user Run entry starts this copy.</param>
    public static bool NeedsWrite(string? taskTarget, bool runEntryIsThisCopy, string thisExe, Func<string, bool> exists)
    {
        var taskIsThisCopy = taskTarget is not null && SamePath(taskTarget, thisExe);
        if (taskTarget is not null && !taskIsThisCopy && !exists(taskTarget))
            return true;
        return runEntryIsThisCopy && (taskTarget is null || taskIsThisCopy);
    }
}
