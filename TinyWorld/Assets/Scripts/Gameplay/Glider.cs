using UnityEngine;

namespace TinyWorld
{
    // Striped canvas hang glider: model, unfold/fold motion, banking, wingtip trails and wind.
    // WandererMotor owns the flight itself.
    [RequireComponent(typeof(WandererMotor))]
    public sealed class Glider : MonoBehaviour
    {
        public float unfoldSeconds = .24f, foldSeconds = .14f, maxBank = 26, span = 4.1f;
        public float Openness { get; private set; }
        WandererMotor motor;
        Transform root;
        TrailRenderer[] trails;
        AudioSource wind;
        Material canvas, stripe;
        Mesh wing;
        float progress, bank, pitch, yawRate, lastYaw;
        bool opening;

        void Awake()
        {
            motor = GetComponent<WandererMotor>();
            root = new GameObject("Glider").transform;
            root.SetParent(motor.visual, false); root.localPosition = new Vector3(0, 2.32f, -.05f);
            canvas = Canvas("Glider canvas", Palette.Terracotta); stripe = Canvas("Glider stripe", Palette.Limestone);
            var sail = new GameObject("Sail", typeof(MeshFilter), typeof(MeshRenderer));
            sail.layer = 2; sail.transform.SetParent(root, false);
            sail.GetComponent<MeshFilter>().sharedMesh = wing = BuildWing();
            sail.GetComponent<MeshRenderer>().sharedMaterials = new[] { canvas, stripe };
            // Brass frame: leading edges, keel, cross spar, A-frame and the bar the wanderer holds.
            Vector3 nose = new(0, .02f, 1), tipL = new(-span * .5f, -.1f, -.55f), tipR = new(span * .5f, -.1f, -.55f);
            Vector3 barL = new(-.36f, -.36f, .16f), barR = new(.36f, -.36f, .16f), apex = new(0, -.02f, .12f);
            Tube(nose, tipL, .028f); Tube(nose, tipR, .028f); Tube(nose, new Vector3(0, .02f, -.62f), .026f);
            Tube(Vector3.Lerp(nose, tipL, .48f), Vector3.Lerp(nose, tipR, .48f), .02f);
            Tube(apex, barL, .018f); Tube(apex, barR, .018f); Tube(barL, barR, .022f);
            var trailMaterial = motor.feedback ? motor.feedback.dust.GetComponent<ParticleSystemRenderer>().sharedMaterial : null;
            trails = new[] { Trail(tipL, trailMaterial), Trail(tipR, trailMaterial) };
            wind = root.gameObject.AddComponent<AudioSource>();
            wind.clip = Resources.Load<AudioClip>("Audio/GlideLoop/flight_wind"); wind.loop = true; wind.playOnAwake = false; wind.spatialBlend = 0; wind.volume = 0;
            root.gameObject.SetActive(false);
        }
        Material Canvas(string name, Color color)
        {
            var m = new Material(motor.ropeMaterial) { name = name };
            m.SetColor("_BaseColor", color); m.SetFloat("_Gloss", 0); m.SetFloat("_Variation", 0);
            m.SetFloat("_Translucency", .45f); m.SetFloat("_RimStrength", .3f); m.SetColor("_EmissionColor", Color.black);
            return m;
        }
        // Delta wing in chordwise strips that alternate terracotta and limestone, billowed upward, both faces.
        Mesh BuildWing()
        {
            const int Strips = 5, Ribs = 4;
            Vector3 nose = new(0, .02f, 1), tail = new(0, .02f, -.62f);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var tris = new[] { new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>() };
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tip = new(side * span * .5f, -.1f, -.55f);
                Vector3 Point(float u, float v)
                {
                    // u: root to tip; v: leading to trailing edge. Trailing edge scallops forward between ribs.
                    var lead = Vector3.Lerp(nose, tip, u);
                    var trail = Vector3.Lerp(tail, tip, u) + Vector3.forward * (.12f * Mathf.Sin(u * Mathf.PI));
                    return Vector3.Lerp(lead, trail, v) + Vector3.up * (.16f * Mathf.Sin(v * Mathf.PI) * (1 - u * .7f));
                }
                for (int s = 0; s < Strips; s++)
                for (int face = 0; face < 2; face++)
                {
                    // The upper face sits a few millimetres higher so the two faces never share vertices.
                    int start = vertices.Count; var lift = Vector3.up * (face * .004f);
                    for (int r = 0; r <= Ribs; r++)
                    {
                        float v = r / (float)Ribs;
                        vertices.Add(Point(s / (float)Strips, v) + lift); vertices.Add(Point((s + 1) / (float)Strips, v) + lift);
                    }
                    var list = tris[s % 2];
                    for (int r = 0; r < Ribs; r++)
                    {
                        int a = start + r * 2, b = a + 1, c = a + 2, d = a + 3;
                        // Winding flips with the side and the face so each face points outward.
                        if ((side > 0) == (face == 0)) list.AddRange(new[] { a, c, b, b, c, d });
                        else list.AddRange(new[] { a, b, c, b, d, c });
                    }
                }
            }
            var mesh = new Mesh { name = "Glider sail" };
            mesh.SetVertices(vertices); mesh.subMeshCount = 2;
            mesh.SetTriangles(tris[0], 0); mesh.SetTriangles(tris[1], 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        void Tube(Vector3 a, Vector3 b, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "Brass spar"; go.layer = 2;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false); go.transform.localPosition = (a + b) * .5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            go.transform.localScale = new Vector3(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2);
            go.GetComponent<Renderer>().sharedMaterial = motor.ropeMaterial;
        }
        TrailRenderer Trail(Vector3 at, Material material)
        {
            var go = new GameObject("Wingtip trail"); go.transform.SetParent(root, false); go.transform.localPosition = at;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material; trail.time = .5f; trail.minVertexDistance = .2f; trail.widthMultiplier = .06f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Palette.Limestone, 0), new GradientColorKey(Palette.Sky, 1) },
                alphaKeys = new[] { new GradientAlphaKey(.55f, 0), new GradientAlphaKey(0, 1) }
            };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.emitting = false;
            return trail;
        }
        public void Open() { opening = true; lastYaw = motor.visual.eulerAngles.y; bank = 0; }
        public void Fold(bool instant = false)
        {
            opening = false;
            if (instant) { progress = Openness = 0; Apply(); }
        }
        void LateUpdate()
        {
            float dt = Time.deltaTime; if (dt <= 0) return;
            progress = Mathf.MoveTowards(progress, opening ? 1 : 0, dt / (opening ? unfoldSeconds : foldSeconds));
            // Unfold overshoots (OutBack); fold snaps shut (OutCubic).
            float p = opening ? progress - 1 : 1 - progress;
            Openness = opening ? 1 + 2.70158f * p * p * p + 1.70158f * p * p : 1 - (1 - Mathf.Pow(1 - p, 3));
            float yaw = motor.visual.eulerAngles.y;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1 - Mathf.Exp(-8 * dt)); lastYaw = yaw;
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * .22f, -maxBank, maxBank), 1 - Mathf.Exp(-5 * dt));
            bool dive = motor.Action == "Glide dive";
            pitch = Mathf.Lerp(pitch, dive ? 12 : -3 + Mathf.Clamp(motor.VerticalSpeed, -3, 3), 1 - Mathf.Exp(-4 * dt));
            Apply();
            float flight = Mathf.Clamp01(Openness) * Mathf.InverseLerp(4, motor.glideDiveSpeed, motor.Speed);
            float volume = motor.feedback ? motor.feedback.volume : .65f;
            wind.volume = Mathf.Lerp(wind.volume, (.12f + .38f * flight) * volume * (progress > 0 ? 1 : 0), 1 - Mathf.Exp(-6 * dt));
            wind.pitch = Mathf.Lerp(.75f, 1.25f, flight);
            foreach (var trail in trails) trail.emitting = opening && progress >= 1 && motor.Speed > 6;
        }
        void Apply()
        {
            bool visible = progress > 0;
            if (root.gameObject.activeSelf != visible)
            {
                root.gameObject.SetActive(visible);
                if (visible) { wind.Play(); foreach (var trail in trails) trail.Clear(); }
            }
            float e = Mathf.Max(0, Openness);
            root.localScale = new Vector3(Mathf.LerpUnclamped(.06f, 1, e), Mathf.LerpUnclamped(.6f, 1, e), Mathf.LerpUnclamped(.4f, 1, e));
            root.localRotation = Quaternion.Euler(pitch * e, 0, bank * e + Mathf.Sin(Time.time * 1.7f) * 1.5f * e);
        }
        void OnDestroy() { Destroy(wing); Destroy(canvas); Destroy(stripe); }
    }
}
