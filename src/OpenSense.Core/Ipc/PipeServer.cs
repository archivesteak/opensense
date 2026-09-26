using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace OpenSense.Core.Ipc;

/// <summary>
/// Serves <see cref="IOpenSenseService"/> on the OpenSense pipe, from the service (LocalSystem), to any number
/// of clients. Signed-in users may connect; network access is denied.
/// </summary>
public sealed partial class PipeServer(IOpenSenseService engine, ILogger<PipeServer> log)
{
    /// <summary>Accepts clients until <paramref name="cancellationToken"/> is cancelled, then disconnects them.</summary>
    /// <exception cref="UnauthorizedAccessException">Another process already owns the pipe name.</exception>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var clients = new List<Task>();
        var first = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            // FirstPipeInstance: fail rather than share the name with a pipe someone else created first.
            var pipe = NamedPipeServerStreamAcl.Create(
                OpenSensePipe.Name,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None),
                inBufferSize: 0,
                outBufferSize: 0,
                Security());
            first = false;

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                break;
            }

            clients.RemoveAll(c => c.IsCompleted);
            clients.Add(ServeAsync(pipe, cancellationToken));
        }
        await Task.WhenAll(clients).ConfigureAwait(false);
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        await using (pipe.ConfigureAwait(false))
        {
            using var rpc = OpenSensePipe.CreateRpc(pipe);
            // Only the interface is callable; the engine's other public members stay private to the host.
            rpc.AddLocalRpcTarget(engine, new JsonRpcTargetOptions { NotifyClientOfEvents = true, DisposeOnDisconnect = false });
            rpc.StartListening();
            LogClientConnected();
            using (cancellationToken.Register(rpc.Dispose))
            {
                try
                {
                    await rpc.Completion.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    LogClientFailed(ex);
                }
            }
            LogClientDisconnected();
        }
    }

    /// <summary>Owned by LocalSystem; signed-in users may talk over it (but not add instances or change it); no network access.</summary>
    internal static PipeSecurity Security()
    {
        var security = new PipeSecurity();
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)); // what clients check
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        return security;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Client connected")]
    private partial void LogClientConnected();

    [LoggerMessage(Level = LogLevel.Information, Message = "Client disconnected")]
    private partial void LogClientDisconnected();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Client connection failed")]
    private partial void LogClientFailed(Exception ex);
}
