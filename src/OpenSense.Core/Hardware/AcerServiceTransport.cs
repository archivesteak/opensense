using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace OpenSense.Core.Hardware;

/// <summary>
/// Talks to AcerService's local JSON API (127.0.0.1:46933): the same channel PredatorSense's own UI uses to reach
/// AcerLightingService. On 2024+ Predators the keyboard's per-zone RGB never reaches the ACPI-WMI class the rest of
/// OpenSense drives (<c>GetGamingKBBacklight</c> errors, and the actual write happens on the embedded controller's own
/// transport, not the USB bus), so this is the only way found so far to set it without reimplementing that transport.
/// Reached only as a fallback when the keyboard doesn't answer the legacy calls; needs AcerLightingService running.
/// </summary>
public static class AcerServiceTransport
{
    private const int Port = 46933;
    private const uint InitializationPacket = 0;
    private const uint GetUpdatedDataPacket = 20;
    private const uint SetDeviceDataPacket = 100;
    private static readonly byte[] Magic = "ACER"u8.ToArray();

    /// <summary>A short connect attempt; true only when something is actually listening on the port.</summary>
    public static bool IsAvailable(int timeoutMs = 200)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect("127.0.0.1", Port, null, null);
            var connected = result.AsyncWaitHandle.WaitOne(timeoutMs) && client.Connected;
            if (connected)
                client.EndConnect(result);
            return connected;
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>GET_UPDATED_DATA for <paramref name="function"/> (e.g. <c>"LIGHTING"</c>); null when it failed.</summary>
    public static JsonNode? Query(string function) => Send(GetUpdatedDataPacket, function, null)?["data"];

    /// <summary>SET_DEVICE_DATA for <paramref name="function"/>; false when it failed or was refused.</summary>
    public static bool SetDeviceData(string function, JsonObject parameters) =>
        Send(SetDeviceDataPacket, function, parameters)?["result"]?.ToString() == "0";

    private static JsonNode? Send(uint packetId, string function, JsonObject? parameters)
    {
        var request = new JsonObject { ["Function"] = function };
        if (parameters is not null)
            request["Parameter"] = parameters;

        try
        {
            using var client = new TcpClient();
            if (!client.ConnectAsync("127.0.0.1", Port).Wait(500))
                return null;
            client.SendTimeout = 2000;
            client.ReceiveTimeout = 2000;
            var stream = client.GetStream();

            var key = ReadAesKey();
            var body = key is null ? Encoding.UTF8.GetBytes(request.ToJsonString()) : Encrypt(key, request.ToJsonString());
            var packet = new byte[8 + body.Length];
            Magic.CopyTo(packet, 0);
            BitConverter.GetBytes(packetId).CopyTo(packet, 4);
            body.CopyTo(packet, 8);
            stream.Write(packet, 0, packet.Length);

            var buffer = new byte[16384];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return null;
            var start = read >= 8 && buffer[0] == 'A' && buffer[1] == 'C' && buffer[2] == 'E' && buffer[3] == 'R' ? 8 : 0;
            var payload = buffer.AsSpan(start, read - start);
            var json = key is null ? Encoding.UTF8.GetString(payload) : Decrypt(key, payload.ToArray());
            return JsonNode.Parse(json);
        }
        catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or AggregateException)
        {
            return null;
        }
    }

    /// <summary>Some installs key the channel with an AES-256-ECB key XSense's installer left in the registry.</summary>
    private static byte[]? ReadAesKey()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Acer\XSense");
        return key?.GetValue("AESkey") as string is { Length: 32 } k ? Encoding.ASCII.GetBytes(k) : null;
    }

    private static byte[] Encrypt(byte[] key, string text)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(text);
        return encryptor.TransformFinalBlock(plain, 0, plain.Length);
    }

    private static string Decrypt(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(data, 0, data.Length);
        return Encoding.UTF8.GetString(plain);
    }
}
