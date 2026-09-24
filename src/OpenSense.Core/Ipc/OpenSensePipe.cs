using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using StreamJsonRpc;

namespace OpenSense.Core.Ipc;

/// <summary>The named pipe between the OpenSense service and its clients (JSON-RPC over StreamJsonRpc).</summary>
public static class OpenSensePipe
{
    public const string Name = "OpenSense";

    /// <summary>Windows service name, as registered by the installer.</summary>
    public const string ServiceName = "OpenSense";

    /// <summary>Raised whenever <see cref="IOpenSenseService"/> changes incompatibly; app and service must agree.</summary>
    public const int ProtocolVersion = 4;

    /// <summary>The OpenSense service is registered on this machine (an installed, not portable, copy).</summary>
    public static bool IsServiceInstalled
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
            return key is not null;
        }
    }

    /// <summary>A JSON-RPC connection over <paramref name="stream"/>; the caller adds targets or proxies and starts listening.</summary>
    public static JsonRpc CreateRpc(Stream stream)
    {
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        return new JsonRpc(new HeaderDelimitedMessageHandler(stream, formatter));
    }

    /// <summary>Connects to the running service. Null when nothing answers within <paramref name="timeout"/>.</summary>
    /// <exception cref="UnauthorizedAccessException">The pipe exists but was not created by OpenSense.</exception>
    public static async Task<OpenSenseConnection?> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        if (!IsTrustedServer(pipe))
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $@"\\.\pipe\{Name} was not created by the OpenSense service. Another program may be posing as it.");
        }

        var rpc = CreateRpc(pipe);
        var service = rpc.Attach<IOpenSenseService>();
        rpc.StartListening();
        return new OpenSenseConnection(rpc, service);
    }

    /// <summary>
    /// The service creates the pipe owned by LocalSystem, which no other program can do without being
    /// SYSTEM itself, so a program that created the pipe first cannot pose as OpenSense to the app.
    /// </summary>
    private static bool IsTrustedServer(NamedPipeClientStream pipe) =>
        pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner &&
        owner.IsWellKnown(WellKnownSidType.LocalSystemSid);
}

/// <summary>A client's open connection to the engine.</summary>
public sealed class OpenSenseConnection(JsonRpc rpc, IOpenSenseService service) : IDisposable
{
    public IOpenSenseService Service { get; } = service;

    /// <summary>Completes when the connection ends (the service stopped, or <see cref="Dispose"/>).</summary>
    public Task Completion => rpc.Completion;

    public void Dispose() => rpc.Dispose();
}
