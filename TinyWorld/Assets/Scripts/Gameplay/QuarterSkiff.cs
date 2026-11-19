using UnityEngine;

namespace TinyWorld
{
    public sealed class QuarterSkiff : QuarterLandmark
    {
        public Transform portOar, starboardOar;
        public float Stroke { get; private set; }
        public Vector3 Seat => transform.TransformPoint(new Vector3(0, -.19f, -.14f));
        WandererMotor rider;
        float speed;
        public override string Verb => rider && rider.Skiff == this ? "Step out of the skiff" : "Board the skiff";
        protected override void Awake()
        {
            base.Awake();
            rider = FindAnyObjectByType<WandererMotor>();
        }
        protected override float Activate()
        {
            if (!rider) return 0;
            speed = 0;
            if (rider.Skiff != this) rider.Board(this);
            else if (TryLanding(out var feet)) rider.Teleport(feet);
            return .45f;
        }
        bool TryLanding(out Vector3 feet)
        {
            feet = transform.position;
            for (int i = 0; i < 8; i++) {
                var direction = Quaternion.AngleAxis(i * 45, Vector3.up) * transform.right;
                var side = transform.position + direction * 2.35f;
                if (!Physics.Raycast(side + Vector3.up * 2.3f, Vector3.down, out var hit, 2.65f, 1)
                    || hit.normal.y < .68f) continue;
                feet = hit.point + Vector3.up * .06f;
                if (CanStepTo(feet)) return true;
            }
            for (int i = 0; i < 8; i++) {
                var direction = Quaternion.AngleAxis(i * 45, Vector3.up) * -transform.right;
                feet = transform.position + direction * 1.55f;
                if (CanStepTo(feet)) return true;
            }
            return false;
        }
        bool CanStepTo(Vector3 feet)
        {
            var bottom = feet + Vector3.up * .34f;
            var top = feet + Vector3.up * 1.48f;
            var to = Vector3.ProjectOnPlane(feet - transform.position, Vector3.up);
            return !Physics.CheckCapsule(bottom, top, .30f, 1, QueryTriggerInteraction.Ignore)
                && !Physics.CapsuleCast(bottom - to, top - to, .30f, to.normalized, to.magnitude, 1, QueryTriggerInteraction.Ignore);
        }
        bool Clear(Vector3 center, Quaternion yaw)
        {
            var axis = yaw * Vector3.forward * 1.45f;
            if (Mathf.Abs(center.x) > 410 || center.z < -373 || center.z > 745) return false;
            return !Physics.CheckCapsule(center - axis + Vector3.up * .25f, center + axis + Vector3.up * .25f, .55f, 1)
                && !Physics.CheckCapsule(center - axis + Vector3.up * .91f, center + axis + Vector3.up * .91f, .39f, 1);
        }
        public void Sail(Vector2 helm, bool fast, float dt)
        {
            var pos = transform.position;
            var yaw = transform.rotation * Quaternion.AngleAxis(helm.x * 88 * dt, Vector3.up);
            if (Clear(pos, yaw)) transform.rotation = yaw;
            speed = Mathf.MoveTowards(speed, helm.y * (helm.y < 0 ? 3.6f : fast ? 13 : 8), dt * (helm.y == 0 ? 3.3f : 5));
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(speed) * dt / .33f));
            for (int i = 0; i < steps; i++) {
                var next = pos + transform.forward * (speed * dt / steps);
                if (!Clear(next, transform.rotation)) { speed = 0; break; }
                pos = next;
            }
            transform.position = pos;
            Stroke += dt * (helm.y * (fast ? 5 : 3.8f) + Mathf.Abs(helm.x));
            float sweep = Mathf.Sin(Stroke) * 32, lift = Mathf.Cos(Stroke) * 12;
            portOar.localRotation = Quaternion.Euler(0, sweep, lift);
            starboardOar.localRotation = Quaternion.Euler(0, -sweep, -lift);
        }
    }
}
