using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        const float CaveX=-136, VentX=-117, CaveHalf=3, VentRise=11.85f;
        static float BluffDepth(float x)
        {
            // Keep the quay frontage open; farther inland the spur runs out in long scalloped toes.
            float relief=Mathf.SmoothStep(0,1,Mathf.InverseLerp(48,103,Mathf.Abs(x)));
            return Mathf.Lerp(6.5f,26 + 6*Mathf.Sin(x*.075f) + 3*Mathf.Cos(x*.18f),relief);
        }
        static float BluffZ(float x,float f)
        {
            float ledge=(Mathf.Sin(f*Mathf.PI*4.5f+x*.042f)*1.25f + Mathf.Sin(x*.29f+f*3)*.65f)*Mathf.Sin(f*Mathf.PI);
            return 399.25f+(1-Mathf.Pow(f,1.04f))*BluffDepth(x)+ledge;
        }
        static MeshCollider RockCollision(Transform t)
        {
            var c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=t.GetComponent<MeshFilter>().sharedMesh;return c;
        }
        static void HarbourHeadlands(Transform root)
        {
            var coast=Node(root,"Saltwind headlands",Vector3.zero);
            float Height(float x)
            {
                float high=-7;
                foreach(var h in Physics.RaycastAll(V(x,245,399.45f),Vector3.down,260,1,QueryTriggerInteraction.Ignore))
                {
                    var parent=h.transform.parent;
                    if(parent && (parent.name.EndsWith("escarpment")||parent.name.EndsWith("uplands")))high=Mathf.Max(high,h.point.y);
                }
                return high;
            }
            float Base(float x)
            {
                int side=x<0?-1:1;
                float expected=4+Mathf.Sin(400*.018f+side*.7f)*1.15f+Mathf.Sin(400*.043f+side)*.6f-.03f;
                return expected;
            }
            var xs=new List<float>();
            for(int i=-300;i<=300;i+=2)xs.Add(i);
            foreach(float center in new[]{CaveX,VentX})
                for(int i=0;i<=12;i++)xs.Add(center-CaveHalf+i*.5f);
            xs.Sort();
            for(int i=xs.Count-1;i>0;i--)if(xs[i]-xs[i-1]<.015f)xs.RemoveAt(i);
            float Arch(float x,float center)
            {
                float t=(x-center)/CaveHalf;
                float shape=Mathf.Sqrt(Mathf.Max(0,1-t*t));
                return shape*(3.35f+.52f*Mathf.Sin(t*4+.6f)+.28f*Mathf.Sin(t*9-1));
            }
            Vector3 Band(float x,float top,int layer)
            {
                float ground=Base(x);
                float y=layer switch{0=>ground-.12f,1=>ground+.18f+Arch(x,CaveX),
                    2=>ground+VentRise,3=>ground+VentRise+.24f+Arch(x,VentX),
                    4=>Mathf.Lerp(ground,top,.40f),5=>Mathf.Lerp(ground,top,.62f),
                    6=>Mathf.Lerp(ground,top,.83f),_=>top+.02f};
                if(top<ground+36)y=Mathf.Lerp(ground-.12f,top+.02f,layer/7f);
                float f=Mathf.InverseLerp(ground-.12f,top+.02f,y);
                return V(x,y,BluffZ(x,f));
            }
            for(int side=-1;side<=1;side+=2)
            {
                var sh=new QuarterForms(Cream);
                float lastX=0,lastH=0;bool continued=false;
                foreach(float x in xs)
                {
                    if(side*x<=0)continue;
                    float height=Height(x);
                    if(height<Base(x)+.8f) {continued=false;continue;}
                    if(continued)
                    {
                        for(int j=0;j<7;j++)
                        {
                            if(j==0&&lastX>=CaveX-CaveHalf-.01f&&x<=CaveX+CaveHalf+.01f)continue;
                            if(j==2&&lastX>=VentX-CaveHalf-.01f&&x<=VentX+CaveHalf+.01f)continue;
                            var a=Band(lastX,lastH,j);var b=Band(x,height,j);var c=Band(x,height,j+1);var d=Band(lastX,lastH,j+1);
                            sh.ink=Color.Lerp(Palette.Hex("EBD4AD"),Palette.Hex("B69478"),.13f+.38f*Mathf.PerlinNoise(x*.053f,j*.63f));
                            sh.Face(a,b,c);sh.Face(a,c,d);
                        }
                        sh.ink=Palette.Sandstone;
                        var l=Band(lastX,lastH,7);var r=Band(x,height,7);
                        sh.Face(l,r,r+V(0,-.15f,-1.8f),l+V(0,-.15f,-1.8f));
                    }
                    lastX=x;lastH=height;continued=true;
                }
                RockCollision(Art(coast,side<0?"West salt bluffs":"East harbour bluffs",sh,"Cliff rock"));
            }
            float deck=Base(CaveX);
            Vector3 Surface(float x,int row)=>Band(x,Height(x),row);
            Grotto(coast,Surface);
            CoastalFolds(coast,Height,Base);
            var talus=new QuarterForms(Cream);var scrub=new QuarterForms(Palette.Olive);
            for(int i=0;i<122;i++)
            {
                float x=-256 + i*4.13f + 1.1f*Mathf.Sin(i*2.4f);
                float height=Height(x),floor=Base(x);
                if(height<floor+8 || Mathf.Abs(x-CaveX)<7 || x>CaveX+3&&x<CaveX+24)continue;
                float f=.002f;
                float y=Mathf.Lerp(floor,height,f);
                var pos=V(x,y-.7f,BluffZ(x,f)+1.22f);
                float n=Mathf.PerlinNoise(x*.12f,3);
                float w=1.2f+3.8f*n;
                float h=i%5==0?5.6f+4*n:1.7f+3.2f*n;
                Boulder(talus,pos,V(w,h,2.2f+1.8f*n),i);
                if(i%2==0)Grass(scrub,pos+V(-.12f*w,h*.79f,.3f),.6f+n,i);
                if(i%4==0)Boulder(talus,pos+V(1.45f,0,2.6f),V(1.7f,.7f,1.4f),i+31);
            }
            RockCollision(Art(coast,"Weathered coastal talus",talus));
            Art(coast,"Salt grasses and sea thrift",scrub,"Foliage");

            var ledges=new QuarterForms(Cream);
            var marker=Node(coast,"Salt stair",Vector3.zero);
            for(int i=0;i<12;i++)
            {
                float t=i/11f;
                float x=Mathf.Lerp(CaveX+4.5f,VentX-1,t);
                float y=deck+Mathf.Lerp(.44f,VentRise,t);
                var top=Height(x);var f=Mathf.InverseLerp(deck-.45f,top+.02f,y);
                float lip=2.45f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.92f,t));
                var p=V(x,y,BluffZ(x,f)+.55f+lip);
                // A chiseled top, an undercut nose, and a root buried in the cliff.
                ledges.ink=i%3==0?Palette.Limestone:Palette.Sandstone;
                var a=p+V(-1.12f,0,-1.9f-lip);var b=p+V(-1.23f,0,.96f);var c=p+V(.68f,0,1.32f);var d=p+V(1.13f,0,.65f);var e=p+V(.95f,0,-1.9f-lip);
                ledges.Face(a,b,c,d,e);
                foreach(var pair in new[]{(a,b),(b,c),(c,d),(d,e)}){
                    var u=pair.Item1;var v=pair.Item2;
                    var lowerU=Vector3.Lerp(u,p+V(.05f,-1.15f,-.79f),.44f);
                    var lowerV=Vector3.Lerp(v,p+V(.05f,-1.15f,-.79f),.44f);
                    ledges.ink=Palette.Hex("C5AE90");ledges.Face(v,u,lowerU,lowerV);
                    ledges.ink=Palette.Hex("9E8C7F");ledges.Face(lowerV,lowerU,p+V(.05f,-1.24f,-1.62f-lip));
                }
                Node(marker,"Shelf "+(i+1),p+V(-.58f,.03f,.81f));
            }
            RockCollision(Art(coast,"Climbable salt shelves",ledges,"Carved limestone"));
        }
        static void Boulder(QuarterForms g,Vector3 p,Vector3 size,int seed)
        {
            int sides=8+seed%3;
            Vector3 R(int i,int level)
            {
                float a=i*2*Mathf.PI/sides+seed*.77f;
                float n=Mathf.PerlinNoise(Mathf.Cos(a)*2+seed,Mathf.Sin(a)*2+level*.16f);
                float radius=(.77f+.27f*n)*(level==0?.83f:level==1?1:level==2?.84f:.49f);
                float h=level switch{0=>-.18f,1=>.20f,2=>.57f,_=>.88f};
                return p+Vector3.Scale(size,V(Mathf.Cos(a)*radius+.12f*h,h+n*.10f-.11f,Mathf.Sin(a)*radius));
            }
            for(int j=0;j<sides;j++)
            {
                for(int k=0;k<3;k++)
                {
                    g.ink=Color.Lerp(Palette.Hex(k==0?"9B978A":"D1BEA5"),Palette.Hex(k==0?"827C74":"AB9B8F"),Mathf.PerlinNoise(j*.83f+seed,k*.36f));
                    g.Face(R(j+1,k),R(j,k),R(j,k+1),R(j+1,k+1));
                }
                g.ink=Palette.Hex("CDBDA2");g.Face(R(j+1,3),R(j,3),p+Vector3.up*size.y*.88f);
            }
        }
        static void Grass(QuarterForms g,Vector3 p,float size,int seed)
        {
            for(int j=0;j<16;j++)
            {
                float a=2.4f*j+seed;var d=V(Mathf.Cos(a),0,Mathf.Sin(a));
                var root=p+d*(.3f*size);var blade=Vector3.Cross(Vector3.up,d)*(.042f*size);
                g.wind=.8f;g.ink=j%4==0?Palette.Hex("B7A16B"):Palette.Olive;
                var tip=root+(d*.28f+V(.15f,.35f+.18f*Mathf.Sin(j),.06f))*size;
                g.Silk(root-blade,root+blade,tip);
                if(j%4==0){g.ink=Palette.Hex("C78791");g.Lathe(tip,5,(-.06f,.014f),(0,.068f),(.05f,.024f));}
            }
        }
        static void Pool(Transform t)
        {
            var prop=t.gameObject.AddComponent<GrottoPool>();prop.drip=Clip("SplashSmall/surface_01");prop.handle=V(.14f,.64f,.5f);
            var g=new QuarterForms(Palette.Hex("245957"));
            for(int j=0;j<27;j++)
            {
                Vector3 P(int k,float r){float a=Mathf.PI*2*k/27;float lip=1+.088f*Mathf.Sin(3*a+.4f)+.056f*Mathf.Cos(5*a);return V(Mathf.Cos(a)*1.58f*r*lip,.012f,Mathf.Sin(a)*r*lip);}
                g.ink=Palette.Hex("466F6B");g.Face(P(j+1,1.16f),P(j+1,1),P(j,1),P(j,1.16f));
                g.ink=Palette.Hex("398780");g.Silk(Vector3.zero,P(j+1,1),P(j,1));
            }
            Art(t,"Verdigris spring basin",g);
            var ripple=new QuarterForms(Palette.Shallows);
            for(int j=0;j<40;j++)
            {
                Vector3 P(int k,float r){float a=Mathf.PI*2*k/40;return V(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}
                ripple.Face(P(j,1),P(j+1,1),P(j+1,.981f),P(j,.981f));ripple.Face(P(j,.981f),P(j+1,.981f),P(j+1,1),P(j,1));
            }
            prop.rings=new Transform[3];prop.rings[0]=Art(t,"Ripple crest",ripple,"Lantern glow",Vector3.up*.035f);
            for(int j=1;j<3;j++)prop.rings[j]=UnityEngine.Object.Instantiate(prop.rings[0],t);
            foreach(var r in prop.rings)r.gameObject.SetActive(false);
        }
    }
}
