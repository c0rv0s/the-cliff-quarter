using UnityEngine;

namespace TinyWorld
{
    // Each marked cell opens onto the waterway connected to the fish pier.
    public sealed class SkiffWaterChart : ScriptableObject
    {
        public const float Spacing = 3f;
        public Vector2 origin;
        public int width, height;
        public byte[] water;
        public Vector3 Point(int x, int z, float y) => new(origin.x+x*Spacing, y, origin.y+z*Spacing);
        public bool Marked(int x, int z)
        {
            if(x<0 || z<0 || x>=width || z>=height)return false;
            int index=z*width+x;
            return (water[index>>3] & (1<<(index&7)))!=0;
        }
    }
}
