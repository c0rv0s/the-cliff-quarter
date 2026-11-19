using UnityEngine;

namespace TinyWorld
{
    public sealed class SwallowBell : QuarterLandmark
    {
        public Transform bell;
        public Transform[] swallows;
        public AudioClip peal;
        public override string Verb => "Ring the swallow bell";
        Transform[] left, right;
        Vector3[] perch;
        float start = -30;
        protected override void Awake()
        {
            base.Awake();
            perch = new Vector3[swallows.Length];
            left = new Transform[swallows.Length]; right = new Transform[swallows.Length];
            for (int i = 0; i < swallows.Length; i++)
            {
                perch[i] = swallows[i].localPosition;
                left[i] = swallows[i].Find("Left wing"); right[i] = swallows[i].Find("Right wing");
            }
        }
        protected override float Activate()
        {
            start = Time.time;
            Sound(peal, .9f);
            return 10;
        }
        void Update()
        {
            FollowVolume();
            float age = Time.time - start;
            if (age > 11) return;
            bell.localRotation = Quaternion.Euler(Mathf.Sin(age * 6.4f) * Mathf.Exp(-.7f * age) * 37, 0, 0);
            for (int i = 0; i < swallows.Length; i++)
            {
                float t = Mathf.Clamp01((age - .12f - i * .11f) / (7.7f + i * .17f));
                float arc = Mathf.Sin(t * Mathf.PI);
                float a = t * Mathf.PI * 2;
                Vector3 flight = new Vector3(12 * (1 - Mathf.Cos(a)), arc * (8 + i * .7f), Mathf.Sin(a) * (7 + i * .75f));
                var bird = swallows[i];
                bird.localPosition = perch[i] + flight;
                float airborne = Mathf.SmoothStep(0, 1, Mathf.Clamp01(arc * 8));
                var heading = new Vector3(12 * Mathf.Sin(a), .5f * Mathf.Cos(a * .5f) * (8 + i * .7f), Mathf.Cos(a) * (7 + i * .75f));
                bird.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.LookRotation(heading), airborne);
                float wings = Mathf.Lerp(67, Mathf.Sin(age * 15 + i) * 36, airborne);
                left[i].localRotation = Quaternion.Euler(0, 0, wings);
                right[i].localRotation = Quaternion.Euler(0, 0, -wings);
            }
        }
    }
}
