using System;
using System.Linq;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static void Residents(Transform root, Vector3 court)
        {
            Resident(root,"Mira", V(court.x+2.5f,4,20),Vector3.forward,Teal,Red,0,
                "Five pipes, five notes. Give them a brush.","I dye the sails before the morning wind.");
            Resident(root,"Ivo",V(12,4,-7.36f),Vector3.right,Red,Blue,1,
                "Ring the roof bell. Watch the arch when you do.","The lotus slip is at the foot of the river shrine.");
            Resident(root,"Nera",V(81,5,452),Vector3.forward,Blue,Gold,2,
                "Mum used to turn that ring around when she was thinking.","Glass looks like a jewel if you hold it up to the sun.");
            Resident(root,"Sami",V(-142,132,42),Vector3.right,Palette.Olive,Teal,3,
                "Stand on the orchestra floor. Even a whisper carries.","The cut stairs climb from here to the royal courtyard.");

            Resident(root,"Lina",V(-12.44f,3.14f,11.0f),Vector3.forward,Blue,Pink,0,
                "We set the crab pots when the water reaches the third step.");
            Resident(root,"Ari",V(-15.50f,3.94f,29.0f),Vector3.back,Red,Cream,1,
                "I can tell which sail is coming in by its stitching.");
            Resident(root,"Yara",V(-9.83f,4.61f,45.0f),Vector3.forward,Teal,Gold,2,
                "The chime maker lets me try the pipes after market.");
            Resident(root,"Tomas",V(44.68f,5.52f,13.0f),Vector3.back,Palette.Olive,Red,3,
                "Yesterday I carried six jars across that bridge. Five came back.");
            Resident(root,"Esme",V(-20.74f,2.54f,-9.0f),Vector3.forward,Cream,Blue,0,
                "That boat with the patched sail? My brother made the patch.");
            Resident(root,"Davi",V(50.82f,5.22f,47.0f),Vector3.back,Pink,Dark,1,
                "I trade olives for a ride upriver.");
            Resident(root,"Vela",V(-8.44f,5.13f,85.0f),Vector3.forward,Gold,Teal,2,
                "There is a sheltered spot to swim beside the shrine.");
            Resident(root,"Lio",V(28.03f,4.21f,114.0f),Vector3.back,Blue,Pink,3,
                "The river pulls harder around the shrine pillars.");
            Resident(root,"Noor",V(-5.95f,4.79f,167.0f),Vector3.forward,Red,Cream,0,
                "When the bell rings, look up before the swallows settle.");
            Resident(root,"Pia",V(14.00f,2.49f,212.0f),Vector3.back,Teal,Gold,1,
                "My basket was full of figs when I left home.");
            Resident(root,"Remi",V(-52.26f,2.32f,286.0f),Vector3.forward,Palette.Olive,Red,2,
                "I take the low road home. Fewer stairs with a full basket.");
            Resident(root,"Fara",V(12.72f,4.66f,316.0f),Vector3.back,Cream,Blue,3,
                "I watch the boats from this crossing.");
            Resident(root,"Nilo",V(-27.18f,31.15f,68.0f),Vector3.forward,Pink,Dark,0,
                "Everyone sends me up for saffron and down for salt.");
            Resident(root,"Ada",V(52.20f,26.49f,90.0f),Vector3.back,Gold,Teal,1,
                "The shade moves to this side after lunch.");
            Resident(root,"Sora",V(-24.74f,26.23f,187.0f),Vector3.forward,Blue,Pink,2,
                "Someone up the hill is baking flatbread.");
            Resident(root,"Enzo",V(53.23f,52.69f,193.0f),Vector3.back,Red,Cream,3,
                "You can hear the instruments from across the gorge.");
            Resident(root,"Tali",V(-40.01f,51.15f,180.0f),Vector3.forward,Teal,Gold,0,
                "I keep a little mint on my windowsill.");
            Resident(root,"Oren",V(55.49f,90.09f,270.0f),Vector3.back,Palette.Olive,Red,1,
                "Up here I see the masts before the boats.");
            Resident(root,"Iris",V(-88.25f,82.88f,245.0f),Vector3.forward,Cream,Blue,2,
                "Have you seen the kite turn in the crosswind?");
            Resident(root,"Dima",V(-142.00f,132.00f,26.0f),Vector3.back,Pink,Dark,3,
                "The stone seats stay warm long after sunset.");
            Resident(root,"Salma",V(-198,146,233),Vector3.forward,Gold,Teal,0,
                "The sun glints on the temple doors each morning.");
            Resident(root,"Nico",V(-45.00f,5.00f,457.0f),Vector3.back,Blue,Pink,1,
                "Three crews asked for rope this morning.");
            Resident(root,"Rosa",V(-77.00f,5.00f,440.0f),Vector3.forward,Red,Cream,2,
                "I keep an eye on that little opening in the cliff.");
            Resident(root,"Belen",V(47.00f,5.00f,449.0f),Vector3.back,Teal,Gold,3,
                "The gulls always find the fish before anyone else.");
            Resident(root,"Paco",V(101.00f,5.00f,466.0f),Vector3.forward,Palette.Olive,Red,0,
                "The captain swears the old ship sails faster.");
            var temple=UnityEngine.Object.FindObjectsByType<QuarterSwingDoor>().Single(d=>d.name=="Temple of the Sun").transform;
            var palace=UnityEngine.Object.FindObjectsByType<QuarterSwingDoor>().Single(d=>d.name=="Doors of the royal audience").transform;
            var king=Resident(root,"King Aurelio",palace.TransformPoint(V(0,1.68f,36)),-palace.right,Blue,Gold,2,
                "See the notch in the eastern tower? My daughter measures the midsummer sun there.",
                "You came by the long stair. Take the gallery on your way down. You can see every sail.");
            king.pace=0; king.body.rotation=Quaternion.LookRotation(-palace.forward);
            var jester=Resident(root,"Pip the jester",palace.TransformPoint(V(-5,0,19)),palace.forward,Red,Teal,0,
                "I asked for a raise. His Majesty pointed to the stairs.","Four bells on my hat and still I sleep through noon.");
            CourtDress(king,true); CourtDress(jester,false);
            Resident(root,"Herald Elia",palace.TransformPoint(V(7,0,12)),palace.forward,Teal,Gold,1,
                "Three knocks for a ship from home. Two for a ship from abroad.");
            Resident(root,"Royal cartographer",palace.TransformPoint(V(-9,0,4)),palace.forward,Palette.Olive,Cream,3,
                "On the southern chart I left the trees uncounted. I ran out of green.");
            Resident(root,"Tess",V(185,5,521),Vector3.right,Teal,Red,1,"We open the bidding when the shade reaches the compass.");
            Resident(root,"Rafi",V(246,5,486),Vector3.right,Cream,Blue,2,"The saffron goes upstairs. Even a little canal water spoils it.");
            Resident(root,"Inigo",V(-160,4,548),Vector3.right,Blue,Gold,3,"One hook for mullet, two for squid. The gulls take either.");
            Resident(root,"Lea",V(-216,4,485),Vector3.left,Red,Cream,2,"I can splice a rope faster than my sister can measure it.");
            var arrivals=new[]{
                ("Cleo",V(-12,3,4),"That brass ring course? I made the first hoop oval by sitting on it."),
                ("Moss",V(-14,4,16),"Every bell has a different voice."),
                ("Oli",V(-14,4,34),"I saw a bird fly off with half a spool this morning."),
                ("Mina",V(-11,4,50),"Take the low quay away from the ships and you’ll smell salt."),
                ("Edda",V(43,5,21),"I sell buttons by the dozen. Nobody needs eleven."),
                ("Berto",V(-21,3,-18),"The tidekeeper paints every rock she runs aground on."),
                ("Isa",V(-57,5,451),"Old Copper has a better spot on the dock than I do."),
                ("Vico",V(-47,5,460),"The little one can swallow a whole sardine."),
                ("Alba",V(51,5,452),"Three knots and this basket will hold."),
                ("Ines",temple.TransformPoint(V(2,0,10)),"The lowest bell is the one that carries farthest."),
                ("Omar",temple.TransformPoint(V(3,0,-7)),"It takes two of us to polish each temple door."),
                ("Eli",V(-144,132,33),"They used to let me do the echo before the show."),
                ("Luz",V(-207,146,232),"My shadow always gets to the temple first.")
            };
            for(int i=0;i<arrivals.Length;i++){
                var (name,at,words)=arrivals[i]; Resident(root,name,at,i%2==0?Vector3.forward:Vector3.back,
                    new[]{Pink,Teal,Cream,Gold,Blue}[i%5],new[]{Dark,Red,Blue}[i%3],i%4,words);
            }
            foreach(var d in UnityEngine.Object.FindObjectsByType<QuarterSwingDoor>()){
                if(d.name!="Salt store" && d.name!="Barn field doors" && d.name!="Tidekeeper's chart room")continue;
                var seed=d.transform.TransformPoint(V(2,0,-3.1f));
                Resident(root,d.name=="Salt store"?"Sol":d.name=="Barn field doors"?"Paz":"Lua",seed,d.transform.right,Cream,Teal,2,
                    d.name=="Salt store"?"Salt is ready when the crystals stack like stairs.":d.name=="Barn field doors"?"Even the fancy tack needs oil.":"A different blue for every depth. That’s my rule.");
            }
        }
        static void CourtDress(QuarterResident who,bool king)
        {
            var cap=new QuarterForms(king?Gold:Teal);
            cap.Lathe(Vector3.zero,16,(.24f,.16f),(.29f,.174f),(.39f,.16f));
            for(int j=0;j<(king?8:3);j++){
                float angle=j*2*Mathf.PI/(king?8:3);
                var ray=V(Mathf.Cos(angle),0,Mathf.Sin(angle));
                if(king){
                    cap.ink=Gold;cap.Rod(ray*.15f+Vector3.up*.36f,ray*.155f+Vector3.up*(j%2==0?.54f:.46f),.056f,.012f,4);
                }else{
                    cap.ink=j==0?Red:j==1?Teal:Gold;
                    var bend=ray*.19f+Vector3.up*.54f;
                    var end=ray*.31f+Vector3.up*.31f;
                    cap.Rod(ray*.09f+Vector3.up*.36f,bend,.098f,.061f);
                    cap.Rod(bend,end,.061f,.012f);cap.ink=Gold;cap.Rod(end,end+Vector3.down*.045f,.039f,.026f);
                }
            }
            Art(who.head,king?"Eight-point royal diadem":"Pip’s bell cap",cap);
            var mantle=new QuarterForms(king?Palette.Hex("853E48"):Gold);
            mantle.Lathe(Vector3.zero,16,(1.13f,.266f),(1.3f,.254f),(1.405f,.127f));
            if(king){
                mantle.Silk(V(-.21f,1.29f,-.11f),V(.22f,1.29f,-.11f),V(.29f,.26f,-.23f));
                mantle.Silk(V(-.21f,1.29f,-.11f),V(.29f,.26f,-.23f),V(-.28f,.25f,-.26f));
            }
            Art(who.body,king?"Crimson coronation mantle":"Jester’s collar",mantle);
        }
        static QuarterResident Resident(Transform root,string name,Vector3 seed,Vector3 bearing,Color coat,Color trim,int variant,params string[] lines)
        {
            if(!Patrol(seed,bearing,out var a,out var b)) throw new InvalidOperationException("No clear street for "+name);
            return ResidentFigure(root,name,a,b,coat,trim,variant,name!="King Aurelio" && name!="Pip the jester",lines);
        }

        static QuarterResident ResidentFigure(Transform root,string name,Vector3 a,Vector3 b,Color coat,Color trim,int variant,bool wares,params string[] lines)
        {
            var t=Node(root,name,a);t.gameObject.layer=2;
            var walker=t.gameObject.AddComponent<CharacterController>();walker.radius=.25f;walker.height=1.64f;walker.center=Vector3.up*.82f;walker.stepOffset=.27f;walker.skinWidth=.025f;
            var n=t.gameObject.AddComponent<QuarterResident>();n.firstName=name;n.lines=lines;n.end=b;n.pace=1.05f+.11f*variant;
            n.reach=2.65f;n.handle=V(0,1.28f,0);
            n.body=Node(t,"Figure",Vector3.zero);n.body.localRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(b-a,Vector3.up));
            var skin=Palette.Hex(new[]{"AE714F","965B3F","B77955","B98767"}[variant]);
            var g=new QuarterForms(coat);
            g.Lathe(Vector3.zero,12,(.61f,.22f),(.65f,.225f),(1.01f,.177f),(1.28f,.222f),(1.37f,.18f),(1.4f,.09f));
            g.ink=trim;g.Lathe(Vector3.zero,12,(.66f,.224f),(.70f,.22f));g.Lathe(Vector3.zero,12,(.97f,.185f),(1.04f,.185f));
            g.ink=Cream; g.Silk(V(-.17f,1.31f,.135f),V(-.09f,1.37f,.14f),V(.14f,.98f,.12f));
            g.ink=skin;g.Rod(V(0,1.34f,0),V(0,1.47f,0),.076f,.07f);
            Art(n.body,name+" coat",g);
            n.head=Node(n.body,"Head",V(0,1.42f,0));
            g=new QuarterForms(skin);g.Lathe(Vector3.zero,14,(0,.078f),(.06f,.114f),(.19f,.138f),(.28f,.122f),(.32f,.064f));
            g.Rod(V(0,.14f,.11f),V(0,.13f,.166f),.037f,.021f,6);
            for(int side=-1;side<=1;side+=2){g.ink=Dark;g.Rod(V(side*.052f,.182f,.112f),V(side*.052f,.182f,.129f),.014f,.013f,8);}
            g.ink=trim;
            if(variant%2==0){g.Lathe(Vector3.zero,16,(.22f,.146f),(.26f,.16f),(.34f,.125f),(.37f,.066f));g.Rod(V(0,.25f,-.095f),V(.09f,-.02f,-.15f),.07f,.03f);}
            else {g.Lathe(Vector3.zero,18,(.22f,.224f),(.244f,.231f),(.25f,.14f),(.34f,.121f),(.35f,.086f));g.ink=Red;g.Lathe(Vector3.zero,18,(.255f,.141f),(.279f,.137f));}
            Art(n.head,name+" face and headdress",g);
            for(int side=-1;side<=1;side+=2)
            {
                g=new QuarterForms(coat);g.Rod(Vector3.zero,V(side*.035f,-.223f,.02f),.077f,.06f);
                g.ink=skin;g.Rod(V(side*.035f,-.22f,.02f),V(side*.035f,-.40f,.065f),.052f,.041f);
                g.Rod(V(side*.035f,-.38f,.064f),V(side*.034f,-.46f,.074f),.049f,.036f);
                var arm=Art(n.body,name+" sleeve "+side,g,"Shutters",V(side*.203f,1.30f,0));
                if(side<0)n.armL=arm;else n.armR=arm;
                g=new QuarterForms(Dark);g.Rod(Vector3.zero,V(0,-.43f,0),.083f,.055f);
                g.ink=Wood;g.Rod(V(0,-.38f,0),V(0,-.55f,.053f),.067f,.075f);g.Rod(V(0,-.54f,0),V(0,-.55f,.132f),.073f,.052f);
                var leg=Art(n.body,name+" boot "+side,g,"Shutters",V(side*.104f,.62f,0));
                if(side<0)n.legL=leg;else n.legR=leg;
            }
            g=new QuarterForms(Teal);
            if(variant%2==0) PaintedUrn(g,V(.015f,-.69f,.1f),.47f,variant==0?Blue:Red);
            else
            {
                g.ink=Wood;g.Lathe(V(0,-.70f,.15f),12,(0,.10f),(.14f,.18f),(.17f,.185f));
                for(int i=0;i<6;i++){g.ink=i%2==0?Gold:Red;g.Lathe(V(.095f*Mathf.Cos(i),-.53f,.15f+.09f*Mathf.Sin(i)),8,(-.04f,.025f),(0,.057f),(.045f,.015f));}
            }
            if(wares) Art(n.armR,name+" wares",g);
            return n;
        }
        static bool Patrol(Vector3 seed,Vector3 heading,out Vector3 start,out Vector3 end)
        {
            bool Floor(Vector3 x,out Vector3 foot)
            {
                foot=default;
                if(!Physics.Raycast(x+Vector3.up*2.2f,Vector3.down,out var hit,4.3f,1,QueryTriggerInteraction.Ignore) || hit.normal.y<.87f)return false;
                foot=hit.point+Vector3.up*.035f;
                return !Physics.CheckCapsule(foot+Vector3.up*.35f,foot+Vector3.up*1.43f,.27f,1,QueryTriggerInteraction.Ignore);
            }
            for(int ring=0;ring<=7;ring++) for(int slot=0;slot<(ring==0?1:12);slot++)
            {
                var origin=seed+Quaternion.Euler(0,30*slot,0)*Vector3.forward*(ring*1.55f);
                if(!Floor(origin,out start))continue;
                for(int angle=0;angle<12;angle++)
                {
                    var d=Quaternion.Euler(0,angle*30,0)*heading; var prev=start;
                    for(int j=1;j<=22;j++)
                    {
                        var sample=prev+d*.55f;
                        if(!Floor(sample,out var foot)||Mathf.Abs(foot.y-prev.y)>.19f ||
                            Physics.CapsuleCast(prev+Vector3.up*.34f,prev+Vector3.up*1.43f,.26f,d,.55f,1,QueryTriggerInteraction.Ignore)) break;
                        prev=foot;
                        if(j>=18){end=foot;return true;}
                    }
                }
            }
            start=end=default;return false;
        }
    }
}
