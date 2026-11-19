using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TinyWorld;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifyTownStories
{
    static WandererMotor Player=>Object.FindAnyObjectByType<WandererMotor>();
    static WandererShell Shell=>Object.FindAnyObjectByType<WandererShell>();
    static QuarterQuests Book=>QuarterQuests.Current;
    static async Task Frame()
    {
        int f=Time.frameCount;
        while(f==Time.frameCount){if(Shell.Paused)Shell.SetPaused(false);await Task.Yield();}
    }
    static async Task<bool> Reach(QuarterLandmark site)
    {
        var p=Player;
        for(int i=0;i<16;i++){
            var center=site.transform.TransformPoint(site.handle);
            var around=Quaternion.Euler(0,45*(i%8),0)*Vector3.forward*(i<8?1.12f:.64f);
            var foot=center+around;
            if(Physics.Raycast(foot+Vector3.up*.75f,Vector3.down,out var hit,3.8f,1))foot.y=hit.point.y+.075f;
            else foot.y=center.y-1.15f;
            p.Teleport(foot);await Frame();await Frame();
            if(QuarterLandmark.WithinReach(p)==site)return true;
        }
        return false;
    }
    public static async Task<object> Errands()
    {
        var report=new List<object>();var people=Object.FindObjectsByType<QuarterResident>().ToDictionary(x=>x.firstName);
        var items=Book.GetComponentsInChildren<QuarterErrandPoint>(true).ToDictionary(x=>x.role);
        var player=Player;var home=player.transform.position;player.TestInput=true;player.TestMove=Vector2.zero;
        try{
            foreach(int i in new[]{0,2,3,4,5}){
                var story=QuarterQuests.Tales[i];
                while(Book.Stage(i)<story.Length){
                    int stage=Book.Stage(i);string role=story[stage].actor;
                    if(i==2 && stage>=4){player.Teleport(Book.barnMeeting+Vector3.forward);await Frame();await Frame();}
                    QuarterLandmark site=people.TryGetValue(role,out var who)?who:items[role];
                    if(!await Reach(site)){report.Add(new{story=i,stage,role,pass=false,why="no interaction from a grounded approach",at=site.transform.position.ToString()});break;}
                    float stop=Time.realtimeSinceStartup+4;while(Book.Stage(i)==stage && Time.realtimeSinceStartup<stop){site.Use();await Task.Delay(25);await Frame();}
                    bool pass=Book.Stage(i)==stage+1;report.Add(new{story=i,stage,role,pass});if(!pass)break;
                }
            }
        }finally{player.Teleport(home);player.TestInput=false;}
        return report;
    }
    public static async Task<object> Circuit(bool diagnostics=false)
    {
        var p=Player;var race=Book.race;var cam=Object.FindAnyObjectByType<WandererCamera>();
        Shell.Begin();
        if(race.Running)typeof(QuarterRace).GetMethod("Finish",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(race,new object[]{false});
        if(Book.Stage(1)==0){if(!await Reach(race.rival))throw new Exception("Cannot reach Moss");race.rival.Use();}
        else if(!race.Running)race.Begin();
        var notes=new List<object>();int jumps=p.Jumps,climbs=p.Traversals,glides=p.Glides;
        var trace=new List<string>();var goal=race.feet;int leg=1;
        p.TestInput=true;p.TestMove=Vector2.zero;p.TestSprint=false;Time.timeScale=1.45f;
        await Frame();await Frame();
        float nextJump=0,stall=Time.time+8,logAt=0;
        try{
            while(race.Running && leg<goal.Length){
                var target=goal[leg];var delta=target-p.transform.position;
                float distance=new Vector2(delta.x,delta.z).magnitude;
                int gate=Array.IndexOf(race.gates,leg);
                bool scored=gate>=0 && race.Cleared>gate;
                bool climbing=leg<=race.gates[7], flying=leg>race.gates[7]&&leg<=race.gates[10];
                bool arrived=gate>=0?scored:(distance<(climbing?.62f:1.2f)&&(flying?Mathf.Abs(delta.y)<4:delta.y<.82f));
                    if(arrived && !(race.Status??"").Contains("courier race") && Time.time>nextJump-.45f){
                    notes.Add(new{leg,gate,at=p.transform.position.ToString("F1"),p.Action});leg++;stall=Time.time+8;continue;
                }
                if(Time.time>stall){notes.Add(new{leg,gate,target=target.ToString(),at=p.transform.position.ToString(),p.Action,status=race.Status});break;}
                if(Time.time<logAt){}else{logAt=Time.time+.35f;if(diagnostics){trace.Add(leg+" "+p.transform.position.ToString("F1")+" "+p.Action);}}
                
                if(distance>.12f)cam.SetOrbit(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,8);
                p.TestMove=distance>.29f?Vector2.up:Vector2.zero;
                p.TestSprint=flying ? (p.transform.position.y-target.y>distance*.29f+.33f) : !climbing||leg==8||leg==9;
                bool jump=p.Hanging||(climbing && delta.y>.24f && distance<3.0f && p.Grounded)||
                    (leg==8&&p.Grounded&&distance<6.3f)||(leg==9 && p.Grounded)||
                    (flying&&!p.Grounded&&!p.Gliding && p.VerticalSpeed<2);
                p.TestJumpHeld=climbing&&delta.y>.46f;
                if(jump && Time.time>nextJump){p.TestJumpPressed=true;nextJump=Time.time+.36f;}
                await Frame();
            }
        }finally{p.TestMove=Vector2.zero;p.TestSprint=p.TestJumpHeld=p.TestInput=false;Time.timeScale=1;}
        return new{won=Book.Stage(1)==2,seconds=race.FinishTime,hoops=race.Cleared,jumps=p.Jumps-jumps,climbs=p.Traversals-climbs,glides=p.Glides-glides,leg,notes,trace};
    }
    public static async Task<object> Ocean()
    {
        var p=Player;var home=p.transform.position;var camera=Object.FindAnyObjectByType<WandererCamera>();
        var house=Object.FindObjectsByType<QuarterSwingDoor>().Single(d=>d.name.StartsWith("Tidekeeper"));
        bool swam=false,landed=false;var checks=new List<object>();
        var shore=house.transform;
        var route=new[]{shore.TransformPoint(new Vector3(0,-4.5f,-50)),shore.TransformPoint(new Vector3(0,-3.6f,-37)),
            shore.TransformPoint(new Vector3(0,-2,-27)),shore.TransformPoint(new Vector3(0,0,-15)),
            shore.TransformPoint(new Vector3(0,0,-4)),shore.TransformPoint(new Vector3(0,0,2.5f))};
        p.Teleport(route[0]+Vector3.up*.16f);p.TestInput=true;Time.timeScale=2;
        try{
            foreach(var to in route.Skip(1)){
                float limit=Time.realtimeSinceStartup+10;
                while(Time.realtimeSinceStartup<limit){
                    var delta=to-p.transform.position;if(new Vector2(delta.x,delta.z).magnitude<.7f)break;
                    camera.SetOrbit(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,7);
                    p.TestMove=Vector2.up;swam|=p.Swimming;if(swam && p.Grounded&&!p.Swimming)landed=true;
                    await Frame();
                }
                var d=to-p.transform.position;bool pass=new Vector2(d.x,d.z).magnitude<.8f;
                checks.Add(new{pass,at=p.transform.position.ToString("F2"),p.Action});if(!pass)break;
            }
        }finally{p.TestInput=false;p.TestMove=Vector2.zero;p.Teleport(home);Time.timeScale=1;}
        return new{swam,landed,checks};
    }
    public static async Task<object> Rematch()
    {
        var race=Book.race;var p=Player;var home=p.transform.position;p.TestInput=true;p.TestMove=Vector2.zero;
        race.Begin();Time.timeScale=5;
        float limit=Time.realtimeSinceStartup+46;var start=race.rival.transform.position;
        while(race.Running && Time.realtimeSinceStartup<limit && !race.Status.StartsWith("Hoop"))await Frame();
        var other=race.hoops[5].position;p.Teleport(other-Vector3.up);
        for(int i=0;i<15;i++)await Frame();bool ordered=race.Cleared==0;
        while(race.Running && Time.realtimeSinceStartup<limit)await Frame();
        bool lost=!race.Running,moved=Vector3.Distance(start,race.rival.transform.position)>4;
        var key=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        try{
            UnityEngine.InputSystem.InputSystem.EnableDevice(key);key.MakeCurrent();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(key,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.T));
            for(int i=0;i<6;i++)await Frame();
            return new{ordered,lost,moved,retried=race.Running&&race.Cleared==0,stage=Book.Stage(1)};
        }finally{
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(key,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            UnityEngine.InputSystem.InputSystem.RemoveDevice(key);p.TestMove=Vector2.zero;p.TestInput=false;Time.timeScale=1;
        }
    }
}
