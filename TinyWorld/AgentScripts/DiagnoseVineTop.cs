using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;
public static class DiagnoseVineTop
{
    public static async Task<object> Run()
    {
        var p=Object.FindAnyObjectByType<WandererMotor>();var cam=Object.FindAnyObjectByType<WandererCamera>();var shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();p.TestInput=true;p.TestMove=Vector2.zero;p.TestDrop=p.TestJumpPressed=p.TestJumpHeld=p.TestSprint=p.TestCrouch=p.TestSwing=false;var result=new List<object>();
        foreach(var target in new[]{new Vector3(-59.11f,47.86f,257.77f),new Vector3(46.23f,63.05f,-.81f)})
        {
            var vine=ClimbableVine.Active.OrderBy(v=>Vector3.Distance(v.Top,target)).First();
            p.Teleport(vine.Bottom+vine.Outward*.75f-Vector3.up*1.1f);cam.SetOrbit(Mathf.Atan2(-vine.Outward.x,-vine.Outward.z)*Mathf.Rad2Deg,0);p.TestMove=Vector2.up;var trace=new List<object>();float until=Time.realtimeSinceStartup+3;
            while(Time.realtimeSinceStartup<until)
            {
                if(shell.Paused)shell.SetPaused(false);if(p.Traversing)p.TestMove=Vector2.zero;
                trace.Add(new{p.Action,p.Hanging,p.Traversing,p.VineClimbing,p.Grounded,p.Crouched,position=p.transform.position.ToString(),end=typeof(WandererMotor).GetField("traverseEnd",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(p).ToString()});
                await Task.Delay(80);
            }
            result.Add(new{top=vine.Top.ToString(),normal=vine.Outward.ToString(),trace});
        }
        p.TestMove=Vector2.zero;p.TestInput=false;return result;
    }
}
