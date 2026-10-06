using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// Hosting, joining and leaving. One phone hosts (it is both server and a player); everyone else joins it over
    /// Wi-Fi by IP, found through <see cref="LanDiscovery"/>.
    /// </summary>
    public class NetworkSession : MonoBehaviour
    {
        public const ushort GamePort = 7777;
        public const int MaxPlayers = 10;

        public enum State { Offline, Connecting, InGame }

        public State Current { get; private set; } = State.Offline;
        public bool IsHost => _nm != null && _nm.IsHost;
        public int PlayerCount => _nm != null && _nm.IsServer ? _nm.ConnectedClientsIds.Count : PlayerAvatar.All.Count;

        /// <summary>Raised on state changes, with a message for the player (may be null).</summary>
        public event Action<State, string> StateChanged;

        /// <summary>Raised on the host when someone joins or leaves (client id).</summary>
        public event Action<ulong, bool> ClientConnectionChanged;

        LanDiscovery _discovery;
        NetworkManager _nm;

        public void Init(LanDiscovery discovery)
        {
            _discovery = discovery;
            _nm = NetworkManager.Singleton != null ? NetworkManager.Singleton : FindAnyObjectByType<NetworkManager>();
            if (_nm == null)
            {
                Debug.LogError("[LightsOut] No NetworkManager in the scene.");
                return;
            }

            _nm.NetworkConfig.ConnectionApproval = true;
            _nm.ConnectionApprovalCallback = Approve;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
            _nm.OnTransportFailure += OnTransportFailure;

            _discovery.HostInfo = () => (GameRoot.LocalPlayerName + "'s party", PlayerCount, MaxPlayers, GamePort);
        }

        void OnDestroy()
        {
            if (_nm == null) return;
            _nm.ConnectionApprovalCallback = null;
            _nm.OnClientConnectedCallback -= OnClientConnected;
            _nm.OnClientDisconnectCallback -= OnClientDisconnected;
            _nm.OnTransportFailure -= OnTransportFailure;
        }

        UnityTransport Transport => (UnityTransport)_nm.NetworkConfig.NetworkTransport;

        public bool CanStart => _nm != null && !_nm.IsListening && !_nm.ShutdownInProgress && Current == State.Offline;

        public void Host()
        {
            if (!CanStart) return;
            Transport.SetConnectionData("127.0.0.1", GamePort, "0.0.0.0");
            _discovery.StopSearching();
            if (_nm.StartHost())
            {
                _discovery.StartHosting();
                SetState(State.InGame, "Hosting at " + NetUtil.GetDisplayAddress());
            }
            else
            {
                SetState(State.Offline, "Could not start hosting. Is another game already running on this device?");
            }
        }

        public void Join(string address, ushort port = GamePort)
        {
            if (!CanStart) return;
            address = address?.Trim();
            if (string.IsNullOrEmpty(address)) return;
            var transport = Transport;
            transport.ConnectTimeoutMS = 1000;
            transport.MaxConnectAttempts = 8;
            transport.SetConnectionData(address, port);
            _discovery.StopSearching();
            if (_nm.StartClient()) SetState(State.Connecting, "Joining " + address + "...");
            else SetState(State.Offline, "Could not connect to " + address);
        }

        public void Leave(string reason = null)
        {
            _discovery.StopHosting();
            if (_nm != null && (_nm.IsListening || _nm.IsClient)) _nm.Shutdown();
            SetState(State.Offline, reason);
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // The host approves itself; everyone else only while there is room.
            bool full = request.ClientNetworkId != NetworkManager.ServerClientId && _nm.ConnectedClientsIds.Count >= MaxPlayers;
            response.Approved = !full;
            response.CreatePlayerObject = !full;
            response.Reason = full ? "The game is full (10 players)." : null;
        }

        void OnClientConnected(ulong clientId)
        {
            if (_nm.IsServer)
            {
                Debug.Log($"[LightsOut] Client {clientId} connected ({_nm.ConnectedClientsIds.Count} players)");
                ClientConnectionChanged?.Invoke(clientId, true);
            }
            if (!_nm.IsServer && clientId == _nm.LocalClientId) SetState(State.InGame, null);
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (_nm.IsServer)
            {
                if (clientId != NetworkManager.ServerClientId)
                {
                    Debug.Log($"[LightsOut] Client {clientId} left");
                    ClientConnectionChanged?.Invoke(clientId, false);
                }
                return;
            }

            // We are a client and got disconnected (host left, refused, or never reached).
            string reason = _nm.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
                reason = Current == State.Connecting ? "Couldn't reach that game. Same Wi-Fi?" : "The host left the game.";
            Leave(reason);
        }

        void OnTransportFailure()
        {
            Leave("Network error. Check your Wi-Fi.");
        }

        void SetState(State state, string message)
        {
            Current = state;
            if (message != null) Debug.Log("[LightsOut] " + message);
            StateChanged?.Invoke(state, message);
        }
    }
}
