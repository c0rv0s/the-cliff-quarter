using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    public sealed class ClimbableVine : MonoBehaviour
    {
        public static readonly List<ClimbableVine> Active = new();
        public float length = 3, width = .9f;
        public Vector3 Top => transform.position;
        public Vector3 Bottom => Top - Vector3.up * length;
        public Vector3 Outward => transform.forward;
        static readonly Dictionary<Vector2Int, List<ClimbableVine>> cells = new();
        Vector2Int cell;
        static Vector2Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / 8), Mathf.FloorToInt(p.z / 8));
        public static void Nearby(Vector3 p, List<ClimbableVine> result)
        {
            result.Clear();
            var a = Cell(p - new Vector3(2,0,2)); var b = Cell(p + new Vector3(2,0,2));
            for (int x = a.x; x <= b.x; x++) for (int z = a.y; z <= b.y; z++)
                if (cells.TryGetValue(new Vector2Int(x,z), out var list)) result.AddRange(list);
        }
        void OnEnable() {
            Active.Add(this); cell = Cell(Top);
            if (!cells.TryGetValue(cell, out var bucket)) cells[cell] = bucket = new List<ClimbableVine>();
            bucket.Add(this);
        }
        void Start() {
            if (Cell(Top) != cell) { OnDisable(); OnEnable(); }
        }
        void OnDisable() {
            Active.Remove(this);
            if (cells.TryGetValue(cell, out var bucket)) { bucket.Remove(this); if (bucket.Count==0) cells.Remove(cell); }
        }
    }
}
