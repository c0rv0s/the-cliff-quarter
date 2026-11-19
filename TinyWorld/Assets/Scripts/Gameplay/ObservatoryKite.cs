using UnityEngine;

namespace TinyWorld
{
    public sealed class ObservatoryKite : QuarterLandmark
    {
        public Transform kite, spool, lineEye;
        public LineRenderer thread;
        public AudioClip canvas;
        public override string Verb => aloft ? "Reel in the sun kite" : "Fly the sun kite";
        bool aloft;
        float lift;
        Vector3 perch;
        protected override void Awake() { base.Awake(); perch = kite.localPosition; }
        protected override float Activate() { aloft = !aloft; Sound(canvas, .75f); return .7f; }
        void Update()
        {
            FollowVolume();
            float last = lift;
            lift = Mathf.MoveTowards(lift, aloft ? 1 : 0, Time.deltaTime * (aloft ? .16f : .25f));
            float travel = Mathf.SmoothStep(0, 1, lift);
            float t = Time.time;
            kite.localPosition = perch + travel * new Vector3(12 + 2 * Mathf.Sin(t * .6f), 13 + Mathf.Cos(t * .9f), 8 + 2.6f * Mathf.Sin(t * .4f));
            kite.localRotation = Quaternion.Euler(-18 * travel, 12 * Mathf.Sin(t * .7f) * travel, 17 * Mathf.Cos(t * .8f) * travel);
            spool.Rotate(Vector3.right, (lift - last) * 1500, Space.Self);
            Vector3 a = lineEye.position, b = kite.position;
            for (int i = 0; i < 9; i++)
            {
                float u = i / 8f;
                thread.SetPosition(i, Vector3.Lerp(a, b, u) + Vector3.down * (Mathf.Sin(u * Mathf.PI) * (.08f + .85f * travel)));
            }
        }
    }
}
