using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;
public static class CaptureVine
{
    public static async Task<object> Stage()
    {
        var p=Object.FindAnyObjectByType<WandererMotor>();var cam=Object.FindAnyObjectByType<WandererCamera>();var shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();
        var vine=ClimbableVine.Active.OrderBy(v=>Vector3.Distance(v.Top,new Vector3(-21.89f,28.6f,8.99f))).First();
        p.TestInput=true;p.TestCrouch=p.TestDrop=p.TestJumpPressed=p.TestJumpHeld=p.TestFire=p.TestSprint=p.TestSwing=p.TestDash=false;
        p.Teleport(vine.Bottom+vine.Outward*.65f-Vector3.up*.9f);cam.SetOrbit(Mathf.Atan2(-vine.Outward.x,-vine.Outward.z)*Mathf.Rad2Deg,0);p.TestMove=Vector2.up;
        float until=Time.realtimeSinceStartup+.2f;while(Time.realtimeSinceStartup<until){if(shell.Paused)shell.SetPaused(false);await Task.Delay(15);}
        p.TestMove=Vector2.zero;p.ControlsEnabled=false;
        return new{p.Action,p.VineClimbing,position=p.transform.position.ToString()};
    }
}
