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
        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
    }
}
