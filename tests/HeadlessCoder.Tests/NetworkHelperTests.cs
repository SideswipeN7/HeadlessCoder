using System.Net;
using System.Net.Sockets;
using HeadlessCoder.Networking;

namespace HeadlessCoder.Tests;

public class NetworkHelperTests
{
    [Fact]
    public void IsPortAvailable_ReturnsFalse_WhenPortIsTaken_ThenTrue_WhenReleased()
    {
        // Grab an ephemeral port, hold it, and confirm the helper reports it as taken.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            Assert.False(NetworkHelper.IsPortAvailable("127.0.0.1", port));
        }
        finally
        {
            listener.Stop();
        }

        Assert.True(NetworkHelper.IsPortAvailable("127.0.0.1", port));
    }

    [Fact]
    public void IsPortAvailable_FallsBackToAnyAddress_ForUnparseableBind()
    {
        int port = FreePort();
        // A bind string that isn't an IP falls back to IPAddress.Any and should still bind.
        Assert.True(NetworkHelper.IsPortAvailable("not-an-ip", port));
    }

    [Fact]
    public void GetLanIpv4_ReturnsNonEmptyString()
    {
        string ip = NetworkHelper.GetLanIpv4();
        Assert.False(string.IsNullOrWhiteSpace(ip));
    }

    [Fact]
    public void GetLanIpv4_ReturnsIpAddressOrLocalhost()
    {
        string ip = NetworkHelper.GetLanIpv4();
        Assert.True(ip == "localhost" || IPAddress.TryParse(ip, out _), $"unexpected value: {ip}");
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
