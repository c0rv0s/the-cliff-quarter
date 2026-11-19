using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    public abstract class QuarterLandmark : MonoBehaviour
    {
        static readonly List<QuarterLandmark> sites = new();
        public Vector3 handle = Vector3.up;
        public float reach = 2.8f;
        public abstract string Verb { get; }
        protected AudioSource voice;
        WandererFeedback feedback;
        float readyAt;

        protected virtual void Awake()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1;
            voice.minDistance = 3;
            voice.maxDistance = 45;
            voice.dopplerLevel = 0;
            feedback = FindAnyObjectByType<WandererFeedback>();
        }
        protected virtual void OnEnable() => sites.Add(this);
        protected virtual void OnDisable() => sites.Remove(this);

        public static QuarterLandmark WithinReach(WandererMotor player)
        {
            if (player.Skiff) return player.Skiff;
            Vector3 eye = player.transform.position + Vector3.up * 1.25f;
            QuarterLandmark nearest = null;
            float best = float.PositiveInfinity;
            foreach (var site in sites)
            {
                var target = site.transform.TransformPoint(site.handle);
                float d = (target - eye).sqrMagnitude;
                if (d > site.reach * site.reach || d >= best) continue;
                // Stop just short of the handle so its own backing can be solid.
                if (Physics.Linecast(eye, Vector3.MoveTowards(target, eye, .22f), 1, QueryTriggerInteraction.Ignore)) continue;
                nearest = site;
                best = d;
            }
            return nearest;
        }

        public void Use()
        {
            if (Time.time < readyAt) return;
            readyAt = Time.time + Activate();
        }

        protected abstract float Activate();
        protected void Sound(AudioClip clip, float gain, float pitch = 1)
        {
            voice.pitch = pitch;
            voice.volume = feedback ? feedback.volume : .65f;
            if (clip) voice.PlayOneShot(clip, gain);
        }
        protected void FollowVolume()
        {
            if (feedback && voice.isPlaying) voice.volume = feedback.volume;
        }
    }
}
