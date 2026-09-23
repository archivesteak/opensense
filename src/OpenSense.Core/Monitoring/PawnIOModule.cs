using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace OpenSense.Core.Monitoring;

/// <summary>
/// A PawnIO executor with one official signed module loaded. PawnIO (https://pawnio.eu) is the signed kernel
/// driver LibreHardwareMonitor and FanControl use to read CPU registers; it is installed system-wide by its own
/// setup, and only elevated processes may use it. The modules are embedded from PawnIO.Modules (LGPL-2.1).
/// </summary>
internal sealed unsafe class PawnIOModule : IDisposable
{
    public const string NotInstalledMessage = "PawnIO is not installed.";

    private static readonly Lazy<Library?> Shared = new(Library.TryLoad);

    private readonly Library _library;
    private nint _handle;

    private PawnIOModule(Library library, nint handle)
    {
        _library = library;
        _handle = handle;
    }

    /// <summary>Opens an executor and loads the embedded module <paramref name="name"/> (e.g. "IntelMSR").</summary>
    public static PawnIOModule? TryOpen(string name, Action<string>? log)
    {
        if (Shared.Value is not { } library)
        {
            log?.Invoke(NotInstalledMessage);
            return null;
        }

        byte[] blob;
        using (var stream = typeof(PawnIOModule).Assembly.GetManifestResourceStream($"PawnIO.{name}.bin")
            ?? throw new InvalidOperationException($"PawnIO module {name} is not embedded."))
        {
            blob = new byte[stream.Length];
            stream.ReadExactly(blob);
        }

        nint handle;
        var hr = library.Open(&handle);
        if (hr < 0)
        {
            log?.Invoke($"PawnIO could not be opened (0x{hr:X8}).");
            return null;
        }
        fixed (byte* data = blob)
            hr = library.Load(handle, data, (nuint)blob.Length);
        if (hr < 0)
        {
            library.Close(handle);
            log?.Invoke($"PawnIO refused module {name} (0x{hr:X8}).");
            return null;
        }
        return new PawnIOModule(library, handle);
    }

    /// <summary>Runs <paramref name="function"/> with one argument and one result; null if the driver refused.</summary>
    public ulong? Call(string function, ulong argument)
    {
        ObjectDisposedException.ThrowIf(_handle == 0, this);

        Span<byte> name = stackalloc byte[function.Length + 1];
        Encoding.ASCII.GetBytes(function, name);
        name[^1] = 0;

        ulong output;
        nuint written;
        int hr;
        fixed (byte* functionName = name)
            hr = _library.Execute(_handle, functionName, &argument, 1, &output, 1, &written);
        return hr >= 0 && written == 1 ? output : null;
    }

    public void Dispose()
    {
        if (_handle == 0)
            return;
        _library.Close(_handle);
        _handle = 0;
    }

    /// <summary>PawnIOLib.dll from the PawnIO install (see PawnIOLib.h).</summary>
    private sealed class Library
    {
        public readonly delegate* unmanaged[Stdcall]<nint*, int> Open;
        public readonly delegate* unmanaged[Stdcall]<nint, byte*, nuint, int> Load;
        public readonly delegate* unmanaged[Stdcall]<nint, byte*, ulong*, nuint, ulong*, nuint, nuint*, int> Execute;
        public readonly delegate* unmanaged[Stdcall]<nint, int> Close;

        private Library(nint module)
        {
            Open = (delegate* unmanaged[Stdcall]<nint*, int>)NativeLibrary.GetExport(module, "pawnio_open");
            Load = (delegate* unmanaged[Stdcall]<nint, byte*, nuint, int>)NativeLibrary.GetExport(module, "pawnio_load");
            Execute = (delegate* unmanaged[Stdcall]<nint, byte*, ulong*, nuint, ulong*, nuint, nuint*, int>)NativeLibrary.GetExport(module, "pawnio_execute");
            Close = (delegate* unmanaged[Stdcall]<nint, int>)NativeLibrary.GetExport(module, "pawnio_close");
        }

        public static Library? TryLoad()
        {
            if (InstallDirectory() is not { } directory ||
                !NativeLibrary.TryLoad(Path.Combine(directory, "PawnIOLib.dll"), out var module))
                return null;
            try
            {
                return new Library(module);
            }
            catch (EntryPointNotFoundException)
            {
                NativeLibrary.Free(module);
                return null;
            }
        }

        private static string? InstallDirectory()
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            return key?.GetValue("InstallLocation") is string location && File.Exists(Path.Combine(location, "PawnIOLib.dll"))
                ? location
                : null;
        }
    }
}
