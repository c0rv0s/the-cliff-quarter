using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object=UnityEngine.Object;
public static class DiagnoseTraversal
{
    public static async Task<object> Run()
    {
        var p=Object.FindAnyObjectByType<WandererMotor>();var cam=Object.FindAnyObjectByType<WandererCamera>();
        var court=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        p.TestInput=true;p.TestMove=Vector2.zero;p.TestSwing=p.TestDrop=p.TestJumpHeld=false;
        p.Teleport(new Vector3(court.x-1,court.y+.12f,24.8f));cam.SetOrbit(0,13);await Task.Delay(400);
        var foot=p.transform.position;
        bool wallHit=Physics.SphereCast(foot+Vector3.up*.6f,.15f,Vector3.forward,out var wall,.85f,1,QueryTriggerInteraction.Ignore);
        var over=wall.point-wall.normal*.5f;
        bool topHit=Physics.Raycast(new Vector3(over.x,foot.y+2.2f,over.z),Vector3.down,out var top,2.7f,1,QueryTriggerInteraction.Ignore);
        var vault=new{foot=foot.ToString(),p.Grounded,wallHit,wall=wall.point.ToString(),normal=wall.normal.ToString(),name=wall.collider?wall.collider.name:"none",topHit,top=top.point.ToString(),height=top.point.y-foot.y};
        p.Teleport(new Vector3(court.x-1,court.y+9.5f,42));p.ControlsEnabled=false;
        var hand=p.transform.position+Vector3.up*1.4f;
        var swing=TraversalAnchor.Active.OrderBy(a=>Vector3.Distance(a.transform.position,hand)).Take(5).Select(a=>{
            var d=a.transform.position-hand;bool hit=Physics.Raycast(hand,d.normalized,out var h,d.magnitude-.3f,1,QueryTriggerInteraction.Ignore);
            return new{name=a.name,position=a.transform.position.ToString(),distance=d.magnitude,blocked=hit,blocker=hit?h.collider.transform.parent.name+"/"+h.collider.name:"",point=h.point.ToString()};}).ToArray();
        p.ControlsEnabled=true;p.TestInput=false;return new{court=court.ToString(),vault,swing};
    }
}
