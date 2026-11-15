using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;

public static class VerifyStairMantles
{
    static WandererMotor p;static WandererCamera cam;static WandererShell shell;
    static async Task Wait(int ms)
    {
        float end=Time.realtimeSinceStartup+ms*.001f;
        while(Time.realtimeSinceStartup<end){if(shell.Paused)shell.SetPaused(false);await Task.Delay(15);}
    }
    public static async Task<object> Run()
    {
        p=Object.FindAnyObjectByType<WandererMotor>();cam=Object.FindAnyObjectByType<WandererCamera>();shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();
        var start=p.transform.position;var results=new List<object>();p.TestInput=true;
        p.TestJumpHeld=p.TestJumpPressed=p.TestSwing=p.TestSprint=p.TestDrop=p.TestCrouch=p.TestFire=false;
        try
        {
            for(int tier=0;tier<3;tier++)foreach(int flight in new[]{0,1})foreach(int side in new[]{-1,1})foreach(float fraction in new[]{.1f,.25f,.6f,.85f})
            {
                var route=GameObject.Find("Stair route -1 "+tier+" 118").transform;
                var a=route.Find("A").position;var turn=route.Find("Turn").position;
                var flat=Vector3.ProjectOnPlane(turn-a,Vector3.up).normalized;var end=turn-flat*3;
                if(flight==1){a=route.Find("Out").position;end=route.Find("B").position;flat=Vector3.ProjectOnPlane(end-a,Vector3.up).normalized;a+=flat*3;end-=flat*3.6f;}
                var normal=Vector3.Cross(Vector3.up,flat)*side;
                var rail=Vector3.Lerp(a,end,fraction)+normal*1.48f;
                bool surface=Physics.Raycast(rail+Vector3.up*2,Vector3.down,out var hit,4,1,QueryTriggerInteraction.Ignore);
                if(!surface){results.Add(new{tier,flight,side,fraction,passed=false,reason="rail not found"});continue;}
                var foot=new Vector3(rail.x,hit.point.y-1.6f,rail.z)+normal*.43f;
                if(Physics.CheckCapsule(foot+Vector3.up*.33f,foot+Vector3.up*1.51f,.28f,1,QueryTriggerInteraction.Ignore))
                {results.Add(new{tier,flight,side,fraction,skipped=true,reason="starting capsule intersects adjacent geometry"});continue;}
                p.TestMove=Vector2.zero;p.Teleport(foot);
                cam.SetOrbit(Mathf.Atan2(-normal.x,-normal.z)*Mathf.Rad2Deg,0);await Wait(40);
                p.TestMove=Vector2.up;await Wait(120);p.TestMove=Vector2.zero;await Wait(50);
                bool caught=p.Hanging;float y=p.transform.position.y;
                p.TestJumpPressed=p.TestJumpHeld=true;await Wait(1050);p.TestJumpHeld=false;
                results.Add(new{tier,flight,side,fraction,caught,passed=caught&&!p.Hanging&&!p.Traversing&&p.Grounded&&p.transform.position.y>y+.45f,
                    p.Action,position=p.transform.position.ToString(),rail=hit.point.ToString(),normal=hit.normal.ToString()});
            }
        }
        finally{p.TestMove=Vector2.zero;p.TestInput=false;p.Teleport(start);}
        return results;
    }
}
