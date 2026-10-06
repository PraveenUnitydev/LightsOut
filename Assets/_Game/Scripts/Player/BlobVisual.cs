using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// The player's look: a wobbly coloured blob with googly eyes that look where it is going and blink now and then.
    /// Built from procedural sprites, driven purely locally from the observed velocity (nothing here is networked).
    /// </summary>
    public class BlobVisual : MonoBehaviour
    {
        const float BodySize = 0.8f;

        SpriteRenderer _shadow, _body, _eyeL, _eyeR, _pupilL, _pupilR;
        Transform _bodyRoot, _eyes;
        Vector2 _look = Vector2.down;
        float _wobblePhase, _moveAmount, _alpha = 1f, _nextBlink, _blinkUntil;

        public void Build(Transform parent)
        {
            transform.SetParent(parent, false);
            _shadow = GameAssets.AddSprite(transform, "Shadow", GameAssets.Circle, new Color(0, 0, 0, 0.35f), 9);
            _shadow.transform.localPosition = new Vector3(0, -0.32f, 0);
            _shadow.transform.localScale = new Vector3(0.7f, 0.22f, 1);

            _bodyRoot = new GameObject("Body").transform;
            _bodyRoot.SetParent(transform, false);
            _body = GameAssets.AddSprite(_bodyRoot, "Blob", GameAssets.Circle, Color.gray, 10);
            _body.transform.localScale = Vector3.one * BodySize;

            _eyes = new GameObject("Eyes").transform;
            _eyes.SetParent(_bodyRoot, false);
            _eyeL = MakeEye(-0.14f, out _pupilL);
            _eyeR = MakeEye(0.14f, out _pupilR);
            _nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        SpriteRenderer MakeEye(float x, out SpriteRenderer pupil)
        {
            var eye = GameAssets.AddSprite(_eyes, "Eye", GameAssets.Circle, Color.white, 11);
            eye.transform.localPosition = new Vector3(x, 0.1f, 0);
            eye.transform.localScale = Vector3.one * 0.24f;
            pupil = GameAssets.AddSprite(eye.transform, "Pupil", GameAssets.Circle, Color.black, 12);
            pupil.transform.localScale = Vector3.one * 0.5f;
            return eye;
        }

        public void SetColor(Color c)
        {
            if (_body != null) _body.color = new Color(c.r, c.g, c.b, _alpha);
        }

        public void SetAlpha(float a)
        {
            _alpha = a;
            SetRendererAlpha(_body, a);
            SetRendererAlpha(_eyeL, a);
            SetRendererAlpha(_eyeR, a);
            SetRendererAlpha(_pupilL, a);
            SetRendererAlpha(_pupilR, a);
            SetRendererAlpha(_shadow, a * 0.35f);
        }

        static void SetRendererAlpha(SpriteRenderer sr, float a)
        {
            var c = sr.color;
            c.a = a;
            sr.color = c;
            sr.enabled = a > 0.01f;
        }

        /// <summary>Animate from the current velocity (world units/s).</summary>
        public void Tick(Vector2 velocity, float dt)
        {
            float speed = velocity.magnitude;
            _moveAmount = Mathf.MoveTowards(_moveAmount, Mathf.Clamp01(speed / 3f), dt * 6f);
            if (speed > 0.3f) _look = Vector2.Lerp(_look, velocity / speed, dt * 10f);

            // Squash and stretch while walking, plus a little hop.
            _wobblePhase += dt * Mathf.Lerp(4f, 16f, _moveAmount);
            float s = Mathf.Sin(_wobblePhase);
            float squash = 1f + s * Mathf.Lerp(0.03f, 0.1f, _moveAmount);
            _bodyRoot.localScale = new Vector3(2f - squash, squash, 1f);
            _bodyRoot.localPosition = new Vector3(0, Mathf.Abs(s) * 0.06f * _moveAmount, 0);
            if (Mathf.Abs(_look.x) > 0.1f) _bodyRoot.localRotation = Quaternion.Euler(0, 0, -_look.x * 6f * _moveAmount);

            // Eyes slide toward the look direction; pupils a bit further.
            _eyes.localPosition = _look * 0.1f;
            _pupilL.transform.localPosition = _look * 0.22f;
            _pupilR.transform.localPosition = _look * 0.22f;

            // Blink.
            if (Time.time >= _nextBlink)
            {
                _blinkUntil = Time.time + 0.12f;
                _nextBlink = Time.time + Random.Range(2f, 5f);
            }
            float eyeY = Time.time < _blinkUntil ? 0.1f : 1f;
            _eyes.localScale = new Vector3(1f, eyeY, 1f);
        }
    }
}
