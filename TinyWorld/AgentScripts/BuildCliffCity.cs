using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using TinyWorld;
using Object = UnityEngine.Object;

public static class BuildCliffCity
{
    const string ScenePath = "Assets/Scenes/RiverCityCliffside.unity";
    const string GeoPath = "Assets/Geometry/CliffCity";
    static Material stone, warm, sand, clay, blue, gold, leaf, darkLeaf, water, wood, rock, paleRock, cloud;
    static Transform city, group;
    static Matrix4x4 frame = Matrix4x4.identity;
    static Dictionary<string, Batch> batches;
    static System.Random rng;
    static int meshId, buildings, anchors;
    static List<(Vector3 top,Vector3 outward,float length)> vines;
    static readonly float[] heights = { 4, 27, 55, 84 };
    static readonly float[] offsets = { 0, 9, 19, 29 };
    static readonly string[] districts = { "River Gates", "Saffron Ascent", "Olive Ward", "Crown Gardens" };
    static float R(float a, float b) => Mathf.Lerp(a,b,(float)rng.NextDouble());
    static float Center(float z) => Mathf.Sin(z * .017f) * 20 + Mathf.Sin(z * .039f + .4f) * 7;
    static float Width(float z) => 17 + Mathf.Sin(z * .023f + 1) * 5 + Mathf.Sin(z * .051f) * 2;
    static float Edge(int side, int tier, float z) => Center(z) + side * (Width(z) + offsets[tier] + Mathf.Sin(z*.025f+side)*6 + Mathf.Sin(z*.067f+side)*2 + (tier==0?0:Mathf.Sin(z*.037f+tier*1.9f+side)*3));
    static float Level(int side, int tier, float z) => heights[tier] + Mathf.Sin(z*.018f+tier*.8f+side*.7f)*(tier==0?1.15f:5+tier*2.5f) + Mathf.Sin(z*.043f+side)*.6f;
    static Vector3 Point(int side,int tier,float z,float outwards=3) => new(Edge(side,tier,z)+side*outwards,Level(side,tier,z),z);

    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty) throw new Exception("Save the open scene before building.");
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene("Assets/Scenes/RiverCity.unity");
        // The original baseline and compact playable remain intact.
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.StartsWith("River Quarter — visual") || root.name.StartsWith("Walkable routes") || root.name == "Cliff City" || root.name == "River water volume") Object.DestroyImmediate(root);
        if (AssetDatabase.IsValidFolder(GeoPath)) AssetDatabase.DeleteAsset(GeoPath);
        AssetDatabase.CreateFolder("Assets/Geometry", "CliffCity");
        AssetDatabase.Refresh();
        rng = new System.Random(731921); meshId = buildings = anchors = 0; vines=new();
        stone = Mat("Limestone"); sand = Mat("Sandstone"); clay = Mat("Terracotta"); blue = Mat("Deep blue shutters"); gold = Mat("Aged brass"); leaf = Mat("Olive foliage");
        warm = NewMat("Honey plaster",Color.Lerp(Palette.Limestone,Palette.Sandstone,.2f));
        rock = NewMat("Weathered cliff",Color.Lerp(Palette.Sandstone,Palette.Limestone,.3f));
        paleRock = NewMat("Pale cliff",Color.Lerp(Palette.Sandstone,Palette.Limestone,.52f));
        wood = NewMat("Cedar",Color.Lerp(Palette.Terracotta,Palette.Shadow,.45f));
        cloud = NewMat("Cloud limestone",Color.Lerp(Palette.Limestone,Palette.Sky,.2f));
        darkLeaf = NewMat("Cypress",Color.Lerp(Palette.Olive,Palette.Shadow,.35f));
        water = NewMat("Living turquoise",Palette.Water); water.shader = Shader.Find("TinyWorld/River");
        water.SetColor("_BaseColor",Color.Lerp(Palette.Water,Palette.Shadow,.18f)); water.SetColor("_Shallow",Color.Lerp(Palette.Water,Palette.Limestone,.3f));
        city = new GameObject("Cliff City").transform;
        for (int side=-1;side<=1;side+=2)
        {
            for (int tier=0;tier<4;tier++)
            {
                float end=342-tier*14+(side==1?12:0);
                for(int segment=0;segment<6;segment++)
                {
                    float z0=-32+segment*64, z1=Mathf.Min(z0+64,end);
                    if(z0>=end) break;
                    if(tier==2 && ((side<0 && segment==0)||(side>0 && segment==4)))continue;
                    if(tier==3 && ((side<0 && segment>=4)||(side>0 && segment<2)))continue;
                    StartGroup($"{(side<0?"West":"East")} {districts[tier]} {segment+1}");
                    Shelf(side,tier,z0,z1);
                    float z=z0+R(8,13);
                    while(z<z1-5)
                    {
                        float width=R(7,14), depth=R(6,11), h=R(6,16)+tier*1.3f;
                        bool parkourSlot=side<0 && tier==0 && z>18 && z<61;
                        bool stairSlot = new[]{-22f,118f,210f,296f}.Any(v=>Mathf.Abs(z-(v-14))<width*.5f+3 || Mathf.Abs(z-(v+14))<width*.5f+3) || parkourSlot;
                        if(!stairSlot)
                        {
                            var p=Point(side,tier,z,5.8f+depth*.32f);
                            float yaw= side<0 ? -90 : 90;
                            yaw+=R(-24,24);
                            Building(p,yaw,width,depth,h,rng.Next(0,9));
                            if(tier<3 && rng.NextDouble()<.53)
                            {
                                var back=Point(side,tier,z+R(-4,4),15);
                                back.y+=R(3,7);
                                Rock(back-Vector3.up*7,new Vector3(5,7,6),paleRock,9);
                                Building(back,yaw+R(-25,25),R(5,8),R(5,8),R(7,17),rng.Next(0,9));
                            }
                            if(rng.NextDouble()<.45)
                            {
                                var terrace=Point(side,tier,z-width*.65f,0);
                                Cylinder(terrace-Vector3.up*.45f,R(3,4.6f),.45f,stone,true,20);
                                Tree(terrace+Vector3.right*side,1,0);
                            }
                            if (rng.NextDouble()<.55) Tree(Point(side,tier,z-width*.55f,3.2f),R(.8f,1.25f),rng.Next(0,3));
                        }
                        else if(tier==0 && !parkourSlot) Market(Point(side,tier,z,8),side*90,R(2.3f,3.6f));
                        z+=width+R(2.2f,5.4f);
                    }
                    for(float zz=z0+4;zz<z1;zz+=R(12,22))
                    {
                        if(StairDistance(zz)>6 && !(side<0 && tier==0 && zz>18 && zz<61)) Planter(Point(side,tier,zz,1.4f),R(.6f,1));
                        if(tier>0 && rng.NextDouble()<.6) Cypress(Point(side,tier,zz,20),R(4,8));
                    }
                    FinishGroup();
                }
            }
            StartGroup(side<0?"West stair streets":"East stair streets");
            for(int tier=0;tier<3;tier++)
                foreach(float z in new[]{-22f,118f,210f,296f})
                {
                    if(!HasStreet(side,tier,z-14) || !HasStreet(side,tier+1,z+14))continue;
                    StairStreet(side,tier,z);
                }
            FinishGroup();
            StartGroup(side<0?"Western limestone escarpment":"Eastern limestone escarpment");
            CliffFace(side);
            for(int i=0;i<11;i++)
            {
                float z=-35+i*37+R(-9,9), h=R(100,143);
                float x=Center(z)+side*(Width(z)+61+R(0,15));
                Rock(new Vector3(x,-7,z),new Vector3(R(8,15),h,R(11,21)),paleRock,11);
                if(i%4==1)Pavilion(new Vector3(x,h-7,z),R(3,4),R(6,9),true);
            }
            FinishGroup();
        }

        StartGroup("River channel and coastal shelves");
        Box(new Vector3(0,-7,150),new Vector3(340,2,560),sand,true);
        Box(new Vector3(0,-.06f,150),new Vector3(390,.1f,570),water,false);
        for(int side=-1;side<=1;side+=2)
            for(int i=0;i<26;i++)
            {
                float z=-33+i*15+R(-4,4), x=Edge(side,0,z)-side*R(0,2);
                Rock(new Vector3(x,-4,z),new Vector3(R(2,4),R(4,9),R(2,5)),paleRock,7);
            }
        FinishGroup();
        StartGroup("River gates and cross-gorge routes");
        Bridge(-12,0,4.2f); Bridge(87,1,4.8f); Bridge(188,2,5.7f); Bridge(272,3,6.5f);
        Bridge(316,0,3.5f);
        FinishGroup();
        StartGroup("Shrine of the current");
        Rock(new Vector3(Center(118),-6,118),new Vector3(8,13,10),paleRock,11);
        frame=Matrix4x4.TRS(new Vector3(Center(118),6,118),Quaternion.identity,Vector3.one);
        Cylinder(Vector3.zero,7,.6f,stone,true,28);
        frame=Matrix4x4.identity;
        Pavilion(new Vector3(Center(118),6.6f,118),4.7f,7.5f,true);
        Stairs(new Vector3(Center(118),-.8f,100),new Vector3(Center(118),6.6f,114),3.2f);
        Tree(new Vector3(Center(118)-4,6.6f,118),1.1f,1);
        FinishGroup();
        StartGroup("Water stairs and moorings");
        for(int side=-1;side<=1;side+=2)
            foreach(float z in new[]{0f,48f,125f,201f,285f,332f})
            {
                var upper=Point(side,0,z,4);
                var lower=new Vector3(upper.x-side*8,-1.3f,z-10);
                Stairs(lower,upper,4);
                frame=Matrix4x4.TRS(lower+new Vector3(-side*3,1.55f,0),Quaternion.Euler(0,side*20,0),Vector3.one);
                Boat(); frame=Matrix4x4.identity;
            }
        FinishGroup();
        StartGroup("Saffron ascent parkour court");
        ParkourCourt();
        FinishGroup();
        StartGroup("Gorge buttresses and hanging gardens");
        for(int side=-1;side<=1;side+=2)
        {
            foreach(float z in new[]{24f,106f,179f,254f,328f})
            {
                if(side<0 && z==24)continue;
                float x=Edge(side,0,z)+side*R(0,4), h=R(12,30);
                Rock(new Vector3(x,-6,z),new Vector3(R(4,7),h,R(5,8)),paleRock,11);
                if(h<24)Pavilion(new Vector3(x,h-6,z),R(2,3),R(4,7),true);
            }
            for(int tier=0;tier<4;tier++)for(float z=-23;z<320;z+=R(7,14))
            {
                if(StairDistance(z)<12 || (side<0 && tier==0 && z>23 && z<57))continue;
                var p=Point(side,tier,z,.3f);
                var old=frame;frame=Matrix4x4.TRS(p,Quaternion.Euler(0,side*90,0),Vector3.one);
                Vine(Vector3.zero,R(1.5f,4));frame=old;
                if(rng.NextDouble()<.5) Tree(Point(side,tier,z,6.5f),R(.65f,1.1f),rng.Next(0,3));
            }
        }
        FinishGroup();
        StartGroup("Crown observatory");
        var crown=Point(-1,3,244,20);
        Pavilion(crown,9,15,true);
        for(int i=0;i<8;i++) Cypress(crown+new Vector3(Mathf.Cos(i*.78f)*13,0,Mathf.Sin(i*.78f)*13),R(7,10));
        FinishGroup();
        Physics.SyncTransforms();
        StartGroup("Hanging vine routes");
        int vineId=0;
        foreach(var request in vines)
        {
            if(!VineRoute(request.top,request.outward,request.length,out var top,out var outward,out var length))continue;
            frame=Matrix4x4.TRS(top,Quaternion.LookRotation(-outward),Vector3.one);
            EmitVine(length,vineId++);frame=Matrix4x4.identity;
        }
        FinishGroup();
        ConfigureScene();
        var volume = new GameObject("River water volume").AddComponent<RiverWaterVolume>();
        volume.transform.position=new Vector3(0,0,150); volume.size=new Vector3(390,20,570);
        var player=Object.FindAnyObjectByType<WandererMotor>();
        var start=GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        player.transform.position=new Vector3(start.x-1,start.y+.15f,23.5f);
        player.walkSpeed=5.6f; player.sprintSpeed=10.2f; player.jumpHeight=2.5f; player.ropeMaterial=gold;
        var orbit=Object.FindAnyObjectByType<WandererCamera>(); orbit.transform.position=player.transform.position+Vector3.up*1.25f; orbit.transform.rotation=Quaternion.Euler(3,20,0);
        var rig=Object.FindAnyObjectByType<CinemachineCamera>(); rig.Lens.FarClipPlane=720;rig.Lens.NearClipPlane=.18f;
        Camera.main.farClipPlane=720;Camera.main.nearClipPlane=.18f;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        Physics.SyncTransforms();
        EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        Debug.Log($"[City] {buildings} unique buildings, {anchors} swing anchors, {meshId} mesh batches");
        return new {scene=ScenePath,buildings,anchors,meshes=meshId,spawn=player.transform.position.ToString(),renderers=city.GetComponentsInChildren<Renderer>().Length};
    }
    static bool HasStreet(int side,int tier,float z)
    {
        int segment=Mathf.FloorToInt((z+32)/64);
        if(tier==2 && ((side<0 && segment==0)||(side>0 && segment==4)))return false;
        if(tier==3 && ((side<0 && segment>=4)||(side>0 && segment<2)))return false;
        return z>=-32 && z<=342-tier*14+(side==1?12:0);
    }
    static float StairDistance(float z) => new[]{-22f,118f,210f,296f}.Min(v=>Mathf.Abs(z-v));
    static void StartGroup(string name)
    {
        group=new GameObject(name).transform; group.SetParent(city,false); batches=new(); frame=Matrix4x4.identity;
    }
    static void FinishGroup()
    {
        foreach(var entry in batches.Values)
        {
            if(entry.v.Count==0) continue;
            var mesh=new Mesh {name=group.name+" "+entry.mat.name,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(entry.v); mesh.SetTriangles(entry.t,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path=GeoPath+"/City_"+(meshId++).ToString("D4")+".asset";
            mesh.UploadMeshData(false);
            AssetDatabase.CreateAsset(mesh,path);
            if(entry.mat!=water && !(group.name=="River channel and coastal shelves" && entry.mat==sand))
            {
                var serialized=new SerializedObject(mesh);
                serialized.FindProperty("m_PreBakeTriangleCollisionMesh").boolValue=true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var go=new GameObject(entry.mat.name+(entry.solid?" · solid":" · detail"),typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(group,false); go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=entry.mat; go.isStatic=true;
            if(entry.solid)
            {
                if(group.name=="River channel and coastal shelves" && entry.mat==sand)
                {
                    var box=go.AddComponent<BoxCollider>();box.center=new Vector3(0,-7,150);box.size=new Vector3(340,2,560);
                }
                else go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            else
            {
                go.GetComponent<MeshRenderer>().shadowCastingMode=entry.mat==water?ShadowCastingMode.Off:ShadowCastingMode.On;
                if(entry.mat!=water)
                {
                    var surface=new GameObject("Sticky surface");surface.layer=8;
                    surface.transform.SetParent(go.transform,false);surface.isStatic=true;
                    surface.AddComponent<MeshCollider>().sharedMesh=mesh;
                }
            }
        }
        frame=Matrix4x4.identity;
    }
    sealed class Batch { public List<Vector3> v=new(); public List<int> t=new(); public Material mat; public bool solid; }
    static Batch Get(Material mat,bool solid)
    {
        string key=mat.name+(solid?"S":"D");
        if(!batches.TryGetValue(key,out var b)) { b=new Batch{mat=mat,solid=solid}; batches[key]=b; } return b;
    }
    static void Tri(Vector3 a,Vector3 b,Vector3 c,Material mat,bool solid=false)
    {
        if(Vector3.Cross(b-a,c-a).sqrMagnitude<.000000001f)return;
        var batch=Get(mat,solid); int n=batch.v.Count;
        batch.v.Add(frame.MultiplyPoint3x4(a)); batch.v.Add(frame.MultiplyPoint3x4(b)); batch.v.Add(frame.MultiplyPoint3x4(c));
        batch.t.Add(n); batch.t.Add(n+1); batch.t.Add(n+2);
    }
    static void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material mat,bool solid=false)
    { Tri(a,b,c,mat,solid); Tri(a,c,d,mat,solid); }
    static void Box(Vector3 p,Vector3 size,Material mat,bool solid=false,bool bottom=true)
    {
        var s=size*.5f;
        var a=p+new Vector3(-s.x,-s.y,-s.z);var b=p+new Vector3(s.x,-s.y,-s.z);var c=p+new Vector3(s.x,-s.y,s.z);var d=p+new Vector3(-s.x,-s.y,s.z);
        var e=a+Vector3.up*size.y;var f=b+Vector3.up*size.y;var g=c+Vector3.up*size.y;var h=d+Vector3.up*size.y;
        Quad(a,e,f,b,mat,solid); Quad(b,f,g,c,mat,solid); Quad(c,g,h,d,mat,solid); Quad(d,h,e,a,mat,solid);
        Quad(e,h,g,f,mat,solid); if(bottom)Quad(a,b,c,d,mat,solid);
    }
    static void Cylinder(Vector3 bottom,float radius,float height,Material mat,bool solid=false,int cuts=16,float topRadius=-1)
    {
        if(topRadius<0)topRadius=radius;
        for(int i=0;i<cuts;i++)
        {
            float a=i*2*Mathf.PI/cuts,b=(i+1)*2*Mathf.PI/cuts;
            var p=bottom+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
            var q=bottom+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
            var u=bottom+new Vector3(Mathf.Cos(a)*topRadius,height,Mathf.Sin(a)*topRadius);
            var v=bottom+new Vector3(Mathf.Cos(b)*topRadius,height,Mathf.Sin(b)*topRadius);
            Quad(p,u,v,q,mat,solid); Tri(bottom+Vector3.up*height,v,u,mat,solid);Tri(bottom,p,q,mat,solid);
        }
    }
    static void Dome(Vector3 bottom,float radius,float height,Material mat,bool solid=false,int cuts=24)
    {
        for(int j=0;j<8;j++)
        {
            float a=j*Mathf.PI/16,b=(j+1)*Mathf.PI/16;
            for(int i=0;i<cuts;i++)
            {
                float x=i*Mathf.PI*2/cuts,y=(i+1)*Mathf.PI*2/cuts;
                Vector3 P(float angle,float ring)=>bottom+new Vector3(Mathf.Cos(angle)*Mathf.Cos(ring)*radius,Mathf.Sin(ring)*height,Mathf.Sin(angle)*Mathf.Cos(ring)*radius);
                Quad(P(x,a),P(x,b),P(y,b),P(y,a),mat,solid);
            }
        }
    }
    static void Ellipsoid(Vector3 p,Vector3 size,Material mat,int cuts)
    {
        for(int j=0;j<8;j++)for(int i=0;i<cuts;i++)
        {
            float a=-Mathf.PI*.5f+j*Mathf.PI/8,b=a+Mathf.PI/8;
            float x=i*Mathf.PI*2/cuts,y=(i+1)*Mathf.PI*2/cuts;
            Vector3 P(float t,float r)=>p+Vector3.Scale(new Vector3(Mathf.Cos(t)*Mathf.Cos(r),Mathf.Sin(r),Mathf.Sin(t)*Mathf.Cos(r)),size);
            Quad(P(x,a),P(x,b),P(y,b),P(y,a),mat);
        }
    }
    static void Arch(Vector3 p,float radius,float rise,float thickness,float depth,Material mat,bool solid=false,int cuts=20)
    {
        for(int i=0;i<cuts;i++)
        {
            float a=Mathf.PI*i/cuts,b=Mathf.PI*(i+1)/cuts;
            Vector3 P(float t,float outer,float z)=>p+new Vector3(Mathf.Cos(t)*(radius+outer),Mathf.Sin(t)*(rise+outer),z);
            var x=P(a,0,-depth*.5f);var y=P(b,0,-depth*.5f);var u=P(a,thickness,-depth*.5f);var v=P(b,thickness,-depth*.5f);
            var dz=Vector3.forward*depth;
            Quad(x,y,v,u,mat,solid);Quad(x+dz,u+dz,v+dz,y+dz,mat,solid);
            Quad(x,x+dz,y+dz,y,mat,solid);Quad(u,v,v+dz,u+dz,mat,solid);
        }
    }
    static void Beam(Vector3 a,Vector3 b,float width,Material mat,bool solid=false)
    {
        var previous=frame; var d=b-a;
        frame=frame*Matrix4x4.TRS((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,d.normalized),Vector3.one);
        Box(Vector3.zero,new Vector3(width,d.magnitude,width),mat,solid);frame=previous;
    }
    static void Rock(Vector3 p,Vector3 size,Material mat,int cuts)
    {
        const int levels=10;
        var rings=new Vector3[levels,cuts];float phase=R(0,6);
        float[] facets=new float[cuts];for(int i=0;i<cuts;i++)facets[i]=R(.74f,1.18f);
        for(int j=0;j<levels;j++) for(int i=0;i<cuts;i++)
        {
            float a=i*Mathf.PI*2/cuts;
            float t=j/(float)(levels-1), taper=1-t*.38f;
            float r=taper*facets[i]*R(.88f,1.1f);
            rings[j,i]=p+new Vector3(Mathf.Cos(a)*size.x*r+Mathf.Sin(j*.7f+phase)*size.x*.12f,t*size.y+(j==0?0:R(-.45f,.45f)),Mathf.Sin(a)*size.z*r);
        }
        for(int j=0;j<levels-1;j++)for(int i=0;i<cuts;i++)
        { int k=(i+1)%cuts; Quad(rings[j,i],rings[j+1,i],rings[j+1,k],rings[j,k],j%4==2?paleRock:mat,true); }
        for(int i=1;i<cuts-1;i++)Tri(rings[levels-1,0],rings[levels-1,i+1],rings[levels-1,i],mat,true);
    }

    static void CliffFace(int side)
    {
        const int zCuts=110, yCuts=19;
        var points=new Vector3[zCuts+1,yCuts+1];
        for(int i=0;i<=zCuts;i++)
        {
            float z=-50+i*4;
            float peak=102+Mathf.Sin(z*.029f+side)*18+Mathf.Sin(z*.081f)*8;
            for(int j=0;j<=yCuts;j++)
            {
                float t=j/(float)yCuts, y=-8+t*(peak+8);
                float line=Width(z)+8+Mathf.Max(0,y-4)*.34f+Mathf.Sin(z*.025f+side)*6+Mathf.Sin(z*.067f+side)*2;
                float fracture=Mathf.Sin(z*.24f+j*.55f+side)*1.8f+Mathf.Sin(z*.61f-j*.7f)*.75f;
                points[i,j]=new Vector3(Center(z)+side*(line+fracture),y+(j==0||j==yCuts?0:R(-1.4f,1.4f)),z+(i==0||i==zCuts?0:R(-1,1)));
            }
        }
        for(int i=0;i<zCuts;i++)for(int j=0;j<yCuts;j++)
        {
            var a=points[i,j];var b=points[i+1,j];var c=points[i+1,j+1];var d=points[i,j+1];
            var mat=((i+j*3)%11<3)?rock:paleRock;
            if(side>0)Quad(a,b,c,d,mat,true);else Quad(b,a,d,c,mat,true);
        }
        for(int i=0;i<zCuts;i++)
        {
            var a=points[i,yCuts];var b=points[i+1,yCuts];
            var c=b+new Vector3(side*55,-13,0);var d=a+new Vector3(side*55,-13,0);
            if(side>0)Quad(a,b,c,d,paleRock,true);else Quad(d,c,b,a,paleRock,true);
        }
    }
    static void Shelf(int side,int tier,float z0,float z1)
    {
        int cuts=Mathf.CeilToInt((z1-z0)/2.5f);
        for(int i=0;i<cuts;i++)
        {
            float a=Mathf.Lerp(z0,z1,i/(float)cuts),b=Mathf.Lerp(z0,z1,(i+1f)/cuts);
            float width= tier==0?11:6.5f+Mathf.Sin(a*.12f+tier)*.9f;
            Vector3 p=Point(side,tier,a,0),q=Point(side,tier,b,0),r=q+Vector3.right*side*width,s=p+Vector3.right*side*width;
            if(side>0)Quad(p,q,r,s,stone,true);else Quad(s,r,q,p,stone,true);
            Vector3 down=Vector3.down*.5f;
            if(side>0)Quad(s+down,r+down,q+down,p+down,stone,true);else Quad(p+down,q+down,r+down,s+down,stone,true);
            if(side>0)Quad(p+down,q+down,q,p,sand,true);else Quad(q+down,p+down,p,q,sand,true);
            if(i%2==0 && StairDistance(a)>16)
            {
                var railP=p+Vector3.right*side*.4f+Vector3.up*.65f;
                var railQ=q+Vector3.right*side*.4f+Vector3.up*.65f;
                Beam(railP,railQ,.18f,stone,true);Beam(p+Vector3.right*side*.4f,railP,.22f,stone,true);
            }
            if(i%7==0 && tier>0 && StairDistance(a)>16)
            {
                // Corbels hold short street shelves against the rock.
                Beam(p+new Vector3(side*5,-4,0),p+new Vector3(side*.5f,-.4f,0),.6f,sand,true);
                Vine(p+Vector3.right*side*.2f,R(2,4),Vector3.left*side);
            }
            if(i%2==0)Beam(p+Vector3.up*.012f+Vector3.right*side*.6f,p+Vector3.up*.012f+Vector3.right*side*(width-.2f),.022f,sand);
        }
    }
    static void Building(Vector3 foot,float yaw,float width,float depth,float height,int type)
    {
        if(IntersectsStair(foot,width,depth,height+6))return;
        buildings++; var previous=frame;frame=Matrix4x4.TRS(foot,Quaternion.Euler(0,yaw,0),Vector3.one);
        var plaster=rng.NextDouble()<.25?warm:stone;
        bool round=type==2 || type==5;
        if(round)
        {
            float radius=Mathf.Min(width,depth)*.53f;
            Cylinder(Vector3.zero,radius,height,plaster,true,12);
            Cylinder(new Vector3(0,height,0),radius+.3f,.4f,stone,true,16);
            if(type==2) { Dome(new Vector3(0,height+.4f,0),radius*.92f,radius*.55f,clay,true); Cylinder(new Vector3(0,height+radius*.55f+.4f,0),.23f,.8f,gold); }
            else PavilionLocal(new Vector3(0,height+.4f,0),radius*.7f,3.5f,false);
            for(int i=0;i<7;i++)
            {
                var old=frame;frame*=Matrix4x4.Rotate(Quaternion.Euler(0,i*360/7f,0));
                Opening(new Vector3(0,1.3f,-radius-.06f),1.2f,2.2f,plaster);
                if(height>9)Opening(new Vector3(0,5.5f,-radius-.06f),1.1f,2.1f,plaster);
                frame=old;
            }
        }
        else
        {
            Box(new Vector3(0,height*.5f,0),new Vector3(width,height,depth),plaster,true,false);
            Box(new Vector3(0,.16f,0),new Vector3(width+.5f,.5f,depth+.5f),sand,true);
            Box(new Vector3(0,height+.1f,0),new Vector3(width+.6f,.35f,depth+.6f),stone,true);
            Box(new Vector3(0,height+.34f,0),new Vector3(width-.5f,.16f,depth-.5f),clay,true);
            if(type==1 || type==6)
            {
                float upper=height*.38f;
                Box(new Vector3(width*.18f,height+upper*.5f+.45f,depth*.13f),new Vector3(width*.55f,upper,depth*.6f),plaster,true);
                Box(new Vector3(width*.18f,height+upper+.5f,depth*.13f),new Vector3(width*.6f,.28f,depth*.65f),stone,true);
                Dome(new Vector3(width*.18f,height+upper+.65f,depth*.13f),width*.23f,width*.18f,clay,true);
            }
            else if(type==3 || type==7)
            {
                PavilionLocal(new Vector3(-width*.18f,height+.44f,0),Mathf.Min(width,depth)*.29f,3.3f,true);
            }
            else if(type==4 || type==8)
            {
                var a=new Vector3(-width*.52f,height+.5f,-depth*.52f);var b=new Vector3(width*.52f,height+.5f,-depth*.52f);
                var c=new Vector3(width*.52f,height+.5f,depth*.52f);var d=new Vector3(-width*.52f,height+.5f,depth*.52f);
                var ridgeA=new Vector3(-width*.35f,height+2.2f,0);var ridgeB=new Vector3(width*.35f,height+2.2f,0);
                Quad(a,ridgeA,ridgeB,b,clay,true);Quad(c,ridgeB,ridgeA,d,clay,true);Tri(a,d,ridgeA,clay,true);Tri(b,ridgeB,c,clay,true);
            }
            // Side elevations keep corner houses from becoming blank boxes.
            for(int faceSide=-1;faceSide<=1;faceSide+=2)
            {
                var saved=frame;
                frame*=Matrix4x4.TRS(new Vector3(faceSide*width*.5f,0,0),Quaternion.Euler(0,faceSide*-90,0),Vector3.one);
                for(int j=0;j<2;j++)
                {
                    Opening(new Vector3((j-.5f)*depth*.42f,1.2f,-.055f),.8f,1.8f,plaster);
                    if(height>8) Opening(new Vector3((j-.5f)*depth*.42f,height*.62f,-.055f),.75f,1.6f,plaster);
                }
                frame=saved;
            }
            Box(new Vector3(0,height*.5f,0),new Vector3(width+.13f,.14f,depth+.13f),sand);
            Box(new Vector3(0,height+.65f,depth*.48f),new Vector3(width,.55f,.2f),stone,true);
            if(type!=4 && type!=8) Planter(new Vector3(-width*.32f,height+.45f,-depth*.25f),.7f);
            int count=Mathf.Max(2,Mathf.FloorToInt(width/3));
            for(int i=0;i<count;i++)
            {
                float x=Mathf.Lerp(-width*.36f,width*.36f,i/(float)(count-1));
                Opening(new Vector3(x,.45f,-depth*.5f-.06f),i==count/2?1.3f:.85f,i==count/2?2.45f:1.75f,plaster);
                if(height>7)Opening(new Vector3(x,height*.56f,-depth*.5f-.06f),.9f,1.7f,plaster);
            }
            float balconyY=height*.5f;
            Box(new Vector3(-width*.13f,balconyY,-depth*.5f-1),new Vector3(width*.66f,.35f,2.5f),stone,true);
            Rail(new Vector3(-width*.46f,balconyY+.2f,-depth*.5f-2.15f),new Vector3(width*.2f,balconyY+.2f,-depth*.5f-2.15f));
            Beam(new Vector3(-width*.35f,balconyY-1.7f,-depth*.5f),new Vector3(-width*.35f,balconyY-.15f,-depth*.5f-1.9f),.23f,wood);
            Beam(new Vector3(width*.13f,balconyY-1.7f,-depth*.5f),new Vector3(width*.13f,balconyY-.15f,-depth*.5f-1.9f),.23f,wood);
            if(type==0 || type==6) Arcade(new Vector3(0,0,-depth*.5f-2),width,3.3f,2.4f);
            if(type==3 || type==8) Awn(new Vector3(0,3.1f,-depth*.5f-.1f),width*.75f,2.2f);
            // Terraced lips form climb chains along a flank.
            int steps=Mathf.CeilToInt(height/1.8f);
            for(int i=0;i<steps;i++)
            {
                float y=Mathf.Min(height+.45f,(i+1)*1.75f);
                Box(new Vector3(width*.5f+.45f,y,-depth*.32f+(i%3)*1.5f),new Vector3(1.25f,.22f,1.8f),stone,true);
                Box(new Vector3(width*.5f+1.08f,y+.02f,-depth*.32f+(i%3)*1.5f),new Vector3(.04f,.08f,1.65f),gold);
            }
        }
        if(rng.NextDouble()<.65)
        {
            var front=new Vector3(-width*.3f,0,-depth*.52f-.3f);
            if(round)Vine(front.normalized*(Mathf.Min(width,depth)*.53f+.3f)+Vector3.up*(height+.4f),R(2.5f,5),front.normalized);
            else Vine(front+Vector3.up*height,R(2.5f,5));
        }
        frame=previous;
    }
    static void Opening(Vector3 p,float width,float height,Material trim)
    {
        Box(p+new Vector3(0,height*.5f,.02f),new Vector3(width,height,.08f),blue);
        // The shadowed arch head is a filled semicircle.
        for(int i=0;i<10;i++)
        {
            float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;
            Tri(p+Vector3.up*height,p+new Vector3(Mathf.Cos(a)*width*.5f,height+Mathf.Sin(a)*width*.5f,-.025f),p+new Vector3(Mathf.Cos(b)*width*.5f,height+Mathf.Sin(b)*width*.5f,-.025f),blue);
        }
        Arch(p+Vector3.up*height,width*.5f,width*.5f,.14f,.2f,trim);
        Box(p+new Vector3(-width*.5f-.07f,height*.5f,-.06f),new Vector3(.14f,height,.2f),sand);
        Box(p+new Vector3(width*.5f+.07f,height*.5f,-.06f),new Vector3(.14f,height,.2f),sand);
        Box(p+new Vector3(0,.04f,-.12f),new Vector3(width+.45f,.18f,.38f),stone);
        Box(p+new Vector3(0,height*.48f,-.045f),new Vector3(.055f,height*.86f,.06f),gold);
    }
    static void Rail(Vector3 a,Vector3 b)
    {
        Beam(a+Vector3.up*.82f,b+Vector3.up*.82f,.15f,stone,true);
        int n=Mathf.CeilToInt(Vector3.Distance(a,b)/.8f);
        for(int i=0;i<=n;i++)
        { var p=Vector3.Lerp(a,b,i/(float)n);Cylinder(p,.09f,.79f,sand,false,6,.07f); }
    }
    static void Arcade(Vector3 p,float width,float height,float depth)
    {
        int n=Mathf.Max(2,Mathf.RoundToInt(width/3.3f));float span=width/n;
        Box(p+new Vector3(0,height+span*.5f+.2f,0),new Vector3(width+.4f,.4f,depth),stone,true);
        for(int i=0;i<=n;i++)
        {
            var c=p+new Vector3(-width*.5f+i*span,0,0); Column(c,height,.22f);
            if(i<n)Arch(c+new Vector3(span*.5f,height,0),span*.5f-.2f,span*.48f,.22f,.5f,stone,true,14);
        }
    }
    static void Column(Vector3 p,float height,float radius)
    {
        Cylinder(p,radius*1.6f,.3f,sand,true,10);
        Cylinder(p+Vector3.up*.3f,radius,height-.55f,stone,true,10,radius*.85f);
        Box(p+Vector3.up*(height-.1f),new Vector3(radius*3,.3f,radius*3),stone);
    }
    static void Pavilion(Vector3 p,float radius,float height,bool dome)
    { var old=frame;frame=Matrix4x4.TRS(p,Quaternion.Euler(0,R(0,90),0),Vector3.one);PavilionLocal(Vector3.zero,radius,height,dome);frame=old; }
    static void PavilionLocal(Vector3 p,float radius,float height,bool dome)
    {
        Cylinder(p,radius+1,.4f,stone,true,24);Cylinder(p+Vector3.up*.4f,radius+.6f,.45f,sand,true,24);
        for(int i=0;i<10;i++)
        {
            float a=i*Mathf.PI/5;var c=p+new Vector3(Mathf.Cos(a)*radius,.85f,Mathf.Sin(a)*radius);
            Column(c,height-.85f,.27f);
        }
        Cylinder(p+Vector3.up*height,radius+.5f,.45f,stone,true,24);
        Cylinder(p+Vector3.up*(height+.45f),radius+.6f,.15f,gold,false,24);
        if(dome) {Dome(p+Vector3.up*(height+.6f),radius+.2f,radius*.62f,clay,true);Cylinder(p+Vector3.up*(height+.6f+radius*.62f),.2f,1.2f,gold);}
        else {Cylinder(p+Vector3.up*(height+.6f),radius*.85f,.35f,stone,true,24);}
    }
    static void Planter(Vector3 p,float radius)
    {
        Cylinder(p,radius,.65f,clay,true,10,radius*1.08f);
        Cylinder(p+Vector3.up*.65f,radius*1.12f,.12f,sand,false,10);
        Dome(p+Vector3.up*.74f,radius*.95f,.65f,leaf);
    }
    static void Tree(Vector3 p,float size,int type)
    {
        if(IntersectsStair(p,2*size,2*size,6*size))return;
        var old=frame;frame=Matrix4x4.TRS(p,Quaternion.Euler(0,R(0,360),0),Vector3.one*size);
        if(type==2) {Cypress(Vector3.zero,5);frame=old;return;}
        Cylinder(Vector3.zero,.85f,.6f,clay,true,12,1);
        Beam(new Vector3(0,.5f,0),new Vector3(.25f,3.5f,.2f),.25f,wood);
        if(type==1 || type==6)
        {
            Beam(new Vector3(.25f,3.5f,.2f),new Vector3(.6f,6,0),.18f,wood);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;var center=new Vector3(.6f,6,0);var end=center+new Vector3(Mathf.Cos(a)*3,-1,Mathf.Sin(a)*3);
                var mid=Vector3.Lerp(center,end,.5f)+Vector3.up*.7f;var side=new Vector3(-Mathf.Sin(a)*.55f,0,Mathf.Cos(a)*.55f);
                Quad(center,mid+side,end,mid-side,leaf);Quad(center,mid-side,end,mid+side,leaf);
            }
        }
        else
        {
            Beam(new Vector3(.1f,2,0),new Vector3(-1.3f,3.7f,.3f),.18f,wood);
            for(int i=0;i<5;i++)
            {
                float a=i*2.4f;var c=new Vector3(Mathf.Cos(a)*1.2f,3.3f+R(0,.9f),Mathf.Sin(a)*1);
                Ellipsoid(c,new Vector3(1.55f,R(1,1.5f),1.3f),i%2==0?leaf:darkLeaf,10);
            }
        }
        frame=old;
    }
    static void Cypress(Vector3 p,float height)
    {
        Cylinder(p,.16f,height*.6f,wood,false,7);
        for(int i=0;i<4;i++)Cylinder(p+Vector3.up*(height*.23f+i*height*.13f),height*.14f*(1-i*.18f),height*.38f,darkLeaf,false,9,.04f);
    }
    static void Vine(Vector3 p,float length,Vector3 outward=default)
    {
        if(outward==default)outward=Vector3.back;
        vines.Add((frame.MultiplyPoint3x4(p),frame.MultiplyVector(outward).normalized,length));
    }
    static bool VineRoom(Vector3 foot,float height)=>!Physics.CheckCapsule(foot+Vector3.up*.34f,foot+Vector3.up*(height-.3f),.3f,1,QueryTriggerInteraction.Ignore);
    static bool VineSweep(Vector3 from,Vector3 to,float height)
    {
        if(!VineRoom(from,height)||!VineRoom(to,height))return false;
        var d=to-from;
        return d.sqrMagnitude<.001f||!Physics.CapsuleCast(from+Vector3.up*.34f,from+Vector3.up*(height-.3f),.3f,d.normalized,d.magnitude,1,QueryTriggerInteraction.Ignore);
    }
    static bool VineRoute(Vector3 source,Vector3 facing,float requestedLength,out Vector3 top,out Vector3 outward,out float length)
    {
        top=source;outward=facing;length=requestedLength;
        var tangent=Vector3.Cross(Vector3.up,facing);
        foreach(float side in new[]{0f,.8f,-.8f,1.6f,-1.6f,3f,-3f,5f,-5f})foreach(float inset in new[]{.5f,1f,2f,3.5f})
        {
            var sample=source+tangent*side-facing*inset;
            if(!Physics.Raycast(sample+Vector3.up*3,Vector3.down,out var floor,6,1,QueryTriggerInteraction.Ignore)||floor.normal.y<.62f)continue;
            var outside=floor.point+facing*8-Vector3.up*.15f;
            if(!Physics.Raycast(outside,-facing,out var wall,8.5f,1,QueryTriggerInteraction.Ignore)||Mathf.Abs(wall.normal.y)>.3f||Vector3.Dot(wall.normal,facing)<.6f)continue;
            var normal=Vector3.ProjectOnPlane(wall.normal,Vector3.up).normalized;
            var edge=wall.point+Vector3.up*.15f;
            if(!Physics.Raycast(edge-normal*.12f+Vector3.up*.6f,Vector3.down,out var lip,1.4f,1,QueryTriggerInteraction.Ignore)||lip.normal.y<.62f)continue;
            edge.y=lip.point.y;
            if(Vector3.Distance(edge,source)>8)continue;
            if(!Physics.Raycast(edge-normal*.8f+Vector3.up*1.3f,Vector3.down,out var landing,2.6f,1,QueryTriggerInteraction.Ignore)||landing.normal.y<.62f)continue;
            var end=landing.point+Vector3.up*(.06f+.3f*(1/landing.normal.y-1));
            bool path=false;
            foreach(float height in new[]{1.8f,1.02f})
            {
                float lift=Mathf.Max(edge.y,end.y)+.35f;
                var high=edge+normal*.65f;high.y=lift;
                var across=new Vector3(end.x,lift,end.z);
                if(VineSweep(high,across,height)&&VineSweep(across,end,height)){path=true;break;}
            }
            if(!path)continue;
            for(float span=requestedLength;span>=1.2f;span-=.4f)
            {
                var low=edge+normal*.65f-Vector3.up*(span*6/7+1.4f);
                var high=edge+normal*.65f+Vector3.up*.4f;
                if(!VineSweep(low,high,1.8f))continue;
                top=edge+normal*.15f;outward=normal;length=span;return true;
            }
        }
        return false;
    }
    static void EmitVine(float length,int id)
    {
        var vine=new GameObject("Climbable hanging vine "+id).AddComponent<ClimbableVine>();
        vine.transform.SetParent(group,false);vine.transform.position=frame.MultiplyPoint3x4(Vector3.zero);
        vine.transform.rotation=Quaternion.LookRotation(frame.MultiplyVector(Vector3.back),Vector3.up);
        vine.length=length*6/7+.45f;
        for(int i=0;i<7;i++)
        {
            var v=new Vector3(Mathf.Sin(i*1.7f)*.4f,-i*length/7,-.1f);
            Ellipsoid(v,new Vector3(.5f,.55f,.34f),i%3==0?darkLeaf:leaf,7);
        }
    }
    static void Awn(Vector3 p,float width,float depth)
    {
        var a=p+new Vector3(-width*.5f,0,0);var b=p+new Vector3(width*.5f,0,0);var c=b+new Vector3(0,-.5f,-depth);var d=a+new Vector3(0,-.5f,-depth);
        Quad(a,b,c,d,clay);Quad(d,c,b,a,clay);
        Beam(d-Vector3.up*2.6f,d,.09f,wood);Beam(c-Vector3.up*2.6f,c,.09f,wood);Beam(d,c,.1f,gold);
    }
    static void Market(Vector3 p,float yaw,float width)
    {
        var old=frame;frame=Matrix4x4.TRS(p,Quaternion.Euler(0,yaw,0),Vector3.one);
        Box(new Vector3(0,.5f,0),new Vector3(width,1,1),wood,true);Awn(new Vector3(0,2.8f,.5f),width+.4f,2.4f);
        for(int i=0;i<3;i++)Cylinder(new Vector3(-width*.3f+i*width*.3f,1,0),.3f,.35f,clay,false,8,.38f);
        frame=old;
    }
    static void Stairs(Vector3 a,Vector3 b,float width)
    {
        var old=frame;var delta=b-a;var flat=new Vector3(delta.x,0,delta.z);
        int count=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(delta.y)/.23f));float tread=flat.magnitude/count;
        frame=Matrix4x4.TRS(a,Quaternion.LookRotation(flat),Vector3.one);
        for(int i=0;i<count;i++)
        {
            float y=delta.y*(i+1f)/count;
            Box(new Vector3(0,y-.14f,(i+.5f)*tread),new Vector3(width,.28f,tread+.025f),stone);
            if(i%5==0)Box(new Vector3(width*.5f-.18f,y+.18f,(i+.5f)*tread),new Vector3(.3f,.36f,tread),sand,true);
        }
        var stairFrame=frame;
        frame*=Matrix4x4.TRS(new Vector3(0,delta.y*.5f+.06f,flat.magnitude*.5f),Quaternion.Euler(-Mathf.Atan2(delta.y,flat.magnitude)*Mathf.Rad2Deg,0,0),Vector3.one);
        Box(Vector3.zero,new Vector3(width,.12f,delta.magnitude),stone,true);
        frame=stairFrame;
        for(int side=-1;side<=1;side+=2)
        {
            Beam(new Vector3(side*(width*.5f-.1f),-.4f,0),new Vector3(side*(width*.5f-.1f),delta.y-.4f,flat.magnitude),.38f,sand);
            Beam(new Vector3(side*(width*.5f-.12f),.7f,0),new Vector3(side*(width*.5f-.12f),delta.y+.7f,flat.magnitude),.12f,stone,true);
            for(int i=0;i<=count;i+=7)
            {
                float t=i/(float)count;var pos=new Vector3(side*(width*.5f-.12f),delta.y*t,flat.magnitude*t);
                Beam(pos,pos+Vector3.up*.7f,.14f,sand);
            }
        }
        frame=old;
    }
    static bool IntersectsStair(Vector3 foot,float width,float depth,float height)
    {
        float radius=Mathf.Max(width,depth)*.65f+1.8f;
        foreach(int side in new[]{-1,1})for(int tier=0;tier<3;tier++)foreach(float z in new[]{-22f,118f,210f,296f})
        {
            if(!HasStreet(side,tier,z-14)||!HasStreet(side,tier+1,z+14))continue;
            var a=Point(side,tier,z-14,3);var b=Point(side,tier+1,z+14,-1);b.y+=.14f;
            float rise=b.y-a.y;var mid=Vector3.Lerp(a,b,.5f);
            mid.z=b.z+Mathf.Max(18,rise*.9f);
            mid.x=(Edge(side,tier,mid.z)+Edge(side,tier+1,mid.z))*.5f-side*3.5f;
            var nodes=new[]{a,mid-Vector3.right*side*2.8f,mid+Vector3.right*side*2.8f,b};
            for(int leg=0;leg<3;leg++)for(int i=0;i<=20;i++)
            {
                var p=Vector3.Lerp(nodes[leg],nodes[leg+1],i/20f);
                if(p.y+2<foot.y || p.y>foot.y+height)continue;
                if(Mathf.Abs(p.x-foot.x)<radius && Mathf.Abs(p.z-foot.z)<radius)return true;
            }
        }
        return false;
    }
    static void StairStreet(int side,int tier,float z)
    {
        // Long switchbacks run along the face, with open landings at the turns.
        var a=Point(side,tier,z-14,3);
        var b=Point(side,tier+1,z+14,-1); b.y+=.14f;
        float rise=b.y-a.y;
        var mid=Vector3.Lerp(a,b,.5f);
        mid.x=Edge(side,tier,z+20)+side*3;
        mid.z=b.z+Mathf.Max(18,rise*.9f);
        mid.x=(Edge(side,tier,mid.z)+Edge(side,tier+1,mid.z))*.5f-side*3.5f;
        var turnIn=mid-Vector3.right*side*2.8f;
        var turnOut=mid+Vector3.right*side*2.8f;
        var route=new GameObject($"Stair route {side} {tier} {z}").transform;route.SetParent(city,false);
        foreach(var pair in new[]{("A",a),("Turn",turnIn),("Out",turnOut),("B",b)}){var node=new GameObject(pair.Item1).transform;node.SetParent(route,false);node.position=pair.Item2;}
        var firstEnd=turnIn-new Vector3(turnIn.x-a.x,0,turnIn.z-a.z).normalized*3f;
        Stairs(a,firstEnd,3.2f);
        Cylinder(turnIn+Vector3.down*.4f,3,.4f,stone,true,32);
        Cylinder(turnOut+Vector3.down*.4f,3,.4f,stone,true,32);
        Box(mid+Vector3.down*.2f,new Vector3(5.6f,.4f,4),stone,true);
        var travel=new Vector3(b.x-turnOut.x,0,b.z-turnOut.z).normalized;
        Stairs(turnOut+travel*3f,b-travel*3.6f,3.2f);
        Cylinder(b+Vector3.down*.4f,3.6f,.4f,stone,true,32);
        for(int i=1;i<9;i++)
        {
            var c=Vector3.Lerp(a,b,i/9f)+new Vector3(-side*2.6f,1,0);
            Box(c-Vector3.up*.15f,new Vector3(1.6f,.3f,2.1f),stone,true);
            Beam(c+new Vector3(-side*.65f,.02f,-.8f),c+new Vector3(-side*.65f,.02f,.8f),.07f,gold);
        }
        var anchorPos=mid+Vector3.up*7+Vector3.right*(-side*2);
        var mast=mid+new Vector3(side*7,0,4);
        Beam(mast,mast+Vector3.up*8,.3f,wood,true);
        Beam(mast+Vector3.up*8,anchorPos+Vector3.up*1.5f,.3f,wood,true);Anchor(anchorPos);
    }
    static void Bridge(float z,int tier,float width)
    {
        var a=Point(-1,tier,z,3);var b=Point(1,tier,z+8,3);
        float y=Mathf.Max(a.y,b.y)+.2f; a.y=b.y=y;
        var d=b-a;float length=new Vector2(d.x,d.z).magnitude;
        var old=frame;frame=Matrix4x4.TRS((a+b)*.5f,Quaternion.Euler(0,-Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg,0),Vector3.one);
        Box(new Vector3(0,-.35f,0),new Vector3(length+.8f,.7f,width),stone,true);
        float rise=Mathf.Min(y-1,length*.31f);
        Arch(new Vector3(0,-rise-.7f,0),length*.5f,rise,1.1f,width*.8f,stone,true,40);
        for(int side=-1;side<=1;side+=2)
        {
            Rail(new Vector3(-length*.5f,.03f,side*(width*.5f-.1f)),new Vector3(length*.5f,.03f,side*(width*.5f-.1f)));
            Arch(new Vector3(0,-rise-.65f,side*width*.42f),length*.5f,rise,.18f,.12f,gold,false,40);
        }
        for(int i=-1;i<=1;i++)
        {
            var c=new Vector3(i*length*.24f,-2,0);
            Anchor(frame.MultiplyPoint3x4(c));
        }
        frame=old;
        if(tier>0)
        {
            Pavilion(a+Vector3.left*4,3,7,false);Pavilion(b+Vector3.right*4,3,7,true);
        }
    }
    static void Anchor(Vector3 p)
    {
        var root=new GameObject("Brass swing ring "+(++anchors));root.transform.SetParent(city,false);root.transform.position=p;
        root.AddComponent<TraversalAnchor>();
        var old=frame;frame=Matrix4x4.TRS(p,Quaternion.identity,Vector3.one);
        // Open ring, readable against stone and sky.
        for(int i=0;i<16;i++)
        {
            float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
            Beam(new Vector3(Mathf.Cos(a)*.42f,Mathf.Sin(a)*.42f,0),new Vector3(Mathf.Cos(b)*.42f,Mathf.Sin(b)*.42f,0),.09f,gold);
        }
        Beam(new Vector3(0,.42f,0),new Vector3(0,1.6f,0),.06f,wood);frame=old;
    }
    static void Boat()
    {
        Box(new Vector3(0,0,0),new Vector3(1.4f,.4f,3.8f),wood);
        for(int side=-1;side<=1;side+=2)Box(new Vector3(side*.7f,.35f,0),new Vector3(.13f,.6f,3.8f),wood);
        for(int i=-1;i<=1;i++)Box(new Vector3(0,.45f,i),new Vector3(1.4f,.12f,.3f),sand);
        Beam(new Vector3(-1.4f,.8f,-.8f),new Vector3(1.4f,.8f,.8f),.08f,wood);
    }
    static void ParkourCourt()
    {
        // Dedicated alley off the lower west quay: walls, lips, launch and swing landing.
        Vector3 p=Point(-1,0,35,5);float y=p.y;
        Box(new Vector3(p.x-1.2f,y-.25f,34),new Vector3(4.5f,.5f,23),stone,true);
        Box(new Vector3(p.x-3,y+3,35),new Vector3(.7f,6,15),warm,true);
        Box(new Vector3(p.x+.7f,y+4.5f,37),new Vector3(.7f,9,12),stone,true);
        for(int side=-1;side<=1;side+=2)
        {
            var old=frame;frame=Matrix4x4.TRS(new Vector3(p.x+(side<0?-2.62f:.32f),y,35),Quaternion.Euler(0,side<0?-90:90,0),Vector3.one);
            for(int j=-1;j<=1;j++)Opening(new Vector3(j*3.5f,.8f,0),.75f,1.7f,stone);
            Box(new Vector3(0,5.5f,0),new Vector3(12,.24f,.3f),sand);
            frame=old;
        }
        for(int i=0;i<5;i++)
        {
            float top=y+1.65f*(i+1);
            Box(new Vector3(p.x-2.5f,top-.15f,30+i*2.1f),new Vector3(1.6f,.3f,2),stone,true);
            Box(new Vector3(p.x-1.67f,top-.03f,30+i*2.1f),new Vector3(.07f,.1f,1.7f),gold);
        }
        Box(new Vector3(p.x-1,y+.48f,26),new Vector3(2.4f,.96f,.65f),sand,true);
        var launch=new Vector3(p.x-1,y+9,42);
        Box(launch-Vector3.up*.2f,new Vector3(4,.4f,3),stone,true);
        var land=launch+new Vector3(0,0,12);
        Box(land-Vector3.up*.2f,new Vector3(4,.4f,4),stone,true);
        Beam(launch+new Vector3(-3,0,6),launch+new Vector3(-3,8,6),.35f,wood,true);
        Beam(launch+new Vector3(-3,8,6),launch+new Vector3(0,8,6),.3f,wood,true);
        Anchor(launch+new Vector3(0,6.5f,6));
        var marker=new GameObject("Route · Saffron wall and swing alley"); marker.transform.SetParent(city,false);marker.transform.position=p;
    }
    static void ConfigureScene()
    {
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        pipeline.shadowDistance=100;pipeline.shadowCascadeCount=2;pipeline.useSRPBatcher=true;EditorUtility.SetDirty(pipeline);
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=Color.Lerp(Palette.Sky,Palette.Limestone,.25f)*.85f;
        RenderSettings.ambientEquatorColor=Color.Lerp(Palette.Shadow,Palette.Sky,.25f)*.9f;
        RenderSettings.ambientGroundColor=Palette.Sandstone*.4f;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=Palette.Sky;
        RenderSettings.fogStartDistance=180;RenderSettings.fogEndDistance=620;
        var sun=RenderSettings.sun;sun.transform.rotation=Quaternion.Euler(47,-32,0);sun.intensity=1.65f;sun.shadowStrength=.78f;
        Camera.main.backgroundColor=Palette.Sky;
        var sky=NewMat("Cliff sky",Palette.Sky);sky.shader=Shader.Find("TinyWorld/CliffSky");
        sky.SetColor("_Sky",Color.Lerp(Palette.Sky,Palette.Water,.24f));
        sky.SetColor("_Horizon",Color.Lerp(Palette.Sky,Palette.Limestone,.45f));
        sky.SetColor("_Cloud",Palette.Limestone);
        RenderSettings.skybox=sky;Camera.main.clearFlags=CameraClearFlags.Skybox;
        // Preserve the previous scene's grading asset.
        var volume=Object.FindAnyObjectByType<Volume>();
        const string profilePath="Assets/Settings/CliffCityDaylight.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if(!profile)
        {
            profile=Object.Instantiate(volume.sharedProfile);profile.name="Cliff City daylight";
            profile.components=volume.sharedProfile.components.Select(c=>Object.Instantiate(c)).ToList();
            AssetDatabase.CreateAsset(profile,profilePath);foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);
        }
        volume.sharedProfile=profile;
        if(profile.TryGet<Vignette>(out var vignette))vignette.intensity.Override(.12f);
        if(profile.TryGet<ColorAdjustments>(out var color)){color.saturation.Override(5);color.contrast.Override(8);color.postExposure.Override(.15f);}
        EditorUtility.SetDirty(profile);
    }
    static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/"+name+".mat");
    static Material NewMat(string name,Color color)
    {
        string path="Assets/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.18f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
}
