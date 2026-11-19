using UnityEngine;

namespace TinyWorld
{
    public sealed class LanternSlip : QuarterLandmark
    {
        public Transform lantern;
        public AudioClip splash;
        public override string Verb => "Float a lotus lantern";
        readonly Transform[] fleet = new Transform[6];
        readonly float[] launched = new float[6];
        int next;
        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < fleet.Length; i++)
            {
                fleet[i] = Instantiate(lantern, transform);
                fleet[i].gameObject.SetActive(false);
            }
        }
        protected override float Activate()
        {
            fleet[next].gameObject.SetActive(true);
            launched[next] = Time.time;
            next = (next + 1) % fleet.Length;
            Sound(splash, .7f);
            return 3.5f;
        }
        void Update()
        {
            FollowVolume();
            for (int i = 0; i < fleet.Length; i++)
            {
                var craft = fleet[i];
                if (!craft.gameObject.activeSelf) continue;
                float t = Time.time - launched[i];
                if (t > 19) { craft.gameObject.SetActive(false); continue; }
                float slide = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .85f));
                Vector3 p = lantern.localPosition;
                p.x += slide * .85f + Mathf.Sin(t * .32f + i) * .22f * slide;
                p.z -= t * .78f;
                p.y = Mathf.Lerp(p.y, .08f - transform.position.y + .035f * Mathf.Sin(t * 2.8f + i), slide);
                craft.localPosition = p;
                craft.localRotation = Quaternion.Euler(2 * Mathf.Sin(t * 2.4f), t * 11 + i * 43, 4 * Mathf.Sin(t * 1.7f));
                craft.localScale = lantern.localScale * (1 - Mathf.SmoothStep(0, 1, (t - 16) / 3));
            }
        }
    }
}
