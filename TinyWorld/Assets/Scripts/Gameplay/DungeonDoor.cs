using UnityEngine;

namespace TinyWorld
{
    public sealed class DungeonDoor : QuarterLandmark
    {
        public string passage;
        public Transform arrival;
        public override string Verb => passage;
        protected override float Activate()
        {
            var player = FindAnyObjectByType<WandererMotor>();
            if (!player || !arrival) return 0;
            player.Teleport(arrival.position);
            player.SetCheckpoint(arrival.position);
            player.visual.rotation = arrival.rotation;
            FindAnyObjectByType<WandererCamera>()?.SetOrbit(arrival.eulerAngles.y, 8);
            if (player.feedback) player.feedback.Grab();
            return .55f;

        }
    }
}
