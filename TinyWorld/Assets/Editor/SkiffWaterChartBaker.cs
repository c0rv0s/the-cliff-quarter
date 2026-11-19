using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static class SkiffWaterChartBaker
    {
        [MenuItem("Tiny World/Bake navigable skiff water")]
        public static void Bake()
        {
            if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="CliffQuarter")
                throw new System.InvalidOperationException("Open CliffQuarter in Edit mode before baking the skiff water.");
            var skiff=Object.FindAnyObjectByType<QuarterSkiff>(FindObjectsInactive.Include);
            var bounds=Object.FindAnyObjectByType<WandererMotor>().travelBounds;
            const string path="Assets/Resources/SkiffWaterChart.asset";
            var chart=AssetDatabase.LoadAssetAtPath<SkiffWaterChart>(path);
            if(!chart){chart=ScriptableObject.CreateInstance<SkiffWaterChart>();AssetDatabase.CreateAsset(chart,path);}
            const float step=SkiffWaterChart.Spacing;
            chart.origin=new Vector2(bounds.xMin,bounds.yMin);
            chart.width=Mathf.CeilToInt(bounds.width/step);
            chart.height=Mathf.CeilToInt(bounds.height/step);
            int n=chart.width*chart.height;
            chart.water=new byte[(n+7)/8];
            var visited=new bool[n];
            var pending=new Queue<int>();
            var pier=skiff.transform.position;
            Physics.SyncTransforms();
            int sx=Mathf.RoundToInt((pier.x-chart.origin.x)/step),sz=Mathf.RoundToInt((pier.z-chart.origin.y)/step);
            var sea=GameObject.Find("River water volume").GetComponent<RiverWaterVolume>();
            bool Afloat(Vector3 p)=>sea.Contains(p+Vector3.down*.7f) &&
                !Physics.CheckCapsule(p+Vector3.up*.22f,p+Vector3.up*.94f,.67f,1,QueryTriggerInteraction.Ignore);
            int start=-1;float nearest=float.PositiveInfinity;
            for(int x=sx-3;x<=sx+3;x++)for(int z=sz-3;z<=sz+3;z++){
                var p=chart.Point(x,z,pier.y);float d=(pier-p).sqrMagnitude;
                if(d<nearest && Afloat(p) && OpenSurface(p)){start=z*chart.width+x;nearest=d;}
            }
            if(start<0)throw new System.InvalidOperationException("No launch water at the fish pier.");
            visited[start]=true;pending.Enqueue(start);
            while(pending.Count>0){
                int i=pending.Dequeue(),x=i%chart.width,z=i/chart.width;
                var at=chart.Point(x,z,pier.y);
                if(OpenSurface(at))chart.water[i>>3]|=(byte)(1<<(i&7));
                Visit(x-1,z);Visit(x+1,z);Visit(x,z-1);Visit(x,z+1);
                void Visit(int nx,int nz){
                    if(nx<0||nz<0||nx>=chart.width||nz>=chart.height)return;
                    int next=nz*chart.width+nx;
                    if(visited[next])return;
                    var p=chart.Point(nx,nz,pier.y);
                    if(!Afloat(p)){visited[next]=true;return;}
                    if(Physics.CapsuleCast(at+Vector3.up*.22f,at+Vector3.up*.94f,.67f,
                        (p-at)/step,step,1,QueryTriggerInteraction.Ignore))return;
                    visited[next]=true;pending.Enqueue(next);
                }
            }
            EditorUtility.SetDirty(chart);AssetDatabase.SaveAssets();
        }
        // Flood through bridges, but reserve open berths where a player can see the boat from shore.
        static bool OpenSurface(Vector3 p)=>!Physics.Raycast(p+Vector3.up*400,Vector3.down,out var hit,425,1,
            QueryTriggerInteraction.Ignore) || hit.point.y<p.y-.48f;
    }
}
