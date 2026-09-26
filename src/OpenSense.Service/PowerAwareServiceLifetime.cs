using System.ServiceProcess;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSense.Core.Engine;

namespace OpenSense.Service;

/// <summary>
/// The standard Windows service lifetime, plus power events: services do not receive SystemEvents, so
/// suspend, resume and AC/battery changes arrive here and go to the engine.
/// </summary>
internal sealed class PowerAwareServiceLifetime : WindowsServiceLifetime
{
    private readonly OpenSenseEngine _engine;

    public PowerAwareServiceLifetime(
        OpenSenseEngine engine,
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        _engine = engine;
        CanHandlePowerEvent = true;
    }

    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
        switch (powerStatus)
        {
            case PowerBroadcastStatus.Suspend:
                _engine.NotifySuspend();
                break;
            // Sent on every resume (ResumeSuspend only follows when the user is present).
            case PowerBroadcastStatus.ResumeAutomatic:
                _engine.NotifyResume();
                break;
            case PowerBroadcastStatus.PowerStatusChange:
                _engine.NotifyPowerSourceChanged();
                break;
        }
        return base.OnPowerEvent(powerStatus);
    }
}
