using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    public sealed class DungeonInterior : MonoBehaviour
    {
        public Bounds bounds;
        public Vector3[] torches = System.Array.Empty<Vector3>();
        static DungeonInterior current;
        static readonly int VolumeMin = Shader.PropertyToID("_DungeonMin");
        static readonly int VolumeMax = Shader.PropertyToID("_DungeonMax");
        static readonly int Torches = Shader.PropertyToID("_DungeonTorches");
        static readonly int TorchCount = Shader.PropertyToID("_DungeonTorchCount");
        static readonly List<DungeonInterior> halls = new();
        static readonly int Lantern = Shader.PropertyToID("_PyramidLantern");
        WandererMotor player;

        void OnEnable() => halls.Add(this);
        void OnDisable() {
            halls.Remove(this);
            if(current==this)Leave();
        }
        static void Leave() {
            current=null;
            Shader.SetGlobalVector(VolumeMin,Vector4.one);
            Shader.SetGlobalVector(VolumeMax,Vector4.zero);
            Shader.SetGlobalInteger(TorchCount,0);
        }
        void Enter() {
            current=this;
            var box=new Bounds(transform.TransformPoint(bounds.center),Vector3.zero);
            for(int i=0;i<8;i++)
                box.Encapsulate(transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            Shader.SetGlobalVector(VolumeMin,box.min);
            Shader.SetGlobalVector(VolumeMax,box.max);
            var lights=new Vector4[8];
            int count=Mathf.Min(lights.Length,torches.Length);
            for(int i=0;i<count;i++)lights[i]=transform.TransformPoint(torches[i]);
            Shader.SetGlobalVectorArray(Torches,lights);
            Shader.SetGlobalInteger(TorchCount,count);
        }
        void Start() => player = FindAnyObjectByType<WandererMotor>();

        public static bool Contains(Vector3 position)
        {

            foreach (var hall in halls)
                if (hall.bounds.Contains(hall.transform.InverseTransformPoint(position))) return true;
            return false;
        }

        void LateUpdate()
        {
            if (!player || !bounds.Contains(transform.InverseTransformPoint(player.transform.position))) {
                if(current==this)Leave();
                return;
            }
            if(current!=this)Enter();
            var lamp = QuarterQuests.Current?.lantern;
            if (lamp && lamp.activeInHierarchy) {
                var p = lamp.transform.position;
                Shader.SetGlobalVector(Lantern, new Vector4(p.x,p.y,p.z,22));
            } else Shader.SetGlobalVector(Lantern, Vector4.zero);
        }
    }
}
