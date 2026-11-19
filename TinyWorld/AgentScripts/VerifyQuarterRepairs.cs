using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TinyWorld;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifyQuarterRepairs
{
    public static object District()
    {
        var city=GameObject.Find("Cliff Quarter").transform;
        var harbour=city.Find("Harbour").GetComponentsInChildren<MeshFilter>();
        var strays=new List<string>();
        foreach(var f in harbour){var m=f.sharedMesh;var v=m.vertices;if(m.triangles.Any(i=>f.transform.TransformPoint(v[i]).z<100))strays.Add(f.name);}
        var steps=city.Find("Ship companionways/Paving · solid").GetComponent<MeshFilter>().sharedMesh;
        var stairSections=steps.vertices.Select(p=>Mathf.Round(p.z/12)).Distinct().Count();
        var farmland=city.Find("Farmlands");
        var upland=city.Find("Eastern uplands").GetComponentsInChildren<MeshCollider>().Where(c=>c.gameObject.layer==0).ToArray();
        var missing=new List<string>();int samples=0;
        // Sample the actual crop meshes. Includes the tips of every row at the plateau boundary.
        foreach(var f in farmland.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Foliage · detail"||f.name=="Blossom · detail"||f.name=="Foliage · solid"||f.name=="Blossom · solid"))
        {
            var m=f.sharedMesh;var v=m.vertices;
            var unique=new HashSet<Vector2Int>();
            foreach(var p in v){
                if(!unique.Add(Vector2Int.RoundToInt(new Vector2(p.x,p.z)*2)))continue;
                samples++;
                bool supported=upland.Any(c=>c.Raycast(new Ray(p+Vector3.up*2,Vector3.down),out var hit,10)&&hit.normal.y>.48f);
                if(!supported&&missing.Count<30)missing.Add(p.ToString("F1"));
            }
        }
        return new {strays,stairSections,herds=farmland.GetComponentsInChildren<Livestock>().Length,samples,missing};
    }

    public static async Task<object> Residents()
    {
        var all=Object.FindObjectsByType<QuarterResident>();var p=Object.FindAnyObjectByType<WandererMotor>();
        var start=p.transform.position;var checks=new List<object>();var shell=Object.FindAnyObjectByType<WandererShell>();
        try{
            shell.Begin();Time.timeScale=3;
            foreach(var group in all.GroupBy(n=>new Vector2Int(Mathf.FloorToInt(n.transform.position.x/100),Mathf.FloorToInt(n.transform.position.z/90)))){
                var people=group.ToArray();var homes=people.Select(n=>n.transform.position).ToArray();
                p.Teleport(homes[0]+Vector3.right*4+Vector3.up*.1f);
                float until=Time.time+3;
                while(Time.time<until){await Task.Yield();if(shell.Paused){shell.SetPaused(false);Time.timeScale=3;}}
                for(int i=0;i<people.Length;i++){
                    var n=people[i];float moved=Vector3.ProjectOnPlane(n.transform.position-homes[i],Vector3.up).magnitude;
                    bool floor=Physics.Raycast(n.transform.position+Vector3.up*.17f,Vector3.down,.36f,1,QueryTriggerInteraction.Ignore);
                    checks.Add(new {n.firstName,moved,floor,talk=n.lines.Length>0&&n.GetComponent<CharacterController>().enabled});
                }
            }
        }finally{Time.timeScale=1;p.Teleport(start);}
        return checks;
    }
}
