using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    public sealed class CityAmbience : MonoBehaviour
    {
        static readonly Bounds EastMarket = new(new Vector3(190, 5, 520), new Vector3(174, 14, 80));
        static readonly Bounds WestMarket = new(new Vector3(-183, 5, 520), new Vector3(128, 14, 80));
        static readonly Bounds Harbour = new(new Vector3(0, 3, 443), new Vector3(214, 16, 92));
        static readonly Bounds Farmlands = new(new Vector3(249, 114, 168), new Vector3(260, 58, 450));
        static readonly Bounds Wildlands = new(new Vector3(0, 30, -612), new Vector3(1010, 80, 806));

        public WandererFeedback feedback;
        readonly List<(AudioSource source, float level)> emitters = new();
        readonly List<Cue> cues = new();
        AudioSource lapping, wind, market, grass, canopy, surf, cave, stream;
        Transform listener, world;
        GrottoPool grotto;
        bool quarter, hasMarket, hasWildlands;

        sealed class Cue
        {
            public AudioSource source;
            public AudioClip[] clips;
            public Vector2 gap;
            public float level, remaining;
            public int last = -1;
        }

        void Start()
        {
            var camera = Camera.main;
            listener = camera ? camera.transform : feedback.player.transform;
            world = new GameObject("City ambience").transform;
            lapping = Bed("river_lapping");
            wind = Bed("high_wind");

            var city = GameObject.Find("Cliff Quarter");
            quarter = city;
            if (quarter)
            {
                hasMarket = city.transform.Find("Canal market") != null;
                hasWildlands = GameObject.Find("Southern wildlands") != null;
                grotto = FindAnyObjectByType<GrottoPool>();
                if (hasMarket) market = Bed("market_murmur");
                if (hasWildlands) canopy = Bed("wild_canopy");
                if (grotto) cave = Bed("grotto_air");
                grass = Bed("field_grass");
                surf = Bed("shore_swell");

                var dock = Resources.LoadAll<AudioClip>("Audio/DockCreaks");
                AddCue("West pier timbers", new(-48, 3, 456), 10, 54, .24f, new(11, 25), dock);
                AddCue("East pier timbers", new(55, 3, 462), 10, 54, .24f, new(11, 25), dock);
                AddCue("Harbour gull", new(0, 14, 467), 38, 130, .19f, new(42, 85),
                    Resources.Load<AudioClip>("Audio/Emitters/gull_call"));

                var palace = GameObject.Find("Doors of the royal audience");
                if (palace)
                {
                    var pools = palace.transform.position + new Vector3(34, -8, 0);
                    AddEmitter("garden_pool", pools + Vector3.forward * 19, 4, 22, .27f);
                    AddEmitter("garden_pool", pools - Vector3.forward * 19, 4, 22, .27f);
                }

                var temple = GameObject.Find("Temple of the Sun");
                if (temple)
                {
                    var entry = temple.transform.position;
                    AddCue("Temple chimes", entry + new Vector3(0, 4, -14), 10, 48, .22f, new(22, 49),
                        Resources.LoadAll<AudioClip>("Audio/TempleChimes"));
                    AddCue("Sanctuary bells", entry + Vector3.up * 20, 30, 155, .18f, new(100, 190),
                        Resources.Load<AudioClip>("Audio/Bells/distant_church_01"));
                }
            }

            foreach (var renderer in FindObjectsByType<Renderer>())
            {
                if (!renderer.sharedMaterial || renderer.sharedMaterial.shader.name != "TinyWorld/Waterfall") continue;
                var bounds = renderer.bounds;
                AddEmitter("waterfall", bounds.center, 5 + bounds.size.y * .25f, 30 + bounds.size.y * .65f, .48f);
            }
            foreach (var flock in FindObjectsByType<Livestock>())
                if (flock.name.StartsWith("Sheep")) AddEmitter("sheep_bells", flock.transform.position, 5, 30, .27f);
            stream = Source("stream", Resources.Load<AudioClip>("Audio/Emitters/stream"), true, transform.position, 4, 31);
        }

        AudioSource Bed(string clip) =>
            Source(clip, Resources.Load<AudioClip>("Audio/Ambience/" + clip), true, Vector3.zero);

        void AddEmitter(string clip, Vector3 position, float near, float far, float level)
        {
            var source = Source(clip, Resources.Load<AudioClip>("Audio/Emitters/" + clip), true, position, near, far);
            emitters.Add((source, level));
        }

        void AddCue(string name, Vector3 position, float near, float far, float level, Vector2 gap, params AudioClip[] clips)
        {
            if (clips.Length == 0 || !clips[0]) return;
            cues.Add(new Cue
            {
                source = Source(name, clips[0], false, position, near, far),
                clips = clips, level = level, gap = gap,
                remaining = Random.Range(gap.x * .35f, gap.x * .7f)
            });
        }

        AudioSource Source(string name, AudioClip clip, bool loop, Vector3 position, float near = 0, float far = 0)
        {
            var node = new GameObject(name);
            node.transform.SetParent(world, false);
            node.transform.position = position;
            var source = node.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0;
            source.priority = 170;
            if (far > 0)
            {
                source.spatialBlend = 1;
                source.dopplerLevel = 0;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = near;
                source.maxDistance = far;
            }
            return source;
        }

        void OnEnable() { if (world) world.gameObject.SetActive(true); }
        void OnDisable() { if (world) world.gameObject.SetActive(false); }
        void OnDestroy() { if (world) Destroy(world.gameObject); }

        void LateUpdate()
        {
            var position = feedback.player.transform.position;
            var ear = listener ? listener.position : position;
            float master = feedback.volume;
            float shelter = DungeonInterior.Contains(position) ? 1 : grotto ? grotto.Shelter(position) : 0;
            float outside = master * (1 - .92f * shelter);
            float blend = master > 0 ? 1 - Mathf.Exp(-2.2f * Time.unscaledDeltaTime) : 1;

            float marketWeight = hasMarket ? Mathf.Max(Weight(EastMarket, position, 22), Weight(WestMarket, position, 22)) : 0;
            float altitude = Ramp(2, 26, position.y);
            float river = 1 - altitude;
            float coast = 0;
            if (quarter)
            {
                float z = position.z;
                float riverCenter = Mathf.Sin(z * .017f) * 20 + Mathf.Sin(z * .039f + .4f) * 7;
                float riverReach = Ramp(-100, -60, z) * (1 - Ramp(390, 424, z));
                river *= riverReach * (1 - Ramp(24, 53, Mathf.Abs(position.x - riverCenter)));
                river = Mathf.Max(river, Weight(Harbour, position, 24));
                coast = (1 - altitude) * Mathf.Max(Ramp(535, 610, z), 1 - Ramp(-165, -68, z));
                coast *= 1 - marketWeight * .8f;
            }

            Fade(lapping, Mathf.Max(.28f * river * (1 - .65f * marketWeight), .11f * marketWeight) * outside, blend);
            Fade(wind, (.05f + .20f * Ramp(46, 164, position.y)) * outside, blend);
            Fade(market, .28f * marketWeight * outside, blend);
            Fade(grass, .31f * Weight(Farmlands, position, 36) * outside, blend);
            Fade(canopy, .32f * Weight(Wildlands, position, 42) * Ramp(.5f, 8, position.y) * outside, blend);
            Fade(surf, .26f * coast * outside, blend);
            Fade(cave, .26f * shelter * master, blend);

            foreach (var (source, level) in emitters) FadeNear(source, ear, level * outside, blend);
            var water = NearestStream(ear);
            if (water) stream.transform.position = water.transform.position;
            FadeNear(stream, ear, water ? .38f * outside : 0, blend);
            foreach (var cue in cues) UpdateCue(cue, position, outside, shelter);
        }

        static float Ramp(float start, float end, float value) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));

        static float Weight(Bounds bounds, Vector3 point, float feather) =>
            1 - Ramp(0, feather * feather, bounds.SqrDistance(point));

        static void FadeNear(AudioSource source, Vector3 ear, float level, float blend)
        {
            float range = source.maxDistance + 4;
            Fade(source, (source.transform.position - ear).sqrMagnitude < range * range ? level : 0, blend);
        }

        static void Fade(AudioSource source, float target, float blend)
        {
            if (!source || !source.clip) return;
            source.volume = Mathf.Lerp(source.volume, target, blend);
            if (target < .001f && source.volume < .001f)
            {
                if (source.isPlaying) source.Stop();
                source.volume = 0;
            }
            else if (!source.isPlaying)
            {
                if (source.clip.loadState == AudioDataLoadState.Unloaded) source.clip.LoadAudioData();
                if (source.clip.loadState != AudioDataLoadState.Loaded) return;
                source.volume = 0;
                source.timeSamples = Random.Range(0, source.clip.samples);
                source.Play();
            }
        }

        static void UpdateCue(Cue cue, Vector3 position, float outside, float shelter)
        {
            var source = cue.source;
            if (outside <= 0)
            {
                if (source.isPlaying) source.Stop();
                return;
            }

            if (source.isPlaying) source.volume = cue.level * outside;
            float range = source.maxDistance * .78f;
            if (shelter > .5f || (source.transform.position - position).sqrMagnitude > range * range) return;
            cue.remaining -= Time.deltaTime;
            if (cue.remaining > 0 || source.isPlaying) return;
            int index = Random.Range(0, cue.clips.Length);
            if (index == cue.last && cue.clips.Length > 1) index = (index + 1) % cue.clips.Length;
            cue.last = index;
            cue.remaining = Random.Range(cue.gap.x, cue.gap.y);
            source.clip = cue.clips[index];
            source.volume = cue.level * outside;
            source.pitch = Random.Range(.95f, 1.05f);
            source.Play();
        }

        static RiverWaterVolume NearestStream(Vector3 from)
        {
            RiverWaterVolume best = null;
            float distance = 35 * 35;
            foreach (var water in RiverWaterVolume.Active)
            {
                if (water.size.x > 50) continue;
                float squared = (water.transform.position - from).sqrMagnitude;
                if (squared >= distance) continue;
                distance = squared;
                best = water;
            }
            return best;
        }
    }
}
