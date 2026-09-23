using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenSense.Core.Engine;
using OpenSense.Core.Ipc;

namespace OpenSense.Service;

/// <summary>Starts the engine, serves it on the pipe, and hands the fans back when the service stops.</summary>
internal sealed partial class EngineHost(OpenSenseEngine engine, PipeServer pipe, ILogger<EngineHost> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Serve right away: clients connecting at sign-in wait for the snapshot while detection runs. Keep
        // serving when the laptop is unsupported, so the app can say so.
        var serving = pipe.RunAsync(stoppingToken);

        // Detection talks to WMI and takes a moment; run it off the service's start thread.
        await Task.Run(engine.Start, stoppingToken).ConfigureAwait(false);
        LogStarted(engine.State);

        try
        {
            await serving.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogPipeUnavailable(ex);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        engine.Dispose(); // restores firmware fan control if configured, saves settings
        LogStopped();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine started: {State}")]
    private partial void LogStarted(Core.Ipc.EngineState state);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Cannot serve the OpenSense pipe; another process may own it")]
    private partial void LogPipeUnavailable(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service stopped")]
    private partial void LogStopped();
}
