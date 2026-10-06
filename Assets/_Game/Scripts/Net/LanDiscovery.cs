using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// Finds games on the same Wi-Fi without typing IPs. The host answers "who's there?" UDP packets on
    /// <see cref="Port"/>; searching phones send that question to the broadcast address and to every address in their
    /// subnet (many Android phones filter incoming broadcasts, but unicast always gets through).
    /// Sockets are non-blocking and polled from Update, so there are no threads.
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        public const int Port = 47777;
        const string Query = "LIGHTSOUT1?";
        const string Reply = "LIGHTSOUT1!";
        const float SweepInterval = 1.0f;
        const float HostTimeout = 3.5f;

        public struct FoundHost
        {
            public string Address;
            public ushort GamePort;
            public string Name;
            public int Players;
            public int MaxPlayers;
            public float LastSeen;
        }

        /// <summary>Hosts heard from recently, by IP.</summary>
        public readonly Dictionary<string, FoundHost> Hosts = new();

        /// <summary>Provides what the host advertises: (name, players, max players, game port).</summary>
        public Func<(string name, int players, int max, ushort port)> HostInfo;

        public bool IsHosting => _hostSocket != null;
        public bool IsSearching => _searchSocket != null;

        Socket _hostSocket, _searchSocket;
        readonly byte[] _buffer = new byte[512];
        readonly List<IPAddress> _targets = new();
        int _targetCursor;
        float _nextSweep, _nextTargetRefresh;

        public void StartHosting()
        {
            StopHosting();
            try
            {
                _hostSocket = NewSocket();
                _hostSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _hostSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                Debug.Log("[LightsOut] Discovery: answering on UDP " + Port);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LightsOut] Discovery: could not open host socket: " + e.Message);
                StopHosting();
            }
        }

        public void StopHosting()
        {
            _hostSocket?.Close();
            _hostSocket = null;
        }

        public void StartSearching()
        {
            StopSearching();
            Hosts.Clear();
            try
            {
                _searchSocket = NewSocket();
                _searchSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                _nextSweep = 0f;
                _nextTargetRefresh = 0f;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LightsOut] Discovery: could not open search socket: " + e.Message);
                StopSearching();
            }
        }

        public void StopSearching()
        {
            _searchSocket?.Close();
            _searchSocket = null;
            Hosts.Clear();
        }

        static Socket NewSocket()
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                EnableBroadcast = true,
                Blocking = false,
            };
            // On Windows an ICMP "port unreachable" makes the next ReceiveFrom throw; turn that off.
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                try
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    s.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
                }
                catch { /* not supported, ignore */ }
            }
            return s;
        }

        void Update()
        {
            if (_hostSocket != null) PollHost();
            if (_searchSocket != null) PollSearch();
        }

        void OnDestroy()
        {
            StopHosting();
            StopSearching();
        }

        void PollHost()
        {
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            for (int guard = 0; guard < 64; guard++)
            {
                int n;
                try
                {
                    if (_hostSocket.Available <= 0) return;
                    n = _hostSocket.ReceiveFrom(_buffer, ref from);
                }
                catch (SocketException) { return; }

                if (Encoding.UTF8.GetString(_buffer, 0, n) != Query || HostInfo == null) continue;
                var info = HostInfo();
                string msg = $"{Reply}|{info.port}|{info.players}|{info.max}|{info.name}";
                try { _hostSocket.SendTo(Encoding.UTF8.GetBytes(msg), from); }
                catch (SocketException) { }
            }
        }

        void PollSearch()
        {
            float now = Time.unscaledTime;

            if (now >= _nextTargetRefresh)
            {
                _nextTargetRefresh = now + 10f; // network can change (hotspot on/off)
                RefreshTargets();
            }

            // Spread the sweep over the interval rather than sending 250 packets in one frame.
            if (now >= _nextSweep && _targets.Count > 0)
            {
                int perFrame = Mathf.Max(8, Mathf.CeilToInt(_targets.Count * Time.unscaledDeltaTime / SweepInterval) + 1);
                byte[] q = Encoding.UTF8.GetBytes(Query);
                for (int i = 0; i < perFrame; i++)
                {
                    var target = new IPEndPoint(_targets[_targetCursor], Port);
                    try { _searchSocket.SendTo(q, target); }
                    catch (SocketException) { }
                    _targetCursor++;
                    if (_targetCursor >= _targets.Count)
                    {
                        _targetCursor = 0;
                        _nextSweep = now + SweepInterval;
                        break;
                    }
                }
            }

            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            for (int guard = 0; guard < 64; guard++)
            {
                int n;
                try
                {
                    if (_searchSocket.Available <= 0) break;
                    n = _searchSocket.ReceiveFrom(_buffer, ref from);
                }
                catch (SocketException) { break; }

                string msg = Encoding.UTF8.GetString(_buffer, 0, n);
                if (!msg.StartsWith(Reply + "|")) continue;
                var parts = msg.Split(new[] { '|' }, 5);
                if (parts.Length < 5) continue;
                string ip = ((IPEndPoint)from).Address.ToString();
                ushort.TryParse(parts[1], out var port);
                int.TryParse(parts[2], out var players);
                int.TryParse(parts[3], out var max);
                bool isNew = !Hosts.ContainsKey(ip);
                Hosts[ip] = new FoundHost
                {
                    Address = ip, GamePort = port, Players = players, MaxPlayers = max, Name = parts[4], LastSeen = now,
                };
                if (isNew) Debug.Log($"[LightsOut] Discovery: found '{parts[4]}' at {ip}:{port}");
            }

            // Forget hosts that stopped answering.
            List<string> stale = null;
            foreach (var kv in Hosts)
                if (now - kv.Value.LastSeen > HostTimeout) (stale ??= new List<string>()).Add(kv.Key);
            if (stale != null) foreach (var k in stale) Hosts.Remove(k);
        }

        void RefreshTargets()
        {
            _targets.Clear();
            _targetCursor = 0;
            _targets.Add(IPAddress.Broadcast);
            _targets.Add(IPAddress.Loopback); // same-device testing
            var seen = new HashSet<string>();
            foreach (var local in NetUtil.GetLocalAddresses())
                foreach (var a in NetUtil.SubnetTargets(local))
                    if (seen.Add(a.ToString())) _targets.Add(a);
        }
    }
}
