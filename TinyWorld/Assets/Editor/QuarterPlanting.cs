using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static class QuarterPlanting
    {
        public static bool TryGround(Vector3 seed, out Vector3 foot)
        {
            // Use the baked hillside. Its coarse triangles can differ from the height formula at cliff edges.
            for(int ring=0;ring<=4;ring++) for(int slot=0;slot<(ring==0?1:12);slot++)
            {
                var p=seed+Quaternion.Euler(0,slot*30,0)*Vector3.forward*(ring*2.25f);
                float best=float.MaxValue;foot=default;
                foreach(var hit in Physics.RaycastAll(p+Vector3.up*4,Vector3.down,11,1,QueryTriggerInteraction.Ignore))
                {
                    if(hit.normal.y<.52f || hit.distance>=best)continue;
                    string name=hit.collider.name;
                    if(!(name.StartsWith("Cliff rock")||name.StartsWith("Paving")||name.StartsWith("Grass")||name.StartsWith("Carved limestone")))continue;
                    best=hit.distance;foot=hit.point-Vector3.up*.045f;
                }
                if(best<float.MaxValue)return true;
            }
            foot=default;return false;
        }
    }
}
