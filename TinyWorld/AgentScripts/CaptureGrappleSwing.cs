using System;
using System.Reflection;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;
public static class CaptureGrappleSwing
{
    delegate void Step(Vector2 input,bool sprint,bool jump,bool held,bool drop,bool swing,bool dash,float dt);
    public static object Stage(float seconds)
    {
        var p=Object.FindAnyObjectByType<WandererMotor>();var camera=Object.FindAnyObjectByType<WandererCamera>();Object.FindAnyObjectByType<WandererShell>().Begin();
        p.TestInput=true;p.TestMove=Vector2.zero;p.TestDrop=p.TestCrouch=p.TestJumpPressed=p.TestJumpHeld=p.TestFire=p.TestSprint=p.TestSwing=p.TestDash=false;p.ControlsEnabled=false;
        var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        p.Teleport(new Vector3(court.x-1,court.y+9.5f,44));camera.SetOrbit(-28,8);
        typeof(WandererMotor).GetField("velocity",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p,new Vector3(2,1,8));
        p.Hand.FireAt(new Vector3(court.x-.58f,court.y+15.5f,48));p.Hand.Tick(.35f);
        bool hit=p.Grappling;var atHit=p.Hand.Point;
        var step=(Step)Delegate.CreateDelegate(typeof(Step),p,typeof(WandererMotor).GetMethod("Step",BindingFlags.Instance|BindingFlags.NonPublic));
        for(int i=0;i<Mathf.RoundToInt(seconds*120);i++)step(Vector2.zero,false,false,false,false,false,false,1f/120);
        return new{seconds,hit,p.Grappling,p.Action,point=atHit.ToString(),position=p.transform.position.ToString(),velocity=p.Velocity.ToString(),p.Hand.RopeLength,p.Hand.Stretch};
    }
}
