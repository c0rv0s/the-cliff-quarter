using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static void MarketWares(Transform root)
        {
            float Street(float x,float z){
                if(Physics.Raycast(V(x,9,z),Vector3.down,out var hit,9,1))return hit.point.y;
                throw new System.InvalidOperationException("Missing market paving at "+x+", "+z);
            }
            var west=Node(root,"The fish exchange",V(-153,Street(-153,540),541));
            var g=new QuarterForms(Wood);
            foreach(float x in new[]{-10f,10f}){
                g.ink=Wood;g.Rod(V(x,0,0),V(x,4.5f,0),.14f,.1f);Solid(west,V(x,2.2f,0),V(.24f,4.4f,.24f));
            }
            g.Strand(V(-10,4.3f,0),V(10,4.3f,0),.68f,.045f);
            for(int j=0;j<16;j++){
                float x=-9.5f+j*1.21f;float y=3.77f-.68f*(1-x*x/100);
                g.ink=Wood;g.Rod(V(x,y+.55f,0),V(x,y-.05f,0),.012f,.012f,5);
                g.ink=j%3==0?Palette.Hex("79968B"):Palette.Hex("72919C");
                g.Lathe(V(x,y-1.12f,0),8,(0,.02f),(.14f,.115f),(.43f,.19f),(.72f,.13f),(.9f,.043f));
                g.Silk(V(x,y-.31f,0),V(x-.23f,y+.02f,0),V(x+.22f,y+.01f,0));
                g.ink=Cream;g.Rod(V(x-.096f,y-.95f,-.066f),V(x-.099f,y-.95f,-.092f),.024f,.022f);
            }
            Art(west,"Silver mullet drying rack",g);
            var tents=Node(root,"Potters’ arcade",V(172,Street(172,526),528));
            var sail=new QuarterForms(Teal);var frame=new QuarterForms(Wood);
            foreach(float x in new[]{-7.4f,7.4f})foreach(float z in new[]{-9.3f,9.3f}){
                float h=x<0?5.4f:6.4f;
                frame.ink=Wood;frame.Rod(V(x,0,z),V(x,h+.42f,z),.13f,.085f);
                Finial(frame,V(x,h+.4f,z),.82f);
                Solid(tents,V(x,h/2,z),V(.19f,h,.19f));
            }
            Vector3 Cloth(float u,float v)=>V((u-.5f)*14.8f,5.4f+u-.74f*Mathf.Sin(v*Mathf.PI)-.63f*Mathf.Sin(u*Mathf.PI),(v-.5f)*18.6f);
            for(int i=0;i<10;i++)for(int j=0;j<8;j++){
                float a=i*.1f,b=(i+1)*.1f,c=j*.125f,d=(j+1)*.125f;
                var p=Cloth(a,c);var q=Cloth(b,c);var r=Cloth(b,d);var s=Cloth(a,d);
                sail.ink=i%5==0?Cream:i%5==1?Gold:Teal;sail.wind=.35f;
                sail.Silk(p,q,r);sail.Silk(p,r,s);
                if(j==0||j==7){
                    var l=j==0?p:s;var h=j==0?q:r;sail.ink=i%2==0?Cream:Red;
                    sail.Silk(l,h,(l+h)/2+Vector3.down*.47f);
                }
            }
            for(int j=0;j<8;j++){
                var p=V(-4.9f+(j%4)*1.32f,0,-2.1f+(j/4)*1.2f);
                PaintedUrn(frame,p,.94f+(j%3)*.28f,j%2==0?Blue:Red);
            }
            Art(tents,"Ochre-edged turquoise awning",sail,"Cloth");
            Art(tents,"Potters’ poles and vessels",frame);
        }
    }
}
