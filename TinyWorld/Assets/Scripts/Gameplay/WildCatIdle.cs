using UnityEngine;

namespace TinyWorld
{
    public sealed class WildCatIdle : MonoBehaviour
    {
        public float phase;
        Vector3 restingScale;
        void Awake() => restingScale=transform.localScale;
        void Update()
        {
            float breath=Mathf.Sin(Time.time*1.9f+phase);
            transform.localScale=Vector3.Scale(restingScale,new Vector3(1,1+.022f*breath,1));
        }
    }
}
