using UnityEngine;

namespace TinyWorld
{
    public sealed class IslandSignal : QuarterLandmark
    {
        public Transform flags;
        bool raised;
        public override string Verb => raised ? "Lower the signal flags" : "Hoist the signal flags";
        protected override float Activate() { raised = !raised; return .35f; }
        void Update()
        {
            var up = Vector3.up * (raised ? 3.25f : .78f);
            flags.localPosition = Vector3.MoveTowards(flags.localPosition, up, Time.deltaTime * 1.9f);
        }
    }
}
