using UnityEngine;

namespace TinyWorld
{
    public sealed class RiverWindsocks : MonoBehaviour
    {
        public Transform[] fish;
        void Update()
        {
            for (int i = 0; i < fish.Length; i++)
            {
                float t = Time.time * .68f + i * 1.9f;
                fish[i].localRotation = Quaternion.Euler(4*Mathf.Cos(t), 12 + 9*Mathf.Sin(t*.8f), -4 + 5*Mathf.Sin(t));
            }
        }
    }
}
