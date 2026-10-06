using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LightsOut
{
    /// <summary>Small helpers for building uGUI from code (reference resolution 1920x1080, landscape).</summary>
    public static class UIKit
    {
        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // phones are wide; scale by height
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Place by anchor point and pixel offset (canvas units), with a size.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool rounded = true)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = GameAssets.Panel;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var t = Rect(name, parent).gameObject.AddComponent<Text>();
            t.font = GameAssets.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Color color, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(name, parent, color);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            var label = Label("Label", img.transform, text, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Stretch();
            return button;
        }

        public static InputField InputField(string name, Transform parent, string placeholder, int fontSize)
        {
            var img = Panel(name, parent, new Color(0.08f, 0.08f, 0.12f, 1f));
            var field = img.gameObject.AddComponent<InputField>();

            var text = Label("Text", img.transform, "", fontSize, Color.white, TextAnchor.MiddleLeft);
            text.rectTransform.Stretch();
            text.rectTransform.offsetMin = new Vector2(24, 0);
            text.rectTransform.offsetMax = new Vector2(-24, 0);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;

            var ph = Label("Placeholder", img.transform, placeholder, fontSize, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleLeft, FontStyle.Italic);
            ph.rectTransform.Stretch();
            ph.rectTransform.offsetMin = new Vector2(24, 0);
            ph.rectTransform.offsetMax = new Vector2(-24, 0);

            field.textComponent = text;
            field.placeholder = ph;
            field.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            return field;
        }
    }
}
