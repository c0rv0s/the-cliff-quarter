using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;

public static class VerifyLedgeClearance
{
    static WandererMotor player;
    static WandererCamera camera;
    static readonly List<object> results=new();
    static readonly Vector3 origin=new(-180,8,-50);
    static GameObject fixture;
    static void Box(Vector3 position,Vector3 size)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(fixture.transform);go.transform.position=origin+position;go.transform.localScale=size;
    }
    static void Drive(Vector2 move,bool jump=false)
    {player.TestMove=move;player.TestJumpPressed=jump;player.TestJumpHeld=jump;}
    static async Task Catch(float top,float front)
    {
        player.Teleport(origin+new Vector3(0,top-1.6f,front-.4f));camera.SetOrbit(0,10);
        Physics.SyncTransforms();Drive(Vector2.up);await Task.Delay(110);Drive(Vector2.zero);await Task.Delay(60);
    }
    static void Check(string test,bool passed)=>results.Add(new{test,passed,player.Action,position=player.transform.position.ToString()});
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Start Play mode first.");
        player=Object.FindAnyObjectByType<WandererMotor>();camera=Object.FindAnyObjectByType<WandererCamera>();
        Object.FindAnyObjectByType<WandererShell>().Begin();var start=player.transform.position;
        player.TestInput=true;player.TestSprint=player.TestDrop=player.TestSwing=player.TestDash=false;results.Clear();
        try
        {
            fixture=new GameObject("Temporary ledge regression fixtures");
            Box(new Vector3(0,2.5f,0),new Vector3(6,5,6));
            Box(new Vector3(0,5.1f,0),new Vector3(6.6f,.35f,6.6f));
            Box(new Vector3(0,5.34f,0),new Vector3(5.5f,.16f,5.5f));
            await Catch(5.275f,-3.3f);Check("catch overhanging cornice",player.Hanging);
            Drive(Vector2.zero,true);await Task.Delay(950);Drive(Vector2.zero);
            Check("mantle over raised roof inset",!player.Hanging&&!player.Traversing&&player.Grounded&&player.transform.position.y>origin.y+5.35f);
            await Catch(5.275f,-3.3f);await Task.Delay(400);
            Check("neutral input holds the ledge",player.Hanging);
            Drive(Vector2.up);await Task.Delay(1000);Drive(Vector2.zero);
            Check("forward input climbs from hang",!player.Hanging&&!player.Traversing&&player.Grounded&&player.transform.position.y>origin.y+5.35f);
            Box(new Vector3(0,5.65f,-3.2f),new Vector3(6.6f,.75f,.2f));
            await Catch(6.025f,-3.3f);Check("catch low parapet",player.Hanging);
            Drive(Vector2.zero,true);await Task.Delay(950);Drive(Vector2.zero);
            Check("mantle past parapet onto lower roof",!player.Hanging&&!player.Traversing&&player.Grounded&&player.transform.position.z>origin.z-2.7f);
            Box(new Vector3(0,7.05f,-2.2f),new Vector3(7,.3f,4));
            await Catch(6.025f,-3.3f);Drive(Vector2.zero,true);await Task.Delay(950);Drive(Vector2.zero);
            Check("retreat clears projecting ceiling edge",!player.Hanging&&!player.Traversing&&player.Grounded&&player.transform.position.y>origin.y+7.2f);
            Box(new Vector3(0,7.05f,-2.2f),new Vector3(7,.3f,8));
            await Catch(6.025f,-3.3f);Check("catch below obstruction",player.Hanging);
            Drive(Vector2.zero,true);await Task.Delay(850);Drive(Vector2.zero);
            Check("blocked ceiling prevents clipping",player.Hanging&&!player.Traversing);
            Drive(Vector2.down,true);await Task.Delay(180);Drive(Vector2.zero);
            Check("back jump escapes blocked hang",!player.Hanging&&player.Velocity.z<0&&player.transform.position.z<origin.z-4.0f&&player.transform.position.z>origin.z-4.8f);
        }
        finally
        {
            if(fixture)Object.DestroyImmediate(fixture);
            Drive(Vector2.zero);player.Teleport(start);player.TestInput=false;
        }
        return results;
    }
}
