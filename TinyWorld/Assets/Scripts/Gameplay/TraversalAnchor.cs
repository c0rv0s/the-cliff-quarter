using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    public sealed class TraversalAnchor : MonoBehaviour
    {
        public static readonly List<TraversalAnchor> Active = new();
        public float reach = 18;
        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
    }
}
