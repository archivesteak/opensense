using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;

namespace OpenSense.Core.Tests;

public sealed class BiosSettingsTests
{
    [Fact]
    public void The_bios_list_and_its_answers_are_read_as_the_bios_writes_them()
    {
        // As the PHN16-73's SMM code builds them; a setting the laptop lacks is an empty string.
        var settings = BiosProtocol.ParseList(
        [
            "Intel VTX, Enabled[Disabled;Enabled;]",
            "Battery Threshold, 20%[10%;20%;30%;]",
            "Type C, Enabled[Disabled;Enabled;]",
            "Set Supervisor Password, Unsupported",
            "Camera, ",
            "",
            "Something New, A[A;B;]",
        ]);

        Assert.Equal(["Intel VTX", "Battery Threshold", "Type C", "Set Supervisor Password", "Something New"], settings.Select(s => s.Name));
        Assert.Equal("20%", settings[1].Current);
        Assert.Equal(["10%", "20%", "30%"], settings[1].Options);
        Assert.Equal(BiosSettingKind.Password, settings[3].Kind);
        Assert.Empty(settings[3].Options);
        // What can cut the laptop off from its ports, or that the BIOS added after this list was made, is not treated as safe.
        Assert.Equal([BiosRisk.Normal, BiosRisk.Normal, BiosRisk.Dangerous, BiosRisk.Normal, BiosRisk.Dangerous], settings.Select(s => s.Risk));

        Assert.Equal(BiosChangeResult.Done, BiosProtocol.Result("0x00 - Success and no error"));
        Assert.Equal(BiosChangeResult.RestartNeeded, BiosProtocol.Result("0x08 - Success and Reboot required to make function works"));
        Assert.Equal(BiosChangeResult.WrongPassword, BiosProtocol.Result("0x02 - Incorrect Password"));
        Assert.Equal(BiosChangeResult.TooManyAttempts, BiosProtocol.Result("0x07 - Password retry count exceeded"));
        Assert.Equal(BiosChangeResult.Failed, BiosProtocol.Result(""));
        Assert.Equal(BiosChangeResult.Failed, BiosProtocol.Result(null));
    }
}

/// <summary>The engine's BIOS settings behind the real JSON-RPC stack, on a laptop whose BIOS has the interface.</summary>
public sealed class BiosSettingsServiceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
    private readonly SimulatedMachine _machine = new(SimulatedModel.Predator2024);
    private OpenSenseEngine _engine = null!;
    private StreamJsonRpc.JsonRpc _server = null!;
    private StreamJsonRpc.JsonRpc _client = null!;
    private IOpenSenseService _service = null!;

    public ValueTask InitializeAsync()
    {
        _engine = new OpenSenseEngine(_machine, Path.Combine(_directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        _engine.Start();
        (_server, _client, _service) = IpcTests.Serve(_engine);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _server.Dispose();
        _engine.Dispose();
        return ValueTask.CompletedTask;
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Changes_only_go_through_what_the_user_allowed()
    {
        var snapshot = await _service.GetSnapshotAsync(Cancel);
        Assert.True(snapshot.Capabilities.BiosSettings);
        Assert.False(snapshot.Settings.Bios.Enabled);

        // Nothing goes to the BIOS while the switch is off, whoever asks.
        Assert.Equal(BiosChangeResult.NotAllowed, await _service.SetBiosSettingAsync("Wi-Fi", "Disabled", "", Cancel));
        Assert.Equal("Enabled", _machine.Laptop!.BiosValue("Wi-Fi"));

        await _service.SetBiosAccessAsync(new BiosAccess { Enabled = true }, Cancel);
        var listed = await _service.ReadBiosSettingsAsync(Cancel);
        Assert.Equal(["Wi-Fi", "Lid Open Resume", "Battery Threshold", "Type C", "Set Supervisor Password"], listed.Select(s => s.Name));
        Assert.Equal(["10%", "20%", "30%"], listed.Single(s => s.Name == "Battery Threshold").Options);

        Assert.Equal(BiosChangeResult.RestartNeeded, await _service.SetBiosSettingAsync("Wi-Fi", "Disabled", "", Cancel));
        Assert.Equal("Disabled", _machine.Laptop.BiosValue("Wi-Fi"));
        Assert.Equal("Disabled", (await _service.ReadBiosSettingsAsync(Cancel)).Single(s => s.Name == "Wi-Fi").Current);
        Assert.Equal(BiosChangeResult.Rejected, await _service.SetBiosSettingAsync("Wi-Fi", "Sometimes", "", Cancel));

        // A dangerous one, and loading defaults, wait for their own switch, which follows the first.
        Assert.Equal(BiosChangeResult.NotAllowed, await _service.SetBiosSettingAsync("Type C", "Disabled", "", Cancel));
        Assert.Equal(BiosChangeResult.NotAllowed, await _service.LoadBiosDefaultsAsync("", Cancel));
        await _service.SetBiosAccessAsync(new BiosAccess { Enabled = false, Dangerous = true }, Cancel);
        Assert.False((await _service.GetSnapshotAsync(Cancel)).Settings.Bios.Dangerous);

        await _service.SetBiosAccessAsync(new BiosAccess { Enabled = true, Dangerous = true }, Cancel);
        Assert.Equal(BiosChangeResult.RestartNeeded, await _service.SetBiosSettingAsync("Type C", "Disabled", "", Cancel));
        Assert.Equal("Disabled", _machine.Laptop.BiosValue("Type C"));
        Assert.Equal(BiosChangeResult.RestartNeeded, await _service.LoadBiosDefaultsAsync("", Cancel));
        Assert.Equal(("Enabled", "30%"), (_machine.Laptop.BiosValue("Wi-Fi"), _machine.Laptop.BiosValue("Battery Threshold")));
        Assert.Equal(BiosChangeResult.Unsupported, await _service.LoadBiosUserDefaultsAsync("", Cancel));
    }

    [Fact]
    public async Task The_supervisor_password_is_asked_for_and_text_the_bios_would_garble_is_turned_away()
    {
        await _service.SetBiosAccessAsync(new BiosAccess { Enabled = true }, Cancel);

        Assert.Equal(BiosChangeResult.RestartNeeded,
            await _service.SetBiosSettingAsync(BiosProtocol.SupervisorPassword, "Secret 42", "", Cancel));
        Assert.Equal("Secret 42", _machine.Laptop!.BiosPassword);

        Assert.Equal(BiosChangeResult.WrongPassword, await _service.SetBiosSettingAsync("Wi-Fi", "Disabled", "", Cancel));
        Assert.Equal(BiosChangeResult.WrongPassword, await _service.SetBiosSettingAsync("Wi-Fi", "Disabled", "wrong", Cancel));
        Assert.Equal("Enabled", _machine.Laptop.BiosValue("Wi-Fi"));
        Assert.Equal(BiosChangeResult.RestartNeeded, await _service.SetBiosSettingAsync("Wi-Fi", "Disabled", "Secret 42", Cancel));

        // The BIOS keeps one byte of each character, so anything else is turned away before it could arrive as another one.
        Assert.Equal(BiosChangeResult.Rejected, await _service.SetBiosSettingAsync("Wi-Fi", "Enabled", "Geheimnis ü", Cancel));
        Assert.Equal(BiosChangeResult.Rejected, await _service.SetBiosSettingAsync("Wi-Fi", "", "Secret 42", Cancel));

        // An empty new password takes the password away, but only for whoever knows the one in use.
        Assert.Equal(BiosChangeResult.WrongPassword, await _service.SetBiosSettingAsync(BiosProtocol.SupervisorPassword, "", "wrong", Cancel));
        Assert.Equal("Secret 42", _machine.Laptop.BiosPassword);
        Assert.Equal(BiosChangeResult.RestartNeeded, await _service.SetBiosSettingAsync(BiosProtocol.SupervisorPassword, "", "Secret 42", Cancel));
        Assert.Equal("", _machine.Laptop.BiosPassword);
    }
}
