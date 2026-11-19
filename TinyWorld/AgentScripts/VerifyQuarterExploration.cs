using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TinyWorld;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

public static class VerifyQuarterExploration
{
    static WandererMotor Player => Object.FindAnyObjectByType<WandererMotor>();
    static WandererShell Shell => Object.FindAnyObjectByType<WandererShell>();
    static async Task Frames(int count)
    {
        int last=Time.frameCount;
        while(count>0){await Task.Yield();if(Time.frameCount!=last){last=Time.frameCount;count--;}}
    }
    static void Stand(Vector3 foot,float yaw)
    {
        var p=Player;p.TestInput=true;p.TestMove=Vector2.zero;p.TestJumpPressed=p.TestJumpHeld=p.TestCrouch=p.TestSprint=false;
        p.Teleport(foot+Vector3.up*.09f);
        Object.FindAnyObjectByType<WandererCamera>().SetOrbit(yaw,8);
    }
    public static async Task<object> UseSites()
    {
        var shell=Shell;var p=Player;var spawn=p.transform.position;
        bool[] enabled=Sunstone.All.Select(x=>x.enabled).ToArray();var stones=Sunstone.All.ToArray();
        var checks=new List<object>();var pad=InputSystem.AddDevice<Gamepad>();var key=InputSystem.AddDevice<Keyboard>();
        var settings=InputSystem.settings;var bg=settings.backgroundBehavior;var behavior=settings.editorInputBehaviorInPlayMode;
        try
        {
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(key);key.MakeCurrent();
            foreach(var stone in stones)stone.enabled=false;
            shell.Begin();Time.timeScale=1;
            foreach(var site in Object.FindObjectsByType<QuarterLandmark>().OrderBy(s=>s.name))
            {
                bool resident=site is QuarterResident;
                var foot=site.transform.position+new Vector3(0,0,site is LanternSlip?-.35f:-1.25f);
                if(site is SwallowBell)foot.x+=.82f;
                Stand(foot,0);await Frames(8);if(shell.Paused)shell.SetPaused(false);key.MakeCurrent();pad.MakeCurrent();
                bool reachable=QuarterLandmark.WithinReach(p)==site;
                bool controller=resident || site is LanternSlip;
                if(controller) InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));else InputSystem.QueueStateEvent(key,new KeyboardState(Key.G));
                await Frames(3);
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.QueueStateEvent(key,new KeyboardState());
                await Task.Delay(350);
                bool answer=site switch{
                    QuarterResident r=>!string.IsNullOrEmpty(r.Speech),
                    GrottoPool pool=>pool.rings.Any(r=>r.gameObject.activeSelf),
                    QuayChimes c=>c.tubes.Any(t=>t.GetComponent<AudioSource>().isPlaying) || c.tubes.Any(t=>Quaternion.Angle(t.localRotation,Quaternion.identity)>5),
                    SwallowBell b=>Vector3.Distance(b.swallows[0].localPosition,b.swallows[1].localPosition)>.8f || Quaternion.Angle(b.bell.localRotation,Quaternion.identity)>1,
                    ObservatoryKite k=>k.Verb.StartsWith("Reel"),
                    LanternSlip l=>l.transform.Cast<Transform>().Any(t=>t.name.Contains("(Clone)")&&t.gameObject.activeSelf),_=>false };
                checks.Add(new {site=site.name,reachable,answer,p.Grounded,input=controller?"pad":"keyboard",at=p.transform.position.ToString("F2")});
            }
            var walker=Object.FindAnyObjectByType<QuayChimes>();Stand(walker.transform.position+Vector3.back*2,0);
            var mira=Object.FindObjectsByType<QuarterResident>().First(r=>r.firstName=="Mira");var before=mira.transform.position;
            float travelled=0;
            for(int i=0;i<25;i++){await Task.Delay(300);var now=mira.transform.position;travelled+=Vector3.Distance(now,before);before=now;if(travelled>1)break;}
            checks.Add(new{residentPatrol=travelled});
            var heading=mira.body.forward;
            Stand(mira.transform.position+heading*2.1f,Mathf.Atan2(-heading.x,-heading.z)*Mathf.Rad2Deg);
            mira.Use();await Frames(5);var collidingFrom=p.transform.position;
            p.TestMove=Vector2.up;await Task.Delay(820);p.TestMove=Vector2.zero;
            float gap=Vector3.ProjectOnPlane(p.transform.position-mira.transform.position,Vector3.up).magnitude;
            checks.Add(new{residentSolid=gap>.4f&&gap<.85f&&Vector3.Distance(collidingFrom,p.transform.position)>.7f,gap});
        }
        finally{settings.editorInputBehaviorInPlayMode=behavior;settings.backgroundBehavior=bg;InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(key);p.TestInput=false;p.Teleport(spawn);for(int i=0;i<stones.Length;i++)stones[i].enabled=enabled[i];}
        return checks;
    }
    public static async Task<object> Hang(bool stage=false)
    {
        var p=Player;var pose=Object.FindAnyObjectByType<WandererPose>();var shell=Shell;shell.Begin();
        var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        var start=new Vector3(court.x-1,court.y+9-1.55f,40.10f);
        Stand(start,0);p.TestMove=Vector2.up;
        for(int i=0;i<25&&!p.Hanging;i++)await Frames(1);
        p.TestMove=Vector2.zero;await Frames(8);
        float Error(Transform hand,string name){var r=hand.Find(name).GetComponent<Renderer>();return r.transform.TransformPoint(r.localBounds.center).y-p.GripCenter.y;}
        bool hanging=p.Hanging;
        var errors=new List<float>(); if(hanging){errors.Add(Error(pose.handL,"Glove.L"));errors.Add(Error(pose.handR,"Glove.R"));}
        var at=p.transform.position;p.TestMove=Vector2.right;await Task.Delay(230);p.TestMove=Vector2.zero;await Frames(4);
        float shimmy=Vector3.Distance(at,p.transform.position);if(p.Hanging){errors.Add(Error(pose.handL,"Glove.L"));errors.Add(Error(pose.handR,"Glove.R"));}
        if(stage&&p.Hanging){var lip=p.GripCenter;Shot(lip+new Vector3(2.15f,.65f,-2.2f),lip-Vector3.up*.26f,37);Time.timeScale=0;}
        bool stood=false;
        if(!stage&&p.Hanging){p.TestJumpPressed=true;await Task.Delay(1100);stood=p.Grounded&&!p.Hanging&&!p.Traversing;}
        return new {hanging,shimmy,stood,errors=errors.ToArray(),at=p.transform.position.ToString(),p.Action};
    }
    static void Shot(Vector3 at,Vector3 aim,float fov)
    {
        var cam=Camera.main;cam.GetComponent<CinemachineBrain>().enabled=false;cam.transform.position=at;cam.transform.LookAt(aim);cam.fieldOfView=fov;
        Shell.GetComponentInChildren<Canvas>().enabled=false;
    }
    public static object View(string view)
    {
        Time.timeScale=1;var p=Player;
        Camera.main.GetComponent<CinemachineBrain>().enabled=true;Shell.GetComponentInChildren<Canvas>().enabled=true;Shell.Begin();
        var cave=GameObject.Find("Grotto entrance").transform.position;var chamber=GameObject.Find("Grotto chamber").transform.position;
        var sills=GameObject.Find("Grotto scramble").transform;
        if(view=="cave")Shot(cave+new Vector3(.1f,1.5f,5.2f),sills.GetChild(2).position+Vector3.up*1.02f,57);
        if(view=="climber")
        {
            var at=sills.GetChild(6).position;var d=sills.GetChild(7).position-at;
            Stand(at,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg);
        }
        if(view=="scramble")Shot(sills.GetChild(5).position+Vector3.up*1.48f,sills.GetChild(7).position+Vector3.up*.65f,70);
        if(view=="window")Shot(sills.GetChild(11).position+Vector3.up*1.63f,sills.GetChild(13).position+Vector3.up*1.55f,67);
        if(view=="chamber"){var pool=Object.FindAnyObjectByType<GrottoPool>();var at=pool.transform.position;pool.Use();Shot(sills.GetChild(7).position+Vector3.up*1.65f,at+Vector3.up*.75f,67);}
        if(view=="coast")Shot(cave+new Vector3(36,18,44),cave+new Vector3(-3,10,-5),65);
        if(view=="theatre")Shot(new Vector3(-126,150,-5),new Vector3(-153,134,23),59);
        if(view=="mountain")Shot(new Vector3(-62,213,181),new Vector3(-175,148,171),74);
        if(view=="fish")
        {
            var fish=Object.FindAnyObjectByType<RiverWindsocks>();var at=fish.fish[1].position;
            Shot(at+new Vector3(4,1.4f,-15),at+Vector3.right*2,54);
        }
        if(view=="resident")
        {
            var r=Object.FindObjectsByType<QuarterResident>().First(x=>x.firstName=="Mira");Stand(r.transform.position-r.body.forward*1.7f,0);r.Use();
            Shot(r.transform.position+r.body.forward*3.8f+Vector3.up*1.2f,r.transform.position+Vector3.up*.93f,47);
        }
        if(view=="quay"){var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;Stand(new Vector3(court.x-1,court.y,23.5f),20);}
        return view;
    }
    public static async Task<object> Harbour()
    {
        var cave=GameObject.Find("Grotto scramble").transform.Cast<Transform>().Select(t=>t.position).ToArray();
        var shelves=GameObject.Find("Salt stair").transform.Cast<Transform>().Select(t=>t.position).ToArray();
        var player=Player;var cam=Object.FindAnyObjectByType<WandererCamera>();
        async Task<object> Climb(Vector3[] route,bool reset=true)
        {
            var checks=new List<object>();Shell.Begin();if(reset)Stand(route[0],0);await Frames(10);
            int jumps=player.Jumps,traversals=player.Traversals;
            for(int i=1;i<route.Length;i++)
            {
                float limit=Time.realtimeSinceStartup+3.8f,nextJump=Time.time;
                bool pass=false;var target=route[i];
                while(Time.realtimeSinceStartup<limit)
                {
                    if(Shell.Paused)Shell.SetPaused(false);
                    var delta=target-player.transform.position;
                    float distance=new Vector2(delta.x,delta.z).magnitude;
                    if(distance<.58f&&Mathf.Abs(delta.y)<.48f&&player.Grounded&&!player.Traversing){pass=true;break;}
                    cam.SetOrbit(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,8);
                    player.TestMove=player.Hanging||distance>.33f?Vector2.up:Vector2.zero;
                    if((player.Grounded||player.Hanging)&&delta.y>.38f&&distance<3.2f&&Time.time>nextJump){
                        player.TestJumpPressed=true;nextJump=Time.time+.42f;
                    }
                    await Frames(1);
                }
                checks.Add(new{pass,sill=i,at=player.transform.position.ToString("F2"),target=target.ToString("F2"),player.Action});
                if(!pass)break;
            }
            return new{checks,jumps=player.Jumps-jumps,mantles=player.Traversals-traversals};
        }
        var start=player.transform.position;
        try
        {
            var inside=await Climb(cave);
            var approach=cave[0]+Vector3.forward*1.4f;
            var seaside=shelves.Append(cave[^1]+Vector3.forward*.9f).ToArray();
            var loopBack=await Climb(seaside.Reverse().Append(approach).ToArray(),false);
            var outside=await Climb(new[]{approach}.Concat(seaside).ToArray());
            return new{inside,loopBack,outside};
        }
        finally{player.TestInput=false;player.TestMove=Vector2.zero;Time.timeScale=1;player.Teleport(start);}
    }
    public static async Task<object> Walk(float[][] points, bool continuous=true)
    {
        var p=Player;var cam=Object.FindAnyObjectByType<WandererCamera>();
        Shell.Begin();Time.timeScale=3;var result=new List<object>();
        var start=p.transform.position;
        try
        {
            Vector3 previous=Vector3.positiveInfinity;
            foreach(var v in points)
            {
                Vector3 a=new(v[0],v[1],v[2]),b=new(v[3],v[4],v[5]);
                if(!continuous||Vector3.Distance(previous,a)>.6f){Stand(a+Vector3.up*.18f,0);await Frames(10);}
                var last=p.transform.position;float timeout=Time.realtimeSinceStartup+3.2f;
                for(int j=0;j<3000;j++)
                {
                    if(Shell.Paused){Shell.SetPaused(false);Time.timeScale=3;}
                    var delta=b-p.transform.position;float flat=new Vector2(delta.x,delta.z).magnitude;
                    if(flat<.52f&&Mathf.Abs(delta.y)<.75f)break;
                    if(Time.realtimeSinceStartup>timeout)break;
                    if(Vector3.Distance(last,p.transform.position)>.45f){last=p.transform.position;timeout=Time.realtimeSinceStartup+3.2f;}
                    cam.SetOrbit(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,10);p.TestMove=Vector2.up;
                    await Frames(1);
                }
                p.TestMove=Vector2.zero;
                var d=b-p.transform.position;bool pass=new Vector2(d.x,d.z).magnitude<.65f&&Mathf.Abs(d.y)<.8f;
                result.Add(new{pass,start=a.ToString("F1"),target=b.ToString("F1"),stop=p.transform.position.ToString("F1"),p.Action});
                previous=pass?b:Vector3.positiveInfinity;
            }
        }
        finally{p.TestMove=Vector2.zero;p.TestInput=false;Time.timeScale=1;p.Teleport(start);}
        return result;
    }
}
