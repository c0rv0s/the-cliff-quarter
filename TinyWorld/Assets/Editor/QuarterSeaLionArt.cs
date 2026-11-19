using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static void Pelt(QuarterForms g,Vector3 at,params (float z,float y,float width,float height)[] sections)
        {
            Vector3 P(int j,(float z,float y,float width,float height) ring){
                float a=j*Mathf.PI/8;return at+V(Mathf.Cos(a)*ring.width,ring.y+Mathf.Sin(a)*ring.height,ring.z);
            }
            for(int i=1;i<sections.Length;i++) for(int j=0;j<16;j++)
                g.Face(P(j+1,sections[i-1]),P(j+1,sections[i]),P(j,sections[i]),P(j,sections[i-1]));
        }
        static void SeaLions(Transform root)
        {
            var colony=Node(root,"Sea lions at the fish pier",Vector3.zero);
            var floatAt=V(-14.8f,.46f,425.65f);
            var raft=Node(colony,"Low landing",floatAt);var timber=new QuarterForms(Wood);
            for(int i=0;i<14;i++)timber.Block(V((i-6.5f)*.46f,-.17f,0),V(.446f,.32f,3.18f));
            for(int i=-1;i<=1;i+=2){timber.Rod(V(i*2.6f,-3,1.43f),V(i*2.6f,.44f,1.43f),.13f,.13f);timber.Strand(V(i*2.6f,.31f,1.43f),V(i*2.6f+.4f,2.82f,-2.05f),.19f,.025f);}
            Art(raft,"Sea lion landing planks",timber);Solid(raft,V(0,-.16f,0),V(6.46f,.32f,3.2f));
            void Lion(string name,Vector3 at,float scale,float yaw,bool swim,int index)
            {
                var t=Node(colony,name,at);t.localRotation=Quaternion.Euler(0,yaw,0);t.localScale=Vector3.one*scale;
                var life=t.gameObject.AddComponent<HarbourSeaLion>();life.phase=index*1.71f;life.swimming=swim;
                var coat=Palette.Hex(new[]{"845F4C","65594F","AC8360","675951","8D705E"}[index]);
                var g=new QuarterForms(coat);
                Pelt(g,Vector3.zero,(-1.25f,.12f,.025f,.025f),(-1.04f,.18f,.16f,.13f),(-.66f,.25f,.34f,.22f),(-.14f,.35f,.48f,.34f),(.24f,.5f,.44f,.46f),(.52f,.62f,.32f,.42f),(.65f,.68f,.20f,.29f),(.68f,.7f,.02f,.03f));
                for(int sign=-1;sign<=1;sign+=2){
                    var fin=V(sign*.11f,.1f,-1.03f);
                    g.ink=Color.Lerp(coat,Dark,.32f);
                    Pelt(g,fin,(-.46f,.01f,.07f,.02f),(-.32f,.024f,.135f,.035f),(-.08f,.035f,.09f,.047f),(.08f,.04f,.042f,.03f));
                }
                Art(t,name+" pelt",g);
                life.head=Node(t,"Head",V(0,.71f,.44f));
                g=new QuarterForms(coat);Pelt(g,Vector3.zero,(-.23f,.03f,.07f,.085f),(-.06f,.18f,.29f,.32f),(.17f,.39f,.26f,.26f),(.35f,.43f,.22f,.21f),(.43f,.42f,.13f,.12f));
                g.ink=Color.Lerp(coat,Cream,.42f);Pelt(g,Vector3.zero,(.27f,.36f,.13f,.11f),(.44f,.37f,.205f,.13f),(.56f,.40f,.11f,.078f),(.59f,.4f,.013f,.016f));
                for(int sign=-1;sign<=1;sign+=2){
                    g.ink=coat;g.Rod(V(sign*.218f,.49f,.1f),V(sign*.255f,.595f,.077f),.063f,.028f);
                    g.ink=Dark;g.Rod(V(sign*.198f,.474f,.348f),V(sign*.213f,.481f,.363f),.029f,.029f);
                    g.ink=Cream;for(int w=0;w<4;w++)g.Rod(V(sign*.13f,.37f+w*.02f,.45f),V(sign*(.32f+.022f*w),.32f+w*.044f,.52f+w*.01f),.009f,.002f,4);
                }
                g.ink=Dark;Pelt(g,Vector3.zero,(.49f,.444f,.06f,.036f),(.555f,.446f,.083f,.044f),(.59f,.434f,.045f,.025f));
                Art(life.head,name+" whiskers and muzzle",g);
                foreach(int sign in new[]{-1,1}){
                    g=new QuarterForms(Color.Lerp(coat,Dark,.25f));
                    var a=V(0,.02f,0);var b=V(sign*.47f,-.24f,-.22f);var c=V(sign*.7f,-.27f,-.57f);
                    g.Rod(a,b,.13f,.112f);g.Rod(b,c,.112f,.025f,7);
                    var fin=Art(t,name+" foreflipper "+sign,g,"Shutters",V(sign*.32f,.30f,.27f));
                    if(sign<0)life.leftFlipper=fin;else life.rightFlipper=fin;
                }
                if(!swim){var body=t.gameObject.AddComponent<CapsuleCollider>();body.direction=2;body.radius=.36f;body.height=1.65f;body.center=V(0,.38f,-.14f);}
            }
            Lion("Old Copper",floatAt+V(-1.85f,0,0),1.12f,-43,false,0);
            Lion("Button",floatAt+V(.03f,0,.18f),.67f,-6,false,2);
            Lion("Mackerel",floatAt+V(1.65f,0,-.02f),.96f,34,false,1);
            Lion("Diver",V(-12,-.43f,430.5f),.98f,17,true,3);
            Lion("Sardine",V(22,-.49f,432),.85f,-23,true,4);
        }
    }
}
