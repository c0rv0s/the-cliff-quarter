using UnityEngine;

namespace TinyWorld
{
    // Ambient fish schools and circling birds. Meshes are generated once and shared.
    public sealed class RiverLife : MonoBehaviour
    {
        public bool birds;
        public int count = 7;
        public float radius = 5, depth = 1.2f;
        public Material material;
        Transform[] members;
        float[] phase, speed, orbit, height;
        static Mesh fishMesh, birdMesh;
        static Transform player;

        void Start()
        {
            var mesh = birds ? birdMesh ??= BirdMesh() : fishMesh ??= FishMesh();
            members = new Transform[count]; phase = new float[count]; speed = new float[count]; orbit = new float[count]; height = new float[count];
            var random = new System.Random(transform.position.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(birds ? "Bird" : "Fish", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material;
                r.shadowCastingMode = birds ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                float scale = birds ? .7f + (float)random.NextDouble() * .4f : .6f + (float)random.NextDouble() * .7f;
                go.transform.localScale = Vector3.one * scale;
                members[i] = go.transform;
                phase[i] = (float)random.NextDouble() * (birds ? Mathf.PI * 2 : 1.8f);
                speed[i] = (birds ? .16f : .3f) * (.8f + (float)random.NextDouble() * .5f) * (random.NextDouble() < .5 ? 1 : -1);
                if (!birds) speed[i] = .43f;
                orbit[i] = radius * (.45f + (float)random.NextDouble() * .55f);
                height[i] = birds ? (float)random.NextDouble() * 9 : -Mathf.Min(depth,.85f) - (float)random.NextDouble() * .28f;
            }
        }
        void Update()
        {
            float t = Time.time;
            if (!player) { var motor = FindAnyObjectByType<WandererMotor>(); if (motor) player = motor.transform; }
            Vector3 center = transform.position;
            for (int i = 0; i < count; i++)
            {
                float a = phase[i] + t * speed[i];
                float wobble = Mathf.Sin(t * .7f + i) * radius * .18f;
                var local = new Vector3(Mathf.Cos(a) * (orbit[i] + wobble), height[i] + Mathf.Sin(t * .9f + i * 2) * (birds ? 1.2f : .15f), Mathf.Sin(a) * (orbit[i] - wobble) * (birds ? 1 : .7f));
                var target = center + local;
                if (!birds && player)
                {
                    var away = target - player.position; away.y = 0;
                    if (away.sqrMagnitude < 9) target += away.normalized * (3 - away.magnitude) * 1.2f;
                }
                var m = members[i];
                var delta = target - m.position;
                m.position = Vector3.Lerp(m.position, target, 1 - Mathf.Exp(-3 * Time.deltaTime));
                if (delta.sqrMagnitude > .0001f)
                {
                    var look = Quaternion.LookRotation(new Vector3(delta.x, birds ? delta.y : 0, delta.z));
                    float wiggle = birds ? -Mathf.Clamp(speed[i] * 60, -25, 25) : Mathf.Sin(t * 9 + i) * 14;
                    m.rotation = Quaternion.Slerp(m.rotation, look * Quaternion.Euler(0, birds ? 0 : wiggle, birds ? wiggle : 0), 1 - Mathf.Exp(-6 * Time.deltaTime));
                }
                if (birds)
                {
                    float flap = Mathf.Sin(t * 7 + i * 1.3f);
                    float glide = Mathf.Sin(t * .4f + i) > .2f ? .25f : 1;
                    var s = m.localScale; s.y = Mathf.Abs(s.x) * Mathf.Lerp(.15f, 1, (flap * glide + 1) * .5f) * Mathf.Sign(flap * glide + .2f);
                    m.localScale = s;
                }
            }
        }
        static Mesh FishMesh()
        {
            var v = new[]{ new Vector3(0,0,.47f),new Vector3(.12f,.015f,.14f),new Vector3(0,.13f,.06f),new Vector3(-.12f,.015f,.14f),new Vector3(0,-.12f,.06f),new Vector3(0,0,-.31f),
                new Vector3(0,.15f,-.49f),new Vector3(0,0,-.425f),new Vector3(0,-.14f,-.49f),new Vector3(0,.205f,-.06f),new Vector3(0,.073f,-.16f),new Vector3(.18f,-.055f,-.04f),new Vector3(-.18f,-.055f,-.04f) };
            var t = new[]{0,1,2,0,2,3,0,4,1,0,3,4,1,5,2,2,5,3,3,5,4,4,5,1,5,6,7,7,6,5,5,7,8,8,7,5,2,9,10,10,9,2,1,11,4,4,11,1,3,4,12,12,4,3};
            var mesh=Build("Silver-sided fish",v,t);
            var colors=mesh.colors;for(int i=0;i<v.Length;i++) colors[i]=(i==2||i==9||i==10?new Color(.14f,.49f,.55f):i==6||i==8?new Color(.95f,.65f,.26f):new Color(.75f,.89f,.84f)).linear;
            mesh.colors=colors;return mesh;
        }
        static Mesh BirdMesh()
        {
            var v = new[] { new Vector3(0, 0, .25f), new Vector3(0, 0, -.2f), new Vector3(.55f, .18f, -.05f), new Vector3(-.55f, .18f, -.05f), new Vector3(0, -.05f, 0) };
            var t = new[] { 0, 2, 1, 0, 1, 2, 0, 1, 3, 0, 3, 1, 0, 4, 1, 0, 1, 4 };
            return Build("Bird", v, t);
        }
        static Mesh Build(string name, Vector3[] v, int[] t)
        {
            var mesh = new Mesh { name = name, vertices = v, triangles = t };
            var colors = new Color[v.Length]; for (int i = 0; i < colors.Length; i++) colors[i] = new Color(1, 1, 1, 0);
            mesh.colors = colors; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
