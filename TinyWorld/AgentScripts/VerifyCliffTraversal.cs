using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object = UnityEngine.Object;

public static class VerifyCliffTraversal
{
    static WandererMotor p;
    static WandererCamera orbit;
    static bool allowPause;
    static readonly List<object> results=new();
    static async Task Wait(int ms)
    {
        var shell=Object.FindAnyObjectByType<WandererShell>();
        float until=Time.realtimeSinceStartup+ms*.001f;
        while(Time.realtimeSinceStartup<until)
        {
            if(!allowPause&&shell.Paused)shell.SetPaused(false);
            await Task.Delay(20);
        }
    }
    static void Check(string name,bool pass,object evidence)
    { results.Add(new {test=name,passed=pass,evidence}); Debug.Log("[VerifyCliff] "+name+" "+pass+" "+evidence); }
    static void Drive(Vector2 move,bool sprint=false,bool jump=false,bool held=false,bool swing=false,bool dash=false,bool drop=false)
    { p.TestInput=true;p.TestMove=move;p.TestSprint=sprint;p.TestJumpPressed=jump;p.TestJumpHeld=held;p.TestSwing=swing;p.TestDash=dash;p.TestDrop=drop; }
    static void Place(Vector3 point,float yaw=0) { Drive(Vector2.zero);p.Teleport(point);orbit.SetOrbit(yaw,13); }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Start Play mode first.");
        results.Clear();allowPause=false;p=Object.FindAnyObjectByType<WandererMotor>();orbit=Object.FindAnyObjectByType<WandererCamera>();
        Object.FindAnyObjectByType<WandererShell>().Begin();
        Vector3 spawn=p.transform.position;
        Drive(Vector2.zero);await Wait(300);Check("spawn supported",p.Grounded,p.transform.position.ToString());
        int frame=Time.frameCount;await Wait(100);Check("frames advance",Time.frameCount>frame,Time.frameCount-frame);
        var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        var runStart=new Vector3(court.x-1,court.y+.15f,29);
        Place(runStart);await Wait(300);Vector3 start=p.transform.position;Drive(Vector2.up);await Wait(500);Drive(Vector2.zero);float walk=Vector3.Distance(start,p.transform.position);
        Check("walk",walk>1.5f,new{distance=walk,position=p.transform.position.ToString()});
        Place(runStart);await Wait(300);start=p.transform.position;Drive(Vector2.up,true);await Wait(500);Drive(Vector2.zero);float run=Vector3.Distance(start,p.transform.position);
        Check("sprint",run>walk*1.2f,new{walk,run});
        Place(spawn);await Wait(200);float y=p.transform.position.y;int jumps=p.Jumps;
        Drive(Vector2.zero,false,true,true);await Wait(300);Check("jump ascent",p.Jumps>jumps&&p.transform.position.y>y+1,p.transform.position.y-y);
        Drive(Vector2.zero);await Wait(1100);Check("jump landing",p.Grounded,p.transform.position.ToString());
        Place(new Vector3(court.x-1,court.y+.12f,24.8f));await Wait(400);
        int traversals=p.Traversals;Drive(Vector2.up,false,true,true);await Wait(550);Drive(Vector2.zero);
        Check("vault real alley bench",p.Traversals>traversals,new{p.Action,position=p.transform.position.ToString()});
        Place(new Vector3(court.x-.1f,court.y+3,39));int wallJumps=p.WallJumps;
        Drive(Vector2.left,true,true,true);await Wait(100);
        Check("wall jump real alley",p.WallJumps>wallJumps&&p.VerticalSpeed>0,new{p.WallJumps,p.VerticalSpeed,position=p.transform.position.ToString()});
        Place(new Vector3(court.x-.1f,court.y+2,35));int wallRuns=p.WallRuns;
        Drive(Vector2.up,true,false,true);await Wait(320);
        Check("wall run real alley",p.WallRuns>wallRuns,new{p.WallRuns,p.Action,position=p.transform.position.ToString()});
        Place(new Vector3(court.x-1.23f,court.y+3.4f,34.2f),-90);int grabs=p.Grabs;
        Drive(Vector2.up);await Wait(100);Drive(Vector2.zero);await Wait(100);
        Check("catch real ledge",p.Hanging&&p.Grabs>grabs,new{p.Action,position=p.transform.position.ToString()});
        start=p.transform.position;Drive(Vector2.right);await Wait(180);Drive(Vector2.zero);
        Check("ledge shimmy",p.Hanging&&Vector3.Distance(start,p.transform.position)>.15f,new{distance=Vector3.Distance(start,p.transform.position),p.Action});
        traversals=p.Traversals;Drive(Vector2.zero,false,true,true);await Wait(850);Drive(Vector2.zero);
        Check("climb from hang",p.Traversals>traversals&&!p.Hanging,new{p.Action,position=p.transform.position.ToString()});
        Place(new Vector3(court.x-1.23f,court.y+3.4f,34.2f),-90);Drive(Vector2.up);await Wait(130);Drive(Vector2.zero,drop:true);await Wait(180);
        Check("drop from ledge",!p.Hanging&&p.VerticalSpeed<0,new{p.Action,p.VerticalSpeed});
        var launch=new Vector3(court.x-1,court.y+9.5f,42);
        Place(launch);int swings=p.Swings;Drive(Vector2.up,swing:true);await Wait(500);
        Check("swing real crossing",p.Swings>swings&&p.Swinging,new{p.Swings,p.Action,position=p.transform.position.ToString()});
        Drive(Vector2.up,jump:true,held:true,swing:true);await Wait(60);
        Check("swing launch",p.Swings>swings&&!p.Swinging&&p.VerticalSpeed>0,new{p.Speed,p.VerticalSpeed});
        Place(new Vector3(0,24,70));int dashes=p.Dashes;start=p.transform.position;Drive(Vector2.up,dash:true);await Wait(150);
        Check("air dash",p.Dashes>dashes&&p.transform.position.z>start.z+2,new{p.Dashes,distance=p.transform.position.z-start.z});
        int dashCount=p.Dashes;Drive(Vector2.up,dash:true);await Wait(100);
        Check("one dash per flight",p.Dashes==dashCount,p.Dashes);
        int respawns=p.Respawns;Place(new Vector3(0,-1,10));Drive(Vector2.up);await Wait(700);
        Check("swim without respawn",p.Swimming&&p.Respawns==respawns,new{p.Action,position=p.transform.position.ToString()});
        Drive(Vector2.zero,drop:true);await Wait(600);Check("dive",p.Swimming&&p.transform.position.y<-2,p.transform.position.ToString());
        Drive(Vector2.zero,held:true);await Wait(800);Check("swim ascent",p.transform.position.y>-1.5f,p.transform.position.ToString());
        Place(new Vector3(0,-1,10));Drive(Vector2.zero);await Wait(180);Drive(Vector2.zero,jump:true,held:true);await Wait(200);
        Check("water leap",!p.Swimming&&p.VerticalSpeed>0,new{p.Action,p.VerticalSpeed});
        Place(new Vector3(0,-25,10));await Wait(100);Check("out of bounds recovery",p.Respawns>respawns,p.Respawns);
        allowPause=true;Object.FindAnyObjectByType<WandererShell>().SetPaused(true);start=p.transform.position;await Wait(200);
        Check("pause freezes movement",Vector3.Distance(start,p.transform.position)<.001f,p.transform.position.ToString());
        Object.FindAnyObjectByType<WandererShell>().SetPaused(false);
        Place(spawn);p.TestInput=false;orbit.SetOrbit(28,13);
        Check("no missing materials",!Object.FindObjectsByType<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m||!m.shader||m.shader.name.Contains("Error"))),"renderer scan");
        return results.ToArray();
    }
}
