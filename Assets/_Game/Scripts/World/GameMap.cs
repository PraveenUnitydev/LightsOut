using System.Collections.Generic;
using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// The office map, built at runtime from an ASCII layout so it is quick to edit.
    /// Legend: '#' wall (blocks movement and sight), 'T' furniture (blocks movement only), 'S' spawn point,
    /// '.' corridor, 'a'-'f' room floors (colours in Palette.Floor). One character = one 1x1 unit cell.
    /// </summary>
    public static class GameMap
    {
        public const int WallLayer = 8;
        public const int PlayerLayer = 9;
        public const int FurnitureLayer = 10;

        public static int WallMask => 1 << WallLayer;

        // a kitchen, b party hall, c boss office, d toilets (stalls), e server room (racks), f storage
        static readonly string[] Rows =
        {
            "############################################",
            "#aTTTTTaaaaaa#bbbbbbbbbbbbbbbb#cccccccccccc#",
            "#aaaaaaaaaaaa#bbbbbbbbbbbbbbbb#cccccccccScc#",
            "#aaSaaaaaaaaa#bbbTTTbbbbTTTbbb#cccTTTTTcccc#",
            "#aaaaaaaaaaaabbbbbbbbSbbbbbbbbccccTTTTTcccc#",
            "#aaaaaaTTTaaabbbbbbbbbbbbbbbbbccccccccccccc#",
            "#aaaaaaTTTaaa#bbbTTTbbbbTTTbbb#cccccccccccc#",
            "#aaaaaaaaaaaa#bbbbbbbbbbbbbbbb#cccccccccccc#",
            "#aaaaaaaaaaaa#bSbbbbbbbbbbbbbb#cccccccccccc#",
            "######..#############..#############..######",
            "#..........................................#",
            "#.....................S....................#",
            "#..S...........T..............T............#",
            "#........................................S.#",
            "#..........................................#",
            "#####..############..##############..#######",
            "#dd#dd#dd#d#eeeeeeeeeeeeeeee#ffffffffffffff#",
            "#dd#dd#dd#d#eeeeeeeeeeeeeeee#ffTTffffffffff#",
            "#dd#dd#dd#d#ee#####ee#####ee#ffTTffffffffff#",
            "#dd#dd#dd#d#eeeeeeeeeeeeeeee#fffffffffTTfff#",
            "#ddddddddddeeeeeeeeeeeeeeeeeffffffffffTTfff#",
            "#ddddddddddeee#####ee#####eefffffffffffffff#",
            "#dddddddddd#eeeeeeeeeeeeeeee#ffffTTffffffff#",
            "#dSdddddddd#eeeeeeeeSeeeeeee#ffffTTfffffSff#",
            "#dddddddddd#eeeeeeeeeeeeeeee#ffffffffffffff#",
            "############################################",
        };

        public static readonly List<Vector2> SpawnPoints = new();
        public static Vector2 Center { get; private set; }
        public static Vector2 Size { get; private set; }

        /// <summary>Cell (column, row) → world position of the cell centre. Row 0 is the top of the map.</summary>
        static Vector2 CellToWorld(float col, int row) => new(col, Rows.Length - 1 - row);

        public static void Build(Transform parent)
        {
            SpawnPoints.Clear();
            int width = 0;
            foreach (var r in Rows) width = Mathf.Max(width, r.Length);
            Size = new Vector2(width, Rows.Length);
            Center = new Vector2((width - 1) * 0.5f, (Rows.Length - 1) * 0.5f);

            var floorRoot = new GameObject("Floor").transform;
            var wallRoot = new GameObject("Walls").transform;
            var furnitureRoot = new GameObject("Furniture").transform;
            floorRoot.SetParent(parent, false);
            wallRoot.SetParent(parent, false);
            furnitureRoot.SetParent(parent, false);

            for (int row = 0; row < Rows.Length; row++)
            {
                string line = Rows[row];
                char lastFloor = '.';
                // Per-cell kind for this row: floor region char, or '#' / 'T'.
                var floorChars = new char[line.Length];
                for (int col = 0; col < line.Length; col++)
                {
                    char c = line[col];
                    if (c == '#' || c == ' ') { floorChars[col] = c; continue; }
                    if (c == 'T' || c == 'S')
                    {
                        floorChars[col] = lastFloor;
                        if (c == 'S') SpawnPoints.Add(CellToWorld(col, row));
                        continue;
                    }
                    lastFloor = c;
                    floorChars[col] = c;
                }

                // Floor runs (everything that is not a wall gets floor, including under furniture).
                ForEachRun(floorChars, ch => ch != '#' && ch != ' ', (start, end, ch) =>
                {
                    var sr = MakeBlock(floorRoot, "Floor", start, end, row, GameAssets.FloorTile, Palette.Floor(ch), 0);
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.size = new Vector2(end - start + 1, 1);
                    sr.transform.localScale = Vector3.one;
                }, sameCharOnly: true);

                // Walls block movement and sight.
                ForEachRun(line.ToCharArray(), ch => ch == '#', (start, end, _) =>
                {
                    var sr = MakeBlock(wallRoot, "Wall", start, end, row, GameAssets.Square, Palette.Wall, 5);
                    sr.gameObject.layer = WallLayer;
                    sr.gameObject.AddComponent<BoxCollider2D>();
                }, sameCharOnly: false);

                // Furniture blocks movement but you can see over it.
                ForEachRun(line.ToCharArray(), ch => ch == 'T', (start, end, _) =>
                {
                    var sr = MakeBlock(furnitureRoot, "Furniture", start, end, row, GameAssets.Square, Palette.Furniture, 4);
                    sr.gameObject.layer = FurnitureLayer;
                    var box = sr.gameObject.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(1f, 0.9f);
                }, sameCharOnly: false);
            }
        }

        static SpriteRenderer MakeBlock(Transform parent, string name, int start, int end, int row, Sprite sprite, Color color, int order)
        {
            var sr = GameAssets.AddSprite(parent, name, sprite, color, order);
            sr.transform.position = CellToWorld((start + end) * 0.5f, row);
            sr.transform.localScale = new Vector3(end - start + 1, 1, 1);
            return sr;
        }

        /// <summary>Calls <paramref name="onRun"/> for each horizontal run of matching cells.</summary>
        static void ForEachRun(char[] cells, System.Func<char, bool> match, System.Action<int, int, char> onRun, bool sameCharOnly)
        {
            int col = 0;
            while (col < cells.Length)
            {
                if (!match(cells[col])) { col++; continue; }
                int start = col;
                char ch = cells[col];
                while (col + 1 < cells.Length && match(cells[col + 1]) && (!sameCharOnly || cells[col + 1] == ch)) col++;
                onRun(start, col, ch);
                col++;
            }
        }
    }
}
