using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        const string GaolName="The crown gaol";
        static void PalaceGaol(Transform root)
        {
            var royal=Object.FindObjectsByType<QuarterSwingDoor>().Single(d=>d.name=="Doors of the royal audience").transform;
            var precinct=Node(root,GaolName,royal.TransformPoint(V(31.7f,-8.39f,10)));
            precinct.rotation=Quaternion.LookRotation(-royal.right);
            var cave=Node(precinct,"Undercroft",V(0,-30,0));
            var zone=cave.gameObject.AddComponent<DungeonInterior>();
            var rockwork=new QuarterForms(ShadowStone);var masonry=new QuarterForms(PaleStone);
            var furniture=new QuarterForms(Wood);var ironwork=new QuarterForms(Dark);
            var glow=new QuarterForms(Gold);var torches=new List<Vector3>();
            var iron=Palette.Hex("4A5051");var straw=Palette.Hex("B29A64");var timber=Palette.Hex("70513A");
            const string file=MeshFile;

            float Level(float z)=>-4.5f*Mathf.InverseLerp(1,11,z)-3.7f*Mathf.InverseLerp(19,29,z)
                -4.5f*Mathf.InverseLerp(50,61,z)-1.5f*Mathf.InverseLerp(79,85,z);
            var profile=new (float z,float radius,float height,float shift)[]{
                (-3,3,4,0),(2,2.8f,3.9f,0),(10,3.15f,4.2f,0),(13,6.2f,5.9f,0),(19,6.4f,6.1f,0),
                (23,3.5f,4.3f,0),(28,9.2f,5.8f,0),(50,9.2f,5.8f,0),(54,3.15f,4.15f,0),
                (60,3.2f,4.5f,1.4f),(64,9,8.1f,1.5f),(77,10,8.7f,1.3f),(82,4.5f,6.1f,-1),
                (87,3.7f,5.3f,-3),(91,2.6f,4.3f,-3.2f)
            };
            Vector3[] Section(float z){
                int k=1;while(k<profile.Length-1 && z>profile[k].z)k++;
                var a=profile[k-1];var b=profile[k];float f=Mathf.InverseLerp(a.z,b.z,z);
                float w=Mathf.Lerp(a.radius,b.radius,f),h=Mathf.Lerp(a.height,b.height,f),shift=Mathf.Lerp(a.shift,b.shift,f);
                var rim=new Vector3[11];
                for(int i=0;i<rim.Length;i++){
                    float angle=i*Mathf.PI/(rim.Length-1);float rough=.32f*(Mathf.PerlinNoise(z*.24f+i*1.7f,i*.67f)-.5f);
                    rim[i]=V(shift+Mathf.Cos(angle)*(w+rough),Level(z)+Mathf.Sin(angle)*(h+rough),z);
                }
                return rim;
            }
            Vector3[] previous=null;
            for(float z=-3;z<=91;z+=2){
                var ring=Section(z);
                if(previous!=null){
                    masonry.ink=Palette.Hex("958572");masonry.Face(previous[^1],ring[^1],ring[0],previous[0]);
                    for(int i=0;i<ring.Length-1;i++){
                        rockwork.ink=Color.Lerp(Palette.Hex("756C61"),Palette.Hex("A09887"),.5f+.35f*Mathf.Sin(z+i*2.4f));
                        rockwork.Face(previous[i],ring[i],ring[i+1],previous[i+1]);
                    }
                }else rockwork.Face(ring);
                previous=ring;
            }
            System.Array.Reverse(previous);rockwork.Face(previous);
            void Block(QuarterForms g,Vector3 p,Vector3 size,Color color){g.ink=color;g.Block(p,size);}
            Vector3 Foot(float x,float z)=>V(x,Level(z),z);
            void Hoop(QuarterForms g,Vector3 p,float radius,bool vertical,bool across=false){
                g.ink=iron;
                Vector3 Point(int i){
                    float t=i*Mathf.PI/6;
                    return p+(vertical?(across?V(0,Mathf.Sin(t),Mathf.Cos(t)):V(Mathf.Cos(t),Mathf.Sin(t),0)):V(Mathf.Cos(t),0,Mathf.Sin(t)))*radius;
                }
                for(int j=0;j<12;j++)g.Rod(Point(j),Point(j+1),.036f,.036f,5);
            }
            void Torch(float x,float z){
                var p=Foot(x,z)+Vector3.up*2.7f;
                Block(furniture,p-Vector3.up*.26f,V(.48f,.42f,.46f),iron);
                furniture.ink=timber;furniture.Rod(p-Vector3.up*2.72f,p,.14f,.12f,7);
                furniture.ink=iron;furniture.Lathe(p,8,(-.37f,.14f),(-.08f,.31f),(.08f,.34f));
                glow.ink=Palette.Hex("F59C56");glow.Lathe(p,8,(0,.20f),(.19f,.24f),(.5f,.14f),(.77f,.025f));
                torches.Add(p+Vector3.up*.34f);
            }
            foreach(var t in new[]{(-2.3f,2f),(3.5f,15f),(-2.3f,24f),(3.2f,32f),(-3.2f,45f),(-1.6f,56f),(-4.8f,66f),(5.5f,75f)})Torch(t.Item1,t.Item2);
            zone.torches=torches.ToArray();
            // The gallery has six masonry cells. Each barred front sits between solid dividing walls.
            var names=new[]{"Iria","Tomas","Vera","Remy"};
            var lines=new[]{
                new[]{"They took my lockpicks and left me my knitting needles.","If you pass the dye market, tell Sela her blue shawl is finished."},
                new[]{"Count the knocks after the supper bell. My brother answers from the next cell.","Nobody remembers why the little door at the back was bricked up."},
                new[]{"I kept the old harbour books. Someone paid a great deal to put me down here.","I can still tell high tide by the sound in that drain."},
                new[]{"Three red tiles above the gate. I counted them every morning as a guard.","My daughter brings oranges. The whole passage smells of them for an hour."}
            };
            int prisoner=0;
            foreach(int side in new[]{-1,1})for(int j=0;j<3;j++){
                float z=32.5f+j*6.2f;var floor=Foot(0,z);float y=floor.y;
                float bars=side*3.65f;
                Block(masonry,V(side*6.68f,y+1.55f,z-3.1f),V(6.05f,3.1f,.53f),ShadowStone);
                if(j==2)Block(masonry,V(side*6.68f,y+1.55f,z+3.1f),V(6.05f,3.1f,.53f),ShadowStone);
                Block(masonry,V(bars,y+3.17f,z),V(.48f,.48f,6.2f),RuinStone);
                Block(masonry,V(bars,y+.11f,z),V(.51f,.22f,6.2f),ShadowStone);
                Block(masonry,V(side*6.25f,y+3.47f,z),V(5.65f,.24f,6.19f),RuinStone);
                for(int k=0;k<15;k++){
                    var p=V(bars,y+.21f,z-2.88f+k*.41f);
                    ironwork.ink=iron;ironwork.Rod(p,p+Vector3.up*2.8f,.062f,.062f,7);
                }
                foreach(float h in new[]{.53f,2.37f})Block(ironwork,V(bars,y+h,z),V(.15f,.13f,5.94f),iron);
                Block(ironwork,V(bars-side*.13f,y+1.16f,z+1.01f),V(.18f,.48f,.38f),Gold);
                Hoop(ironwork,V(bars-side*.19f,y+1.49f,z+1.01f),.14f,true,true);
                var bed=V(side*6.63f,y+.36f,z-.85f);
                Block(furniture,bed,V(1.14f,.46f,2.27f),timber);
                Block(furniture,bed+Vector3.up*.31f,V(1.11f,.23f,2.21f),straw);
                PaintedUrn(furniture,V(side*5.97f,y,z+1.52f),.7f,ShadowStone);
                if((side<0&&j==2)||(side>0&&j==0))continue;
                var foot=V(side*4.55f,y+.04f,z);
                var actor=ResidentFigure(cave,names[prisoner],foot,foot+Vector3.left*side,
                    new[]{PaleStone,Palette.Olive,RuinJade,RedPlaster}[prisoner],ShadowStone,prisoner,false,lines[prisoner]);
                actor.pace=0;actor.handle=V(-side*1.18f,1.3f,0);prisoner++;
            }
            // A ledger desk and wall of keys mark the guardroom.
            var desk=Foot(-2.8f,16);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                Block(furniture,desk+V(x*1.24f,.53f,z*.49f),V(.18f,1.06f,.18f),timber);
            Block(furniture,desk+Vector3.up*1.15f,V(2.85f,.22f,1.28f),timber);
            Block(furniture,desk+V(-.48f,1.29f,0),V(.68f,.065f,.53f),PaleStone);
            for(int i=0;i<4;i++){
                var p=desk+V(.16f+i*.24f,1.32f,-.19f);
                Hoop(ironwork,p,.095f,false);Block(ironwork,p+V(0,0,.14f),V(.047f,.047f,.21f),Gold);
            }
            foreach(float z in new[]{10f,19f,25f,53f,60f}){
                float y=Level(z);
                foreach(int side in new[]{-1,1}){
                    Block(masonry,V(side*2.38f+(z>=60?1.4f:0),y+1.25f,z),V(.62f,2.5f,.86f),RuinStone);
                    for(int k=0;k<3;k++)Block(masonry,V(side*(2.22f-k*.34f)+(z>=60?1.4f:0),y+2.68f+k*.32f,z),V(.88f,.32f,.88f),PaleStone);
                }
                Block(masonry,V(z>=60?1.4f:0,y+3.58f,z),V(3.26f,.27f,.91f),ShadowStone);
            }
            // Historic furnishings silhouette against the braziers in the lower chamber.
            var rack=Foot(-2.9f,71.9f);
            for(int i=0;i<7;i++)
                Block(furniture,rack+V((i-3)*.255f,1.02f,0),V(.24f,.17f,3.95f),timber);
            foreach(int end in new[]{-1,1}){
                foreach(int side in new[]{-1,1})Block(furniture,rack+V(side*.98f,.66f,end*1.91f),V(.25f,1.32f,.32f),timber);
                furniture.ink=ShadowStone;furniture.Rod(rack+V(-1.2f,1.37f,end*1.78f),rack+V(1.2f,1.37f,end*1.78f),.22f,.22f,10);
                Hoop(ironwork,rack+V(.6f,1.49f,end*1.29f),.18f,false);
            }
            var stocks=Foot(4.45f,68.5f);
            foreach(int side in new[]{-1,1})Block(furniture,stocks+V(side*1.63f,.93f,0),V(.32f,1.86f,.45f),timber);
            // A pierced yoke, cut as a single silhouette around the three circular openings.
            float[] cuts={-1.52f,-.89f,-.63f,-.27f,.27f,.63f,.89f,1.52f};
            for(int i=0;i<cuts.Length-1;i++){
                float x0=cuts[i],x1=cuts[i+1];bool hole=i%2==1;
                for(int k=0;k<(hole?10:1);k++){
                    int n=hole?10:1;
                    float a=Mathf.Lerp(x0,x1,k/(float)n),b=Mathf.Lerp(x0,x1,(k+1f)/n);
                    float Aperture(float x){float r=(x1-x0)*.5f,dx=x-(x0+x1)*.5f;return hole?Mathf.Sqrt(Mathf.Max(0,r*r-dx*dx)):0;}
                    foreach(int sign in new[]{-1,1}){
                        Vector3 P(float x,float h,float z)=>stocks+V(x,1.25f+sign*h,z);
                        var l=P(a,Aperture(a),-.13f);var r=P(b,Aperture(b),-.13f);
                        var u=P(a,.43f,-.13f);var v=P(b,.43f,-.13f);var deep=Vector3.forward*.26f;
                        furniture.ink=timber;
                        furniture.Silk(l,r,v);furniture.Silk(l,v,u);furniture.Silk(u+deep,v+deep,r+deep);furniture.Silk(u+deep,r+deep,l+deep);
                        furniture.Face(l,l+deep,r+deep,r);furniture.Face(u,v,v+deep,u+deep);
                    }
                }
            }
            for(int k=0;k<3;k++){
                var beam=Foot(-4.6f+k*1.02f,76.8f);
                Block(furniture,beam+Vector3.up*2.6f,V(.19f,5.2f,.23f),timber);
                for(int j=0;j<7+k;j++)Hoop(ironwork,beam+V(0,4.1f-j*.21f,-.23f),.13f,true,j%2==0);
                Hoop(ironwork,beam+V(0,2.6f-k*.21f,-.23f),.22f,true);
            }
            var shaft=Foot(-3.15f,87);
            for(int k=0;k<7;k++)Block(ironwork,shaft+V((k-3)*.31f,.10f,0),V(.12f,.13f,2.65f),iron);
            var shell=WildMesh(cave,"Natural cave vault",Vector3.zero,rockwork.Bake("Crown gaol cliff",file),Mat("Cliff rock"));
            RockCollision(shell);RockCollision(WildMesh(cave,"Guardroom and cell stonework",Vector3.zero,masonry.Bake("Crown gaol floor and cells",file),Mat("Carved limestone")));
            RockCollision(WildMesh(cave,"Lower chamber furnishings",Vector3.zero,furniture.Bake("Crown gaol furnishings",file),Mat("Shutters")));
            RockCollision(WildMesh(cave,"Locked grilles and ironwork",Vector3.zero,ironwork.Bake("Crown gaol iron",file),Mat("Shutters")));
            WildMesh(cave,"Burning sconces",Vector3.zero,glow.Bake("Crown gaol flames",file),Mat("Lantern glow"));
            zone.bounds=shell.GetComponent<MeshFilter>().sharedMesh.bounds;zone.bounds.Expand(3);
            Node(cave,"Guardroom",Foot(0,16));Node(cave,"Cell gallery",Foot(0,39));Node(cave,"Torture chamber",Foot(1.2f,71));

            var doors=new QuarterForms(ShadowStone);
            foreach(int side in new[]{-1,1})for(int j=0;j<4;j++)
                Block(doors,V(side*(1.37f-j*.031f),.43f+j*.81f,-.40f),V(.54f,.8f,.88f),j%2==0?RuinStone:PaleStone);
            Block(doors,V(0,3.49f,-.4f),V(3.47f,.55f,1.02f),PaleStone);
            for(int j=-4;j<=4;j++)Block(doors,V(j*.24f,1.53f,-.26f),V(.231f,3.06f,.3f),timber);
            foreach(float y in new[]{.52f,2.44f})Block(doors,V(0,y,-.44f),V(2.19f,.16f,.09f),iron);
            Block(doors,V(.64f,1.4f,-.48f),V(.36f,.36f,.11f),Gold);
            var portal=doors.Bake("Crown gaol oak door",file);
            RockCollision(WildMesh(precinct,"Garden postern",Vector3.zero,portal,Mat("Shutters")));
            var inner=Node(cave,"Garden stair",V(0,0,-2.25f));inner.localRotation=Quaternion.Euler(0,180,0);
            RockCollision(WildMesh(inner,"Oak door",Vector3.zero,portal,Mat("Shutters")));
            var outside=Node(precinct,"Garden landing",V(0,.05f,-2.3f));outside.localRotation=Quaternion.Euler(0,180,0);
            var inside=Node(cave,"Cave landing",V(0,.08f,.4f));
            var enter=precinct.gameObject.AddComponent<DungeonDoor>();enter.passage="Enter the palace gaol";enter.arrival=inside;enter.handle=V(0,1.39f,-.82f);
            var exit=inner.gameObject.AddComponent<DungeonDoor>();exit.passage="Return to the royal gardens";exit.arrival=outside;exit.handle=enter.handle;
        }

        public static void RefreshPalaceGaol(){
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try{
                var previous=root.transform.Find(GaolName);if(previous)Object.DestroyImmediate(previous.gameObject);
                PalaceGaol(root.transform);AssetDatabase.SaveAssets();PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
