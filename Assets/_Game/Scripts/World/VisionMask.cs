using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// Among Us-style sight: a mesh of darkness covering everything the local player cannot see.
    /// Each frame rays are cast around the player against walls; the mesh runs from each ray's end out to a far ring,
    /// with a soft fade before the vision radius and a hard edge at walls.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionMask : MonoBehaviour
    {
        public static VisionMask Instance { get; private set; }

        [SerializeField] float _radius = 3.6f;
        [SerializeField, Range(0f, 1f)] float _darkness = 0.94f;
        [SerializeField] int _rays = 240;
        [Tooltip("Width of the soft fade at the edge of sight.")]
        [SerializeField] float _softEdge = 1.1f;
        [Tooltip("How far past a wall's surface is still lit, so wall faces are visible.")]
        [SerializeField] float _wallReveal = 0.45f;
        [SerializeField] float _farDistance = 80f;

        /// <summary>The player whose sight is shown. Null hides the mask (menus).</summary>
        public Transform Target;

        /// <summary>Current (animated) vision radius in world units.</summary>
        public float Radius { get; private set; }

        /// <summary>Vision radius the mask grows/shrinks toward.</summary>
        public float TargetRadius { get; set; }

        Mesh _mesh;
        Vector3[] _verts;
        Color32[] _colors;
        MeshRenderer _renderer;

        void Awake()
        {
            Instance = this;
            Radius = TargetRadius = _radius;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = GameAssets.DarknessMaterial;
            _renderer.sortingOrder = 1000;

            // 3 vertices per ray: inner (clear), edge (dark), far (dark).
            _verts = new Vector3[_rays * 3];
            _colors = new Color32[_rays * 3];
            var tris = new int[_rays * 12];
            byte dark = (byte)(_darkness * 255);
            for (int i = 0; i < _rays; i++)
            {
                int j = (i + 1) % _rays;
                int a0 = i * 3, b0 = j * 3;
                _colors[a0] = new Color32(0, 0, 0, 0);
                _colors[a0 + 1] = new Color32(0, 0, 0, dark);
                _colors[a0 + 2] = new Color32(0, 0, 0, dark);
                int t = i * 12;
                // Soft band: inner → edge.
                tris[t] = a0; tris[t + 1] = b0; tris[t + 2] = b0 + 1;
                tris[t + 3] = a0; tris[t + 4] = b0 + 1; tris[t + 5] = a0 + 1;
                // Solid band: edge → far.
                tris[t + 6] = a0 + 1; tris[t + 7] = b0 + 1; tris[t + 8] = b0 + 2;
                tris[t + 9] = a0 + 1; tris[t + 10] = b0 + 2; tris[t + 11] = a0 + 2;
            }

            _mesh = new Mesh { name = "Vision Mask" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.colors32 = _colors;
            _mesh.triangles = tris;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            transform.position = Vector3.zero;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            _renderer.enabled = Target != null;
            if (Target == null) return;

            Radius = Mathf.MoveTowards(Radius, TargetRadius, 3f * Time.deltaTime);
            Vector2 origin = Target.position;
            float inner = Mathf.Max(0.2f, Radius - _softEdge);
            int mask = GameMap.WallMask;

            for (int i = 0; i < _rays; i++)
            {
                float angle = i * Mathf.PI * 2f / _rays;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var hit = Physics2D.Raycast(origin, dir, Radius, mask);
                float edge = hit.collider != null ? hit.distance + _wallReveal : Radius;
                edge = Mathf.Min(edge, Radius);
                int v = i * 3;
                _verts[v] = origin + dir * Mathf.Min(inner, edge);
                _verts[v + 1] = origin + dir * edge;
                _verts[v + 2] = origin + dir * _farDistance;
            }

            _mesh.vertices = _verts;
            _mesh.RecalculateBounds();
        }

        /// <summary>True if the point is within sight of the target (in range and not behind a wall).</summary>
        public bool CanSee(Vector2 point, float margin = 0f)
        {
            if (Target == null) return false;
            Vector2 origin = Target.position;
            Vector2 delta = point - origin;
            float dist = delta.magnitude;
            if (dist > Radius + margin) return false;
            if (dist < 0.01f) return true;
            return !Physics2D.Raycast(origin, delta / dist, dist, GameMap.WallMask);
        }
    }
}
