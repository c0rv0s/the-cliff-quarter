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
        // Accents from the concepts: blossoms, shutters, shallows.
        public static readonly Color Wisteria = Hex("A68AD8");
        public static readonly Color Bougainvillea = Hex("D2457E");
        public static readonly Color Shutter = Hex("2F5F8F");
        public static readonly Color Shallows = Hex("5ED8C8");
        public static readonly Color DeepWater = Hex("145068");

        public static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
