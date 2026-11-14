using UnityEngine;

namespace TinyWorld
{
    public static class Palette
    {
        public static readonly Color Limestone = Hex("F0DFBF");
        public static readonly Color Sandstone = Hex("BC956F");
        public static readonly Color Terracotta = Hex("B66547");
        public static readonly Color Water = Hex("268F98");
        public static readonly Color Olive = Hex("63774C");
        public static readonly Color Shadow = Hex("354E63");
        public static readonly Color Brass = Hex("DAAE57");
        public static readonly Color Sky = Hex("8FC4DF");

        static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
