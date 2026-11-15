using UnityEngine;
using Unity.Cinemachine;

namespace TinyWorld
{
    public sealed class WandererFeedback : MonoBehaviour
    {
        public WandererMotor player;
        public WandererPose pose;
        public ParticleSystem dust;
        public AudioSource source;
        public CinemachineImpulseSource impulse;
        public float volume = .65f;
        AudioClip step, jump, land, climb, splash, sticky;
        float cadence;
        void Awake()
        {
            splash = Sound("Water splash", .38f, 340, .88f);
            sticky = Sound("Rubber hand", .19f, 430, .16f);
            step = Sound("Limestone footstep", .085f, 120, .65f);
            jump = Sound("Cloth lift", .16f, 280, .32f);
            land = Sound("Soft landing", .2f, 70, .7f);
            climb = Sound("Ledge grip", .14f, 170, .5f);
        }
        void Update()
        {
            if (!player.ControlsEnabled || !player.Grounded || player.Speed < 1 || player.Traversing) { cadence = .15f; return; }
            cadence -= Time.deltaTime;
            if (cadence <= 0)
            {
                cadence = player.Speed > 5 ? .25f : .38f;
                Play(step, .3f); Puff(2);
            }
        }
        AudioClip Sound(string name, float seconds, float frequency, float noise)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            uint seed = 7831;
            for (int i = 0; i < samples.Length; i++)
            {
                seed = seed * 1664525 + 1013904223;
                float n = ((seed >> 8) / 16777215f) * 2 - 1;
                float t = i / (float)rate, envelope = Mathf.Pow(1 - i / (float)samples.Length, 2) * Mathf.Min(1, i / 80f);
                samples[i] = (n * noise + Mathf.Sin(t * frequency * Mathf.PI * 2) * (1 - noise)) * envelope * .35f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        void Play(AudioClip clip, float gain)
        {
            source.pitch = Random.Range(.92f, 1.08f);
            source.PlayOneShot(clip, gain * volume);
        }
        void Puff(int count)
        {
            dust.transform.position = transform.position + Vector3.up * .08f;
            dust.Emit(count);
        }
        public void Jump() { Play(jump, .65f); Puff(6); pose.Launch(); }
        public void Splash() { Play(splash, .7f); pose.Impact(.2f); }
        public void Climb() { Play(climb, .6f); }
        public void StickyShot() { Play(sticky, .7f); }
        public void Land(float strength)
        {
            Play(land, .35f + strength * .45f); Puff(5 + Mathf.RoundToInt(strength * 8));
            pose.Impact(strength);
            if (strength > .45f) impulse.GenerateImpulseWithVelocity(Vector3.down * strength * .1f);
        }
        void OnDestroy()
        {
            Destroy(sticky); Destroy(splash); Destroy(step); Destroy(jump); Destroy(land); Destroy(climb);
        }
    }
}
