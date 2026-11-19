using UnityEngine;

namespace TinyWorld
{
    public sealed class QuayChimes : QuarterLandmark
    {
        public Transform[] tubes;
        public AudioClip note;
        public override string Verb => "Brush the wind chimes";
        readonly float[] notes = { 1, 1.12246f, 1.25992f, 1.49831f, 1.68179f };
        AudioSource[] voices;
        float started = -20;
        int struck;
        bool reverse;
        WandererFeedback gain;
        protected override void Awake()
        {
            base.Awake();
            gain = FindAnyObjectByType<WandererFeedback>();
            voices = new AudioSource[tubes.Length];
            for (int i = 0; i < tubes.Length; i++)
            {
                var s = tubes[i].gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 1; s.minDistance = 2; s.maxDistance = 25;
                s.clip = note; s.pitch = notes[i]; voices[i] = s;
            }
        }
        protected override float Activate()
        {
            started = Time.time; struck = 0; reverse = !reverse;
            return 1.15f;
        }
        void Update()
        {
            float age = Time.time - started;
            while (struck < tubes.Length && age < 5 && age >= struck * .105f)
            {
                int index = reverse ? struck : tubes.Length - 1 - struck;
                voices[index].volume = .48f * (gain ? gain.volume : .65f);
                voices[index].Play(); struck++;
            }
            for (int i = 0; i < tubes.Length; i++)
            {
                float t = age - .105f * (reverse ? i : tubes.Length - 1 - i);
                float swing = t >= 0 && t < 5 ? Mathf.Sin(t * (9 + i * .5f)) * Mathf.Exp(-1.35f * t) * 28 : 0;
                tubes[i].localRotation = Quaternion.Euler(0, 0, swing + Mathf.Sin(Time.time * 1.65f + i) * 3);
                if (voices[i].isPlaying && gain) voices[i].volume = .48f * gain.volume;
            }
        }
    }
}
