using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;

public static class CaptureClimbAssist
{
    public static async Task<object> Stage(string view)
    {
        var p=Object.FindAnyObjectByType<WandererMotor>();var camera=Object.FindAnyObjectByType<WandererCamera>();
        var shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();
        var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        p.TestInput=true;p.TestCrouch=p.TestDrop=p.TestJumpPressed=p.TestJumpHeld=p.TestFire=p.TestSprint=p.TestSwing=p.TestDash=false;p.TestMove=Vector2.zero;
        if(view=="sticky")
        {
            p.Teleport(new Vector3(court.x-.8f,court.y+.1f,34.2f));camera.SetOrbit(-90,-28);await Task.Delay(200);
            p.Hand.FireAt(new Vector3(court.x-1.7f,court.y+4.85f,34.2f));await Task.Delay(200);
            p.TestMove=Vector2.up;await Task.Delay(380);p.TestMove=Vector2.zero;
        }
        else
        {
            p.Teleport(new Vector3(court.x-1.23f,court.y+3.4f,34.2f));camera.SetOrbit(-90,0);await Task.Delay(40);
            p.TestMove=Vector2.up;await Task.Delay(100);p.TestMove=Vector2.right;await Task.Delay(700);p.TestMove=Vector2.zero;
            p.TestJumpPressed=p.TestJumpHeld=true;await Task.Delay(900);p.TestJumpHeld=false;
            camera.SetOrbit(-145,5);
        }
        await Task.Delay(150);p.ControlsEnabled=false;
        return new{view,p.Action,p.Crouched,p.Hanging,p.Grappling,position=p.transform.position.ToString(),stretch=p.Hand.Stretch};
    }
}
