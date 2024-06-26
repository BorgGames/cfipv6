namespace cfipv6;

using System.Net;
using System.Net.NetworkInformation;

public static class IP {
    public static UnicastIPAddressInformation? GetPublicStableIPv6() {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            if (adapter.OperationalStatus != OperationalStatus.Up)
                continue;

            foreach (var address in adapter.GetIPProperties().UnicastAddresses) {
                if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                    if (address.Address.IsIPv6LinkLocal
                        || address.Address.IsIPv6SiteLocal
                        || address.Address.IsIPv6UniqueLocal
                        || IPAddress.IsLoopback(address.Address))
                        continue;

#warning TODO: filter out temporary addresses
                    return address;
                }
            }
        }

        return null;
    }
}