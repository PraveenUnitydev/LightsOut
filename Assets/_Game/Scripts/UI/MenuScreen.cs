using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LightsOut
{
    /// <summary>Main menu: pick a name, host a game, or join one found on the Wi-Fi (or by IP).</summary>
    public class MenuScreen : MonoBehaviour
    {
        const int MaxListed = 5;

        NetworkSession _session;
        LanDiscovery _discovery;
        InputField _nameField, _ipField;
        Text _status, _searching;
        Button _hostButton, _joinIpButton;
        readonly List<Button> _gameButtons = new();
        readonly List<string> _gameAddresses = new();
        readonly List<ushort> _gamePorts = new();
        float _nextRefresh;

        public static MenuScreen Create(Transform canvas, NetworkSession session, LanDiscovery discovery)
        {
            var root = UIKit.Panel("Menu", canvas, new Color(0.03f, 0.03f, 0.05f, 0.82f), rounded: false);
            root.rectTransform.Stretch();
            var menu = root.gameObject.AddComponent<MenuScreen>();
            menu._session = session;
            menu._discovery = discovery;
            menu.Build(root.transform);
            return menu;
        }

        void Build(Transform root)
        {
            var title = UIKit.Label("Title", root, "LIGHTS OUT", 132, Palette.UiAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1400, 160));
            var sub = UIKit.Label("Subtitle", root, "Somebody cut the power at the office party. Bonk whoever you bump into.",
                34, new Color(1, 1, 1, 0.7f));
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -215), new Vector2(1600, 50));

            // Left column: name + host.
            var left = UIKit.Rect("Left", root).Place(new Vector2(0.5f, 0.5f), new Vector2(-430, -60), new Vector2(680, 560));
            left.pivot = new Vector2(0.5f, 0.5f);
            UIKit.Label("NameLabel", left, "YOUR NAME", 30, new Color(1, 1, 1, 0.6f), TextAnchor.LowerLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0), new Vector2(680, 50));
            _nameField = UIKit.InputField("Name", left, "Type a silly name", 44);
            _nameField.RT().Place(new Vector2(0, 1), new Vector2(0, -60), new Vector2(500, 110));
            _nameField.characterLimit = 16;
            _nameField.text = GameRoot.LocalPlayerName;
            _nameField.onEndEdit.AddListener(OnNameEdited);
            UIKit.Button("Random", left, "?!", Palette.UiButton, 52, () =>
            {
                _nameField.text = FunnyNames.Random();
                OnNameEdited(_nameField.text);
            }).GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(0, -60), new Vector2(160, 110));

            _hostButton = UIKit.Button("Host", left, "HOST A GAME", new Color(0.85f, 0.3f, 0.25f), 56, () =>
            {
                OnNameEdited(_nameField.text);
                _session.Host();
            });
            _hostButton.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(0, -230), new Vector2(680, 150));
            UIKit.Label("HostHint", left, "Host on one phone. Everyone else joins on the same Wi-Fi (or the host's hotspot).",
                    28, new Color(1, 1, 1, 0.5f), TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, -400), new Vector2(680, 100));

            // Right column: games nearby + join by IP.
            var right = UIKit.Rect("Right", root).Place(new Vector2(0.5f, 0.5f), new Vector2(430, -60), new Vector2(680, 560));
            right.pivot = new Vector2(0.5f, 0.5f);
            UIKit.Label("GamesLabel", right, "GAMES NEARBY", 30, new Color(1, 1, 1, 0.6f), TextAnchor.LowerLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0), new Vector2(680, 50));
            _searching = UIKit.Label("Searching", right, "Looking for games...", 32, new Color(1, 1, 1, 0.45f), TextAnchor.UpperLeft, FontStyle.Italic);
            _searching.rectTransform.Place(new Vector2(0, 1), new Vector2(0, -70), new Vector2(680, 60));
            for (int i = 0; i < MaxListed; i++)
            {
                int index = i;
                var b = UIKit.Button("Game" + i, right, "", new Color(0.2f, 0.45f, 0.3f), 34, () => JoinListed(index));
                b.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(0, -60 - i * 78), new Vector2(680, 68));
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
                b.gameObject.SetActive(false);
                _gameButtons.Add(b);
                _gameAddresses.Add(null);
                _gamePorts.Add(0);
            }

            UIKit.Label("IpLabel", right, "OR JOIN BY IP", 26, new Color(1, 1, 1, 0.45f), TextAnchor.LowerLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 0), new Vector2(0, 100), new Vector2(680, 40));
            _ipField = UIKit.InputField("Ip", right, "192.168.1.5", 38);
            _ipField.RT().Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(480, 90));
            _ipField.contentType = InputField.ContentType.Standard;
            _ipField.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            _ipField.text = PlayerPrefs.GetString("lastIp", "");
            _joinIpButton = UIKit.Button("JoinIp", right, "JOIN", new Color(0.25f, 0.4f, 0.75f), 40, () =>
            {
                OnNameEdited(_nameField.text);
                PlayerPrefs.SetString("lastIp", _ipField.text.Trim());
                _session.Join(_ipField.text);
            });
            _joinIpButton.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(0, 0), new Vector2(180, 90));

            _status = UIKit.Label("Status", root, "", 34, new Color(1f, 0.75f, 0.4f));
            _status.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1700, 60));

            var version = UIKit.Label("Version", root, "v" + Application.version, 22, new Color(1, 1, 1, 0.25f), TextAnchor.LowerRight);
            version.rectTransform.Place(new Vector2(1, 0), new Vector2(-20, 12), new Vector2(300, 30));
        }

        void OnNameEdited(string value)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value)) value = FunnyNames.Random();
            _nameField.text = value;
            GameRoot.LocalPlayerName = value;
        }

        public void SetStatus(string message)
        {
            if (_status != null) _status.text = message ?? "";
        }

        void JoinListed(int index)
        {
            if (_gameAddresses[index] == null) return;
            OnNameEdited(_nameField.text);
            _session.Join(_gameAddresses[index], _gamePorts[index]);
        }

        void OnEnable()
        {
            if (_discovery != null) _discovery.StartSearching();
        }

        void OnDisable()
        {
            if (_discovery != null) _discovery.StopSearching();
        }

        void Update()
        {
            bool canStart = _session.CanStart;
            _hostButton.interactable = canStart;
            _joinIpButton.interactable = canStart;

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            if (!_discovery.IsSearching && canStart) _discovery.StartSearching();

            int i = 0;
            foreach (var host in _discovery.Hosts.Values)
            {
                if (i >= MaxListed) break;
                var b = _gameButtons[i];
                b.gameObject.SetActive(true);
                bool full = host.Players >= host.MaxPlayers;
                b.GetComponentInChildren<Text>().text = $"{host.Name}   ({host.Players}/{host.MaxPlayers}){(full ? "  FULL" : "")}";
                b.interactable = canStart && !full;
                _gameAddresses[i] = host.Address;
                _gamePorts[i] = host.GamePort;
                i++;
            }
            for (; i < MaxListed; i++)
            {
                _gameButtons[i].gameObject.SetActive(false);
                _gameAddresses[i] = null;
            }
            _searching.gameObject.SetActive(_discovery.Hosts.Count == 0);
        }
    }

    static class UIExtensions
    {
        public static RectTransform RT(this Component c) => (RectTransform)c.transform;
    }
}
