using UnityEngine;

namespace TinyWorld
{
    public sealed class QuarterSkiff : QuarterLandmark
    {
        public Transform portOar, starboardOar;
        public float Pull { get; private set; }
        public float Recovery { get; private set; }
        float phase, cadence, effort;
        SkiffWaterChart chart;
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
            var bounds=rider.travelBounds;
            if(center.x<bounds.xMin+10 || center.x>bounds.xMax-10 || center.z<bounds.yMin+12 || center.z>bounds.yMax-15)return false;
            return !Physics.CheckCapsule(center - axis + Vector3.up * .25f, center + axis + Vector3.up * .25f, .55f, 1)
                && !Physics.CheckCapsule(center - axis + Vector3.up * .91f, center + axis + Vector3.up * .91f, .39f, 1);
        }
        public bool CallToShore()
        {
            var quests = QuarterQuests.Current;
            if (!quests || !quests.Earned(5) || !rider || rider.Skiff) return false;
            if (!chart) chart = Resources.Load<SkiffWaterChart>("SkiffWaterChart");
            if (!chart || !NearestBerth(rider.transform.position, out var berth, out var heading)) {
                quests.Announce("The skiff couldn’t find clear water.", 3);
                return false;
            }
            transform.SetPositionAndRotation(berth, heading);
            speed = cadence = effort = phase = Pull = Recovery = 0;
            portOar.localRotation = starboardOar.localRotation = Quaternion.identity;
            if (rider.feedback) rider.feedback.CallSkiff(berth);
            var delta = berth - rider.transform.position;
            var compass = new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
            int bearing = Mathf.RoundToInt(Mathf.Atan2(delta.x, delta.z) / (Mathf.PI / 4));
            float distance = new Vector2(delta.x, delta.z).magnitude;
            quests.Announce(distance < 4 ? "Your skiff is alongside." :
                $"Your skiff is {distance:0} m {compass[(bearing + 8) % 8]}.", 4);
            return true;
        }

        bool NearestBerth(Vector3 from, out Vector3 berth, out Quaternion heading)
        {
            const float step = SkiffWaterChart.Spacing;
            int cx = Mathf.Clamp(Mathf.RoundToInt((from.x - chart.origin.x) / step), 0, chart.width - 1);
            int cz = Mathf.Clamp(Mathf.RoundToInt((from.z - chart.origin.y) / step), 0, chart.height - 1);
            Vector3 best = default;
            Quaternion yaw = Quaternion.identity;
            float bestDistance = float.PositiveInfinity;
            from.y = transform.position.y;

            void Consider(Vector3 p) {
                float d = (p - from).sqrMagnitude;
                if (d < 1.65f * 1.65f || d >= bestDistance || !TryBerth(p, from, out var facing)) return;
                best = p; yaw = facing; bestDistance = d;
            }
            void Visit(int x, int z) {
                if (chart.Marked(x,z)) Consider(chart.Point(x,z,from.y));
            }
            for (int radius = 0; radius < Mathf.Max(chart.width,chart.height); radius++) {
                if (radius > 0 && (radius - .5f) * step > Mathf.Sqrt(bestDistance)) break;
                for (int x = cx-radius; x <= cx+radius; x++) {
                    Visit(x,cz-radius);
                    if (radius > 0) Visit(x,cz+radius);
                }
                for (int z = cz-radius+1; z < cz+radius; z++) {
                    Visit(cx-radius,z); Visit(cx+radius,z);
                }
            }
            if (!float.IsPositiveInfinity(bestDistance)) {
                var node = best;
                for (int dx=-4; dx<=4; dx++) for (int dz=-4; dz<=4; dz++) {
                    var offset = new Vector3(dx*.75f,0,dz*.75f);
                    if ((node+offset-from).sqrMagnitude >= bestDistance || offset.sqrMagnitude == 0) continue;
                    if (!Physics.CapsuleCast(node+Vector3.up*.22f,node+Vector3.up*.94f,.67f,
                        offset.normalized,offset.magnitude,1,QueryTriggerInteraction.Ignore)) Consider(node+offset);
                }
            }
            berth = best; heading = yaw;
            return !float.IsPositiveInfinity(bestDistance);
        }

        bool TryBerth(Vector3 p, Vector3 from, out Quaternion facing)
        {
            facing = Quaternion.identity;
            if (!RiverWaterVolume.At(p-Vector3.up*.7f) || !Deep(p)) return false;
            var sideways = Quaternion.LookRotation(Vector3.Cross(Vector3.up,p-from));
            for (int i=0; i<8; i++) {
                var yaw = Quaternion.AngleAxis(i*45,Vector3.up)*sideways;
                var axis = yaw*Vector3.forward;
                if (!Clear(p,yaw) || !Clear(p+axis*3,yaw) || !Deep(p+axis*3)) continue;
                var bow = p+axis*1.45f;
                if (Physics.CapsuleCast(bow+Vector3.up*.25f,bow+Vector3.up*.91f,.55f,axis,3,1,QueryTriggerInteraction.Ignore)) continue;
                facing = yaw;
                return true;
            }
            return false;
        }
        static bool Deep(Vector3 p) => !Physics.Raycast(p+Vector3.up*400,Vector3.down,out var hit,425,1,
            QueryTriggerInteraction.Ignore) || hit.point.y < p.y-.48f;

        public void Sail(Vector2 helm, bool fast, float dt)
        {
            var pos = transform.position;
            var yaw = transform.rotation * Quaternion.AngleAxis(helm.x * 88 * dt, Vector3.up);
            if (Clear(pos, yaw)) transform.rotation = yaw;
            bool pullingHard = fast && helm.y > 0;
            float targetSpeed = helm.y * (helm.y < 0 ? 3.6f : pullingHard ? 16f : 8f);
            float acceleration = helm.y == 0 ? 3.3f : pullingHard ? 8f : 5f;
            speed = Mathf.MoveTowards(speed, targetSpeed, dt * acceleration);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(speed) * dt / .33f));
            for (int i = 0; i < steps; i++) {
                var next = pos + transform.forward * (speed * dt / steps);
                if (!Clear(next, transform.rotation)) { speed = 0; break; }
                pos = next;
            }
            transform.position = pos;
            float targetCadence = helm.y * (pullingHard ? 5.7f : 3.8f) + Mathf.Abs(helm.x);
            float previous = cadence;
            cadence = Mathf.Lerp(cadence, targetCadence, 1 - Mathf.Exp(-6 * dt));
            effort = Mathf.Lerp(effort, Mathf.Clamp01(Mathf.Abs(helm.y) + Mathf.Abs(helm.x)), 1 - Mathf.Exp(-5 * dt));
            phase = Mathf.Repeat(phase + (previous + cadence) * .5f * dt, 2 * Mathf.PI);
            Pull = Mathf.Sin(phase) * effort;
            Recovery = Mathf.Cos(phase) * effort;
            // Immerse the blades as they sweep toward the stern, then lift them on the return.
            float sweep = Pull * 32, lift = -Recovery * 18;
            portOar.localRotation = Quaternion.Euler(0, sweep, lift);
            starboardOar.localRotation = Quaternion.Euler(0, -sweep, -lift);
        }
    }
}
