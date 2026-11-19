using UnityEngine;

namespace TinyWorld
{
    [DefaultExecutionOrder(-80)]
    public sealed class QuarterSwingDoor : MonoBehaviour
    {
        public Transform left, right;
        public float width, height;
        static WandererMotor walker;
        float angle, direction, linger;

        void Update()
        {
            if (!walker) walker = FindAnyObjectByType<WandererMotor>();
            if (!walker || Time.deltaTime <= 0) return;
            var p = transform.InverseTransformPoint(walker.transform.position);
            float reach = width * .5f + .85f;
            bool near = p.y > -.9f && p.y < height && Mathf.Abs(p.x) < width * .5f + .25f && Mathf.Abs(p.z) < reach;
            if (near)
            {
                if (direction == 0) direction = p.z > 0 ? 1 : -1;
                linger = .65f;
            }
            else linger -= Time.deltaTime;
            float target = linger > 0 ? direction * 93 : 0;
            float next = Mathf.MoveTowards(angle, target, Time.deltaTime * (near ? 300 : 100));
            if (next != angle)
            {
                angle = next;
                left.localRotation = Quaternion.Euler(0, angle, 0);
                right.localRotation = Quaternion.Euler(0, -angle, 0);
            }
            if (!near && angle == 0) direction = 0;
        }
    }
}
