using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static void BurialChambers(Transform district, Vector3 doorGround, bool jaguar)
        {
            string name = jaguar ? "Vaults of the Night Jaguar" : "Halls of the Buried Sun";
            var gate = Node(district,name+" gate",doorGround);
            gate.localRotation = Quaternion.Euler(0,jaguar ? -90 : 90,0);
            var under = Node(district,name,V(doorGround.x,-48,doorGround.z));
            under.localRotation = gate.localRotation;
            var ward = under.gameObject.AddComponent<DungeonInterior>();
            var exterior = new QuarterForms(PaleStone);
            var stone = new QuarterForms(RuinStone);
            var relics = new QuarterForms(PaleStone);
            var cave = new QuarterForms(ShadowStone);
            var jade = Palette.Hex("72998B");
            var ochre = Palette.Hex("C38B50");
            var paint = jaguar ? jade : ochre;
            const float span = 5.5f;
            var wing = new Dictionary<Vector2Int,float>();
            var namedRooms = new List<(string name,int x,int z)>();

            void Fill(int x0,int x1,int z0,int z1,float height) {
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
                    var k=new Vector2Int(x,z);
                    wing[k]=wing.TryGetValue(k,out float other)?Mathf.Max(other,height):height;
                }
            }
            void Room(string title,int x0,int x1,int z0,int z1,float height) {
                Fill(x0,x1,z0,z1,height);namedRooms.Add((title,(x0+x1)/2,(z0+z1)/2));
            }

            if(!jaguar){
                Room("Hall of First Light",-1,1,0,2,5.5f);
                Fill(0,0,3,7,3.8f);
                Room("Calendar chamber",-1,1,8,10,6);
                Fill(-4,4,9,9,3.7f);
                Room("West scriptorium",-6,-4,8,11,5);
                Room("Ochre offering room",4,6,8,11,5.7f);
                Fill(-5,-5,12,18,3.8f);Fill(-5,-3,18,18,4);
                Fill(0,0,11,16,4.1f);
                Room("Hall of the Nine Suns",-3,3,17,22,11.7f);
                Fill(0,0,23,24,3.9f);
                Room("Royal ossuary",-2,2,25,28,6.5f);
            }else{
                Room("Gate of Pawprints",-1,1,0,2,5.1f);
                Fill(1,1,3,5,3.65f);Fill(1,4,5,5,3.8f);
                Room("Root gallery",3,5,6,9,5.9f);
                Fill(-3,3,8,8,3.8f);
                Room("Obsidian cache",-5,-3,6,9,4.8f);
                Fill(-4,-4,10,15,3.65f);Fill(-4,-2,15,15,3.65f);
                Fill(4,4,10,12,4);
                Room("Echo well",2,5,13,15,6.75f);
                Fill(2,2,16,18,3.95f);Fill(-2,-2,15,18,4);
                Room("Court of the Sleeping Jaguar",-4,2,19,24,12.6f);
                Fill(-4,-4,25,27,3.8f);Fill(-6,-4,27,27,4);
                Room("Burial grotto",-8,-6,25,28,7.8f);
            }
            // Grades continue across cell seams so both legs of each circuit descend to the same floor.
            float Floor(float z) {
                float Down(float start,float end,float depth)=>depth*Mathf.InverseLerp(start,end,z/span);
                return jaguar ? -Down(2.5f,5.5f,4)-Down(9.5f,12.5f,3.6f)-Down(15.5f,18.5f,4.3f)-Down(24.5f,26.5f,2.8f)
                    : -Down(2.5f,7.5f,5.1f)-Down(11.5f,16.5f,5.7f)-Down(22.5f,24.5f,3.7f);
            }
            float Crown(float x,float z,float h) => h + (jaguar ? .75f*Mathf.PerlinNoise(7+x*.069f,61+z*.097f) : 0);
            void Toward(QuarterForms mesh,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal) {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0)mesh.Face(a,b,c,d);else mesh.Face(d,c,b,a);
            }
            var directions=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.down,Vector2Int.up};
            var rng=new System.Random(jaguar ? 21377 : 93771);
            foreach(var cell in wing){
                int x=cell.Key.x,z=cell.Key.y;float h=cell.Value;
                float x0=(x-.5f)*span,x1=(x+.5f)*span,z0=(z-.5f)*span,z1=(z+.5f)*span;
                Vector3 Ground(float xx,float zz)=>V(xx,Floor(zz),zz);
                Vector3 Roof(Vector3 p,float headroom)=>p+Vector3.up*Crown(p.x,p.z,headroom);
                var a=Ground(x0,z0);var b=Ground(x0,z1);var c=Ground(x1,z1);var d=Ground(x1,z0);
                stone.ink=Color.Lerp(ShadowStone,RuinStone,.22f+.20f*(float)rng.NextDouble());
                stone.Face(a,b,c,d);
                stone.ink=jaguar?Palette.Hex("766F67"):RuinStone;
                stone.Face(Roof(d,h),Roof(c,h),Roof(b,h),Roof(a,h));
                foreach(var dir in directions){
                    Vector3 l,r;
                    if(dir==Vector2Int.left){l=a;r=b;}else if(dir==Vector2Int.right){l=c;r=d;}
                    else if(dir==Vector2Int.down){l=d;r=a;}else{l=b;r=c;}
                    var inward=V(-dir.x,0,-dir.y);
                    if(wing.TryGetValue(cell.Key+dir,out var next)){
                        if(next>=h)continue;
                        stone.ink=ShadowStone;
                        Toward(stone,Roof(l,next),Roof(r,next),Roof(r,h),Roof(l,h),inward);
                        continue;
                    }
                    stone.ink=Color.Lerp(RuinStone,PaleStone,.24f+.34f*(float)rng.NextDouble());
                    Toward(stone,l,r,Roof(r,h),Roof(l,h),inward);
                    var foot=(l+r)*.5f;var along=(r-l);along.y=0;along.Normalize();
                    bool grotto = jaguar && ((z>=6 && z<=9 && x>=3) || (z>=25 && x<=-6) );
                    if(grotto){
                        // Fractured rock wraps the older masonry in the galleries furthest from the gate.
                        Vector3 Crag(int band,int lane){
                            float u=lane/3f, v=band/3f;
                            var f=Vector3.Lerp(l,r,u);float noise=Mathf.PerlinNoise(f.x*.41f+v*9,f.z*.31f+v*3);
                            float bulge=.16f+Mathf.Sin(v*Mathf.PI)*(.4f+noise*1.24f);
                            return f+inward*bulge+Vector3.up*(v*(h+.35f));
                        }
                        for(int band=0;band<3;band++)for(int lane=0;lane<3;lane++){
                            cave.ink=Color.Lerp(Palette.Hex("716B5A"),Palette.Hex("9CA095"),(float)rng.NextDouble());
                            Toward(cave,Crag(band,lane),Crag(band,lane+1),Crag(band+1,lane+1),Crag(band+1,lane),inward);
                        }
                        for(int tooth=0;tooth<2;tooth++){
                            var anchor=foot+inward*(.74f+tooth*.47f)+along*(tooth==0?-1.3f:1.4f);
                            cave.ink=tooth==0?ShadowStone:RuinStone;
                            cave.Rod(anchor-Vector3.up*.4f,anchor+Vector3.up*(tooth==0?1.9f:1.15f),.63f,.08f,6);
                            var high=anchor+Vector3.up*(h+.38f);
                            cave.Rod(high,high+inward*.22f-Vector3.up*(tooth==0?1.29f:2.17f),.53f,.05f,6);
                        }
                    }else if(jaguar && h>4.1f && (x+2*z)%3!=0){
                        var rib=foot+inward*(.29f+.34f*(float)rng.NextDouble());
                        relics.ink=ShadowStone;
                        relics.Rod(foot+inward*.22f-Vector3.up*.25f,rib+Vector3.up*(h*.47f),.71f,.46f,5);
                        relics.Rod(rib+Vector3.up*(h*.47f),foot+Vector3.up*h,.46f,.84f,5);
                        if((x+z)%2==0){
                            relics.ink=Palette.Hex("75654D");
                            var root=foot+Vector3.up*h+inward*.55f;
                            for(int j=0;j<3;j++){
                                var end=root+inward*.16f+along*(j%2==0?.3f:-.42f)-Vector3.up*.85f;
                                relics.Rod(root,end,.11f-j*.022f,.075f-j*.022f,5);root=end;
                            }
                        }
                    }else{
                        var p=foot+inward*.09f+Vector3.up*1.78f;
                        relics.ink=ShadowStone;
                        Toward(relics,p-along*2.24f,p+along*2.24f,p+along*2.24f+Vector3.up*.99f,p-along*2.24f+Vector3.up*.99f,inward);
                        relics.ink=paint;
                        for(int j=-2;j<=2;j++){
                            var m=p+along*(j*.79f)+Vector3.up*.5f+inward*.055f;
                            var u=along*(jaguar?.24f:.31f);var v=Vector3.up*(jaguar?.18f:.32f);
                            Toward(relics,m-u,m+v,m+u,m-v,inward);
                        }
                        relics.ink=PaleStone;
                        relics.Rod(l+inward*.16f+Vector3.up*.78f,r+inward*.16f+Vector3.up*.78f,.14f,.14f,5);
                    }
                }
                // Scattered dressed lintels frame the narrow descending runs.
                if(h<=4.1f && z%3==1 && wing.ContainsKey(cell.Key+Vector2Int.up) && wing.ContainsKey(cell.Key+Vector2Int.down)){
                    float y=Floor(z*span);stone.ink=ShadowStone;
                    foreach(int flank in new[]{-1,1})stone.Block(V(x*span+flank*(span*.5f-.22f),y+1.65f,z*span),V(.44f,3.7f,.67f));
                    stone.ink=PaleStone;stone.Block(V(x*span,y+3.28f,z*span),V(span,.47f,.81f));
                }
            }
            // Columns keep the processional aisles clear under the high vault.
            int hallZ=jaguar?21:19;
            for(int i=-1;i<=1;i++)foreach(int side in new[]{-1,1}){
                float x=(jaguar?-span:0)+side*span*1.88f,z=(hallZ+i*1.65f)*span;
                var p=V(x,Floor(z),z);float height=jaguar?12.7f:11.7f;
                stone.ink=ShadowStone;stone.Block(p+Vector3.up*.28f,V(2.9f,.56f,2.9f));
                stone.ink=PaleStone;stone.Rod(p+Vector3.up*.56f,p+Vector3.up*(height-1.1f),1.09f,.84f,jaguar?7:12);
                stone.ink=paint;
                foreach(float y in new[]{1.2f,3.5f,height-1.5f})stone.Rod(p+Vector3.up*y,p+Vector3.up*(y+.33f),1.1f,1.08f,12);
                stone.ink=RuinStone;stone.Block(p+Vector3.up*(height-.55f),V(3.1f,1.1f,3.1f));
                if(!jaguar){
                    var tip=p+Vector3.up*(height-1.2f);relics.ink=RedPlaster;
                    relics.Silk(tip+Vector3.right*.85f,tip-Vector3.right*.85f,tip+V(.42f,-3.5f,.24f));
                }else{
                    relics.ink=ShadowStone;
                    var ceiling=p+V(-side*2.1f,height,1.6f);
                    relics.Rod(ceiling,ceiling+V(.31f,-2.55f,.19f),.71f,.055f,6);
                }
            }
            foreach(var room in namedRooms){
                var center=V(room.x*span,Floor(room.z*span),room.z*span);
                Node(under,room.name,center);
                if(room.z<4 || room.z==hallZ || room.name.StartsWith("Court") || room.name.StartsWith("Hall of the Nine"))continue;
                var offering=center+V(-2.28f,0,1.48f);
                stone.ink=ShadowStone;stone.Block(offering+Vector3.up*.37f,V(2,.74f,1.25f));
                relics.ink=paint;
                relics.Lathe(offering+Vector3.up*.74f,10,(0,.38f),(.26f,.51f),(.86f,.39f),(1.03f,.19f),(1.12f,.25f));
                relics.ink=PaleStone;relics.Lathe(offering+V(.66f,.74f,.09f),9,(0,.14f),(.25f,.23f),(.41f,.21f));
            }
            // The two halls have different focal pieces: a solar wheel and a reclining jaguar.
            var altar=V(jaguar?-span:0,Floor((hallZ+2)*span),(hallZ+2.45f)*span);
            stone.ink=ShadowStone;stone.Block(altar+Vector3.up*.55f,V(8.4f,1.1f,2.8f));
            if(jaguar){
                stone.ink=RuinStone;
                stone.Rod(altar+V(-2.4f,1.82f,0),altar+V(1.9f,1.85f,0),.91f,1.13f,8);
                stone.Block(altar+V(2.18f,2.7f,-.12f),V(1.85f,1.8f,1.52f));
                foreach(int side in new[]{-1,1}){
                    stone.Block(altar+V(2.18f+side*.64f,3.68f,0),V(.36f,.55f,.46f));
                    relics.ink=jade;relics.Block(altar+V(2.18f+side*.43f,2.85f,-.91f),V(.46f,.2f,.15f));
                }
                stone.Block(altar+V(2.18f,2.35f,-.88f),V(1.21f,.69f,.76f));
                relics.ink=ShadowStone;relics.Block(altar+V(2.18f,2.47f,-1.29f),V(.6f,.26f,.16f));
                foreach(int side in new[]{-1,1}){
                    stone.ink=PaleStone;
                    stone.Rod(altar+V(1.34f,1.46f,side*.65f),altar+V(2.41f,1.31f,side*.78f),.37f,.29f,7);
                    stone.Rod(altar+V(-1.36f,1.6f,side*.55f),altar+V(-.69f,1.34f,side*.67f),.54f,.29f,7);
                    for(int j=0;j<6;j++){
                        float x=-2+j*.59f;
                        var spot=altar+V(x,1.94f+.23f*(j%2),side*(.92f+.14f*j/5));
                        relics.ink=ShadowStone;relics.Rod(spot,spot+Vector3.forward*side*.07f,.19f,.16f,6);
                    }
                }
                stone.ink=RuinStone;
                var tail=altar+V(-2.11f,1.65f,.34f);
                foreach(var next in new[]{V(-3.06f,1.46f,.68f),V(-3.38f,1.43f,.13f),V(-3.08f,1.47f,-.46f)}){
                    stone.Rod(tail,altar+next,.22f,.17f,7);tail=altar+next;
                }
            }else{
                var disk=altar+Vector3.up*4.2f;
                stone.ink=RuinStone;stone.Block(altar+V(0,2.6f,.69f),V(1.8f,3,.98f));
                relics.ink=ochre;relics.Rod(disk,disk+Vector3.back*.44f,2.33f,2.33f,20);
                var face=disk+Vector3.back*.51f;
                relics.ink=RedPlaster;relics.Rod(face,face+Vector3.back*.13f,1.65f,1.65f,20);
                relics.ink=ochre;relics.Rod(face+Vector3.back*.17f,face+Vector3.back*.27f,.87f,.87f,12);
                for(int ray=0;ray<12;ray++){
                    float t=ray*Mathf.PI/6;var arm=V(Mathf.Cos(t),Mathf.Sin(t),0);
                    relics.ink=PaleStone;
                    relics.Rod(face+arm*1.96f,face+arm*1.96f+Vector3.back*.08f,.13f,.12f,ray%3==0?4:8);
                    relics.ink=ochre;relics.Rod(disk+arm*2.42f,disk+arm*3.23f,.18f,.05f,5);
                }
            }

            var shell=WildMesh(under,"Chambers and passages",Vector3.zero,stone.Bake(name+" masonry",WildMeshFile),Mat("Carved limestone"));
            RockCollision(shell);
            if(jaguar)RockCollision(WildMesh(under,"Limestone caverns",Vector3.zero,cave.Bake(name+" caves",WildMeshFile),Mat("Cliff rock")));
            WildMesh(under,"Carvings, roots and offerings",Vector3.zero,relics.Bake(name+" relics",WildMeshFile),Mat("Shutters"));
            ward.bounds=shell.GetComponent<MeshFilter>().sharedMesh.bounds;
            ward.bounds.Expand(new Vector3(2,4,2));

            void Doorway(QuarterForms g){
                foreach(int side in new[]{-1,1}){
                    for(int i=0;i<4;i++){
                        g.ink=i%2==0?PaleStone:RuinStone;
                        g.Block(V(side*(1.86f-i*.047f),.57f+i*1.04f,.36f),V(1.02f,1.03f,2.35f));
                    }
                    g.ink=paint;g.Block(V(side*1.81f,2.21f,-.85f),V(.69f,.83f,.15f));
                }
                g.ink=ShadowStone;g.Block(V(0,4.54f,.33f),V(5.27f,.87f,2.65f));
                g.ink=paint;g.Block(V(0,4.64f,-1.05f),V(3.85f,.32f,.18f));
                g.ink=Palette.Hex("423D39");g.Block(V(0,1.97f,.99f),V(3,4.12f,.33f));
                g.ink=paint;g.Rod(V(0,2.1f,.81f),V(0,2.1f,.69f),.59f,.53f,jaguar?4:12);
            }
            Doorway(exterior);
            var doorMesh=exterior.Bake(name+" doorway",WildMeshFile);
            RockCollision(WildMesh(gate,"Carved side doorway",Vector3.zero,doorMesh,Mat("Shutters")));
            var innerGate=Node(under,"Stair to daylight",V(0,0,-1.45f));
            innerGate.localRotation=Quaternion.Euler(0,180,0);
            RockCollision(WildMesh(innerGate,"Inner doorway",Vector3.zero,doorMesh,Mat("Carved limestone")));
            var outside=Node(gate,"Forest threshold",V(0,.12f,-2.45f));outside.localRotation=Quaternion.Euler(0,180,0);
            var inside=Node(under,"Chamber threshold",V(0,.09f,1.45f));
            var enter=gate.gameObject.AddComponent<DungeonDoor>();
            enter.handle=V(0,1.4f,-.89f);enter.reach=3.6f;enter.passage="Enter "+name;enter.arrival=inside;
            var exit=innerGate.gameObject.AddComponent<DungeonDoor>();
            exit.handle=enter.handle;exit.reach=enter.reach;exit.passage="Return to the wildlands";exit.arrival=outside;
        }
    }
}
