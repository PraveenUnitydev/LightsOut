using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// Procedural sprites and the two materials in Resources. Everything visual in the prototype is built from these,
    /// so there is no art pipeline to wait on.
    /// </summary>
    public static class GameAssets
    {
        static Material _spriteMaterial, _darknessMaterial;
        static Sprite _square, _circle, _tile, _panel;
        static Font _font;

        /// <summary>Unlit sprite material (2D renderer lit sprites would need lights).</summary>
        public static Material SpriteMaterial => _spriteMaterial ??= Resources.Load<Material>("SpriteUnlit");
        public static Material DarknessMaterial => _darknessMaterial ??= Resources.Load<Material>("Darkness");
        public static Font Font => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>1x1 unit white square, centred pivot.</summary>
        public static Sprite Square => _square ??= MakeSquare();

        /// <summary>Circle with 1 unit diameter, centred pivot.</summary>
        public static Sprite Circle => _circle ??= MakeCircle(64);

        /// <summary>1x1 unit floor tile with a faint darker border, for tiled floors.</summary>
        public static Sprite FloorTile => _tile ??= MakeFloorTile(32);

        /// <summary>Rounded rectangle with 9-slice borders, for UI panels and buttons.</summary>
        public static Sprite Panel => _panel ??= MakeRoundedRect(64, 18);

        public static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (SpriteMaterial != null) sr.sharedMaterial = SpriteMaterial;
            return sr;
        }

        static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        static Sprite MakeSquare()
        {
            var tex = NewTexture(4, 4);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect);
        }

        static Sprite MakeCircle(int size)
        {
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeFloorTile(int size)
        {
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0;
                byte v = edge ? (byte)200 : (byte)255;
                px[y * size + x] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        static Sprite MakeRoundedRect(int size, int radius)
        {
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance to the nearest corner circle centre, clamped to the inner rectangle.
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        }
    }
}
