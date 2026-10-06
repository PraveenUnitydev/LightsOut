using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LightsOut
{
    /// <summary>
    /// Floating on-screen joystick: touch anywhere in its area (left side of the screen) and drag.
    /// Writes <see cref="GameInput.TouchMove"/>.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float Range = 110f;   // canvas units the knob can travel
        const float DeadZone = 0.12f;

        RectTransform _area, _base, _knob;
        Image _baseImage, _knobImage;
        int _pointerId = int.MinValue;
        Vector2 _restPosition;

        public static VirtualJoystick Create(Transform parent)
        {
            var area = UIKit.Rect("Joystick Area", parent);
            area.anchorMin = new Vector2(0, 0);
            area.anchorMax = new Vector2(0.45f, 0.8f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var hit = area.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // invisible, but catches touches

            var js = area.gameObject.AddComponent<VirtualJoystick>();
            js._area = area;
            js._restPosition = new Vector2(260, 240);
            js._baseImage = UIKit.Panel("Base", area, new Color(1, 1, 1, 0.12f));
            js._baseImage.sprite = GameAssets.Circle;
            js._baseImage.type = Image.Type.Simple;
            js._baseImage.raycastTarget = false;
            js._base = js._baseImage.rectTransform.Place(Vector2.zero, js._restPosition, new Vector2(Range * 2.2f, Range * 2.2f));
            js._base.pivot = new Vector2(0.5f, 0.5f);

            js._knobImage = UIKit.Panel("Knob", js._base, new Color(1, 1, 1, 0.35f));
            js._knobImage.sprite = GameAssets.Circle;
            js._knobImage.type = Image.Type.Simple;
            js._knobImage.raycastTarget = false;
            js._knob = js._knobImage.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            js._knob.pivot = new Vector2(0.5f, 0.5f);
            return js;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId != int.MinValue) return;
            _pointerId = e.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out var local))
                _base.anchoredPosition = local - _area.rect.min; // area anchors are bottom-left based
            SetAlpha(0.22f, 0.6f);
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_base, e.position, e.pressEventCamera, out var local)) return;
            Vector2 offset = Vector2.ClampMagnitude(local, Range);
            _knob.anchoredPosition = offset;
            Vector2 v = offset / Range;
            GameInput.TouchMove = v.magnitude < DeadZone ? Vector2.zero : v;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            _pointerId = int.MinValue;
            Release();
        }

        void OnDisable()
        {
            _pointerId = int.MinValue;
            Release();
        }

        void Release()
        {
            GameInput.TouchMove = Vector2.zero;
            if (_knob == null) return;
            _knob.anchoredPosition = Vector2.zero;
            _base.anchoredPosition = _restPosition;
            SetAlpha(0.12f, 0.35f);
        }

        void SetAlpha(float baseAlpha, float knobAlpha)
        {
            _baseImage.color = new Color(1, 1, 1, baseAlpha);
            _knobImage.color = new Color(1, 1, 1, knobAlpha);
        }
    }
}
