using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    // Ambience: 2D beds crossfaded by the listener's altitude (river quays → town → windy uplands),
    // plus 3D loops at the harbour, each waterfall, the nearest upland stream and the sheep flocks,
    // and a distant church peal now and then. Volumes follow WandererFeedback.volume.
    public sealed class CityAmbience : MonoBehaviour
    {
        public WandererFeedback feedback;
        public Vector3 harbour = new(0, 4, 450); // basin between the BuildQuarter piers
        readonly List<(AudioSource source, float level)> emitters = new();
        AudioSource lapping, town, wind, cicadas, stream, bells;
        Transform listener, world;
        float bellTimer;

        void Start()
        {
            listener = Camera.main.transform;
            world = new GameObject("City ambience").transform; // emitters stay put while the player moves
            lapping = Bed("Ambience/river_lapping"); town = Bed("Ambience/town_day");
            wind = Bed("Ambience/high_wind"); cicadas = Bed("Ambience/cicadas");
            Emitter("harbour_gulls", harbour, 35, .8f);
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial && r.sharedMaterial.shader.name == "TinyWorld/Waterfall")
                    Emitter("waterfall", r.bounds.center, 4 + r.bounds.size.y * .3f, .75f);
            foreach (var flock in FindObjectsByType<Livestock>(FindObjectsSortMode.None))
                if (flock.name.StartsWith("Sheep")) Emitter("sheep_bells", flock.transform.position, 8, .5f);
            stream = Emitter("stream", transform.position, 6, .6f);
            bells = gameObject.AddComponent<AudioSource>(); bells.clip = Resources.Load<AudioClip>("Audio/Bells/distant_church_01");
            bells.playOnAwake = false; bells.spatialBlend = 0;
            bellTimer = Random.Range(30f, 50f);
        }
        AudioSource Bed(string path)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>("Audio/" + path); s.loop = true; s.spatialBlend = 0; s.volume = 0;
            s.time = Random.Range(0, s.clip.length); s.Play(); return s;
        }
        AudioSource Emitter(string clip, Vector3 position, float near, float level)
        {
            var go = new GameObject("Ambience · " + clip); go.transform.SetParent(world, false); go.transform.position = position;
            var s = go.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>("Audio/Emitters/" + clip); s.loop = true; s.spatialBlend = 1; s.dopplerLevel = 0;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = near; s.maxDistance = near * 30;
            s.time = Random.Range(0, s.clip.length); s.Play();
            emitters.Add((s, level)); return s;
        }
        void OnDestroy() { if (world) Destroy(world.gameObject); }
        void Update()
        {
            float y = listener.position.y, master = feedback.volume, k = 1 - Mathf.Exp(-2 * Time.unscaledDeltaTime);
            Fade(lapping, .5f * (1 - Mathf.InverseLerp(3, 30, y)), master, k);
            Fade(town, .4f * (1 - Mathf.InverseLerp(95, 150, y)), master, k);
            Fade(wind, .06f + .3f * Mathf.InverseLerp(30, 170, y), master, k);
            Fade(cicadas, .22f * Mathf.InverseLerp(85, 110, y), master, k);
            foreach (var (source, level) in emitters) source.volume = level * master;
            bells.volume = .35f * master;
            stream.transform.position = NearestStream(listener.position);
            if ((bellTimer -= Time.deltaTime) <= 0) { bellTimer = Random.Range(150f, 260f); bells.Play(); }
        }
        static void Fade(AudioSource s, float level, float master, float k) => s.volume = Mathf.Lerp(s.volume, level * master, k);
        // Upland stream segments are the small water volumes; the sea-level river is one large one.
        static Vector3 NearestStream(Vector3 from)
        {
            Vector3 best = from + Vector3.down * 1000; float distance = float.MaxValue;
            foreach (var water in RiverWaterVolume.Active)
            {
                if (water.size.x > 50) continue;
                var p = water.transform.position; float d = (p - from).sqrMagnitude;
                if (d < distance) { distance = d; best = p; }
            }
            return best;
        }
    }
}
