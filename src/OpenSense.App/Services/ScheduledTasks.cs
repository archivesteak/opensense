using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.TaskScheduler;
using TaskSchedulerClass = Windows.Win32.System.TaskScheduler.TaskScheduler;

namespace OpenSense.App.Services;

/// <summary>The Task Scheduler calls OpenSense needs: tasks in the root folder, described by their XML.</summary>
internal static class ScheduledTasks
{
    /// <summary>The task's XML, or null when there is no such task.</summary>
    public static string? ReadXml(string name)
    {
        using var root = new Root();
        if (root.FindTask(name) is not { } task)
            return null;
        try
        {
            var xml = task.Xml;
            try
            {
                return xml.ToString();
            }
            finally
            {
                PInvoke.SysFreeString(xml);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(task);
        }
    }

    /// <summary>Creates the task, or replaces the one of that name. A task that runs with the highest rights needs an elevated caller.</summary>
    public static void Register(string name, string xml)
    {
        using var root = new Root();
        using var path = new Bstr(name);
        using var text = new Bstr(xml);
        root.Folder.RegisterTask(path, text, (int)TASK_CREATION.TASK_CREATE_OR_UPDATE, null, null,
            TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN, null, out var task);
        Marshal.FinalReleaseComObject(task);
    }

    /// <summary>Removes the task; nothing happens when there is none.</summary>
    public static void Delete(string name)
    {
        using var root = new Root();
        using var path = new Bstr(name);
        try
        {
            root.Folder.DeleteTask(path, 0);
        }
        catch (FileNotFoundException)
        {
        }
    }

    /// <summary>A connection to the Task Scheduler service and its root folder.</summary>
    private sealed class Root : IDisposable
    {
        private readonly ITaskService _service = (ITaskService)new TaskSchedulerClass();

        public Root()
        {
            _service.Connect(null, null, null, null);
            using var path = new Bstr(@"\");
            _service.GetFolder(path, out var folder);
            Folder = folder;
        }

        public ITaskFolder Folder { get; }

        /// <summary>The task, or null when there is none of that name (Task Scheduler answers "file not found").</summary>
        public IRegisteredTask? FindTask(string name)
        {
            using var path = new Bstr(name);
            try
            {
                Folder.GetTask(path, out var task);
                return task;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            Marshal.FinalReleaseComObject(Folder);
            Marshal.FinalReleaseComObject(_service);
        }
    }

    /// <summary>A BSTR for the length of a call.</summary>
    private readonly struct Bstr : IDisposable
    {
        private readonly nint _value;

        public Bstr(string text) => _value = Marshal.StringToBSTR(text);

        public static implicit operator BSTR(Bstr bstr) => new(bstr._value);

        public void Dispose() => Marshal.FreeBSTR(_value);
    }
}
