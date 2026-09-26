using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using OpenSense.Core.Ipc;

namespace OpenSense.Core.Tests;

/// <summary>The pipe between the service (LocalSystem) and the apps: who may use it, and how the app knows it is the service's.</summary>
public class PipeTests
{
    [Fact]
    public void Signed_in_users_may_talk_over_the_pipe_and_nobody_from_the_network()
    {
        var rules = PipeServer.Security().GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>().ToList();
        PipeAccessRule For(WellKnownSidType who) => Assert.Single(rules, r => ((SecurityIdentifier)r.IdentityReference).IsWellKnown(who));

        var network = For(WellKnownSidType.NetworkSid);
        Assert.Equal(AccessControlType.Deny, network.AccessControlType);
        Assert.Equal(PipeAccessRights.FullControl, network.PipeAccessRights);

        // Users talk to the service but can't add pipe instances of their own (which would carry the service's owner) or
        // change who may connect.
        var users = For(WellKnownSidType.AuthenticatedUserSid);
        Assert.Equal(AccessControlType.Allow, users.AccessControlType);
        Assert.Equal(PipeAccessRights.ReadWrite, users.PipeAccessRights & PipeAccessRights.ReadWrite);
        Assert.Equal((PipeAccessRights)0,
            users.PipeAccessRights & (PipeAccessRights.CreateNewInstance | PipeAccessRights.ChangePermissions | PipeAccessRights.TakeOwnership));
    }

    [Fact]
    public async Task The_app_refuses_a_pipe_another_program_created()
    {
        var ct = TestContext.Current.CancellationToken;
        var name = "OpenSense.Tests." + Guid.NewGuid().ToString("N");
        await using var impostor = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connected = impostor.WaitForConnectionAsync(ct);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => OpenSensePipe.ConnectAsync(name, TimeSpan.FromSeconds(10), ct));
        await connected;
    }
}
