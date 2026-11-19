using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TinyWorld;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifyWorldExpansion
{
    static Transform Door(string name)=>Object.FindObjectsByType<QuarterSwingDoor>().Single(d=>d.name==name).transform;
    static readonly Vector3 Up=Vector3.up;

    public static object Survey()
    {
        Physics.SyncTransforms();
        var city=GameObject.Find("Cliff Quarter").transform;
        var island=GameObject.Find("Southern wildlands").transform;
        var land=island.Find("Hooked bay and outer keys").GetComponent<MeshCollider>();
        int wild=0,main=0;
        for(int x=-550;x<=570;x+=6){
            for(int z=-1015;z< -200;z+=6){
                var probe=new Ray(island.TransformPoint(new Vector3(x,305,z)),Vector3.down);
                if(land.Raycast(probe,out _,305))wild++;
            }
            for(int z=-199;z<565;z+=6)
                if(Physics.Raycast(new Vector3(x,305,z),Vector3.down,304.95f,1,QueryTriggerInteraction.Ignore))main++;
        }
        var castle=Door("Doors of the royal audience");var temple=Door("Temple of the Sun");
        var highest=city.GetComponentsInChildren<MeshRenderer>().OrderByDescending(r=>r.bounds.max.y).First();
        var houses=Object.FindObjectsByType<QuarterResident>();
        int crops=city.Find("Farmlands").GetComponentsInChildren<MeshFilter>()
            .Where(f=>f.name.StartsWith("Foliage")||f.name.StartsWith("Blossom"))
            .Sum(f=>f.sharedMesh.vertices.Count(v=>v.x>300 && v.y>75));
        float QuayArea(string name,float offset){
            var mesh=city.Find(name+"/Paving · solid").GetComponent<MeshFilter>().sharedMesh;
            var points=mesh.vertices;var indices=mesh.triangles;float area=0;
            for(int i=0;i<indices.Length;i+=3){
                var a=points[indices[i]];var b=points[indices[i+1]];var c=points[indices[i+2]];
                int side=a.x<0?-1:1;
                float quay=3.97f+Mathf.Sin(7.2f+side*.7f)*1.15f+Mathf.Sin(17.2f+side)*.6f+offset;
                if(Mathf.Abs(a.y-quay)<.003f && Mathf.Abs(b.y-quay)<.003f && Mathf.Abs(c.y-quay)<.003f)
                    area+=Mathf.Abs(Vector3.Cross(b-a,c-a).y)*.5f;
            }
            return Mathf.RoundToInt(area);
        }
        float oldQuay=QuayArea("Harbour",0),newQuay=QuayArea("Canal market",.09f);
        return new {originalQuaySquareMetres=oldQuay,expandedQuaySquareMetres=oldQuay+newQuay,mainLandSquareMetres=main*36,wildLandSquareMetres=wild*36,canopyCats=island.GetComponentsInChildren<WildCatIdle>().Length,
            wildHerds=island.GetComponentsInChildren<Livestock>().Select(h=>new {h.name,h.count}).ToArray(),
            palace=castle.position.ToString(),temple=temple.position.ToString(),highest=highest.transform.parent.name,pinnacle=highest.bounds.max.y,
            chartHouse=Door("Tidekeeper's chart room").position.ToString(),outerCropVertices=crops,
            royalHousehold=houses.Where(n=>Vector3.Distance(n.transform.position,castle.position)<45).Select(n=>n.firstName).ToArray()};
    }

    public static async Task<object> Routes()
    {
        var player=Object.FindAnyObjectByType<WandererMotor>();
        var camera=Object.FindAnyObjectByType<WandererCamera>();
        var shell=Object.FindAnyObjectByType<WandererShell>();
        shell.Begin();var spawn=player.transform.position;var scale=Time.timeScale;
        var findings=new List<object>();var failures=new List<string>();
        void Check(string name,bool ok,string evidence){findings.Add(new {name,ok,evidence});if(!ok)failures.Add(name);}
        async Task Frame(){
            int before=Time.frameCount;
            while(before==Time.frameCount){if(shell.Paused)shell.SetPaused(false);await Task.Yield();}
        }
        async Task Drive(string name,Vector3 start,params Vector3[] route){
            player.Teleport(start);player.TestMove=Vector2.zero;await Frame();
            foreach(var target in route){
                float best=float.PositiveInfinity,stall=Time.realtimeSinceStartup+3;
                while(Time.realtimeSinceStartup<stall){
                    var d=target-player.transform.position;float distance=new Vector2(d.x,d.z).magnitude;
                    if(distance<.55f)break;
                    if(distance<best-.3f){best=distance;stall=Time.realtimeSinceStartup+3;}
                    camera.SetOrbit(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,8);
                    player.TestMove=Vector2.up;await Frame();
                }
                var delta=target-player.transform.position;
                if(new Vector2(delta.x,delta.z).magnitude>1 || Mathf.Abs(delta.y)>1.5f){
                    player.TestMove=Vector2.zero;Check(name,false,player.transform.position+" heading for "+target+" "+player.Action);return;
                }
            }
            player.TestMove=Vector2.zero;Check(name,true,player.transform.position.ToString());
        }
        player.TestInput=true;player.TestSprint=true;Time.timeScale=2;
        try{
            var door=Door("Doors of the royal audience");
            Vector3 Palace(float x,float y,float z)=>door.TransformPoint(new Vector3(x,y-8.55f,z-3));
            await Drive("Hillside stair into the royal courtyard",new Vector3(-128,132.07f,61),
                new(-143,138,58),new(-165,146,57),new(-185,152,58),new(-193,155,64),new(-180,159.5f,70),
                new(-158,165,70),new(-145,168,70),new(-132,168,72),Palace(-33,.16f,-40),Palace(-27,.16f,-40),
                Palace(-27,.16f,-46.5f),Palace(0,.2f,-46.5f));
            await Drive("Courtyard, stair and royal doors",Palace(0,.27f,-54.5f),Palace(0,.2f,-21),
                Palace(0,8.55f,1),Palace(0,8.55f,21),Palace(0,10.21f,37.5f));
            var king=GameObject.Find("King Aurelio").GetComponent<QuarterResident>();
            Check("Audience with the king",QuarterLandmark.WithinReach(player)==king,king.transform.position.ToString());
            if(QuarterLandmark.WithinReach(player)==king)king.Use();
            await Drive("Throne to lookout on the royal axis",Palace(0,10.23f,37.5f),Palace(0,8.55f,20),
                Palace(0,8.55f,0),Palace(0,.16f,-20),Palace(0,.16f,-53),Palace(0,.16f,-113.5f));
            await Drive("Courtyard through both royal garden walks",Palace(-18,.24f,-15),Palace(-40.9f,.16f,-15),
                Palace(-40.9f,.16f,55),Palace(0,.16f,55),Palace(4,.16f,55),Palace(4,.16f,80),Palace(42,.16f,80),
                Palace(42,.16f,-15),Palace(19,.16f,-15));
            var temple=Door("Temple of the Sun");
            await Drive("Relocated temple and bell hall",temple.position+new Vector3(0,-9.4f,-36),
                temple.position+new Vector3(0,-3.56f,-24.6f),temple.position+new Vector3(0,0,-12),temple.position+new Vector3(0,0,8));
            var y=GameObject.Find("Tess").transform.position.y;
            await Drive("Market footbridges",new Vector3(194,y+.2f,484),new Vector3(194,y,486),new Vector3(192,y,488),
                new Vector3(193,y,512),new Vector3(193,y,528),new Vector3(223,y+1,528),new Vector3(229.6f,y,528));
            await Drive("Outer farm lane",new Vector3(283,115.26f,126),new Vector3(281,113.59f,138),
                new Vector3(281,112.64f,144),new Vector3(283,111.34f,150));
            var book=QuarterQuests.Current;var skiff=book.skiff;
            var saved=skiff.transform.position;var rotation=skiff.transform.rotation;bool active=skiff.gameObject.activeSelf;
            try{
                skiff.gameObject.SetActive(true);
                skiff.transform.SetPositionAndRotation(new Vector3(300,.02f,-290),Quaternion.identity);
                float Row(bool fast,int ticks){
                    float start=skiff.transform.position.z;
                    for(int j=0;j<ticks;j++)skiff.Sail(Vector2.up,fast,.04f);
                    return skiff.transform.position.z-start;
                }
                Row(false,75);float cruise=Row(false,25);
                Row(true,50);float sprint=Row(true,25);
                Row(false,50);float released=Row(false,25);
                Check("Fast stroke and return to cruising speed",Mathf.Abs(cruise-8)<.1f && Mathf.Abs(sprint-16)<.1f &&
                    Mathf.Abs(released-cruise)<.1f,$"{cruise:F1} / {sprint:F1} / {released:F1} m/s");
                foreach(float z in new[]{510f,547f}){
                    skiff.transform.SetPositionAndRotation(new Vector3(262,.02f,z),Quaternion.Euler(0,-90,0));
                    for(int j=0;j<540;j++)skiff.Sail(Vector2.up,true,.04f);
                    Check("Row canal into ship basin at "+z,skiff.transform.position.x<48,skiff.transform.position.ToString());
                }
                skiff.transform.SetPositionAndRotation(new Vector3(5,.02f,587),Quaternion.identity);
                for(int j=0;j<420;j++)skiff.Sail(Vector2.up,true,.04f);
                Check("Clear shipping lane past lighthouse island",skiff.transform.position.z>790,skiff.transform.position.ToString());
            }finally{skiff.transform.SetPositionAndRotation(saved,rotation);skiff.gameObject.SetActive(active);}
            var wild=GameObject.Find("Southern wildlands").transform;
            var shore=wild.Find("Hooked bay and outer keys").GetComponent<MeshCollider>();
            Vector3 Coast(float x,float z){shore.Raycast(new Ray(wild.TransformPoint(new Vector3(x,70,z)),Vector3.down),out var hit,100);return hit.point;}
            int recoveries=player.Respawns;
            await Drive("Swim the hooked bay and walk ashore",wild.TransformPoint(new Vector3(125.8f,-.8f,-573.2f)),Coast(123.5f,-600.8f),Coast(116.5f,-617));
            var outerSea=wild.TransformPoint(new Vector3(0,-1,-1060));
            Check("Water and skiff room beyond the southern coast",RiverWaterVolume.At(outerSea) &&
                player.travelBounds.Contains(new Vector2(outerSea.x,outerSea.z)),outerSea.ToString());
            var ruins=wild.Find("Court of the broken sun").position;
            await Drive("Under the ruined arch",ruins+new Vector3(16,.22f,2.4f),ruins+new Vector3(5,0,2.4f));
            foreach(var site in new[]{(name:"Rising Sun",x:14f,z:-731f,front:1,half:38f,rise:36f),
                                    (name:"Jaguar",x:-228f,z:-565f,front:-1,half:31f,rise:29.05f)}){
                float toe=site.z+site.front*(site.half+22);
                var top=Coast(site.x,site.z)+Vector3.up*(1.1f+site.rise);
                await Drive("Climb the Temple of the "+site.name,Coast(site.x,toe+site.front*3)+Vector3.up*.1f,
                    Coast(site.x,toe+site.front*.2f),top+Vector3.forward*(site.front*8),top);
            }
            foreach(var dungeon in Object.FindObjectsByType<DungeonInterior>().Where(d=>d.transform.parent.name=="City of the Two Suns")){
                bool jaguar=dungeon.name.Contains("Jaguar");
                var gate=Object.FindObjectsByType<DungeonDoor>().Single(d=>d.passage=="Enter "+dungeon.name);
                var inside=dungeon.GetComponentInChildren<DungeonDoor>();
                player.Teleport(inside.arrival.position);await Frame();
                Check("Ground entrance: "+dungeon.name,QuarterLandmark.WithinReach(player)==gate,player.transform.position.ToString());
                if(QuarterLandmark.WithinReach(player)==gate)gate.Use();await Frame();
                Check("Enter "+dungeon.name,DungeonInterior.Contains(player.transform.position)&&!RiverWaterVolume.At(player.transform.position),player.transform.position.ToString());
                Vector3 At(int x,int z){
                    var pos=dungeon.transform.TransformPoint(new Vector3(x*5.5f,0,z*5.5f));
                    Physics.Raycast(pos+Vector3.up*.8f,Vector3.down,out var hit,25,1,QueryTriggerInteraction.Ignore);
                    return hit.collider?hit.point:pos;
                }
                var path=jaguar?new[]{(1,1),(1,5),(4,5),(4,8),(-4,8),(-4,15),(-2,15),(-2,20),(-4,20),(-4,27),(-7,27)}
                    :new[]{(0,9),(-5,9),(-5,18),(-1,18),(-1,22),(0,22),(0,26)};
                await Drive("Lower hall and burial chamber: "+dungeon.name,player.transform.position,
                    path.Select(k=>At(k.Item1,k.Item2)).ToArray());
                var back=jaguar?new[]{(-4,27),(-4,20),(-1,20),(2,20),(2,14),(4,14),(4,5),(1,5),(1,1),(0,1),(0,0)}
                    :new[]{(0,22),(-1,22),(-1,18),(0,17),(0,9),(5,9),(0,9),(0,0)};
                await Drive("Return circuit: "+dungeon.name,player.transform.position,back.Select(k=>At(k.Item1,k.Item2)).ToArray());
                Check("Find the exit: "+dungeon.name,QuarterLandmark.WithinReach(player)==inside,player.transform.position.ToString());
                if(QuarterLandmark.WithinReach(player)==inside)inside.Use();await Frame();
                Check("Exit to the forest: "+dungeon.name,!DungeonInterior.Contains(player.transform.position) && Vector3.Distance(player.transform.position,inside.arrival.position)<2,player.transform.position.ToString());
            }
            var gaol=GameObject.Find("The crown gaol").GetComponent<DungeonDoor>();
            var cellar=gaol.arrival.GetComponentInParent<DungeonInterior>();
            var postern=cellar.GetComponentInChildren<DungeonDoor>();
            player.Teleport(postern.arrival.position);await Frame();
            bool foundPostern=QuarterLandmark.WithinReach(player)==gaol;
            if(foundPostern)gaol.Use();await Frame();
            Check("Palace postern enters the cave",foundPostern && DungeonInterior.Contains(player.transform.position),player.transform.position.ToString());
            Vector3 Gaol(float x,float z){
                float y=-4.5f*Mathf.InverseLerp(1,11,z)-3.7f*Mathf.InverseLerp(19,29,z)-4.5f*Mathf.InverseLerp(50,61,z);
                return cellar.transform.TransformPoint(new Vector3(x,y,z));
            }
            await Drive("Guardroom to the occupied cells",player.transform.position,Gaol(0,16),Gaol(0,25),Gaol(0,32.5f),Gaol(-2.2f,32.5f));
            var prisoner=QuarterLandmark.WithinReach(player) as QuarterResident;
            if(prisoner)prisoner.Use();
            bool locked=Physics.CapsuleCast(Gaol(-2.2f,32.5f)+Up*.34f,Gaol(-2.2f,32.5f)+Up*1.4f,.30f,-cellar.transform.right,2,1,QueryTriggerInteraction.Ignore);
            Check("Talk through the locked grille",prisoner && !string.IsNullOrEmpty(prisoner.Speech) && locked,
                prisoner?prisoner.firstName+" / locked="+locked:"no prisoner");
            await Drive("Cell gallery into the torture chamber",player.transform.position,Gaol(0,38),Gaol(0,53),Gaol(1.3f,61),Gaol(1.3f,73));
            await Drive("Return up the guard stair",player.transform.position,Gaol(1.3f,61),Gaol(0,53),Gaol(0,25),Gaol(0,0));
            if(QuarterLandmark.WithinReach(player)==postern)postern.Use();await Frame();
            Check("Return to the palace garden",Vector3.Distance(player.transform.position,postern.arrival.position)<1.1f,player.transform.position.ToString());
            Check("Wildlands traversal boundary",player.Respawns==recoveries,player.travelBounds.ToString());
            Check("Antelope spawned",wild.Find("Savannah antelope").childCount==7,wild.Find("Savannah antelope").childCount.ToString());
        }finally{player.Teleport(spawn);player.TestInput=false;player.TestMove=Vector2.zero;player.TestSprint=false;Time.timeScale=scale;}
        Debug.Log("[World expansion] "+(failures.Count==0?"All routes passed":string.Join(", ",failures)));
        return new {findings,failures};
    }
}
