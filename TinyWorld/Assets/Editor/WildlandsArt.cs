using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        const string WildMeshFile = Folder + "/Southern wildlands.asset";
        static Vector2 Chart(float u,float v) => new((u-475)*1.5f,-227-(v-78)*1.5f);
        static Vector2[] Outline(params (int x,int y)[] points)
        {
            var coast=new List<Vector2>();
            for(int i=0;i<points.Length;i++){
                Vector2 P(int offset){
                    var p=points[(i+offset+points.Length)%points.Length];return Chart(p.x,p.y);
                }
                var a=P(-1);var b=P(0);var c=P(1);var d=P(2);
                for(int j=0;j<5;j++){
                    float t=j*.2f;
                    coast.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
                }
            }
            return coast.ToArray();
        }
        static readonly Vector2[][] WildCoasts={
            Outline((139,565),(131,544),(151,517),(179,489),(195,455),(218,436),(226,417),(250,399),
                (248,341),(248,265),(265,251),(295,240),(318,207),(365,190),(397,171),(422,157),
                (455,157),(476,169),(458,177),(453,196),(488,198),(510,189),(544,161),(565,162),
                (605,177),(608,194),(587,202),(583,215),(587,251),(559,266),(524,266),(499,273),
                (488,290),(503,306),(545,326),(574,328),(595,312),(630,275),(653,262),(709,260),
                (725,244),(733,208),(732,179),(708,152),(688,122),(711,117),(748,129),(790,160),
                (818,188),(829,210),(817,231),(782,265),(751,303),(716,333),(689,358),(677,389),
                (677,419),(658,441),(614,477),(584,512),(556,536),(498,540),(472,545),(439,569),
                (410,582),(375,579),(350,566),(330,551),(283,541),(239,529),(201,526),(182,538)),
            Outline((164,269),(177,272),(195,265),(210,278),(212,309),(209,326),(199,324),(200,304),(180,290)),
            Outline((550,84),(592,84),(626,77),(650,82),(680,91),(662,99),(645,98),(621,92),(596,93),(562,91))
        };
        static readonly Vector2 RuinCenter=Chart(365,387);
        static readonly Vector2 MeadowCenter=Chart(343,468);
        static readonly Vector2[][] Trails={
            new[]{Chart(567,320),Chart(554,340),Chart(522,344),Chart(494,356),Chart(464,378),Chart(426,372),Chart(398,386),RuinCenter},
            new[]{Chart(426,372),Chart(418,404),Chart(390,428),Chart(367,454),MeadowCenter},
            new[]{RuinCenter,Chart(332,370),Chart(310,343),Chart(288,328),Chart(251,328)}
        };
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            var d=b-a;return (p-a-d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude)).sqrMagnitude;
        }
        static float CoastDistance(Vector2 p)
        {
            float best=float.MaxValue;bool land=false;
            foreach(var ring in WildCoasts){
                bool inside=false;var a=ring[ring.Length-1];
                foreach(var b in ring){
                    if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                    best=Mathf.Min(best,SegmentDistance(p,a,b));a=b;
                }
                land|=inside;
            }
            return Mathf.Sqrt(best)*(land?1:-1);
        }
        static float TrailDistance(Vector2 p)
        {
            float best=float.MaxValue;
            foreach(var path in Trails)for(int j=1;j<path.Length;j++)best=Mathf.Min(best,SegmentDistance(p,path[j-1],path[j]));
            return Mathf.Sqrt(best);
        }
        static float Meadow(Vector2 p)
        {
            var n=p-MeadowCenter;
            return new Vector2(n.x/96,n.y/65).magnitude+.13f*Mathf.Sin(p.y*.042f)+.09f*Mathf.Sin(p.x*.063f);
        }
        public const int WildlandsShiftSouth = 255;
        const float WildStep=5;
        static float WildHeight(Vector2 p,float coast)
        {
            float earth=6+24*Mathf.PerlinNoise(p.x*.0065f+18,p.y*.008f+12)+5*Mathf.PerlinNoise(p.x*.024f,p.y*.022f);
            float ruinBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,53,Vector2.Distance(p,RuinCenter)));
            earth=Mathf.Lerp(19,earth,ruinBlend);
            if(coast<0)return Mathf.Max(-10,coast*.32f);
            return Mathf.Lerp(Mathf.Min(4,coast*.18f),earth,Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,63,coast)));
        }
        static Mesh WildSurface()
        {
            const int nx=225,nz=166;const float x0=-550,z0=-1015;
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            var dry=Palette.Hex("BEA86D");var sand=Palette.Hex("E7CFA3");
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){
                var p=new Vector2(x0+x*WildStep,z0+z*WildStep);
                float coast=CoastDistance(p),h=WildHeight(p,coast);
                var forest=Color.Lerp(Palette.Hex("667A50"),Palette.Hex("969161"),Mathf.PerlinNoise(p.x*.036f,p.y*.033f));
                var soil=Color.Lerp(dry,forest,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1.28f,Meadow(p))));
                soil=Color.Lerp(sand,soil,Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,16,coast)));
                soil=Color.Lerp(Palette.Hex("C8B38B"),soil,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.7f,6,TrailDistance(p))));
                var color=soil.linear;color.a=0;colors.Add(color);vertices.Add(V(p.x,h,p.y));
            }
            for(int z=0;z<nz-1;z++)for(int x=0;x<nx-1;x++){
                int a=z*nx+x,b=a+nx;
                if(Mathf.Max(Mathf.Max(vertices[a].y,vertices[b].y),Mathf.Max(vertices[a+1].y,vertices[b+1].y)) < -9.6f)continue;
                triangles.Add(a);triangles.Add(b);triangles.Add(b+1);triangles.Add(a);triangles.Add(b+1);triangles.Add(a+1);
            }
            Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(WildMeshFile);
            if(!mesh){mesh=new Mesh{name="Low island, hooked bay and outer keys"};AssetDatabase.CreateAsset(mesh,WildMeshFile);}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
        static Transform WildMesh(Transform parent,string name,Vector3 foot,Mesh mesh,Material material)
        {
            var at=Node(parent,name,foot);
            at.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;at.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            return at;
        }
        static float TreeHeightFactor(int kind,float yaw)
        {
            float age=Mathf.Repeat(yaw*.037f,1);
            return kind switch {
                0 or 5 => Mathf.Lerp(.76f,2.05f,age*age),
                4 => Mathf.Lerp(.68f,1.28f,age),
                2 => Mathf.Lerp(.72f,1.52f,age),
                _ => Mathf.Lerp(.62f,1.62f,age)
            };
        }
        static void Wildlands(Transform root)
        {
            var island=Node(root,"Southern wildlands",Vector3.zero);
            var stone=Mat("Shutters");var foliage=Mat("Foliage");
            var land=WildMesh(island,"Hooked bay and outer keys",Vector3.zero,WildSurface(),stone);
            var ground=RockCollision(land);
            Vector3 Floor(Vector2 p){
                if(ground.Raycast(new Ray(V(p.x,80,p.y),Vector3.down),out var hit,96))return hit.point;
                return V(p.x,-10,p.y);
            }
            var trees=new Mesh[6];
            for(int i=0;i<trees.Length;i++)trees[i]=WildTree(i).Bake(new[]{"Kapok","Strangler fig","Coastal fan palm","Broadleaf fig","Umbrella thorn","Rain tree"}[i],WildMeshFile);
            var fern=Understory(false).Bake("Fern and heliconia",WildMeshFile);
            var grass=Understory(true).Bake("Savannah tussocks",WildMeshFile);
            var random=new System.Random(79521);
            float Between(float low,float high)=>Mathf.Lerp(low,high,(float)random.NextDouble());
            var groves=new Dictionary<Vector2Int,Transform>();
            Transform Grove(Vector2 p){
                var key=Vector2Int.FloorToInt(p/78);
                if(!groves.TryGetValue(key,out var grove)){grove=Node(island,"Grove "+key.x+" "+key.y,Vector3.zero);groves.Add(key,grove);}
                return grove;
            }
            var cats=new[]{Chart(494,365),Chart(383,374),Chart(308,350),Chart(435,406)};
            Transform Plant(Transform grove,Vector3 foot,int kind,float size,float yaw){
                var t=WildMesh(grove,trees[kind].name,foot-Vector3.up*.18f,trees[kind],foliage);
                t.localScale=new Vector3(size,size*TreeHeightFactor(kind,yaw),size);t.localRotation=Quaternion.Euler(0,yaw,0);
                var trunk=t.gameObject.AddComponent<CapsuleCollider>();trunk.radius=kind==2?.36f:.56f;
                trunk.height=8.5f;trunk.center=Vector3.up*3.95f;
                return t;
            }
            for(float z=-1002;z< -215;z+=11.7f)for(float x=-527;x<545;x+=11.7f){
                var p=new Vector2(x+Between(-4.8f,4.8f),z+Between(-4.8f,4.8f));
                var foot=Floor(p);
                if(foot.y<.8f || Vector2.Distance(p,RuinCenter)<25 || TrailDistance(p)<4.4f)continue;
                bool savannah=Meadow(p)<1;
                var grove=Grove(p);
                if(random.NextDouble()<(savannah?.12:.89) && !Array.Exists(cats,q=>(p-q).sqrMagnitude<75)){
                    int kind=savannah?4:foot.y<4.2f?2:random.Next(0,4);
                    if(!savannah && kind==2 && foot.y>7 && random.Next(3)>0)kind=5;
                    Plant(grove,foot,kind,Between(.74f,1.42f),Between(0,360));
                }
                if(random.NextDouble()<.74){
                    var floor=Floor(p+new Vector2(Between(-3,3),Between(-3,3)));
                    if(floor.y<.9f)continue;
                    var cover=WildMesh(grove,savannah?"Golden grass":"Forest understory",floor-Vector3.up*.08f,savannah?grass:fern,foliage);
                    cover.localRotation=Quaternion.Euler(0,Between(0,360),0);cover.localScale=Vector3.one*Between(.67f,1.37f);
                }
            }
            // The cats lie on actual boughs above the footpaths.
            var catMesh=CanopyCat().Bake("Spotted canopy cat",WildMeshFile);
            for(int j=0;j<cats.Length;j++){
                var tree=Plant(Grove(cats[j]),Floor(cats[j]),0,1.1f,j*89+16);
                var perch=Node(tree,"Cat bough",V(2.7f,5.35f,0));
                perch.localScale=new Vector3(1,tree.localScale.x/tree.localScale.y,1);
                Solid(perch,V(0,-.26f,0),V(4,.46f,.82f));
                var cat=WildMesh(perch,"Resting wild cat",Vector3.zero,catMesh,stone);
                cat.localRotation=Quaternion.Euler(0,90,0);
                cat.gameObject.AddComponent<WildCatIdle>().phase=j*1.76f;
            }
            var ibex=GrazingAnimal(true).Bake("Spiral-horn antelope",WildMeshFile);
            var boar=GrazingAnimal(false).Bake("Forest boar",WildMeshFile);
            void HerdAt(string name,Vector2 p,Mesh species,int count,float radius){
                var herd=Node(island,name,Floor(p)).gameObject.AddComponent<Livestock>();
                herd.mesh=species;herd.material=stone;herd.radius=radius;herd.count=count;herd.walkSpeed=.85f;herd.skittish=true;
            }
            HerdAt("Savannah antelope",MeadowCenter,ibex,7,15);
            HerdAt("Boars in the fig grove",Chart(410,407),boar,4,8);
            HerdAt("Western boars",Chart(301,365),boar,3,9);
            BuildRuinDistrict(island,ground);
            island.localPosition = Vector3.back * WildlandsShiftSouth;
        }
    }
}
