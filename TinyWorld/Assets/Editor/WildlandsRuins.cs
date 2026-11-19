using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static readonly Vector2 SunTemple = new(14,-731), JaguarTemple = new(-228,-565);
        static readonly Vector2 BallCourt = new(-101,-726);
        static readonly Vector2[][] Sacbe = {
            new Vector2[]{new(116,-617),new(85,-632),new(47,-645),new(14,-671)},
            new Vector2[]{new(14,-671),new(-43,-652),new(-100,-665),new(-144,-688)},
            new Vector2[]{new(-144,-688),new(-183,-669),new(-206,-642),new(-228,-615)},
            new Vector2[]{new(-144,-688),new(-108,-701),new(-101,-708)},
            new Vector2[]{new(-183,-669),new(-236,-697),new(-279,-728)}
        };
        static readonly Color RuinStone=Palette.Hex("BCA888"), PaleStone=Palette.Hex("DED0AD"),
            ShadowStone=Palette.Hex("8A8069"), RuinMoss=Palette.Hex("77824D"),
            RuinJade=Palette.Hex("477C70"), RedPlaster=Palette.Hex("AD634A");
        const string RuinDistrict="City of the Two Suns";

        sealed class RuinSector
        {
            public readonly QuarterForms solid=new(RuinStone), details=new(RuinMoss);
            public readonly List<Rect> clearings=new();
            public readonly Func<Vector2, Vector3> ground;
            public RuinSector(Func<Vector2, Vector3> ground) { this.ground=ground; }
            public void Foundation(Vector3 center,Vector3 size) {
                float top=center.y+size.y*.5f;
                float bottom=FoundationBottom(ground,center,size);
                center.y=(top+bottom)*.5f; size.y=top-bottom;
                solid.Block(center,size);
            }
        }
        // Sample the whole footprint, including the downhill corners. Keep the authored top elevation.
        static float FoundationBottom(Func<Vector2,Vector3> ground,Vector3 center,Vector3 size)
        {
            float bottom=center.y-size.y*.5f;
            int nx=Mathf.Max(1,Mathf.CeilToInt(size.x/2)), nz=Mathf.Max(1,Mathf.CeilToInt(size.z/2));
            for(int x=0;x<=nx;x++)for(int z=0;z<=nz;z++){
                var point=new Vector2(center.x+size.x*(x/(float)nx-.5f),center.z+size.z*(z/(float)nz-.5f));
                bottom=Mathf.Min(bottom,ground(point).y-.65f);
            }
            return bottom;
        }

        static Rect Footprint(Vector2 p,float rx,float rz) => Rect.MinMaxRect(p.x-rx,p.y-rz,p.x+rx,p.y+rz);

        static void BuildRuinDistrict(Transform island,MeshCollider ground)
        {
            var district=Node(island,RuinDistrict,Vector3.zero);
            var sectors=new Dictionary<Vector2Int,RuinSector>();
            var clearings=new List<Rect>();
            Vector3 Ground(Vector2 p){
                var ray=new Ray(island.TransformPoint(V(p.x,100,p.y)),Vector3.down);
                if(!ground.Raycast(ray,out var hit,130))return V(p.x,-10,p.y);
                return island.InverseTransformPoint(hit.point);
            }
            RuinSector Sector(Vector2 p){
                var key=Vector2Int.FloorToInt(p/80);
                if(!sectors.TryGetValue(key,out var sector))sectors.Add(key,sector=new RuinSector(Ground));
                return sector;
            }
            void BakeSite(string name,RuinSector site){
                var stone=site.solid.Bake(name+" stone",WildMeshFile);
                RockCollision(WildMesh(district,name,Vector3.zero,stone,Mat("Shutters")));
                WildMesh(district,name+" overgrowth",Vector3.zero,site.details.Bake(name+" detail",WildMeshFile),Mat("Foliage"));
                clearings.AddRange(site.clearings);
            }
            var oldCourt=island.Find("Court of the broken sun");
            if(oldCourt)Object.DestroyImmediate(oldCourt.gameObject);
            RuinedCourt(island,Ground(RuinCenter),Mat("Shutters"),Ground);
            if(Ground(SunTemple).y<5 || Ground(JaguarTemple).y<5)
                throw new InvalidOperationException("Pyramid sites must be on the wildlands terrain.");
            void Pyramid(string name,Vector2 site,float half,int levels,float rise,int front,bool jaguar){
                var s=new RuinSector(Ground);
                SteppedTemple(s,Ground(site),half,levels,rise,front,Ground(site+Vector2.up*front*(half+22)).y,jaguar);
                BakeSite(name,s);
            }
            Pyramid("Temple of the Rising Sun",SunTemple,38,9,4,1,false);
            Pyramid("Temple of the Jaguar",JaguarTemple,31,7,4.15f,-1,true);
            void Passage(Vector2 site,float half,int side,bool jaguar){
                var mouth=site+Vector2.right*(side*(half+2.4f));
                BurialChambers(district,Ground(mouth),jaguar);
                var approach=mouth+Vector2.right*(side*6);
                clearings.Add(Footprint(approach,9,10));
                clearings.Add(Footprint(approach+Vector2.up*(-side*half*.53f),4.6f,half*.55f));
                var stones=Sector(approach);
                for(int j=0;j<3;j++){
                    var at=approach+Vector2.up*(-side*(6+j*half*.39f))+Vector2.right*side*2.8f;
                    Stela(stones,Ground(at),j==0?3.1f:1.65f,j+(jaguar?5:0));
                }
            }
            Passage(SunTemple,38,-1,false);
            Passage(JaguarTemple,31,1,true);
            var ball=Sector(BallCourt);
            AncientBallcourt(ball,Ground(BallCourt));
            clearings.Add(Footprint(BallCourt,23,19));
            var protectedTrees=island.GetComponentsInChildren<WildCatIdle>(true)
                .Select(cat=>cat.transform.parent.parent.localPosition).Select(p=>new Vector2(p.x,p.z)).ToArray();
            float RoadDistance(Vector2 p){
                float best=float.MaxValue;
                foreach(var route in Sacbe)for(int i=1;i<route.Length;i++)
                    best=Mathf.Min(best,SegmentDistance(p,route[i-1],route[i]));
                return Mathf.Sqrt(best);
            }
            var rand=new System.Random(38127);
            float Noise()=>(float)rand.NextDouble();
            var monuments=new List<Rect>(clearings){Footprint(RuinCenter,24,21)};
            for(int row=0;row<11;row++)for(int col=0;col<16;col++){
                var p=new Vector2(-332+col*30+(row%2)*11+(Noise()-.5f)*10,-809+row*31+(Noise()-.5f)*11);
                var e=p-new Vector2(-107,-658);
                if(e.x*e.x/(235*235)+e.y*e.y/(153*153)>1 || CoastDistance(p)<15 || Meadow(p)<.91f ||
                    TrailDistance(p)<11 || RoadDistance(p)<12 || protectedTrees.Any(c=>(p-c).sqrMagnitude<195))continue;
                var footprint=Footprint(p,10,9);
                if(monuments.Any(r=>r.Overlaps(footprint)))continue;
                var site=Sector(p);
                OutlyingRuin(site,Ground(p),(col+row*3)%5,row*16+col);
                site.clearings.Add(footprint);
            }
            foreach(var route in Sacbe)for(int leg=1;leg<route.Length;leg++){
                var from=route[leg-1];var to=route[leg];var d=(to-from).normalized;
                var side=new Vector2(-d.y,d.x);
                int slabs=Mathf.CeilToInt(Vector2.Distance(from,to)/3.6f);
                for(int j=0;j<slabs;j++){
                    var a=Vector2.Lerp(from,to,j/(float)slabs);var b=Vector2.Lerp(from,to,(j+.96f)/slabs);
                    var center=(a+b)*.5f;var site=Sector(center);
                    if(j%9==6)continue;
                    float width=2.35f+(j%5)*.08f;
                    Vector3 At(Vector2 v)=>Ground(v)+Vector3.up*.13f;
                    site.solid.ink=(j%4==0)?RuinStone:PaleStone;
                    site.solid.Face(At(a+side*width),At(b+side*width),At(b-side*width),At(a-side*width));
                    if(j==1 || j==slabs-2){
                        var marker=center+side*(j%2==0?4:-4);
                        if(Vector2.Distance(marker,SunTemple+Vector2.up*60)>12 &&
                           Vector2.Distance(marker,JaguarTemple-Vector2.up*53)>12)
                            Stela(site,Ground(marker),2.8f+j%3,j);
                    }
                }
            }
            foreach(var pair in sectors)BakeSite("Lost wards "+pair.Key.x+" "+pair.Key.y,pair.Value);
            // Keep the jungle between buildings. Open the actual stairways, doorways and roads.
            foreach(Transform grove in island.Cast<Transform>().Where(t=>t.name.StartsWith("Grove "))){
                foreach(var plant in grove.Cast<Transform>().ToArray()){
                    if(plant.Find("Cat bough"))continue;
                    var p=new Vector2(plant.localPosition.x,plant.localPosition.z);
                    if(clearings.Any(r=>r.Contains(p)) || RoadDistance(p)<4)
                        Object.DestroyImmediate(plant.gameObject);
                }
            }
        }

        static void SteppedTemple(RuinSector site,Vector3 center,float half,int levels,float rise,int front,float landingY,bool jaguar)
        {
            var g=site.solid;var art=site.details;float baseY=center.y+1.1f, topY=levels*rise;
            Vector3 P(float x,float y,float z)=>V(center.x+x,baseY+y,center.z+z*front);
            void Block(QuarterForms mesh,float x,float y,float z,float w,float h,float depth,Color color){
                mesh.ink=color;mesh.Block(P(x,y,z),V(w,h,depth));
            }
            float summit=jaguar?8.6f:10.2f;
            float inset=(half-summit)/levels;
            g.ink=ShadowStone;site.Foundation(P(0,-6.6f,0),V(half*2+1.2f,13.2f,half*2+1.2f));
            for(int i=0;i<levels;i++){
                float outR=half-i*inset, upper=outR-1.1f, low=i*rise;
                for(int side=0;side<4;side++){
                    Vector3 Rim(float r,float along,float y){
                        var d=Quaternion.Euler(0,side*90,0)*V(along*r,0,r);
                        return center+V(d.x,baseY-center.y+y,d.z);
                    }
                    g.ink=Color.Lerp(RuinStone,PaleStone,(i%4)*.12f);
                    g.Face(Rim(outR,-1,low),Rim(outR,1,low),Rim(upper,1,low+rise),Rim(upper,-1,low+rise));
                    int bays=Mathf.FloorToInt(upper*.45f);
                    for(int j=0;j<bays;j++){
                        float t=-1+(j+.5f)*2/bays;
                        if((j+side+i)%5==0)continue;
                        var at=Rim(upper+.08f,t,low+rise-.62f);
                        art.ink=(j+side+i)%7==0?RuinMoss:ShadowStone;
                        art.Block(at,side%2==0?V(upper*1.65f/bays,.31f,.22f):V(.22f,.31f,upper*1.65f/bays));
                    }
                }
                Block(g,0,low+rise-.15f,0,upper*2+.45f,.3f,upper*2+.45f,i%3==0?ShadowStone:PaleStone);
            }
            // Continuous ceremonial stair with individual treads and a smooth walking surface.
            float toe=half+22,lip=summit+inset-1.1f+.225f, bottom=landingY-baseY-.18f, halfStair=jaguar?4.2f:5.3f;
            int steps=Mathf.CeilToInt((topY-bottom)/.18f);float tread=(toe-lip)/steps;
            for(int i=0;i<steps;i++){
                float t=(i+.5f)/steps, y=Mathf.Lerp(bottom,topY,t), z=toe-(i+.5f)*tread;
                Block(art,0,y-.17f,z,halfStair*2,.34f,tread,i%7==0?RuinStone:PaleStone);
            }
            var left=P(-halfStair,bottom-.13f,toe);var right=P(halfStair,bottom-.13f,toe);
            var ltop=P(-halfStair,topY-.13f,lip);var rtop=P(halfStair,topY-.13f,lip);
            g.ink=RuinStone;
            if(front>0)g.Face(left,right,rtop,ltop);else g.Face(right,left,ltop,rtop);
            foreach(int side in new[]{-1,1}){
                float x=side*(halfStair+.6f);
                // Filled cheek walls carry the projecting stair all the way down to the landing.
                float edge=side*(halfStair+.96f);
                g.ink=ShadowStone;
                var upper=P(edge,topY-.11f,lip);var lower=P(edge,bottom-.11f,toe);
                var cheekSize=V(1.92f,0,toe-lip);
                float footing=FoundationBottom(site.ground,P(x,bottom-4,(toe+lip)*.5f),cheekSize)-baseY;
                var toeFoot=P(edge,footing,toe);var hillFoot=P(edge,footing,lip);
                if(side*front>0)g.Face(lower,toeFoot,hillFoot,upper);else g.Face(upper,hillFoot,toeFoot,lower);
                for(int k=0;k<18;k++){
                    float t=(k+.5f)/18,z=Mathf.Lerp(toe,lip,t), y=Mathf.Lerp(bottom,topY,t);
                    Block(g,x,y-.75f,z,1.15f,2.65f,(toe-lip)/18+.02f,k%4==0?ShadowStone:RuinStone);
                }
                g.ink=ShadowStone;site.Foundation(P(x,bottom+.66f,toe+1.1f),V(2.1f,1.5f,3.6f));
                Block(art,x,bottom+1.47f,toe+1.45f,1.72f,.26f,2.4f,jaguar?RuinJade:RedPlaster);
            }
            // The final tier cap is the entire summit floor and meets the last tread.
            float w=jaguar?13:15,wall=jaguar?6.6f:7.6f, depth=jaguar?8.8f:10.4f;
            foreach(int side in new[]{-1,1}){
                Block(g,side*(w/2-.6f),topY+wall*.5f,-.3875f,1.2f,wall,depth-2.075f,RuinStone);
                Block(g,side*(w+4.2f)/4,topY+wall*.5f,depth*.5f-.8f,(w-4.2f)/2,wall,1.25f,PaleStone);
            }
            Block(g,0,topY+wall*.5f,-depth*.5f, w,wall,1.3f,RuinStone);
            // A stepped corbel closes the chamber above the open front.
            for(int i=0;i<4;i++){
                float y=topY+wall+(i+.5f)*.52f;
                foreach(int side in new[]{-1,1})Block(g,side*(w/2-1.05f-i*.6f),y,0,2.3f+i*1.15f,.52f,depth+.4f+i*.08f,PaleStone);
            }
            Block(g,0,topY+wall+2.53f,0,w+1,.9f,depth+1.5f,ShadowStone);
            for(int i=-3;i<=3;i++){
                float h=(jaguar?3.4f:5.8f)-Mathf.Abs(i)*.58f;
                Block(g,i*1.84f,topY+wall+2.5f+h*.5f,-1.8f,1.18f,h,1.36f,RuinStone);
            }
            Block(g,0,topY+wall+(jaguar?5.7f:7.7f),-1.8f,10.9f,.78f,1.53f,PaleStone);
            for(int side=-1;side<=1;side+=2)for(int j=0;j<6;j++){
                float x=side*(4.1f+.35f*(j%2));
                Block(art,x,topY+1.2f+j*.82f,depth*.5f-.05f,1.7f,.51f,.36f,j%3==0?RuinJade:RedPlaster);
            }
            // Broken plaster masks and sun discs distinguish the two sanctuaries from across the canopy.
            if(jaguar){
                foreach(int side in new[]{-1,1}){
                    float x=side*4.4f;
                    Block(art,x,topY+4.6f,depth*.5f+.2f,3.65f,2.8f,.84f,ShadowStone);
                    Block(art,x,topY+3.7f,depth*.5f+.8f,2.85f,1.18f,1.34f,RuinStone);
                    foreach(int eye in new[]{-1,1}){
                        Block(art,x+eye*.95f,topY+4.95f,depth*.5f+.7f,1.16f,.46f,.64f,RuinJade);
                        Block(art,x+eye*.85f,topY+3.02f,depth*.5f+1.16f,.49f,1.03f,.44f,PaleStone);
                    }
                }
            }else{
                var disk=P(0,topY+wall+4.9f,-.92f);
                art.ink=ShadowStone;art.Rod(disk,disk+Vector3.forward*.36f,2.44f,2.44f,16);
                art.ink=RedPlaster;art.Rod(disk+Vector3.forward*.38f,disk+Vector3.forward*.54f,1.78f,1.78f,16);
            }
            var weather=new System.Random(jaguar?7351:9083);
            float Noise()=>(float)weather.NextDouble();
            // Roots gather in sheltered corners, moss spreads across terraces, vines spill down the battered faces.
            for(int flank=0;flank<4;flank++){
                var occupied = new List<(int tier,float left,float right)>();
                var rotation=Quaternion.Euler(0,flank*90,0);
                var outward=rotation*Vector3.forward;var tangent=rotation*Vector3.right;
                for(int i=0;i<(jaguar?43:36);i++){
                    int level=weather.Next(levels);float rim=half-inset*level-.84f;
                    float patch=1.45f+Noise()*2.25f;
                    float along=(Noise()*2-1)*(rim-patch-2.25f);
                    float leftEdge=along-patch-.45f, rightEdge=along+patch+.45f;
                    if(occupied.Any(b=>b.tier==level && leftEdge<b.right && rightEdge>b.left))continue;
                    if(Vector3.Dot(outward,Vector3.forward*front)>.99f && Mathf.Abs(along)<halfStair+patch+1.2f)continue;
                    occupied.Add((level,leftEdge,rightEdge));
                    var ledge=center+Vector3.up*(baseY-center.y+(level+1)*rise+.045f)+tangent*along+outward*rim;

                    art.ink=Color.Lerp(RuinMoss,Palette.Hex("456745"),Noise());
                    // A low hummock gives each moss patch a silhouette, even viewed along the stone.
                    var ridge=ledge-outward*.65f+Vector3.up*.19f;
                    var edge=new[]{ledge-tangent*patch-outward*.95f,ledge-tangent*(patch+.3f)-outward*.19f,
                        ledge+outward*.08f,ledge+tangent*patch-outward*.28f,ledge+tangent*(patch*.64f)-outward*1.21f};
                    for(int k=0;k<edge.Length;k++) art.Face(ridge,edge[k],edge[(k+1)%edge.Length]);
                    for(int strand=0;strand<3;strand++){
                        float length=1.3f+Noise()*3.3f;
                        var start=ledge+tangent*((Noise()-.5f)*patch*1.5f);
                        for(int j=0;j<4;j++){
                            var next=start-Vector3.up*(length*.25f)+outward*(.22f+length*.05f)+tangent*((Noise()-.5f)*.55f);
                            art.ink=Palette.Hex("5E6541");
                            art.Face(start+tangent*.11f,start-tangent*.11f,next-tangent*.08f,next+tangent*.08f);
                            var tip=next+outward*.14f-Vector3.up*(.55f+Noise()*.7f);
                            art.ink=Color.Lerp(Palette.Olive,RuinMoss,Noise());
                            art.Silk(next+tangent*.16f,next-tangent*.17f,tip-tangent*(.23f+Noise()*.38f));
                            art.Silk(next-tangent*.13f,next+tangent*.25f,tip+tangent*(.29f+Noise()*.32f));start=next;
                        }
                    }
                    if(i%4==0){
                        var root=ledge-outward*.96f;
                        for(int k=0;k<5;k++)Frond(art,root,k*1.26f+i,1.8f+Noise()*1.1f,.53f+Noise(),RuinMoss);
                        art.wind=0;
                    }
                }
            }
            for(int j=0;j<(jaguar?4:2);j++){
                int level=Mathf.Min(levels-1,j*2+1); float rim=half-inset*level-1.8f, y=(level+1)*rise;
                var root=P(j%2==0?rim-.8f:-rim+1.3f,y,-rim+1.5f);
                var elbow=root+V(-.8f,3.2f,.65f);var crown=elbow+V(-.85f,2.5f,-.38f);
                art.ink=Palette.Hex("655642");art.Rod(root,elbow,.55f,.34f,7);art.Rod(elbow,crown,.37f,.18f,7);
                for(int k=0;k<4;k++){
                    var branch=crown+V(Mathf.Cos(k*1.57f)*2,1.3f,Mathf.Sin(k*1.57f)*2);
                    art.ink=Palette.Hex("655642");art.Rod(elbow,branch,.24f,.10f);
                    LeafCrown(art,branch,V(2.7f,1.6f,2.3f),Palette.Hex("617B4E"),k+j);
                    art.wind=0;
                }
            }
            var p=new Vector2(center.x,center.z);
            site.clearings.Add(Footprint(p,half+4,half+4));
            site.clearings.Add(Footprint(p+Vector2.up*(front*(half+11)),halfStair+4,14));
        }

        static void Drapery(QuarterForms art,Vector3 top,Vector3 outward,float length,int seed)
        {
            var last=top;
            for(int i=1;i<=4;i++){
                var next=top+outward*(i*.095f)+V(Mathf.Sin(seed+i)*.36f,-length*i/4,0);
                art.ink=ShadowStone;art.Rod(last,next,.076f,.06f,5);
                art.ink=i%2==0?RuinMoss:Palette.Olive;
                var edge=next+V(i%2==0?.43f:-.43f,-.1f,.12f);
                art.Silk(next,edge,next+Vector3.down*.65f);last=next;
            }
        }
        static void Stela(RuinSector s,Vector3 p,float height,int seed,bool onPlatform=false)
        {
            var g=s.solid;var art=s.details;
            g.ink=ShadowStone;
            if(onPlatform)g.Block(p+Vector3.up*.04f,V(2.7f,1.12f,2.1f));
            else s.Foundation(p+Vector3.up*.04f,V(2.7f,1.12f,2.1f));
            g.ink=RuinStone;g.Block(p+Vector3.up*(.4f+height*.5f),V(1.48f,height,.85f));
            art.ink=seed%2==0?RuinJade:RedPlaster;
            for(int j=0;j<4;j++)art.Block(p+V(j%2==0?.13f:-.13f,.8f+height*j*.21f,.45f),V(.74f,.19f,.10f));
            Drapery(art,p+V(-.55f,height+.43f,.42f),Vector3.forward,height*.45f,seed);
        }
        static void OutlyingRuin(RuinSector s,Vector3 p,int kind,int seed)
        {
            var g=s.solid;var art=s.details;
            void Stone(float x,float y,float z,float w,float h,float d,Color col,bool foundation=false){
                g.ink=col;
                if(foundation)s.Foundation(p+V(x,y,z),V(w,h,d));
                else g.Block(p+V(x,y,z),V(w,h,d));
            }
            if(kind==0){
                for(int i=0;i<4;i++)Stone(0,-1.4f+i*1.1f,0,16-i*2,3.1f-i*.49f,13-i*1.65f,i%2==0?ShadowStone:RuinStone,i==0);
                Stela(s,p+Vector3.up*2.7f,4,seed,true);
            }else if(kind==1){
                for(int j=0;j<6;j++){
                    float x=-6+j*2.4f;int rows=1+(seed+j*3)%4;
                    for(int i=0;i<rows;i++)Stone(x+.18f*(i%2),-.5f+i*.96f,0,2.38f,1.06f,1.8f,i==rows-1?RuinMoss:RuinStone,i==0);
                }
                for(int j=0;j<3;j++)Stone(-6,-.5f+j*.66f,2+j*.9f,2.38f,2.2f,2.3f,RuinStone,true);
            }else if(kind==2){
                for(int side=-1;side<=1;side+=2){
                    Stone(side*3,1.35f,0,2.4f,5.4f,3.4f,RuinStone,true);
                    for(int j=0;j<3;j++)Stone(side*(2.8f-j*.42f),4.25f+j*.65f,0,2.9f,.72f,3.8f,PaleStone);
                }
                Stone(0,6.23f,0,6.2f,.81f,3.9f,ShadowStone);Drapery(art,p+V(1.45f,6.65f,1.92f),Vector3.forward,4,seed);
            }else if(kind==3){
                Stela(s,p,4.5f+seed%3,seed);Stela(s,p+V(4,-.6f,-3),2.1f,seed+1);
                Stone(-3,-.15f,2,5,1.3f,3,PaleStone,true);
            }else{
                for(int i=0;i<5;i++){
                    var foot=p+V(-6+3*i,0,i%2==0?-2:2);float h=1.3f+(seed+i*7)%4;
                    float bottom=FoundationBottom(s.ground,foot-Vector3.up,V(2.1f,0,2.1f));
                    g.ink=RuinStone;g.Rod(V(foot.x,bottom,foot.z),foot+Vector3.up*h,1.05f,.95f,8);
                    g.ink=RuinMoss;g.Block(foot+Vector3.up*h,V(2.1f,.31f,2.1f));
                }
            }
        }
        static void AncientBallcourt(RuinSector s,Vector3 p)
        {
            var g=s.solid;var art=s.details;
            foreach(int side in new[]{-1,1}){
                for(int i=0;i<4;i++){
                    g.ink=i%2==0?RuinStone:PaleStone;
                    s.Foundation(p+V(side*(8+i*1.25f),-.8f+i*1.2f,0),V(2.2f,3.4f,32-i*.7f));
                }
                var ring=p+V(side*11.6f,5.6f,0);
                for(int j=0;j<16;j++){
                    Vector3 At(int n)=>ring+V(0,Mathf.Sin(n*Mathf.PI/8)*1.25f,Mathf.Cos(n*Mathf.PI/8)*1.25f);
                    art.ink=ShadowStone;art.Rod(At(j),At(j+1),.31f,.31f,6);
                }
                Stela(s,p+V(side*13.6f,0,15),4.4f,side+1);
            }
        }

        public static void RefreshWildRuins()
        {
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try{
                var island=root.transform.Find("Southern wildlands");
                var old=island.Find(RuinDistrict);
                if(old)Object.DestroyImmediate(old.gameObject);
                var ground=island.Find("Hooked bay and outer keys").GetComponent<MeshCollider>();
                Physics.SyncTransforms();BuildRuinDistrict(island,ground);
                AssetDatabase.SaveAssets();PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
