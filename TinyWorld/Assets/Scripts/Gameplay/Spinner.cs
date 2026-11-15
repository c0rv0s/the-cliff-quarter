using UnityEngine;

namespace TinyWorld
{
    // Windmill sails, water wheels and other slow turning parts.
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float speed = 20;
        void Update() => transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
    }
}
