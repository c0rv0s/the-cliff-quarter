using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        // Curtain leaves are baked into district foliage. Recover each connected strand at its drawn location.
        static void WallIvy(Transform root)
        {
            var city = GameObject.Find("Cliff Quarter");
            var routes = Node(root,"Wall ivy",Vector3.zero);
            foreach (var filter in city.GetComponentsInChildren<MeshFilter>()) {
                if (filter.name != "Foliage · detail · smooth") continue;
                var mesh = filter.sharedMesh;
                var verts = mesh.vertices; var colors = mesh.colors; var tris = mesh.triangles;
                var parents = new int[verts.Length];
                for (int i=0;i<parents.Length;i++) parents[i]=i;
                int Root(int i) {
                    while (parents[i]!=i) { parents[i]=parents[parents[i]]; i=parents[i]; }
                    return i;
                }
                for (int i=0;i<tris.Length;i+=3) {
                    int a=tris[i]; if (Mathf.Abs(colors[a].a-.5f)>.005f) continue;
                    parents[Root(tris[i+1])]=Root(a); parents[Root(tris[i+2])]=Root(a);
                }
                var strands=new Dictionary<int,List<Vector3>>();
                for(int i=0;i<verts.Length;i++) {
                    if(Mathf.Abs(colors[i].a-.5f)>.005f) continue;
                    int id=Root(i);
                    if (!strands.TryGetValue(id,out var strand)) strands[id]=strand=new List<Vector3>(30);
                    strand.Add(filter.transform.TransformPoint(verts[i]));
                }
                foreach(var strand in strands.Values) {
                    if (strand.Count!=30) continue; // seven sides, four middle rings and two tips
                    var high=strand[0];var low=high;
                    foreach(var vertex in strand){if(vertex.y>high.y)high=vertex;if(vertex.y<low.y)low=vertex;}
                    if(high.y-low.y<.85f || Vector3.ProjectOnPlane(high-low,Vector3.up).sqrMagnitude>.012f)continue;
                    var face=Vector3.zero; var grip=high;
                    var probe=Vector3.Lerp(high,low,.26f);
                    float best=2;
                    for(int j=0;j<12;j++) {
                        var outward=Quaternion.Euler(0,j*30,0)*Vector3.forward;
                        if (!Physics.Raycast(probe+outward*1.9f,-outward,out var wall,3.7f,1)
                            || Mathf.Abs(wall.normal.y)>.76f || Vector3.Dot(wall.normal,outward)<.75f)continue;
                        var normal=Vector3.ProjectOnPlane(wall.normal,Vector3.up).normalized;
                        float offset=Vector3.Dot(probe-wall.point,normal);
                        float score=Mathf.Abs(offset-.35f);
                        if(score>=best)continue;
                        best=score;face=normal;grip=high+normal*(.13f-offset);
                    }
                    if(face==Vector3.zero)face=high.x<0?Vector3.right:Vector3.left;
                    var topProbe=grip-face*.55f+Vector3.up*1.5f;
                    if(Physics.Raycast(topProbe,Vector3.down,out var ledge,2.1f,1)&&ledge.normal.y>.65f)
                        grip.y=Mathf.Max(high.y,ledge.point.y);
                    var hold=Node(routes,"Ivy strand",grip).gameObject.AddComponent<ClimbableVine>();
                    hold.transform.rotation=Quaternion.LookRotation(face);hold.length=grip.y-low.y;hold.width=1.12f;
                }
            }
        }
    }
}
