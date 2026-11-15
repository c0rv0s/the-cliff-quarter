using System.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;
using TinyWorld;
using Object=UnityEngine.Object;
public static class CaptureCliffViews
{
    public static async Task<object> Stage(string view)
    {
        var p=Object.FindAnyObjectByType<WandererMotor>();var orbit=Object.FindAnyObjectByType<WandererCamera>();
        var shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();p.TestInput=true;p.TestMove=Vector2.zero;p.TestSwing=p.TestDrop=p.TestJumpHeld=false;
        var cam=Camera.main;var brain=cam.GetComponent<CinemachineBrain>();brain.enabled=true;
        var canvas=shell.GetComponentInChildren<Canvas>();canvas.enabled=true;
        if(view=="river")
        {
            p.ControlsEnabled=false;canvas.enabled=false;brain.enabled=false;
            cam.transform.position=new Vector3(10,24,-36);cam.transform.LookAt(new Vector3(0,42,148));cam.fieldOfView=64;
        }
        else if(view=="crown")
        {
            var route=GameObject.Find("Stair route -1 2 118").transform;
            p.Teleport(route.Find("B").position+Vector3.up*.2f);orbit.SetOrbit(108,18);await Task.Delay(300);p.ControlsEnabled=false;
        }
        else if(view=="swing")
        {
            var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
            p.Teleport(new Vector3(court.x-1,court.y+9.5f,42));orbit.SetOrbit(0,3);p.TestMove=Vector2.up;p.TestSwing=true;
            await Task.Delay(850);p.ControlsEnabled=false;
        }
        else
        {
            var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
            p.Teleport(new Vector3(court.x-1,court.y+.15f,23.5f));orbit.SetOrbit(20,3);await Task.Delay(250);p.ControlsEnabled=false;
        }
        await Task.Delay(100);
        return new{view,p.Action,position=p.transform.position.ToString(),camera=cam.transform.position.ToString()};
    }
}
