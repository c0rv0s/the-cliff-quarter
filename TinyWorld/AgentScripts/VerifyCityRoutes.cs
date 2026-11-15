using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;
public static class VerifyCityRoutes
{
    static WandererMotor p;static WandererCamera cam;
    static async Task<bool> Walk(Vector3 target)
    {
        float until=Time.realtimeSinceStartup+17;
        var shell=Object.FindAnyObjectByType<WandererShell>();
        while(Time.realtimeSinceStartup<until)
        {
            if(shell.Paused)shell.SetPaused(false);
            Vector3 delta=target-p.transform.position;
            if(new Vector2(delta.x,delta.z).magnitude<1.05f && Mathf.Abs(delta.y)<.7f){p.TestMove=Vector2.zero;return true;}
            cam.SetOrbit(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,13);p.TestMove=Vector2.up;p.TestSprint=false;
            await Task.Delay(35);
        }
        p.TestMove=Vector2.zero;return false;
    }
    public static async Task<object> Run()
    {
        p=Object.FindAnyObjectByType<WandererMotor>();cam=Object.FindAnyObjectByType<WandererCamera>();Object.FindAnyObjectByType<WandererShell>().Begin();
        var origin=p.transform.position;var results=new List<object>();
        p.TestInput=true;p.TestJumpHeld=p.TestJumpPressed=p.TestSwing=p.TestDrop=p.TestDash=false;
        for(int tier=0;tier<3;tier++)
        {
            var route=GameObject.Find("Stair route -1 "+tier+" 118").transform;
            var a=route.Find("A").position;var turn=route.Find("Turn").position;var b=route.Find("B").position;
            Object.FindAnyObjectByType<WandererShell>().Begin();
            p.TestMove=Vector2.zero;p.Teleport(a+Vector3.up*.6f);await Task.Delay(300);
            bool first=await Walk(turn);results.Add(new{test="west ascent "+tier+" first flight",passed=first,position=p.transform.position.ToString(),target=turn.ToString()});
            if(!first)p.Teleport(turn+Vector3.up*.6f);
            bool across=await Walk(route.Find("Out").position);
            bool second=across && await Walk(b);results.Add(new{test="west ascent "+tier+" second flight",passed=second,position=p.transform.position.ToString(),target=b.ToString()});
        }
        p.TestMove=Vector2.zero;p.TestInput=false;p.Teleport(origin);cam.SetOrbit(28,13);return results;
    }
}
