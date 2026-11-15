using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;

public static class VerifyClimbAssist
{
    static WandererMotor p;
    static WandererCamera camera;
    static WandererShell shell;
    static GameObject root;
    static Vector3 origin=new(-170,12,-40);
    static readonly List<object> checks=new();
    static async Task Wait(int ms)
    {
        float end=Time.realtimeSinceStartup+ms*.001f;
        while(Time.realtimeSinceStartup<end){if(shell.Paused)shell.SetPaused(false);await Task.Delay(15);}
    }
    static GameObject Box(Vector3 point,Vector3 size)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform);
        go.transform.position=origin+point;go.transform.localScale=size;return go;
    }
    static void Drive(Vector2 move,bool jump=false,bool crouch=false,bool drop=false)
    {p.TestMove=move;p.TestJumpPressed=jump;p.TestJumpHeld=jump;p.TestCrouch=crouch;p.TestDrop=drop;}
    static async Task Place(Vector3 point,float yaw=0)
    {Drive(Vector2.zero);p.Teleport(origin+point);camera.SetOrbit(yaw,0);await Wait(40);}
    static void Check(string name,bool passed)=>checks.Add(new{name,passed,p.Action,p.Crouched,p.Hanging,position=p.transform.position.ToString(),normal=p.LedgeNormal.ToString(),stretch=p.Hand.Stretch});
    static async Task Catch(Vector3 foot,float yaw)
    {await Place(foot,yaw);Drive(Vector2.up);await Wait(100);Drive(Vector2.zero);}
    public static async Task<object> Run()
    {
        p=Object.FindAnyObjectByType<WandererMotor>();camera=Object.FindAnyObjectByType<WandererCamera>();shell=Object.FindAnyObjectByType<WandererShell>();
        shell.Begin();var spawn=p.transform.position;p.TestInput=true;p.TestSwing=p.TestDash=p.TestSprint=p.TestFire=false;checks.Clear();
        root=new GameObject("Temporary climb-assist fixtures");
        try
        {
            Box(new Vector3(0,-.2f,0),new Vector3(22,.4f,22));
            Box(new Vector3(0,1.89f,0),new Vector3(1.25f,.22f,1.8f));
            await Catch(new Vector3(1.03f,.45f,0),-90);
            Check("catch unmarked platform side",p.Hanging&&p.LedgeNormal.x>.8f);
            await Catch(new Vector3(0,.45f,-1.3f),0);
            Check("catch platform front",p.Hanging);
            Drive(Vector2.right);await Wait(750);Drive(Vector2.zero);
            Check("shimmy around outside corner",p.Hanging&&p.LedgeNormal.x>.8f&&p.transform.position.x>origin.x+.9f);
            Drive(Vector2.zero,true);await Wait(850);Drive(Vector2.zero);
            Check("pull up from platform side",!p.Hanging&&!p.Traversing&&p.Grounded&&p.transform.position.y>origin.y+1.95f);
            var upper=Box(new Vector3(0,3.64f,0),new Vector3(1.25f,.22f,1.8f));
            await Catch(new Vector3(0,.45f,-1.3f),0);Drive(Vector2.zero,true);await Wait(850);Drive(Vector2.zero);
            Check("crouched pull-up under next platform",p.Crouched&&p.Grounded&&!p.Hanging&&p.transform.position.y>origin.y+1.95f);
            await Wait(250);Check("cannot stand through low ceiling",p.Crouched&&p.GetComponent<CharacterController>().height<1.1f);
            Object.DestroyImmediate(upper);await Wait(500);Check("stands when ceiling clears",!p.Crouched);
            await Place(new Vector3(4,.05f,-4));await Wait(200);float foot=p.transform.position.y;
            Drive(Vector2.zero,crouch:true);await Wait(150);
            Check("manual crouch keeps feet planted",p.Crouched&&Mathf.Abs(p.transform.position.y-foot)<.1f);
            Drive(Vector2.zero);await Wait(150);Check("manual crouch releases",!p.Crouched);
            Box(new Vector3(5,5,2),new Vector3(7,10,.4f));
            await Place(new Vector3(5,2,1.37f));int kicks=p.WallJumps;
            Drive(Vector2.up,true);await Wait(140);Drive(Vector2.zero);
            Check("climb input does not eject from wall",p.WallJumps==kicks&&p.transform.position.z>origin.z+1&&p.VerticalSpeed>0);
            await Place(new Vector3(5,2,1.37f));Drive(Vector2.down,true);await Wait(140);Drive(Vector2.zero);
            Check("away input deliberately wall jumps",p.WallJumps>kicks&&p.transform.position.z<origin.z+1&&p.VerticalSpeed>0);
            var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
            Drive(Vector2.zero);
            p.Teleport(new Vector3(court.x-1.23f,court.y+3.4f,34.2f));camera.SetOrbit(-90,0);await Wait(40);
            Drive(Vector2.up);await Wait(100);Drive(Vector2.right);await Wait(700);Drive(Vector2.zero);
            Check("corner shimmy on real city platform",p.Hanging&&p.LedgeNormal.z>.8f);
            Drive(Vector2.zero,true);await Wait(900);Drive(Vector2.zero);
            Check("crouched clearance between real city platforms",!p.Hanging&&!p.Traversing&&p.Grounded&&p.transform.position.y>court.y+4.8f);
        }
        finally
        {
            Drive(Vector2.zero);p.Teleport(spawn);p.TestInput=false;
            Object.DestroyImmediate(root);
        }
        return checks;
    }
}
