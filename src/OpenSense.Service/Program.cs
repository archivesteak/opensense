using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using OpenSense.Core.Engine;
using OpenSense.Core.Ipc;
using OpenSense.Core.Settings;
using OpenSense.Service;
using Serilog;

// The OpenSense service: owns the laptop's firmware (as LocalSystem) and serves the app over a named
// pipe, so the app needs no administrator rights. Registered by the installer.

SettingsPaths.SecureMachineDirectory();

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    DisableDefaults = true,
});
builder.Services.AddWindowsService(options => options.ServiceName = OpenSensePipe.ServiceName);
if (WindowsServiceHelpers.IsWindowsService())
    builder.Services.AddSingleton<IHostLifetime, PowerAwareServiceLifetime>(); // also forwards sleep/resume and AC events

builder.Services.AddSerilog(log => log
    .MinimumLevel.Warning()
    .WriteTo.File(Path.Combine(SettingsPaths.MachineDirectory, "logs", "service-.log"), formatProvider: CultureInfo.InvariantCulture,
        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7));

builder.Services.AddSingleton(services => new OpenSenseEngine(
    new WindowsMachine(), SettingsPaths.Machine, services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenSenseEngine>>()));
builder.Services.AddSingleton(services => new PipeServer(
    services.GetRequiredService<OpenSenseEngine>(),
    services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PipeServer>>()));
builder.Services.AddHostedService<EngineHost>();

try
{
    await builder.Build().RunAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}
