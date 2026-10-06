using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace LightsOut
{
    /// <summary>Local network helpers: this device's IPv4 addresses and subnet masks.</summary>
    public static class NetUtil
    {
        public readonly struct LocalAddress
        {
            public readonly IPAddress Address;
            public readonly IPAddress Mask;
            public LocalAddress(IPAddress address, IPAddress mask) { Address = address; Mask = mask; }
        }

        /// <summary>
        /// IPv4 addresses of active, non-loopback interfaces, private LAN ranges first.
        /// Falls back to the "UDP connect" trick (no packets are sent) when interface enumeration fails, which it can on
        /// some Android versions.
        /// </summary>
        public static List<LocalAddress> GetLocalAddresses()
        {
            var result = new List<LocalAddress>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(ua.Address)) continue;
                        IPAddress mask;
                        try { mask = ua.IPv4Mask; }
                        catch { mask = null; }
                        if (mask == null || mask.Equals(IPAddress.Any)) mask = IPAddress.Parse("255.255.255.0");
                        AddUnique(result, new LocalAddress(ua.Address, mask));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LightsOut] Interface enumeration failed: " + e.Message);
            }

            if (result.Count == 0)
            {
                try
                {
                    using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    s.Connect("10.255.255.255", 9);
                    if (s.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address) && !ep.Address.Equals(IPAddress.Any))
                        AddUnique(result, new LocalAddress(ep.Address, IPAddress.Parse("255.255.255.0")));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LightsOut] Fallback address lookup failed: " + e.Message);
                }
            }

            result.Sort((a, b) => Rank(a.Address).CompareTo(Rank(b.Address)));
            return result;
        }

        /// <summary>Best address to show to other players ("join me at ...").</summary>
        public static string GetDisplayAddress()
        {
            var list = GetLocalAddresses();
            return list.Count > 0 ? list[0].Address.ToString() : "unknown";
        }

        static string _cachedDisplay;
        static float _cachedAt = -100f;

        /// <summary>GetDisplayAddress, refreshed at most every few seconds (safe to call every frame).</summary>
        public static string GetDisplayAddressCached()
        {
            if (_cachedDisplay == null || Time.unscaledTime - _cachedAt > 5f)
            {
                _cachedDisplay = GetDisplayAddress();
                _cachedAt = Time.unscaledTime;
            }
            return _cachedDisplay;
        }

        static void AddUnique(List<LocalAddress> list, LocalAddress a)
        {
            foreach (var x in list)
                if (x.Address.Equals(a.Address)) return;
            list.Add(a);
        }

        static int Rank(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 192 && b[1] == 168) return 0;
            if (b[0] == 10) return 1;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return 2;
            if (b[0] == 169 && b[1] == 254) return 9; // link-local, rarely useful
            return 5;
        }

        /// <summary>
        /// All unicast addresses in the /24 (or smaller) subnet around <paramref name="local"/>, plus its broadcast.
        /// Larger subnets are clamped to the /24 containing the device, to keep the sweep small.
        /// </summary>
        public static IEnumerable<IPAddress> SubnetTargets(LocalAddress local)
        {
            byte[] ip = local.Address.GetAddressBytes();
            byte[] mask = local.Mask.GetAddressBytes();
            uint ipU = ToUInt(ip), maskU = ToUInt(mask);
            if (maskU < 0xFFFFFF00u) maskU = 0xFFFFFF00u;
            uint network = ipU & maskU;
            uint broadcast = network | ~maskU;
            for (uint a = network + 1; a < broadcast; a++)
                if (a != ipU) yield return FromUInt(a);
            yield return FromUInt(broadcast);
        }

        static uint ToUInt(byte[] b) => ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        static IPAddress FromUInt(uint v) => new(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
    }
}
