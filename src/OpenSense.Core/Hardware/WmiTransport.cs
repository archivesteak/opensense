using System.Globalization;
using System.Management;

namespace OpenSense.Core.Hardware;

/// <summary>Result of a WMI call whose parameters include byte arrays.</summary>
/// <param name="Status">The scalar output parameter (status/return code), 0 if none.</param>
/// <param name="Data">The byte-array output parameter, if the method has one.</param>
public readonly record struct WmiArrayResult(ulong Status, byte[]? Data);

/// <summary>Invokes methods on the Acer ACPI-WMI classes.</summary>
public interface IWmiTransport : IDisposable
{
    bool IsClassAvailable(string className);

    /// <summary>Calls a method with one integer input and returns its integer output.</summary>
    /// <exception cref="AcerWmiException">The class is missing, access was denied, or the call failed.</exception>
    ulong Invoke(string className, string method, ulong input);

    /// <summary>Calls a method whose input is a byte array (or that has no input) and returns its outputs.</summary>
    /// <exception cref="AcerWmiException">The class is missing, access was denied, or the call failed.</exception>
    WmiArrayResult InvokeArray(string className, string method, byte[]? input);

    /// <summary>Whether the method's input parameter is declared as an array (firmware-version dependent for some methods).</summary>
    bool HasArrayInput(string className, string method);
}

public class AcerWmiException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class AcerWmiAccessDeniedException(Exception? inner = null)
    : AcerWmiException("Administrator rights are required to access the Acer firmware interface.", inner);

/// <summary>Real transport over System.Management. Calls are serialised; the firmware is not re-entrant.</summary>
public sealed class WmiTransport : IWmiTransport
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ManagementObject?> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ManagementScope _scope = new(@"\\.\root\WMI");

    public bool IsClassAvailable(string className)
    {
        lock (_gate)
            return GetInstance(className) is not null;
    }

    public ulong Invoke(string className, string method, ulong input)
    {
        lock (_gate)
        {
            return Call(className, method, inParams =>
            {
                var inProp = inParams!.Properties.Cast<PropertyData>().Single();
                inParams[inProp.Name] = inProp.Type switch
                {
                    CimType.UInt8 => (byte)input,
                    CimType.UInt16 => (ushort)input,
                    CimType.UInt32 => (uint)input,
                    _ => (object)input,
                };
            }, outParams => Scalar(outParams) ?? throw new AcerWmiException($"{className}.{method} returned no output value."));
        }
    }

    public WmiArrayResult InvokeArray(string className, string method, byte[]? input)
    {
        lock (_gate)
        {
            return Call(className, method, inParams =>
            {
                if (input is null || inParams is null)
                    return;
                var inProp = inParams.Properties.Cast<PropertyData>().Single();
                inParams[inProp.Name] = input;
            }, outParams => new WmiArrayResult(
                Scalar(outParams) ?? 0,
                outParams.Properties.Cast<PropertyData>().Select(p => p.Value).OfType<byte[]>().FirstOrDefault()));
        }
    }

    public bool HasArrayInput(string className, string method)
    {
        lock (_gate)
        {
            var instance = GetInstance(className);
            using var inParams = instance?.GetMethodParameters(method);
            return inParams?.Properties.Cast<PropertyData>().Any(p => p.IsArray) ?? false;
        }
    }

    private T Call<T>(string className, string method, Action<ManagementBaseObject?> fillInput, Func<ManagementBaseObject, T> readOutput)
    {
        var instance = GetInstance(className)
            ?? throw new AcerWmiException($"WMI class {className} is not present on this machine.");
        try
        {
            using var inParams = instance.GetMethodParameters(method);
            fillInput(inParams);
            using var outParams = instance.InvokeMethod(method, inParams, null);
            return readOutput(outParams);
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            throw new AcerWmiAccessDeniedException(ex);
        }
        catch (ManagementException ex)
        {
            throw new AcerWmiException($"{className}.{method} failed: {ex.ErrorCode}", ex);
        }
    }

    private static ulong? Scalar(ManagementBaseObject outParams)
    {
        foreach (var p in outParams.Properties)
        {
            if (p.Name != "ReturnValue" && !p.IsArray && p.Value is not null)
                return Convert.ToUInt64(p.Value, CultureInfo.InvariantCulture);
        }
        return null;
    }

    private ManagementObject? GetInstance(string className)
    {
        if (_instances.TryGetValue(className, out var cached))
            return cached;

        ManagementObject? found = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery($"SELECT * FROM {className}"));
            found = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            throw new AcerWmiAccessDeniedException(ex);
        }
        catch (ManagementException ex) when (ex.ErrorCode is ManagementStatus.InvalidClass or ManagementStatus.NotFound)
        {
            found = null;
        }

        _instances[className] = found;
        return found;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var obj in _instances.Values)
                obj?.Dispose();
            _instances.Clear();
        }
    }
}
