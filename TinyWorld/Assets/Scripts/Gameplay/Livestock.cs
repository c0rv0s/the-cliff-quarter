using UnityEngine;

namespace TinyWorld
{
    // Farm animals that graze, wander inside a radius and keep to the ground. Skittish ones scatter from the player.
    public sealed class Livestock : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        public int count = 6;
        public float radius = 10, walkSpeed = .8f, scale = 1, bob = .03f;
        public bool skittish, hops;
        Transform[] animals;
        Vector3[] targets;
        float[] wait, phase, pace;
        static Transform player;

        void Start()
        {
            var random = new System.Random(transform.position.GetHashCode());
            animals = new Transform[count]; targets = new Vector3[count]; wait = new float[count]; phase = new float[count]; pace = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.transform.localScale = Vector3.one * scale * (.85f + (float)random.NextDouble() * .3f);
                go.transform.position = Ground(Pick(random));
                go.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
                animals[i] = go.transform; targets[i] = Pick(random);
                wait[i] = (float)random.NextDouble() * 4; phase[i] = (float)random.NextDouble() * 10; pace[i] = .8f + (float)random.NextDouble() * .4f;
            }
        }
        Vector3 Pick(System.Random random)
        {
            float a = (float)random.NextDouble() * Mathf.PI * 2, r = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return transform.position + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
        }
        static Vector3 Ground(Vector3 p) => Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var hit, 8, 1, QueryTriggerInteraction.Ignore) ? hit.point : p;
        void Update()
        {
            if (animals == null) return;
            if (!player) { var motor = FindAnyObjectByType<WandererMotor>(); if (motor) player = motor.transform; }
            float dt = Time.deltaTime, t = Time.time;
            for (int i = 0; i < animals.Length; i++)
            {
                var a = animals[i]; var p = a.position;
                bool fleeing = skittish && player && (player.position - p).sqrMagnitude < 16;
                if (fleeing) { var away = p - player.position; away.y = 0; targets[i] = transform.position + Vector3.ClampMagnitude(p - transform.position + away.normalized * 4, radius); wait[i] = 0; }
                var to = targets[i] - p; to.y = 0;
                float tilt = 0, roll = 0, lift = 0;
                if (wait[i] > 0 || to.magnitude < .3f)
                {
                    if (wait[i] <= 0) { wait[i] = Random.Range(2f, 7f); targets[i] = transform.position + Random.insideUnitSphere.normalized * Random.Range(0, radius); targets[i].y = transform.position.y; }
                    wait[i] -= dt;
                    tilt = hops ? Mathf.Max(0, Mathf.Sin(t * 5 + phase[i])) * 35 : 6 + Mathf.Sin(t * 1.3f + phase[i]) * 4;
                }
                else
                {
                    float speed = walkSpeed * pace[i] * (fleeing ? 3.2f : 1);
                    phase[i] += dt * speed * (hops ? 9 : 5);
                    var heading = Quaternion.LookRotation(to.normalized);
                    a.rotation = Quaternion.Slerp(Quaternion.Euler(0, a.eulerAngles.y, 0), heading, 1 - Mathf.Exp(-4 * dt));
                    p += a.forward * speed * dt;
                    roll = Mathf.Sin(phase[i]) * (hops ? 8 : 3);
                    lift = Mathf.Abs(Mathf.Sin(phase[i])) * bob * (hops ? 3 : 1);
                }
                var ground = Ground(p);
                a.position = new Vector3(p.x, ground.y + lift, p.z);
                a.rotation = Quaternion.Euler(tilt, a.eulerAngles.y, roll);
            }
        }
    }
}
