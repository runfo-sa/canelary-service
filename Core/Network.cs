using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Core
{
    public static class Network
    {
        public static string GetIpAddress()
        {
            var gatewayAddr = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .SelectMany(ni => ni.GetIPProperties().GatewayAddresses)
                .Select(g => g?.Address)
                .FirstOrDefault(a => a is { AddressFamily: AddressFamily.InterNetwork });

            if (gatewayAddr is null)
            {
                // Sin gateway IPv4: devolver la primera IPv4 no-loopback como mejor aproximacion.
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Select(u => u.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    ?.ToString() ?? "127.0.0.1";
            }

            string gwStr = gatewayAddr.ToString();
            int lastDot = gwStr.LastIndexOf('.');
            string prefix = lastDot > 0 ? gwStr[..lastDot] : gwStr;

            return NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address.ToString())
                .FirstOrDefault(ip => ip.StartsWith(prefix, StringComparison.Ordinal))
                ?? gatewayAddr.ToString();
        }
    }
}
