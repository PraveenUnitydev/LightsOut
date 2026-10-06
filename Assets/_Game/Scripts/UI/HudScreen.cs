using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LightsOut
{
    /// <summary>In-game overlay: who you are, player count, host address, leave button, joystick, toasts, name tags.</summary>
    public class HudScreen : MonoBehaviour
    {
        NetworkSession _session;
        Text _info, _toast;
        float _toastUntil;
        RectTransform _tagRoot;
        readonly List<Text> _tags = new();

        public static HudScreen Create(Transform canvas, NetworkSession session)
        {
            var root = UIKit.Rect("Hud", canvas).Stretch();
            var hud = root.gameObject.AddComponent<HudScreen>();
            hud._session = session;
            hud.Build(root);
            return hud;
        }

        void Build(RectTransform root)
        {
            // Name tags sit below the rest of the HUD.
            _tagRoot = UIKit.Rect("NameTags", root).Stretch();

            VirtualJoystick.Create(root);

            _info = UIKit.Label("Info", root, "", 30, Color.white, TextAnchor.UpperLeft);
            _info.rectTransform.Place(new Vector2(0, 1), new Vector2(30, -24), new Vector2(900, 200));
            _info.supportRichText = true;

            UIKit.Button("Leave", root, "LEAVE", new Color(0.3f, 0.3f, 0.4f, 0.85f), 30, () => _session.Leave())
                .GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(-24, -24), new Vector2(180, 80));

            _toast = UIKit.Label("Toast", root, "", 40, Palette.UiAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _toast.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1400, 60));
        }

        public void Toast(string message, float seconds = 2.5f)
        {
            _toast.text = message;
            _toastUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            var me = PlayerAvatar.Local;
            string who = me != null
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(me.Color)}>●</color> {me.DisplayName}  <size=24>({Palette.PlayerColorName(me.ColorIndex.Value)})</size>"
                : "Connecting...";
            string lines = $"{who}\n<size=26>Players: {PlayerAvatar.All.Count}/{NetworkSession.MaxPlayers}</size>";
            if (_session.IsHost) lines += $"\n<size=24><color=#ffffff88>Hosting at {NetUtil.GetDisplayAddressCached()}</color></size>";
            _info.text = lines;

            _toast.enabled = Time.unscaledTime < _toastUntil;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            var players = PlayerAvatar.All;
            while (_tags.Count < players.Count)
            {
                var t = UIKit.Label("Tag", _tagRoot, "", 26, Color.white, TextAnchor.LowerCenter, FontStyle.Bold);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
                t.rectTransform.pivot = new Vector2(0.5f, 0f);
                t.rectTransform.sizeDelta = new Vector2(400, 40);
                t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
                _tags.Add(t);
            }

            float scale = _tagRoot.lossyScale.x;
            for (int i = 0; i < _tags.Count; i++)
            {
                var tag = _tags[i];
                if (i >= players.Count || cam == null || players[i].VisibleAlpha <= 0.01f)
                {
                    tag.enabled = false;
                    continue;
                }
                var p = players[i];
                Vector3 screen = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 0.55f);
                tag.enabled = true;
                tag.text = p.DisplayName;
                var c = p.Color;
                c.a = p.VisibleAlpha;
                tag.color = c;
                tag.rectTransform.anchoredPosition = new Vector2(screen.x, screen.y) / Mathf.Max(scale, 0.0001f);
            }
        }
    }
}
