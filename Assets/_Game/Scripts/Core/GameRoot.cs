using System;
using Unity.Netcode;
using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// Bootstraps the game scene: builds the map, vision mask and UI, wires networking, and switches between menu and
    /// HUD. The scene only needs this, a camera with CameraFollow, and the NetworkManager.
    ///
    /// Command line (for testing several copies on one PC; see CLAUDE.md):
    ///   -lo-host            host immediately
    ///   -lo-join &lt;ip&gt;      join that address immediately
    ///   -lo-autojoin        join the first game found on the network
    ///   -lo-name &lt;name&gt;    player name
    ///   -lo-bot             wander randomly
    ///   -lo-quit &lt;seconds&gt; quit after that long, logging a summary
    ///   -lo-shot &lt;seconds&gt; &lt;file.png&gt; save a screenshot after that long
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        const string NameKey = "playerName";

        static string _localName;

        /// <summary>This device's player name (saved between sessions).</summary>
        public static string LocalPlayerName
        {
            get => _localName ??= PlayerPrefs.GetString(NameKey, FunnyNames.Random());
            set
            {
                _localName = value;
                PlayerPrefs.SetString(NameKey, value);
            }
        }

        NetworkSession _session;
        LanDiscovery _discovery;
        MenuScreen _menu;
        HudScreen _hud;
        bool _autoJoin;
        float _quitAt = -1f;
        float _shotAt = -1f;
        string _shotPath;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics2D.IgnoreLayerCollision(GameMap.PlayerLayer, GameMap.PlayerLayer, true);

            GameMap.Build(new GameObject("Map").transform);

            var vision = new GameObject("Vision Mask", typeof(MeshFilter), typeof(MeshRenderer));
            vision.AddComponent<VisionMask>();

            _discovery = gameObject.AddComponent<LanDiscovery>();
            _session = gameObject.AddComponent<NetworkSession>();
            _session.StateChanged += OnStateChanged;
            _session.ClientConnectionChanged += OnClientConnectionChanged;
            PlayerAvatar.RosterChanged += OnRosterChanged;

            UIKit.EnsureEventSystem();
            var canvas = UIKit.CreateCanvas("UI", 0);
            _hud = HudScreen.Create(canvas.transform, _session);
            _menu = MenuScreen.Create(canvas.transform, _session, _discovery);
            ShowMenu(true);
        }

        void Start()
        {
            // In Start, not Awake: NetworkManager.Singleton is only set once its own Awake/OnEnable has run.
            _session.Init(_discovery);
            ParseCommandLine();
        }

        void OnDestroy()
        {
            PlayerAvatar.RosterChanged -= OnRosterChanged;
        }

        void ParseCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-lo-name" when next != null: LocalPlayerName = next; break;
                    case "-lo-bot": GameInput.BotMode = true; break;
                    case "-lo-shot" when i + 2 < args.Length && float.TryParse(next, out var t):
                        _shotAt = Time.unscaledTime + t;
                        _shotPath = args[i + 2];
                        break;
                    case "-lo-quit" when next != null && float.TryParse(next, out var s): _quitAt = Time.unscaledTime + s; break;
                }
            }
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-lo-host": _session.Host(); break;
                    case "-lo-join" when next != null: _session.Join(next); break;
                    case "-lo-autojoin": _autoJoin = true; break;
                }
            }
        }

        void Update()
        {
            if (_autoJoin && _session.CanStart)
            {
                foreach (var host in _discovery.Hosts.Values)
                {
                    Debug.Log("[LightsOut] Auto-joining " + host.Address);
                    _autoJoin = false;
                    _session.Join(host.Address, host.GamePort);
                    break;
                }
            }

            if (_shotAt > 0f && Time.unscaledTime >= _shotAt)
            {
                _shotAt = -1f;
                ScreenCapture.CaptureScreenshot(_shotPath);
                Debug.Log("[LightsOut] Screenshot: " + _shotPath);
            }

            if (_quitAt > 0f && Time.unscaledTime >= _quitAt)
            {
                _quitAt = -1f;
                LogSummary();
                Application.Quit();
            }
        }

        void LogSummary()
        {
            var nm = NetworkManager.Singleton;
            string role = nm == null ? "none" : nm.IsHost ? "host" : nm.IsClient ? "client" : "offline";
            Debug.Log($"[LightsOut] SUMMARY role={role} state={_session.Current} players={PlayerAvatar.All.Count}");
            foreach (var p in PlayerAvatar.All)
                Debug.Log($"[LightsOut]   player client={p.OwnerClientId} name='{p.DisplayName}' colour={Palette.PlayerColorName(p.ColorIndex.Value)} " +
                          $"pos={(Vector2)p.transform.position} local={p.IsOwner} visible={p.VisibleAlpha:0.00}");
        }

        void OnStateChanged(NetworkSession.State state, string message)
        {
            ShowMenu(state != NetworkSession.State.InGame);
            if (state == NetworkSession.State.InGame)
            {
                if (!string.IsNullOrEmpty(message)) _hud.Toast(message, 4f);
            }
            else
            {
                _menu.SetStatus(message);
            }
        }

        void OnClientConnectionChanged(ulong clientId, bool joined)
        {
            if (!joined) _hud.Toast("Someone left the party");
        }

        int _lastCount;

        void OnRosterChanged()
        {
            // Announce new arrivals on every device, once their name is known.
            int count = PlayerAvatar.All.Count;
            if (count > _lastCount && _session.Current == NetworkSession.State.InGame && _lastCount > 0)
                _hud.Toast("A new player stumbled in");
            _lastCount = count;
        }

        void ShowMenu(bool menu)
        {
            _menu.gameObject.SetActive(menu);
            _hud.gameObject.SetActive(!menu);
        }
    }
}
