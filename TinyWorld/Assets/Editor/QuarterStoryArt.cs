using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static void Stories(Transform root,Vector3 court)
        {
            var book=root.gameObject.AddComponent<QuarterQuests>();
            var residents=root.GetComponentsInChildren<QuarterResident>().ToDictionary(n=>n.firstName);
            void Mark(Transform t,string who,float y){
                var marker=t.gameObject.AddComponent<QuarterQuestMark>();marker.who=who;
                var g=new QuarterForms(Gold);g.Lathe(Vector3.zero,8,(.17f,.043f),(.48f,.075f),(.52f,.058f));
                g.Lathe(Vector3.zero,8,(0,.05f),(.07f,.055f),(.095f,.034f));marker.glyph=Art(t,"Brass exclamation",g,"Shutters",Vector3.up*y);
            }
            foreach(string who in new[]{"Nera","Moss","Davi","Ines","Belen","Pia"}) Mark(residents[who].transform,who,2.12f);
            QuarterErrandPoint Point(Transform parent,Vector3 at,string role,string prompt,QuarterForms g,bool permanent=false){
                var t=Node(parent,role,at);var item=Art(t,role+" token",g);
                var p=t.gameObject.AddComponent<QuarterErrandPoint>();p.role=role;p.prompt=prompt;p.handle=Vector3.up*.19f;p.item=item.gameObject;p.permanent=permanent;return p;
            }
            var pool=root.GetComponentInChildren<GrottoPool>();
            var ring=new QuarterForms(Gold);
            for(int i=0;i<20;i++){
                Vector3 P(int n)=>V(Mathf.Cos(n*Mathf.PI/10)*.142f,.15f+Mathf.Sin(n*Mathf.PI/10)*.142f,.02f);
                ring.Rod(P(i),P(i+1),.026f,.026f,6);
            }
            ring.ink=Teal;ring.Lathe(V(0,.26f,0),8,(0,.061f),(.047f,.085f),(.10f,.025f));
            Point(root,pool.passage[8]+V(.62f,.095f,.47f),"ring","Pick up the blue glass ring",ring);
            var bird=new QuarterForms(Blue);
            Pelt(bird,Vector3.zero,(-.3f,.16f,.025f,.025f),(-.12f,.2f,.14f,.13f),(.1f,.25f,.17f,.145f),(.21f,.31f,.11f,.12f));
            bird.ink=Red;Pelt(bird,Vector3.zero,(.09f,.31f,.11f,.09f),(.21f,.37f,.14f,.14f),(.31f,.36f,.09f,.085f));
            bird.ink=Gold;bird.Rod(V(0,.35f,.27f),V(0,.33f,.43f),.054f,0,5);
            foreach(int s in new[]{-1,1}){bird.ink=Dark;bird.Silk(V(s*.06f,.20f,-.12f),V(s*.15f,.14f,-.53f),V(s*.065f,.14f,-.32f));bird.Rod(V(s*.102f,.386f,.24f),V(s*.122f,.386f,.247f),.021f,.021f);}
            var terrace = Object.FindObjectsByType<Sunstone>().Single(s => s.place=="Olive Ward roof terrace" && s.transform.position.x<0);
            var footing = terrace.transform.position + V(1.2f,1.8f,0);
            if (!Physics.Raycast(footing, Vector3.down, out var tile, 6, 1))
                throw new System.InvalidOperationException("Swallow perch needs a roof footing.");
            var post=Node(root,"Swallow perch",tile.point-Vector3.up*.12f);
            var stand=new QuarterForms(Wood);
            stand.ink=Gold;stand.Lathe(Vector3.zero,9,(0,.17f),(.34f,.14f),(.39f,.11f));
            stand.ink=Wood;
            stand.Rod(Vector3.zero,V(0,1.26f,0),.105f,.082f,8);Art(post,"Swallow perch cedar",stand);
            var swallow=Point(post,Vector3.up*1.24f,"bird","Listen to the swallow",bird,true);Mark(swallow.transform,"bird",.7f);
            var spool=new QuarterForms(Cream);spool.Lathe(Vector3.zero,12,(0,.18f),(.055f,.19f),(.056f,.108f),(.25f,.108f),(.251f,.18f),(.305f,.18f));
            spool.ink=Teal;for(int i=0;i<7;i++)spool.Lathe(Vector3.zero,12,(.058f+.027f*i,.135f),(.077f+.027f*i,.142f));
            Point(root,V(court.x-.15f,court.y+.07f,27.5f),"thread","Unwind the blue thread",spool);
            var nest=new QuarterForms(Wood);nest.Lathe(Vector3.zero,16,(0,.15f),(.08f,.25f),(.20f,.30f),(.22f,.26f),(.065f,.14f));
            for(int i=0;i<11;i++){float a=i*.57f;nest.Rod(V(Mathf.Sin(a)*.28f,.13f,Mathf.Cos(a)*.28f),V(Mathf.Sin(a+.75f)*.29f,.23f,Mathf.Cos(a+.75f)*.29f),.021f,.009f);}
            Art(post,"Swallow’s woven nest",nest,"Shutters",V(0,1.11f,-.06f));
            var temple=Object.FindObjectsByType<QuarterSwingDoor>().Single(d=>d.name=="Temple of the Sun").transform;
            for(int i=0;i<3;i++){
                var metal=new QuarterForms(Wood);metal.Rod(V(-.47f,0,0),V(-.47f,1.92f,0),.075f,.058f);metal.Rod(V(-.47f,1.88f,0),V(.33f,1.88f,0),.065f,.055f);
                metal.ink=Gold;metal.Lathe(V(0,.93f,0),18,(0,.31f),(.08f,.28f),(.22f,.19f),(.53f,.14f),(.61f,.065f));
                metal.ink=new[]{Red,Blue,Gold}[i];metal.Rod(V(0,1.85f,0),V(0,1.49f,0),.035f,.032f);
                var point=Point(root,temple.TransformPoint(V(i==1?4.6f:-4.6f,.04f,5+i*9)),new[]{"earth","sea","sun"}[i],"Ring the "+new[]{"earth","sea","sun"}[i]+" bell",metal,true);
                point.handle=Vector3.up*1.22f;point.note=Clip("Sunstone/glock_c_01");point.pitch=new[]{.75f,1f,1.5f}[i];
            }
            foreach(var pair in new[]{("Old Copper","copper"),("Button","pup")}){
                var lion=root.GetComponentsInChildren<HarbourSeaLion>().Single(l=>l.name==pair.Item1);
                var p=lion.gameObject.AddComponent<QuarterErrandPoint>();p.role=pair.Item2;p.prompt="Inspect "+pair.Item1;p.handle=V(0,.87f,.47f);p.reach=3.05f;
            }
            var barn=Object.FindObjectsByType<QuarterSwingDoor>().Single(d=>d.name=="Barn field doors").transform;
            book.suitor=residents["Davi"];book.courier=residents["Pia"];book.barnMeeting=barn.TransformPoint(V(0,.04f,7.3f));
            var horse=new QuarterForms(Palette.Hex("8C5741"));Pelt(horse,V(0,.95f,0),(-1,.15f,.10f,.13f),(-.79f,.20f,.36f,.39f),(-.35f,.22f,.42f,.49f),(.25f,.24f,.41f,.49f),(.73f,.33f,.29f,.45f),(.84f,.45f,.12f,.29f));
            horse.Rod(V(0,1.48f,.57f),V(0,2.14f,.9f),.29f,.18f);horse.Rod(V(0,2.12f,.87f),V(0,1.94f,1.39f),.19f,.12f);
            for(int sign=-1;sign<=1;sign+=2){horse.Rod(V(sign*.095f,2.18f,.87f),V(sign*.13f,2.46f,.86f),.064f,.014f);
                foreach(float z in new[]{-.62f,.52f}){horse.Rod(V(sign*.24f,1.17f,z),V(sign*.28f,.53f,z+.06f),.114f,.075f);horse.Rod(V(sign*.28f,.54f,z+.06f),V(sign*.28f,.035f,z),.078f,.073f);}}
            horse.ink=Dark;horse.Rod(V(0,1.36f,-.79f),V(.05f,.42f,-1.18f),.135f,.072f);
            horse.ink=Gold;Pelt(horse,V(0,.99f,0),(-.63f,.28f,.39f,.41f),(-.38f,.29f,.455f,.47f),(.19f,.3f,.45f,.5f),(.56f,.34f,.40f,.47f),(.68f,.37f,.31f,.42f));
            horse.ink=Teal;horse.Block(V(0,1.78f,-.07f),V(.38f,.12f,.54f));
            book.paradeHorse=Art(root,"The parade horse in gold barding",horse,"Shutters",barn.TransformPoint(V(2.65f,.04f,9.3f))).gameObject;
            book.paradeHorse.transform.rotation=barn.rotation*Quaternion.Euler(0,-25,0);
            book.race=Course(root,court,residents["Moss"]);
            Rewards(root, book);
        }
        static QuarterRace Course(Transform root,Vector3 court,QuarterResident racer)
        {
            var course=Node(root,"The courier circuit",Vector3.zero);var race=course.gameObject.AddComponent<QuarterRace>();race.rival=racer;
            var feet=new List<Vector3>();var times=new List<float>();var arcs=new List<float>();var gates=new List<int>();
            void Step(Vector3 at,float time,float arc=0,bool gate=false){if(feet.Count>0){times.Add(time);arcs.Add(arc);}feet.Add(at);if(gate)gates.Add(feet.Count-1);}
            float x=court.x,y=court.y;
            Step(V(x-1.1f,y,23),0);Step(V(x-1,y+1,26),3,.62f,true);
            for(int i=0;i<5;i++)Step(V(x-2.13f,y+1.65f*(i+1),30+i*2.1f),3.2f,.55f,i==1||i==4);
            Step(V(x-1.03f,y+9,42),2,.45f,true);
            // An old cloth hoist carries a narrow deck between the belfry and the climbing court.
            var hoist=new QuarterForms(Wood);hoist.Block(V(x-1,y+8.82f,47.9f),V(2.25f,.34f,2.3f));
            hoist.Rod(V(x-3,y+16.7f,48),V(x-1.9f,y+8.94f,48.8f),.041f,.041f);
            hoist.Rod(V(x-3,y+16.7f,48),V(x-.05f,y+8.94f,47),.041f,.041f);
            var hoistArt=Art(course,"Courier cloth hoist",hoist);Solid(hoistArt,V(x-1,y+8.82f,47.9f),V(2.25f,.34f,2.3f));
            Step(V(x-1,y+9,47.9f),2.1f,1.35f);Step(V(x-1,y+10.3f,50.9f),.9f,.12f,true);
            Step(V(x-1,y+9,53.7f),1.15f,.06f,true);
            // Wall-mounted landings lead up the actual tiled house behind the belfry.
            for(int i=0;i<5;i++){
                float z=55.1f+i*1.65f,top=y+10.35f+i*1.42f;
                if(!Physics.Raycast(V(-10,top-.12f,z),Vector3.left,out var wall,14,1))throw new System.InvalidOperationException("Missing roof course facade");
                var mid=wall.point+V(.6f,.005f,0);var g=new QuarterForms(i%2==0?Cream:Palette.Sandstone);
                g.Block(mid,V(1.56f,.23f,1.63f));g.ink=Wood;
                foreach(int side in new[]{-1,1})g.Rod(mid+V(-.61f,-.78f,side*.54f),mid+V(.58f,-.12f,side*.54f),.064f,.064f);
                var pad=Art(course,"Courier window seat "+i,g);Solid(pad,mid,V(1.56f,.23f,1.63f));
                Step(mid+Vector3.up*.115f,2.75f,.47f,i==2);
            }
            Step(V(-16.45f,22.15f,62.85f),3.2f,.64f,true);
            Step(V(-11.4f,23.2f,63.4f),1.4f,.6f);
            Step(V(3.8f,19.45f,63.85f),3.8f,0,true);
            Step(V(22.8f,14.6f,64.6f),4.8f,0,true);
            Vector3 Quay(float z){
                float center=Mathf.Sin(z*.017f)*20+Mathf.Sin(z*.039f+.4f)*7;
                float edge=center+17+Mathf.Sin(z*.023f+1)*5+Mathf.Sin(z*.051f)*2+Mathf.Sin(z*.025f+1)*6+Mathf.Sin(z*.067f+1)*2;
                return V(edge+3.4f,4+Mathf.Sin(z*.018f+.7f)*1.15f+Mathf.Sin(z*.043f+1)*.6f,z);
            }
            Step(Quay(64),4.4f,0,true);
            for(int i=0;i<12;i++)Step(Quay(59-i*5),1.3f,0,i%4==1);
            var a=V(29.9f,5.3f,-4);var b=V(-19,2.7f,-12);
            Step(a,3.1f,0,true);
            for(int i=1;i<=8;i++){float t=i/8f;var p=Vector3.Lerp(a,b,t);if(Physics.Raycast(p+Vector3.up*3,Vector3.down,out var hit,5,1))p.y=hit.point.y;Step(p,1.5f,0,i==4||i==8);}
            race.feet=feet.ToArray();race.lengths=times.ToArray();race.arcs=arcs.ToArray();race.gates=gates.ToArray();race.jumpGate=4;race.glideGate=8;
            var hoops=new List<Transform>();
            foreach(int id in gates){
                var g=new QuarterForms(Gold);float r=1.21f;
                for(int n=0;n<40;n++){Vector3 P(int j)=>V(r*Mathf.Cos(j*Mathf.PI/20),r*Mathf.Sin(j*Mathf.PI/20),0);g.ink=n%10==0?Cream:Gold;g.Rod(P(n),P(n+1),n%10==0?.074f:.053f,.053f,6);}
                var hoop=Art(course,"Courier hoop "+(hoops.Count+1),g,"Shutters",feet[id]+Vector3.up*1.05f);
                var heading=feet[id]-feet[id-1];heading.y=0;hoop.rotation=Quaternion.LookRotation(heading);hoops.Add(hoop);
            }
            race.hoops=hoops.ToArray();
            var wing=new QuarterForms(Red);for(int s=-1;s<=1;s+=2){wing.Silk(V(0,2.13f,.55f),V(s*1.84f,1.85f,-.43f),V(0,1.92f,-.65f));wing.ink=Gold;wing.Rod(V(0,2.13f,.55f),V(s*1.84f,1.85f,-.43f),.04f,.02f);wing.ink=Red;}
            race.rivalWing=Art(racer.body,"Moss courier wing",wing).gameObject;race.rivalWing.SetActive(false);
            return race;
        }
    }
}
