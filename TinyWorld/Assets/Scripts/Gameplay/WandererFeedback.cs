using UnityEngine;
using Unity.Cinemachine;

namespace TinyWorld
{
    // Player, pickup and menu sound. Clips load from Resources/Audio/<folder>; sources are credited in Setup/AudioCredits.md.
    [DefaultExecutionOrder(-10)]
    public sealed class WandererFeedback : MonoBehaviour
    {
        public WandererMotor player;
        public WandererPose pose;
        public ParticleSystem dust;
        public AudioSource source;
        public CinemachineImpulseSource impulse;
        public float volume = .65f;
        // Major pentatonic steps: successive sunstones climb the scale.
        static readonly int[] Scale = { 0, 2, 4, 7, 9, 12, 14 };
        Bank step, jump, mantle, land, landHeavy, roll, grab, dash, whoosh, splash, surface, swim, stickyShot, stickyAttach, stickyRelease;
        Bank ring, vine, respawn, sunstone, milestone, glideOpen, glideFold, click, hover;
        AudioSource[] voices;
        static readonly int Wake = Shader.PropertyToID("_WandererWake");
        static readonly int Ripple = Shader.PropertyToID("_WandererRipple");
        static readonly int Visitor = Shader.PropertyToID("_WandererVisitor");
        float wake;
        int nextVoice;
        float cadence;

        sealed class Bank
        {
            readonly AudioClip[] clips; int last = -1;
            public Bank(string folder) => clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            public AudioClip Next()
            {
                int i = Random.Range(0, clips.Length);
                if (i == last && clips.Length > 1) i = (i + 1) % clips.Length;
                return clips[last = i];
            }
        }

        void Awake()
        {
            step = new("Footsteps"); jump = new("Jump"); mantle = new("Mantle"); land = new("Land"); landHeavy = new("LandHeavy");
            roll = new("Roll"); grab = new("Grab"); dash = new("Dash"); whoosh = new("Whoosh"); splash = new("Splash");
            surface = new("SplashSmall"); swim = new("Swim"); stickyShot = new("StickyShot"); stickyAttach = new("StickyAttach");
            stickyRelease = new("StickyRelease"); ring = new("Ring"); vine = new("Vine"); respawn = new("Respawn");
            sunstone = new("Sunstone"); milestone = new("Milestone"); glideOpen = new("GlideOpen"); glideFold = new("GlideFold");
            click = new("UI"); hover = new("UIHover");
            // Each voice keeps its own pitch, so overlapping sounds never bend each other's tails.
            voices = new AudioSource[10]; voices[0] = source;
            for (int i = 1; i < voices.Length; i++) voices[i] = gameObject.AddComponent<AudioSource>();
            foreach (var v in voices) { v.playOnAwake = false; v.spatialBlend = 0; v.dopplerLevel = 0; }
            gameObject.AddComponent<CityAmbience>().feedback = this;
        }
        void OnEnable() => Sunstone.Collected += Collected;
        void OnDisable()
        {
            Sunstone.Collected -= Collected;
            Shader.SetGlobalVector(Wake, Vector4.zero);
            Shader.SetGlobalVector(Ripple, new Vector4(0,0,-100,0));
            Shader.SetGlobalVector(Visitor, Vector4.zero);
        }
        void LateUpdate()
        {
            var p = player.transform.position;
            Shader.SetGlobalVector(Visitor, new Vector4(p.x,p.y+.9f,p.z,1));
            wake = Mathf.MoveTowards(wake, player.Swimming ? Mathf.Clamp01(player.Speed/5f) : 0, 2.5f*Time.deltaTime);
            var v = player.Velocity;
            float angle = Mathf.Atan2(v.x, v.z);
            Shader.SetGlobalVector(Wake, new Vector4(p.x,p.z,angle,wake));
        }
        void Update()
        {
            if (!player.ControlsEnabled) { cadence = .15f; return; }
            bool walking = player.Grounded && player.Speed > 1 && !player.Traversing;
            bool swimming = player.Swimming && player.Speed > 1;
            if (!walking && !player.WallRunning && !swimming) { cadence = .15f; return; }
            cadence -= Time.deltaTime;
            if (cadence > 0) return;
            if (swimming) { cadence = player.Speed > 6 ? .55f : .8f; Play(swim, .3f, .9f, 1.1f); return; }
            cadence = player.WallRunning ? .2f : player.Speed > 5 ? .27f : .4f;
            Play(step, player.Crouched ? .14f : player.Speed > 5 ? .34f : .26f, .9f, 1.08f);
            if (walking) Puff(2);
        }
        void Play(Bank bank, float gain, float low = .94f, float high = 1.06f) => Voice(bank.Next(), gain, Random.Range(low, high));
        void Voice(AudioClip clip, float gain, float pitch)
        {
            var v = voices[nextVoice]; nextVoice = (nextVoice + 1) % voices.Length;
            v.clip = clip; v.pitch = pitch; v.volume = gain * volume; v.Play();
        }
        void Puff(int count)
        {
            dust.transform.position = transform.position + Vector3.up * .08f;
            dust.Emit(count);
        }
        void Collected(Sunstone stone)
        {
            int n = Sunstone.CollectedCount;
            Voice(sunstone.Next(), .5f, Mathf.Pow(2, Scale[(n - 1) % Scale.Length] / 12f));
            if (n == Sunstone.All.Count || n % 10 == 0) Voice(milestone.Next(), n == Sunstone.All.Count ? .7f : .45f, 1);
        }

        public void QuestComplete() => Voice(milestone.Next(), .55f, 1);
        public void Jump() { Play(jump, .42f); Puff(6); pose.Launch(); }
        public void Splash(float strength = 1)
        {
            var p = player.transform.position;
            Shader.SetGlobalVector(Ripple, new Vector4(p.x,p.z,Time.time,strength));
            Play(strength > .6f ? splash : surface, .3f + .4f * strength); pose.Impact(.2f * strength);
        }
        public void CallSkiff(Vector3 at)
        {
            Shader.SetGlobalVector(Ripple, new Vector4(at.x,at.z,Time.time,.8f));
            Play(ring,.36f,1.1f,1.1f);
        }
        public void Grab() { Play(grab, .32f, .88f, 1.04f); }
        public void Mantle() { Play(mantle, .5f); Play(grab, .14f, .85f, .95f); }
        public void WallRun() { Play(grab, .2f, 1, 1.1f); Play(jump, .2f); }
        public void Dash() { Play(dash, .45f); Play(jump, .2f); Puff(4); }
        public void Whoosh() { Play(whoosh, .38f); }
        public void Ring() { Play(ring, .42f); Play(grab, .16f, 1.05f, 1.15f); }
        public void Vine() { Play(vine, .45f); }
        public void Respawn() { Play(respawn, .4f, 1, 1); Puff(8); }
        public void Roll() { Play(roll, .5f); Play(land, .22f); Puff(8); pose.Impact(.22f); }
        public void GlideOpen() { Play(glideOpen, .5f); pose.Launch(); }
        public void GlideFold() { Play(glideFold, .35f); }
        public void StickyShot() { Play(stickyShot, .4f); Play(whoosh, .12f, 1.3f, 1.4f); }
        public void StickyAttach() { Play(stickyAttach, .45f); }
        public void StickyRelease() { Play(stickyRelease, .25f); }
        public void Click() => Play(click, .3f, .97f, 1.03f);
        public void Hover() => Play(hover, .14f, .97f, 1.03f);
        public void Land(float strength)
        {
            Play(strength > .5f ? landHeavy : land, .22f + strength * .4f); Puff(5 + Mathf.RoundToInt(strength * 8));
            pose.Impact(strength);
            if (strength > .45f) impulse.GenerateImpulseWithVelocity(Vector3.down * strength * .1f);
        }
    }
}
