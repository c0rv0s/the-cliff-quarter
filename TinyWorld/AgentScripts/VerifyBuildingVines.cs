using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;
public static class VerifyBuildingVines
{
    static WandererShell shell;
    static async Task Wait(int ms){float end=Time.realtimeSinceStartup+ms*.001f;while(Time.realtimeSinceStartup<end){if(shell.Paused)shell.SetPaused(false);await Task.Delay(15);}}
    public static async Task<object> Run()
    {
        var p=Object.FindAnyObjectByType<WandererMotor>();var cam=Object.FindAnyObjectByType<WandererCamera>();shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();var start=p.transform.position;p.TestInput=true;p.TestDrop=p.TestJumpHeld=p.TestJumpPressed=p.TestCrouch=p.TestSwing=p.TestSprint=false;var result=new List<object>();
        foreach(var v in ClimbableVine.Active.Where(v=>Mathf.Abs(v.Outward.z)>.12f).OrderBy(v=>v.name).Take(8))
        {
            p.TestMove=Vector2.zero;p.Teleport(v.Bottom+v.Outward*.7f-Vector3.up*.9f);cam.SetOrbit(Mathf.Atan2(-v.Outward.x,-v.Outward.z)*Mathf.Rad2Deg,0);p.TestMove=Vector2.up;await Wait(150);bool caught=p.VineClimbing;float end=Time.realtimeSinceStartup+3;
            while(p.VineClimbing&&!p.Traversing&&Time.realtimeSinceStartup<end)await Wait(20);
            p.TestMove=Vector2.zero;await Wait(950);
            result.Add(new{vine=v.name,passed=caught&&!p.VineClimbing&&!p.Traversing&&!p.Hanging&&p.Grounded&&p.transform.position.y>v.Top.y, caught,top=v.Top.ToString(),normal=v.Outward.ToString(),position=p.transform.position.ToString(),p.Action});
        }
        p.TestMove=Vector2.zero;p.TestInput=false;p.Teleport(start);return result;
    }
}
