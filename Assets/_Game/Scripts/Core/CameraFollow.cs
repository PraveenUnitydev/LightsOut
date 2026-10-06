using UnityEngine;

namespace LightsOut
{
    /// <summary>Follows the local player in game; shows the whole map behind the menu.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] float _gameSize = 5.5f;
        [SerializeField] float _smoothTime = 0.12f;

        Camera _cam;
        Vector3 _velocity;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            var me = PlayerAvatar.Local;
            Vector2 target;
            float size;
            if (me != null)
            {
                target = me.transform.position;
                size = _gameSize;
            }
            else
            {
                target = GameMap.Center;
                // Fit the whole map on screen.
                size = Mathf.Max(GameMap.Size.y * 0.5f, GameMap.Size.x * 0.5f / Mathf.Max(_cam.aspect, 0.1f)) + 1f;
            }

            var goal = new Vector3(target.x, target.y, -10f);
            if (me != null && (transform.position - goal).sqrMagnitude > 400f) transform.position = goal; // snap after spawn
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, _smoothTime);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, size, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
    }
}
