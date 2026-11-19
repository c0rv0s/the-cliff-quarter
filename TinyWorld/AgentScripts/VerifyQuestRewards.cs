using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TinyWorld;
using Object=UnityEngine.Object;

public static class VerifyQuestRewards
{
    static WandererMotor Player => Object.FindAnyObjectByType<WandererMotor>();
    static QuarterQuests Book => QuarterQuests.Current;
    static async Task Frame()
    {
        int frame=Time.frameCount;
        while(Time.frameCount==frame) { var shell=Object.FindAnyObjectByType<WandererShell>();if(shell.Paused)shell.SetPaused(false);await Task.Yield(); }
    }
    public static async Task<object> Restart()
    {
        var p=Player; var race=Book.race; bool locked=!Book.CanGlide;
        Book.Speak("Moss"); bool loan=Book.CanGlide;
        var key=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(key);key.MakeCurrent();
        try {
            Time.timeScale=2;
            while(race.Running&&!race.Status.StartsWith("Hoop"))await Frame();
            p.Teleport(race.feet[3]+Vector3.up*.12f);
            InputSystem.QueueStateEvent(key,new KeyboardState(Key.T));
            for(int i=0;i<4;i++)await Frame();
            bool restarted=race.Running && race.Cleared==0 && Vector3.Distance(p.transform.position,race.feet[0])<1.1f;
            InputSystem.QueueStateEvent(key,new KeyboardState());
            p.Respawn(); for(int i=0;i<5;i++)await Frame();
            return new{locked,loan,restarted,returnedAfterLoss=!Book.CanGlide,stage=Book.Stage(1)};
        } finally { InputSystem.RemoveDevice(key);Time.timeScale=1; }
    }
    public static async Task<object> Gear()
    {
        var p=Player;var boat=Book.skiff;
        var home=p.transform.position; p.TestInput=true;
        try {
            p.Teleport(new Vector3(6,25,95));await Frame();
            p.TestJumpPressed=p.TestJumpHeld=true;await Frame();await Frame();
            bool flip=p.FlipProgress>0 && p.Velocity.y>3;
            p.TestJumpHeld=false;
            var rail=GameObject.Find("Low landing");p.Teleport(rail.transform.position+new Vector3(.5f,.07f,1.3f));
            for(int i=0;i<4;i++)await Frame();
            bool dock=QuarterLandmark.WithinReach(p)==boat;
            if(!dock){p.Teleport(boat.transform.position+boat.transform.right*1.15f);await Frame();}
            if(QuarterLandmark.WithinReach(p)==boat)boat.Use();
            bool aboard=p.Skiff==boat;
            var start=boat.transform.position;
            p.TestMove=Vector2.up;p.TestSprint=true;
            float end=Time.time+2;while(Time.time<end)await Frame();
            p.TestMove=Vector2.zero;p.TestSprint=false;
            float sailed=Vector3.Distance(start,boat.transform.position);
            if(p.Skiff)QuarterLandmark.WithinReach(p).Use();await Frame();
            var journal=Book.Journal;Book.ToggleOutfit();
            return new{flip,dock,aboard,sailed,steppedOut=!p.Skiff,light=Book.lantern.activeInHierarchy,
                outfitChanged=journal!=Book.Journal,bootReward=Book.Earned(4),ownsGlider=Book.Earned(1)};
        } finally{p.TestMove=Vector2.zero;p.TestJumpHeld=p.TestSprint=p.TestInput=false;p.Teleport(home);}
    }
    public static async Task<object> Climb()
    {
        var p=Player;var home=p.transform.position;var cam=Object.FindAnyObjectByType<WandererCamera>();
        var plants=GameObject.Find("Wall ivy").GetComponentsInChildren<ClimbableVine>();
        var checks=new List<object>(); var last=Vector3.positiveInfinity;int topped=0;
        p.TestInput=true;Time.timeScale=2;
        try{
            foreach(var v in plants.Where(v=>v.length>4 && v.length<11 && Mathf.Abs(v.Outward.y)<.2f)) {
                if(Vector3.Distance(last,v.Top)<8)continue;
                if(!Physics.Raycast(v.Top-v.Outward*.85f+Vector3.up,Vector3.down,out var floor,1.6f,1)||floor.normal.y<.8f)continue;
                last=v.Top;var start=v.Bottom+v.Outward*.71f-Vector3.up*1.07f;
                p.Teleport(start);cam.SetOrbit(Mathf.Atan2(-v.Outward.x,-v.Outward.z)*Mathf.Rad2Deg,10);
                p.TestMove=Vector2.up;
                bool caught=false;float until=Time.time+v.length/3+2;
                while(Time.time<until) {await Frame();caught|=p.VineClimbing;if(p.Traversing)p.TestMove=Vector2.zero;if(caught&&p.Grounded&&p.transform.position.y>v.Top.y-.3f)break;}
                bool top=caught && p.Grounded && p.transform.position.y>v.Top.y-.3f;
                checks.Add(new {at=v.Top.ToString("F1"),caught,top,height=p.transform.position.y-start.y});if(top)topped++;
                if(topped>=3||checks.Count>=7)break;
            }
        }finally{p.TestInput=false;p.TestMove=Vector2.zero;p.Teleport(home);Time.timeScale=1;}
        return new{strands=plants.Length,topped,checks};
    }
}
