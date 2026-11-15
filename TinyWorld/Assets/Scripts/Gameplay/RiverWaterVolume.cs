using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    public sealed class RiverWaterVolume : MonoBehaviour
    {
        public static readonly List<RiverWaterVolume> Active = new();
        public Vector3 size = new(400, 24, 600);
        public float Surface => transform.position.y;
        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
        public bool Contains(Vector3 point)
        {
            var p = transform.InverseTransformPoint(point);
            return Mathf.Abs(p.x) < size.x * .5f && Mathf.Abs(p.z) < size.z * .5f && p.y < .4f && p.y > -size.y;
        }
        public static RiverWaterVolume At(Vector3 point)
        {
            foreach (var water in Active) if (water.Contains(point)) return water;
            return null;
        }
    }
}
