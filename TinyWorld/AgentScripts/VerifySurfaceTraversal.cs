using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;

public static class VerifySurfaceTraversal
{
    static WandererMotor p;static WandererCamera cam;static WandererShell shell;static GameObject root;
    static Vector3 origin=new(-170,12,-40);static List<object> checks=new();
    static async Task Wait(int ms){float end=Time.realtimeSinceStartup+ms*.001f;while(Time.realtimeSinceStartup<end){if(shell.Paused)shell.SetPaused(false);await Task.Delay(15);}}
    static GameObject Box(Vector3 point,Vector3 size,int layer=0){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform);go.transform.position=origin+point;go.transform.localScale=size;go.layer=layer;Physics.SyncTransforms();return go;}
    static void Drive(Vector2 move,bool jump=false,bool drop=false){p.TestMove=move;p.TestJumpPressed=p.TestJumpHeld=jump;p.TestDrop=drop;}
    static async Task<bool> TopOut(ClimbableVine plant)
    {
        Drive(Vector2.up);float end=Time.realtimeSinceStartup+3;
        while(p.VineClimbing&&!p.Traversing&&Time.realtimeSinceStartup<end)await Wait(20);
        Drive(Vector2.zero);await Wait(900);
        return !p.VineClimbing&&!p.Hanging&&!p.Traversing&&p.Grounded&&p.transform.position.y>plant.Top.y;
    }
    static async Task Place(Vector3 pos){Drive(Vector2.zero);p.Teleport(origin+pos);cam.SetOrbit(0,0);await Wait(40);}
    static void Check(string test,bool passed)=>checks.Add(new{test,passed,p.Action,p.VineClimbing,p.Hanging,p.Grounded,position=p.transform.position.ToString(),stretch=p.Hand.Stretch});
    public static async Task<object> Run()
    {
        p=Object.FindAnyObjectByType<WandererMotor>();cam=Object.FindAnyObjectByType<WandererCamera>();shell=Object.FindAnyObjectByType<WandererShell>();shell.Begin();var spawn=p.transform.position;
        p.TestInput=true;p.TestSprint=p.TestSwing=p.TestDrop=p.TestCrouch=p.TestDash=p.TestFire=false;checks.Clear();root=new GameObject("Temporary surface traversal fixtures");
        try
        {
            Box(new Vector3(0,-.25f,0),new Vector3(25,.5f,25));await Place(new Vector3(0,.05f,-4));await Wait(150);
            bool fired=p.Hand.FireAt(p.Hand.Origin+Vector3.up*30);Check("empty sky shot launches",fired&&p.Hand.Flying);await Wait(450);Check("empty shot returns at reach limit",!p.Hand.Flying&&!p.Hand.Attached);
            p.TestFireHeld=true;
            var far=Box(new Vector3(0,2,8.7f),new Vector3(3,4,.2f));await Place(new Vector3(0,.05f,-4));p.Hand.FireAt(origin+new Vector3(0,1.3f,5));await Wait(350);Check("12.7 metre shot attaches",p.Hand.AttachedCollider==far.GetComponent<Collider>()&&p.Hand.maxReach==12.96f);
            await Place(new Vector3(0,.05f,-4));var near=Box(new Vector3(0,2,0),new Vector3(3,4,.2f));p.Hand.FireAt(origin+new Vector3(0,1.3f,5));await Wait(250);Check("first collision wins over aimed surface",p.Hand.AttachedCollider==near.GetComponent<Collider>());
            Object.DestroyImmediate(near);await Place(new Vector3(0,.05f,-4));p.Hand.FireAt(origin+new Vector3(0,1.3f,5));await Wait(35);near=Box(new Vector3(0,2,.5f),new Vector3(3,4,.2f));await Wait(250);Check("flight detects newly obstructed path",p.Hand.AttachedCollider==near.GetComponent<Collider>());Object.DestroyImmediate(near);
            await Place(new Vector3(0,.05f,-4));p.Hand.FireAt(p.Hand.Origin+Vector3.up*30);p.Hand.Release();Check("release cancels shot in flight",!p.Hand.Flying&&!p.Hand.Attached);
            var ceiling=Box(new Vector3(0,5,-4),new Vector3(3,.2f,3));p.Hand.FireAt(origin+new Vector3(0,5,-4));await Wait(200);Check("hand sticks to ceiling",p.Hand.AttachedCollider==ceiling.GetComponent<Collider>()&&p.Hand.Normal.y<-.8f);Object.DestroyImmediate(ceiling);
            await Place(new Vector3(0,2,-4));p.Hand.FireAt(origin+new Vector3(0,0,-4));await Wait(200);Check("hand sticks to floor",p.Hand.Attached&&p.Hand.Normal.y>.8f);
            var detail=Box(new Vector3(0,2,-1),new Vector3(2,4,.2f),8);await Place(new Vector3(0,.05f,-4));p.Hand.FireAt(origin+new Vector3(0,1.3f,-1));await Wait(200);Check("hand sticks to decorative surface",p.Hand.AttachedCollider==detail.GetComponent<Collider>());
            p.Hand.Release();Drive(Vector2.up);await Wait(850);Drive(Vector2.zero);Check("detail collider does not block walking",p.transform.position.z>origin.z-.5f);Object.DestroyImmediate(detail);Object.DestroyImmediate(far);
            Box(new Vector3(0,2.5f,1.4f),new Vector3(6,5,3.2f));
            var plant=new GameObject("Fixture climbing vine").AddComponent<ClimbableVine>();plant.transform.SetParent(root.transform);plant.transform.position=origin+new Vector3(0,4.7f,-.5f);plant.transform.forward=Vector3.back;plant.length=4;
            await Place(new Vector3(0,.05f,-1.1f));Drive(Vector2.up);await Wait(150);Drive(Vector2.zero);Check("grab hanging vine",p.VineClimbing);
            float y=p.transform.position.y;Drive(Vector2.up);await Wait(500);Drive(Vector2.zero);Check("climb vine upward",p.VineClimbing&&p.transform.position.y>y+1.1f);
            y=p.transform.position.y;await Wait(250);Check("vine holds without input",p.VineClimbing&&Mathf.Abs(p.transform.position.y-y)<.08f);
            Drive(Vector2.down);await Wait(250);Drive(Vector2.zero);Check("climb vine downward",p.VineClimbing&&p.transform.position.y<y-.5f);
            bool fixtureTop=await TopOut(plant);Check("pull from vine onto roof",fixtureTop);
            await Place(new Vector3(0,1,-1.1f));Drive(Vector2.up);await Wait(150);Drive(Vector2.zero,drop:true);await Wait(100);Drive(Vector2.zero);Check("drop from vine",!p.VineClimbing&&!p.Hanging);
            var pool=ClimbableVine.Active.Where(v=>v!=plant&&v.Top.y>8&&v.length>2.6f).ToArray();
            var cityVines=new[]{new Vector3(-59.11f,47.86f,257.77f),new Vector3(46.23f,63.05f,-.81f)}
                .Select(t=>pool.OrderBy(v=>Vector3.Distance(v.Top,t)).First()).Concat(pool).Distinct().ToArray();
            int caught=0,climbed=0;var examples=new List<object>();
            foreach(var v in cityVines)
            {
                if(caught>=6)break;
                Drive(Vector2.zero);p.Teleport(v.Bottom+v.Outward*.75f-Vector3.up*1.1f);cam.SetOrbit(Mathf.Atan2(-v.Outward.x,-v.Outward.z)*Mathf.Rad2Deg,0);Drive(Vector2.up);await Wait(150);
                if(!p.VineClimbing)continue;
                caught++;float before=p.transform.position.y;
                bool topped=await TopOut(v);bool rose=p.transform.position.y>before+1;
                if(rose&&topped)climbed++;examples.Add(new{top=v.Top.ToString(),rose,topped,position=p.transform.position.ToString(),p.Action});
            }
            checks.Add(new{test="six generated city vines climb and top out",passed=caught==6&&climbed==6,caught,climbed,examples});
        }
        finally{Drive(Vector2.zero);p.Teleport(spawn);p.TestFireHeld=false;p.TestInput=false;Object.DestroyImmediate(root);}
        return checks;
    }
}
