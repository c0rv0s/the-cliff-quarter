using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;

public static class VerifyGrappleSwing
{
    delegate void Step(Vector2 input,bool sprint,bool jump,bool held,bool drop,bool swing,bool dash,float dt);
    static WandererMotor p;static Step step;static GameObject fixtures;static List<object> results;
    static readonly Vector3 origin=new(-170,16,-45);
    static readonly FieldInfo velocity=typeof(WandererMotor).GetField("velocity",BindingFlags.Instance|BindingFlags.NonPublic);
    [Serializable] class Sample{public float[] delta,velocity,expectedVelocity;public float length,dt,expectedLength;}
    static Vector3 V(float[] a)=>new(a[0],a[1],a[2]);
    static GameObject Box(string name,Vector3 center,Vector3 size)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(fixtures.transform);go.transform.position=origin+center;go.transform.localScale=size;Physics.SyncTransforms();return go;
    }
    static void Place(Vector3 foot,Vector3 speed=default){p.Teleport(origin+foot);velocity.SetValue(p,speed);Object.FindAnyObjectByType<WandererCamera>().SetOrbit(0,0);}
    static void Tick(float seconds,Vector2 input=default,float dt=1f/120)
    {for(int i=0;i<Mathf.RoundToInt(seconds/dt);i++)step(input,false,false,false,false,false,false,dt);}
    static bool Fire(Vector3 target){p.Hand.FireAt(origin+target);p.Hand.Tick(.4f);return p.Grappling;}
    static void Check(string test,bool passed,object evidence=null)=>results.Add(new{test,passed,evidence});
    public static object Run()
    {
        p=Object.FindAnyObjectByType<WandererMotor>();var shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();var start=p.transform.position;p.ControlsEnabled=false;
        p.TestInput=true;p.TestDrop=p.TestCrouch=p.TestSprint=p.TestSwing=p.TestJumpHeld=p.TestJumpPressed=false;p.TestMove=Vector2.zero;
        step=(Step)Delegate.CreateDelegate(typeof(Step),p,typeof(WandererMotor).GetMethod("Step",BindingFlags.Instance|BindingFlags.NonPublic));
        results=new();fixtures=new GameObject("Temporary grapple swing fixtures");
        try
        {
            var reference=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.GetFullPath("../output/grapple-swing/nerf-reference.json")));
            foreach(var row in reference["cases"])
            {
                var sample=new Sample{delta=row["delta"].ToObject<float[]>(),velocity=row["velocity"].ToObject<float[]>(),expectedVelocity=row["expectedVelocity"].ToObject<float[]>(),length=(float)row["length"],dt=(float)row["dt"],expectedLength=(float)row["expectedLength"]};
                float length=sample.length;var actual=GrappleMotion.Pull(V(sample.velocity),V(sample.delta),ref length,sample.dt);
                Check("force parity with Nerf grapple",Vector3.Distance(actual,V(sample.expectedVelocity))<.0001f&&Mathf.Abs(length-sample.expectedLength)<.0001f,new{actual=actual.ToString(),length});
            }
            Box("Ground",new Vector3(0,-.25f,0),new Vector3(34,.5f,34));
            Box("High grip",new Vector3(0,14,0),Vector3.one*.3f);
            Place(new Vector3(-4,6,0),new Vector3(0,0,8));var incoming=p.Velocity;bool attached=Fire(new Vector3(0,14,0));
            Check("attachment preserves incoming velocity",attached&&(p.Velocity-incoming).sqrMagnitude<.0001f,p.Velocity.ToString());
            float lengthAtHit=p.Hand.RopeLength;Vector3 begin=p.transform.position;Tick(.65f);
            Check("neutral input produces a broad swing",p.Grappling&&p.transform.position.z>begin.z+2&&Vector3.Angle(begin+Vector3.up*1.044f-p.Hand.Point,p.TetherOrigin-p.Hand.Point)>30,new{position=p.transform.position.ToString(),speed=p.Velocity.ToString()});
            Check("rope reels toward anchor",p.Hand.RopeLength<lengthAtHit-.8f,new{lengthAtHit,p.Hand.RopeLength});
            Vector3 release=p.Velocity;step(Vector2.zero,false,false,false,true,false,false,1f/120);
            Check("release does not replace velocity",!p.Grappling&&(release-p.Velocity).sqrMagnitude<.0001f,new{release=release.ToString(),after=p.Velocity.ToString()});
            Tick(.4f);Check("release keeps horizontal momentum",Mathf.Abs(p.Velocity.x-release.x)<.02f&&Mathf.Abs(p.Velocity.z-release.z)<.02f,p.Velocity.ToString());
            Check("release follows gravity",Mathf.Abs(p.Velocity.y-(release.y-10))<.05f,p.VerticalSpeed);
            Place(new Vector3(-4,6,0),new Vector3(0,0,8));Fire(new Vector3(0,14,0));Tick(.3f);release=p.Velocity;
            step(Vector2.zero,false,true,true,false,false,false,1f/120);Check("Space keeps a held swing attached",p.Grappling);
            release=p.Velocity;p.Hand.SetTrigger(false,false);Check("button release preserves momentum",!p.Grappling&&(release-p.Velocity).sqrMagnitude<.0001f);
            Place(new Vector3(-4,6,0),new Vector3(0,0,8));Fire(new Vector3(0,14,0));Tick(.5f);var neutral=p.transform.position;
            Place(new Vector3(-4,6,0),new Vector3(0,0,8));Fire(new Vector3(0,14,0));Tick(.5f,Vector2.right);Check("steering changes swing arc",Vector3.Distance(neutral,p.transform.position)>.3f,p.transform.position.ToString());
            var second=Box("Second grip",new Vector3(4,11,3),Vector3.one*.4f);release=p.Velocity;Fire(new Vector3(4,11,3));
            Check("reattach preserves swing momentum",p.Hand.AttachedCollider==second.GetComponent<Collider>()&&(release-p.Velocity).sqrMagnitude<.0001f);
            Place(new Vector3(0,.05f,-4));Tick(.2f);Fire(new Vector3(2,0,-3));Tick(.8f,Vector2.up);
            Check("floor attachment cannot levitate player",p.transform.position.y<origin.y+.15f&&p.Grounded,p.transform.position.ToString());
            Place(new Vector3(-4,6,0),new Vector3(0,0,8));Fire(new Vector3(0,14,0));
            var wall=Box("New obstacle",new Vector3(-1.5f,10,0),new Vector3(.4f,20,20));Tick(1.4f);
            Check("swing cannot pass through new wall",p.transform.position.x<=origin.x-1.95f,p.transform.position.ToString());Object.DestroyImmediate(wall);
            Place(new Vector3(-4,6,0),new Vector3(0,0,28));Fire(new Vector3(0,14,0));float maxStretch=0,maxSpeed=0;
            for(int i=0;i<360;i++){Tick(1f/120);if(p.Grappling)maxStretch=Mathf.Max(maxStretch,Vector3.Distance(p.TetherOrigin,p.Hand.Point));maxSpeed=Mathf.Max(maxSpeed,p.Velocity.magnitude);}
            Check("fast swing stays within maximum stretch",maxStretch<=p.Hand.maxReach+.05f,maxStretch);Check("swing speed stays bounded",maxSpeed<=40.5f,maxSpeed);
            Place(new Vector3(-4,6,0),new Vector3(0,0,8));Fire(new Vector3(0,14,0));Tick(1,dt:1f/60);var sixty=p.transform.position;
            Place(new Vector3(-4,6,0),new Vector3(0,0,8));Fire(new Vector3(0,14,0));Tick(1);Check("60 and 120 Hz remain consistent",Vector3.Distance(sixty,p.transform.position)<.35f,Vector3.Distance(sixty,p.transform.position));
            Place(new Vector3(-4,6,0));Fire(new Vector3(0,14,0));
            for(int i=0;i<990;i++){p.Hand.SetTrigger(false,true);p.Hand.Tick(.01f);}
            Check("held grip survives 9.9 seconds",p.Grappling,p.Hand.HoldSeconds);
            float beforePause=p.Hand.HoldSeconds;p.Hand.Tick(0);Check("paused time does not spend grip lifetime",p.Hand.HoldSeconds==beforePause&&p.Grappling);
            p.Hand.Tick(.11f);Check("held grip expires at ten seconds",!p.Grappling,p.Hand.HoldSeconds);
            int shots=p.Hand.Shots;p.Hand.SetTrigger(false,true);p.Hand.Tick(.2f);Check("holding after timeout cannot auto-fire",p.Hand.Shots==shots&&!p.Hand.Flying&&!p.Grappling);
            p.Hand.SetTrigger(false,false);p.Hand.SetTrigger(true,true);Check("fresh press fires after timeout",p.Hand.Shots==shots+1&&(p.Hand.Flying||p.Grappling));
            p.Hand.SetTrigger(false,false);Check("button release cancels projectile",!p.Hand.Flying&&!p.Grappling);
            p.Hand.SetTrigger(true,false);Check("press and release in one frame never latches",!p.Hand.Flying&&!p.Grappling);
            Place(new Vector3(-4,6,0));Fire(new Vector3(0,14,0));p.Hand.Tick(8);Fire(new Vector3(0,14,0));p.Hand.Tick(3);Check("new attachment gets a fresh lifetime",p.Grappling&&Mathf.Abs(p.Hand.HoldSeconds-3)<.001f,p.Hand.HoldSeconds);
            var beginTraverse=typeof(WandererMotor).GetMethod("BeginTraverse",BindingFlags.Instance|BindingFlags.NonPublic);
            bool mantling=(bool)beginTraverse.Invoke(p,new object[]{p.transform.position+Vector3.right*.5f,"Climb up",Vector3.zero,float.NegativeInfinity});Tick(.7f);
            Check("pull-up keeps grip attached",mantling&&!p.Traversing&&p.Grappling);
            Place(new Vector3(-4,6,0));Fire(new Vector3(0,14,0));
            var body=p.GetComponent<CharacterController>();body.enabled=false;p.transform.position=origin+new Vector3(-13,6,0);body.enabled=true;
            var blocker=Box("Blocked rope correction",new Vector3(-11.5f,10,0),new Vector3(.4f,20,20));Tick(.2f);
            Check("blocked full stretch does not snap grip",p.Grappling&&p.transform.position.x<origin.x-11.9f,new{position=p.transform.position.ToString(),stretch=Vector3.Distance(p.TetherOrigin,p.Hand.Point)});Object.DestroyImmediate(blocker);
            Place(new Vector3(0,2,-4));bool launched=p.Hand.FireAt(p.Hand.Origin+Vector3.back*30);Check("empty shot still launches",launched&&p.Hand.Flying);p.Hand.Tick(.4f);Check("empty shot retracts at 12.96 metres",!p.Grappling&&!p.Hand.Flying&&p.Hand.maxReach==12.96f);
        }
        finally{Object.DestroyImmediate(fixtures);p.Teleport(start);p.TestInput=false;p.ControlsEnabled=true;}
        return results;
    }
}
