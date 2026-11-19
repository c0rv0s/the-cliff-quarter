using UnityEngine;

namespace TinyWorld
{
    public sealed class HarbourSeaLion : MonoBehaviour
    {
        public Transform head, leftFlipper, rightFlipper;
        public bool swimming;
        public float phase;
        Vector3 home;
        Quaternion bearing;
        static WandererMotor visitor;
        void Start() { home = transform.position; bearing = transform.rotation; }
        void Update()
        {
            if (!visitor) visitor = FindAnyObjectByType<WandererMotor>();
            if (!visitor || (visitor.transform.position-home).sqrMagnitude > 135*135) return;
            float t=Time.time*.48f+phase;
            float turn=0, nod=Mathf.Sin(t*1.7f)*3, stroke=2;
            if (swimming) {
                var dir=bearing*new Vector3(-Mathf.Sin(t)*3.4f,0,Mathf.Cos(t)*1.7f);
                transform.position=home+bearing*new Vector3(3.4f*Mathf.Cos(t),.23f*Mathf.Sin(t*2)-.5f*Mathf.Max(0,Mathf.Sin(t*.7f)),1.7f*Mathf.Sin(t));
                transform.rotation=Quaternion.LookRotation(dir)*Quaternion.Euler(6*Mathf.Cos(t*2),0,0);
                nod+=13; stroke=21;
            } else {
                var local=transform.InverseTransformPoint(visitor.transform.position);
                if(local.sqrMagnitude<55) turn=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-44,44);
                else turn=Mathf.Sin(t*.64f)*16;
            }
            head.localRotation=Quaternion.Slerp(head.localRotation,Quaternion.Euler(nod,turn,0),1-Mathf.Exp(-3*Time.deltaTime));
            float beat=Mathf.Sin(t*4)*stroke;
            leftFlipper.localRotation=Quaternion.Euler(0,-beat,beat*.41f);
            rightFlipper.localRotation=Quaternion.Euler(0,beat,-beat*.41f);
        }
    }
}
