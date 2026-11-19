using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static void LeafCrown(QuarterForms g,Vector3 c,Vector3 size,Color green,float seed)
        {
            const int sides=9;
            Vector3 Ring(int i,int band){
                float a=i*Mathf.PI*2/sides+.24f*band+seed;
                float r=(band==1?1:.65f)*(.92f+.13f*Mathf.Sin(i*7.1f+seed*3));
                return c+Vector3.Scale(V(Mathf.Cos(a)*r,(band-1)*.58f+.13f*Mathf.Sin(i+seed),Mathf.Sin(a)*r),size);
            }
            g.wind=.42f;
            for(int i=0;i<sides;i++){
                for(int band=0;band<2;band++){
                    g.ink=Color.Lerp(green,band==0?Dark:Palette.Limestone,band==0?.19f:.06f);
                    g.Face(Ring(i+1,band),Ring(i,band),Ring(i,band+1),Ring(i+1,band+1));
                }
                g.ink=Color.Lerp(green,Palette.Limestone,.14f+.06f*Mathf.Sin(i+seed));
                g.Face(Ring(i+1,2),Ring(i,2),c+Vector3.up*size.y);
                g.ink=Color.Lerp(green,Dark,.24f);
                g.Face(Ring(i,0),Ring(i+1,0),c-Vector3.up*size.y*.83f);
            }
            g.wind=0;
        }
        static void Frond(QuarterForms g,Vector3 root,float angle,float reach,float lift,Color green)
        {
            var d=V(Mathf.Sin(angle),0,Mathf.Cos(angle));var across=Vector3.Cross(Vector3.up,d);
            Vector3 Spine(float t)=>root+d*reach*t+Vector3.up*(lift*Mathf.Sin(t*2.25f)-reach*.17f*t*t);
            for(int j=0;j<7;j++){
                float t=j/7f,u=(j+1)/7f;var a=Spine(t);var b=Spine(u);
                float broad=(.10f+.90f*Mathf.Sin(Mathf.PI*(t+.06f)))*reach*.142f;
                foreach(int sign in new[]{-1,1}){
                    var edge=a+across*(sign*broad)+d*reach*.18f-Vector3.up*broad*.36f;
                    g.ink=Color.Lerp(green,sign>0?Cream:Dark,sign>0?.12f:.09f);
                    g.wind=t*.85f;g.Silk(a,b,edge);
                }
            }
            g.wind=0;
        }
        static QuarterForms WildTree(int kind)
        {
            var bark=Palette.Hex(kind==2?"91785A":kind==4?"766D55":"725A44");
            var green=Palette.Hex(new[]{"56835E","658756","779358","467965","91925B","63895F"}[kind]);
            var g=new QuarterForms(bark);
            float height=kind==4?7.4f:kind==2?9.6f:kind==0?12:10.4f;
            var fork=V(kind==2?.8f:.3f,height*.51f,.22f);
            var peak=V(kind==2?1.6f:-.6f,height,.48f);
            g.Rod(V(0,-.35f,0),fork,kind==2?.42f:.8f,kind==2?.31f:.49f,9);
            g.Rod(fork,peak,kind==2?.31f:.49f,.22f,9);
            if(kind==2){
                for(int j=0;j<13;j++)Frond(g,peak,j*2.39996f,4.8f+j%3,.95f+(j%4)*.48f,green);
                return g;
            }
            for(int j=0;j<6;j++){
                float a=j*Mathf.PI/3+.24f*kind;var d=V(Mathf.Cos(a),0,Mathf.Sin(a));
                g.ink=bark;g.Rod(d*2.2f-Vector3.up*.25f,d*.48f+Vector3.up*1.1f,.28f,.49f,5);
                g.Silk(d*2.7f,d*.7f+Vector3.up*2.65f,d*.14f);
                float span=kind==4?4.2f:3.3f+j%2;
                var elbow=fork+d*span*.55f+Vector3.up*.8f;
                var end=fork+d*span+Vector3.up*(kind==4?2.35f:2.4f+j%3);
                g.Rod(fork,elbow,.31f,.23f,7);g.Rod(elbow,end,.23f,.11f,6);
                var scale=kind==4?V(3.4f,1.23f,3.15f):V(3.1f+j%3,2.25f+j%2,3.6f);
                LeafCrown(g,end,scale,green,j*.7f+kind);
                if(kind==1 && j%2==0){
                    g.ink=Palette.Hex("72805A");g.Rod(end,end-Vector3.up*4.6f,.092f,.037f,5);
                }
            }
            LeafCrown(g,peak,V(4.2f,kind==4?1.4f:2.7f,4.25f),green,kind+2);
            if(kind==0){g.ink=bark;g.Rod(V(0,5.2f,0),V(2,5.09f,0),.51f,.41f,9);g.Rod(V(2,5.09f,0),V(5.1f,5.18f,0),.41f,.16f,8);}
            return g;
        }
        static QuarterForms Understory(bool dry)
        {
            var g=new QuarterForms(Palette.Olive);
            for(int j=0;j<(dry?10:4);j++){
                float a=j*2.39996f;
                var p=V(Mathf.Cos(a)*(j%4)*.91f,0,Mathf.Sin(a)*(j%3)*1.4f);
                for(int i=0;i<7;i++){
                    float turn=i*.898f+a;
                    if(dry){
                        var d=V(Mathf.Cos(turn),0,Mathf.Sin(turn));var s=Vector3.Cross(Vector3.up,d)*.12f;
                        g.ink=Palette.Hex(i%2==0?"C8AD65":"A3A062");g.wind=.75f;
                        g.Silk(p-s,p+s,p+d*(.4f+(i%3)*.19f)+Vector3.up*(.65f+(i%3)*.34f));
                    }else Frond(g,p,turn,1.45f+.3f*(j%3),.62f+.2f*(i%3),Palette.Hex(j%2==0?"4B7954":"759257"));
                }
                if(!dry && j%2==0){
                    g.wind=0;g.ink=Palette.Olive;g.Rod(p,p+Vector3.up*1.72f,.045f,.018f,5);
                    for(int i=0;i<4;i++){g.ink=i%2==0?Red:Gold;g.Rod(p+Vector3.up*(1.15f+i*.13f),p+V((i%2==0?.18f:-.18f),1.34f+i*.13f,.04f),.10f,.012f,6);}
                }
            }
            return g;
        }
        static QuarterForms CanopyCat()
        {
            var ochre=Palette.Hex("BE8A45");var g=new QuarterForms(ochre);
            Pelt(g,Vector3.zero,(-.9f,.28f,.09f,.11f),(-.68f,.32f,.30f,.28f),(-.27f,.33f,.34f,.27f),(.38f,.35f,.27f,.22f),(.61f,.42f,.24f,.20f));
            Pelt(g,Vector3.zero,(.43f,.49f,.18f,.19f),(.64f,.61f,.26f,.23f),(.84f,.60f,.22f,.18f),(.98f,.54f,.15f,.10f));
            foreach(int side in new[]{-1,1}){
                g.ink=ochre;g.Rod(V(side*.166f,.76f,.61f),V(side*.213f,.92f,.60f),.096f,.027f,6);
                g.Rod(V(side*.235f,.30f,.34f),V(side*.27f,.12f,.72f),.11f,.085f);
                g.Rod(V(side*.27f,.12f,.72f),V(side*.25f,.10f,.94f),.085f,.08f);
                g.ink=Dark;g.Rod(V(side*.157f,.654f,.844f),V(side*.169f,.66f,.856f),.03f,.031f,8);
                for(int j=0;j<9;j++){
                    float z=-.62f+j*.126f, y=.39f+.07f*Mathf.Sin(j*4);
                    float w=.325f-.071f*Mathf.Abs(z);
                    g.Rod(V(side*(w-.04f),y,z),V(side*w,y+.012f,z),.057f,.048f,6);
                }
            }
            var tip=V(0,.32f,-.82f);
            for(int j=0;j<9;j++){
                var next=V(.22f*Mathf.Sin((j+1)*.56f),.32f-.057f*j*j*.26f,-.82f-.12f*(j+1));
                g.ink=j%2==0?ochre:Dark;g.Rod(tip,next,.082f-.004f*j,.078f-.004f*j,7);tip=next;
            }
            return g;
        }
        static QuarterForms GrazingAnimal(bool antelope)
        {
            var hide=Palette.Hex(antelope?"BC8959":"765C4E");var g=new QuarterForms(hide);
            float lift=antelope?.4f:0;
            Pelt(g,Vector3.up*lift,(-.82f,.58f,.06f,.09f),(-.62f,.62f,.3f,.35f),(.12f,.67f,.36f,.39f),(.51f,.69f,.27f,.29f),(.65f,.63f,.16f,.22f));
            var head=antelope?V(0,1.65f,.81f):V(0,.65f,.79f);
            g.Rod(V(0,.74f+lift,.43f),head,.22f,antelope?.12f:.22f);
            Pelt(g,head,(-.13f,0,.11f,.13f),(.08f,.03f,.18f,.18f),(.27f,-.065f,.13f,.12f),(.42f,-.11f,.096f,.073f));
            foreach(int s in new[]{-1,1}){
                g.ink=hide;g.Rod(head+V(s*.11f,.12f,.035f),head+V(s*.27f,.36f,-.06f),.09f,.015f,6);
                g.ink=Dark;g.Rod(head+V(s*.148f,.061f,.15f),head+V(s*.164f,.065f,.16f),.029f,.027f);
                if(antelope){
                    var tip=head+V(s*.095f,.16f,0);
                    for(int j=0;j<6;j++){
                        var next=tip+V(s*(.03f-.01f*j),.095f,-.035f+.013f*j);
                        g.Rod(tip,next,.061f-.007f*j,.055f-.008f*j,7);tip=next;
                    }
                }else{g.ink=Cream;g.Rod(head+V(s*.09f,-.12f,.29f),head+V(s*.17f,.012f,.32f),.048f,.008f,6);}
                foreach(float z in new[]{-.44f,.35f}){
                    g.ink=hide;var knee=V(s*.23f,.29f+lift*.55f,z-.056f);
                    g.Rod(V(s*.21f,.59f+lift,z),knee,.102f,.06f);
                    g.ink=Dark;g.Rod(knee,V(s*.24f,.025f,z+.05f),.061f,.067f,6);
                }
            }
            return g;
        }
        static void RuinedCourt(Transform island,Vector3 at,Material stone,System.Func<Vector2,Vector3> ground)
        {
            var t=Node(island,"Court of the broken sun",at);
            var masonry=new QuarterForms(Cream);
            var moss=Palette.Hex("89906B");var ashlar=Palette.Hex("BBA98C");
            void Block(Vector3 p,Vector3 size,int age){
                masonry.ink=Color.Lerp(ashlar,Cream,(age%6)*.095f);
                if(p.y-size.y*.5f < .05f){
                    float top=p.y+size.y*.5f;
                    float bottom=FoundationBottom(ground,at+p,size)-at.y;
                    p.y=(top+bottom)*.5f;size.y=top-bottom;
                }
                masonry.Block(p,size);
                if(age%3==0){masonry.ink=moss;masonry.Block(p+Vector3.up*(size.y*.5f+.021f),V(size.x*.94f,.044f,size.z*.92f));}
            }
            for(int row=0;row<3;row++)for(int i=-3;i<=3;i++){
                if(i>0 && row>0 || i==-1 && row==2)continue;
                Block(V(i*3.3f+.43f*(row%2),.57f+row*1.12f,-9),V(3.26f,1.10f,2.3f),row+6+i);
            }
            for(int s=-1;s<=1;s+=2)for(int j=0;j<4;j++){
                var basePt=V(s*8,0,-5+j*5);int drums=1+(j*3+(s+1)*2)%6;
                Block(basePt+Vector3.up*.32f,V(2.55f,.65f,2.55f),j+3);
                for(int k=0;k<drums;k++){
                    masonry.ink=k==drums-1?moss:ashlar;
                    masonry.Rod(basePt+Vector3.up*(.65f+k*1.02f),basePt+Vector3.up*(1.64f+k*1.02f),.94f-k*.031f,.92f-k*.031f,10);
                }
            }
            // Voussoirs form a freestanding arch over the eastern entrance.
            for(int j=0;j<13;j++){
                float a=j*Mathf.PI/13+.009f,b=(j+1)*Mathf.PI/13-.009f;
                Vector3 P(float angle,float r,float dx)=>V(12+dx,3.08f+r*Mathf.Sin(angle),r*Mathf.Cos(angle));
                masonry.ink=j%4==0?moss:Cream;
                var a0=P(a,3,-1);var a1=P(a,4.1f,-1);var b0=P(b,3,-1);var b1=P(b,4.1f,-1);
                var d=Vector3.right*2;
                masonry.Face(a0,b0,b1,a1);masonry.Face(a1+d,b1+d,b0+d,a0+d);
                masonry.Face(a0,a0+d,b0+d,b0);masonry.Face(a1,b1,b1+d,a1+d);
                masonry.Face(a0,a1,a1+d,a0+d);masonry.Face(b0+d,b1+d,b1,b0);
            }
            foreach(int s in new[]{-1,1})for(int i=0;i<3;i++)Block(V(12,.51f+i*1.02f,s*3.55f),V(2,1.01f,1.12f),i);
            float dialFoot=FoundationBottom(ground,at,V(6.4f,0,6.4f))-at.y;
            masonry.ink=ashlar;masonry.Lathe(Vector3.zero,16,(dialFoot,3.2f),(.32f,3.2f),(.42f,2.9f),(.43f,0));
            masonry.ink=Gold;for(int j=0;j<12;j++){
                var ray=V(Mathf.Cos(j*Mathf.PI/6),0,Mathf.Sin(j*Mathf.PI/6));
                masonry.Rod(ray*.87f+Vector3.up*.45f,ray*2.4f+Vector3.up*.45f,.085f,.025f,5);
            }
            var art=WildMesh(t,"Weathered colonnade and sun dial",Vector3.zero,masonry.Bake("Ruined sun court",WildMeshFile),stone);
            RockCollision(art);
        }
    }
}
