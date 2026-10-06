using UnityEngine;

namespace LightsOut
{
    /// <summary>All game colours in one place.</summary>
    public static class Palette
    {
        public static readonly Color[] Players =
        {
            Hex("e8423f"), // Red
            Hex("3f7ee8"), // Blue
            Hex("3fc45a"), // Green
            Hex("f2d43a"), // Yellow
            Hex("f28a2e"), // Orange
            Hex("9b5de5"), // Purple
            Hex("3fd6d6"), // Cyan
            Hex("f272b8"), // Pink
            Hex("f0f0f0"), // White
            Hex("8a5a3b"), // Brown
        };

        public static readonly string[] PlayerNames =
        {
            "Red", "Blue", "Green", "Yellow", "Orange", "Purple", "Cyan", "Pink", "White", "Brown",
        };

        public static readonly Color Background = Hex("07070b");
        public static readonly Color Wall = Hex("8d93a3");
        public static readonly Color Furniture = Hex("6b4a33");
        public static readonly Color Ui = Hex("1b1b26");
        public static readonly Color UiAccent = Hex("ffd23f");
        public static readonly Color UiButton = Hex("3a3a52");

        public static Color Player(int index) => index < 0 ? Color.gray : Players[index % Players.Length];
        public static string PlayerColorName(int index) => index < 0 ? "?" : PlayerNames[index % PlayerNames.Length];

        /// <summary>Floor colour for a map region character (see GameMap).</summary>
        public static Color Floor(char region) => region switch
        {
            'a' => Hex("3d4f3a"), // kitchen: green tiles
            'b' => Hex("4a2f4f"), // party hall: purple carpet
            'c' => Hex("4f3a2a"), // boss office: wood
            'd' => Hex("2f4a52"), // toilets: blue tiles
            'e' => Hex("2a2d36"), // server room: grey
            'f' => Hex("47432c"), // storage: concrete
            _ => Hex("38363f"),   // corridor
        };

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
