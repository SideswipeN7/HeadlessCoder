using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HeadlessCoder.Networking;

/// <summary>
/// Helpers for discovering the machine's LAN IPv4 address so the UI can be reached
/// from other devices on the same network.
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// Returns the best-guess LAN IPv4 address (e.g. 192.168.1.42), or "localhost"
    /// if none can be determined.
    /// </summary>
    public static string GetLanIpv4()
    {
        var candidates = new List<(IPAddress Address, int Score)>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            // De-prioritise virtual adapters (Hyper-V, VMware, WSL, Docker, etc.).
            string name = (ni.Name + " " + ni.Description).ToLowerInvariant();
            bool looksVirtual = name.Contains("virtual") || name.Contains("vmware") ||
                                name.Contains("hyper-v") || name.Contains("wsl") ||
                                name.Contains("docker") || name.Contains("loopback") ||
                                name.Contains("vethernet") || name.Contains("vpn") ||
                                name.Contains("tailscale") || name.Contains("zerotier");

            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(ua.Address))
                    continue;

                int score = 0;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211)
                    score += 30;
                else if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    score += 20;
                if (IsPrivate(ua.Address))
                    score += 40;
                if (looksVirtual)
                    score -= 50;

                candidates.Add((ua.Address, score));
            }
        }

        var best = candidates.OrderByDescending(c => c.Score).FirstOrDefault();
        if (best.Address is not null)
            return best.Address.ToString();

        // Fallback: ask the OS which local address would be used to reach the internet.
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address))
                return ep.Address.ToString();
        }
        catch
        {
            // ignored
        }

        return "localhost";
    }

    /// <summary>
    /// Returns true if the given port can be bound on the configured address
    /// (i.e. it's free). Used to fail fast with a friendly message.
    /// </summary>
    public static bool IsPortAvailable(string bindAddress, int port)
    {
        IPAddress addr = IPAddress.TryParse(bindAddress, out var a) ? a : IPAddress.Any;
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(addr, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }

    private static bool IsPrivate(IPAddress address)
    {
        byte[] b = address.GetAddressBytes();
        return b[0] switch
        {
            10 => true,                               // 10.0.0.0/8
            172 => b[1] >= 16 && b[1] <= 31,          // 172.16.0.0/12
            192 => b[1] == 168,                       // 192.168.0.0/16
            169 => b[1] != 254,                       // exclude link-local 169.254/16
            _ => false,
        };
    }
}
