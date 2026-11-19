using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TinyWorld;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyRoofEdges
{
    static async Task Frame()
    {
        var shell=Object.FindAnyObjectByType<WandererShell>();if(shell.Paused)shell.SetPaused(false);
        int f=Time.frameCount;
        while(f==Time.frameCount)await Task.Yield();
    }

    // Locate a real front window directly below an exposed limestone cornice.
    public static object Windows()
    {
        var result=new List<float[]>();
        foreach(var f in GameObject.Find("Cliff Quarter").GetComponentsInChildren<MeshFilter>())
        {
            string district=f.transform.parent.name;
            if(f.name!="Carved limestone · solid"||!(district.StartsWith("East ")||district.StartsWith("West ")))continue;
            float sign=district.StartsWith("West ")?1:-1;
            var m=f.sharedMesh;var v=m.vertices;var tr=m.triangles;
            for(int k=0;k+5<tr.Length;k+=3)
            {
                var a=v[tr[k]];var b=v[tr[k+1]];var c=v[tr[k+2]];
                if(Mathf.Abs(a.y-b.y)>.001f||Mathf.Abs(c.y-a.y)>.001f||Vector3.Cross(b-a,c-a).y<=0)continue;
                var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                if(Mathf.Abs(bounds.size.x-.24f)>.012f||bounds.size.z<.95f||bounds.size.z>2)continue;
                var lip=bounds.center+Vector3.right*sign*bounds.extents.x;
                var n=Vector3.right*sign;
                // The uppermost sill may be inside an oriel or behind a projecting balcony.
                if(Physics.Raycast(lip+n*2.6f-Vector3.up*.055f,-n,out var face,2.8f,1) &&
                   Vector3.Dot(face.point-lip,n)>.09f)continue;
                if(Physics.Raycast(lip+n*.55f+Vector3.up*.85f,Vector3.down,1.45f,1))continue;
                var origin=lip+Vector3.up*4.52f+Vector3.right*sign*.08f;
                if(!Physics.Raycast(origin,Vector3.down,out var roof,3f,1,QueryTriggerInteraction.Ignore)||roof.collider!=f.GetComponent<Collider>()||roof.normal.y<.8f)continue;
                if(!Physics.Raycast(roof.point-Vector3.right*sign*1.55f+Vector3.up*.2f,Vector3.down,out var landing,.7f,1,QueryTriggerInteraction.Ignore))continue;
                if(Physics.Raycast(landing.point+Vector3.up*1.83f,Vector3.down,out var boot,2f,1) && Mathf.Abs(boot.point.y-landing.point.y)>.18f)continue;
                if(Physics.Raycast(landing.point+Vector3.up*1.1f+n*3,-n,out var wall,3.12f,1) && wall.collider.name.StartsWith("Plaster") && Vector3.Dot(wall.point-landing.point,n)>.25f)continue;
                if(Physics.CheckCapsule(landing.point+Vector3.up*.34f,landing.point+Vector3.up*1.53f,.27f,1))continue;
                if(result.Any(q=>Mathf.Abs(q[0]-lip.x)<1 && Mathf.Abs(q[2]-lip.z)<4))continue;
                result.Add(new[]{lip.x,lip.y,lip.z,sign,landing.point.y});
                break;
            }
        }
        return result;
    }

    public static async Task<object> Terrace()
    {
        var root=new GameObject("Roof sill and balustrade");
        Vector3 o=new(-166,20,-53);
        void Block(Vector3 center,Vector3 size){
            var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.transform.SetParent(root.transform);
            b.transform.position=o+center;b.transform.localScale=size;
        }
        try{
            Block(new Vector3(2,1.5f,0),new Vector3(4,3,7));
            Block(new Vector3(-.12f,-.07f,0),new Vector3(.24f,.14f,1.15f));
            Block(new Vector3(2,-1.7f,0),new Vector3(4,3.4f,7));
            Block(new Vector3(1.7f,3.15f,0),new Vector3(4.65f,.30f,7.3f));
            Block(new Vector3(.08f,3.5f,0),new Vector3(.30f,.40f,7));
            Block(new Vector3(.08f,4.52f,0),new Vector3(.17f,.17f,7));
            Physics.SyncTransforms();
            return await Climb(new[]{new[]{o.x-.24f,o.y,o.z,-1f,o.y+3.3f},new[]{o.x-.625f,o.y+3.3f,o.z,-1f,o.y+3.3f}});
        }
        finally{Object.DestroyImmediate(root);}
    }

    public static async Task<object> Climb(float[][] windows)
    {
        var motor=Object.FindAnyObjectByType<WandererMotor>();var cam=Object.FindAnyObjectByType<WandererCamera>();
        Object.FindAnyObjectByType<WandererShell>().Begin();Time.timeScale=1;
        var start=motor.transform.position;var answers=new List<object>();motor.TestInput=true;
        try
        {
            foreach(var v in windows)
            {
                Vector3 lip=new(v[0],v[1],v[2]);var normal=Vector3.right*v[3];
                cam.SetOrbit(-v[3]*90,8);await Frame();
                foreach(float distance in new[]{.40f,.72f,.96f}){
                    motor.Teleport(lip+normal*distance-Vector3.up*1.60f);Physics.SyncTransforms();motor.TestMove=Vector2.up;
                    for(int i=0;i<32&&!motor.Hanging;i++){if(i==4 && motor.Grounded)motor.TestJumpPressed=true;await Frame();}
                    if(motor.Hanging)break;
                }
                if(!motor.Hanging){
                    // Some windows open behind a lower parapet. Jump from its walkable cap.
                    foreach(float side in new[]{.36f,-.36f}) {
                        var probe=lip+normal*1.2f+Vector3.forward*side+Vector3.up*.1f;
                        if(!Physics.Raycast(probe,Vector3.down,out var floor,2f,1) || floor.normal.y<.7f)continue;
                        var foot=floor.point+Vector3.up*.08f;
                        if(Physics.CheckCapsule(foot+Vector3.up*.33f,foot+Vector3.up*1.51f,.28f,1))continue;
                        motor.TestMove=Vector2.zero;motor.Teleport(foot);await Frame();await Frame();
                        motor.TestMove=Vector2.up;motor.TestJumpPressed=true;
                        for(int i=0;i<48&&!motor.Hanging;i++)await Frame();
                        if(motor.Hanging)break;
                    }
                }
                bool caught=motor.Hanging;motor.TestMove=Vector2.zero;
                float furthest=0, rise=0;int grabs=motor.Grabs;
                for(int action=0;action<9&&motor.Hanging;action++)
                {
                    int before=motor.Grabs;motor.TestJumpPressed=true;
                    float until=Time.time+1.25f;
                    while(Time.time<until){
                        await Frame();
                        furthest=Mathf.Max(furthest,Vector3.Dot(motor.transform.position-lip,normal)-.4f);
                        rise=Mathf.Max(rise,motor.transform.position.y-(lip.y-1.65f));
                    }
                    if(motor.Hanging && before==motor.Grabs)break;
                }
                if(caught&&motor.Grounded&&motor.transform.position.y>=lip.y+.5f&&motor.transform.position.y<v[4]-.23f){
                    // Continue from an intermediate balcony using the ordinary grounded jump.
                    motor.TestMove=Vector2.up;motor.TestJumpPressed=true;
                    for(int i=0;i<75;i++){await Frame();if(motor.Grounded&&motor.transform.position.y>=v[4]-.23f)break;}
                    motor.TestMove=Vector2.zero;
                }
                answers.Add(new{at=lip.ToString("F2"),caught,additionalGrabs=motor.Grabs-grabs,furthest,rise,
                    pass=caught&&motor.Grounded&&!motor.Hanging&&motor.transform.position.y>=v[4]-.23f,stop=motor.transform.position.ToString("F2"),motor.Action});
            }
        }
        finally{motor.TestInput=false;motor.Teleport(start);}
        return answers;
    }
}
