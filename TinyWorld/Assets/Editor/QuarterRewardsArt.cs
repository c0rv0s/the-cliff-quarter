using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        static void Rewards(Transform root, QuarterQuests book)
        {
            var t = Node(root, "Belen’s painted skiff", V(-14.8f, .02f, 428.44f));
            t.rotation = Quaternion.Euler(0, 90, 0);
            var boat = t.gameObject.AddComponent<QuarterSkiff>();
            book.skiff = boat; boat.reach = 3.5f; boat.handle = V(0,.88f,0);
            var hull = new QuarterForms(Wood);
            float[] z = {-2.08f,-1.66f,-.87f,.48f,1.47f,2.13f};
            float[] widths = {.055f,.52f,.76f,.81f,.56f,.035f};
            foreach (int side in new[] {-1,1}) {
                Vector3 Plank(int station,int course) {
                    float f = course / 4f;
                    return V(side * widths[station] * Mathf.Lerp(.26f, 1, f),
                        -.29f + .82f*f + Mathf.Pow(Mathf.Abs(z[station])/2.13f, 2)*.28f, z[station]);
                }
                for (int i=0; i<z.Length-1; i++) {
                    for (int j=0; j<4; j++) {
                        var a=Plank(i,j);var b=Plank(i+1,j);var c=Plank(i+1,j+1);var d=Plank(i,j+1);
                        hull.ink=j<2?Teal:j==2?Blue:Red;
                        hull.Silk(a,b,c); hull.Silk(a,c,d);
                    }
                    hull.ink=Cream; hull.Rod(Plank(i,4),Plank(i+1,4),.038f,.038f);
                    hull.ink=Wood; hull.Silk(Plank(i,0),Plank(i+1,0),V(0,-.3f,z[i+1]));
                    hull.Silk(Plank(i,0),V(0,-.3f,z[i+1]),V(0,-.3f,z[i]));
                }
                var oar = new QuarterForms(Wood);
                oar.Rod(V(-side*.53f,.12f,0),V(side*.9f,-.17f,0),.035f,.052f);
                oar.ink=Teal;oar.Rod(V(side*.83f,-.16f,0),V(side*1.32f,-.28f,0),.10f,.13f,5);
                var blade=Art(t,side<0?"Port oar":"Starboard oar",oar,"Shutters",V(side*.70f,.53f,.14f));
                if(side<0) boat.portOar=blade; else boat.starboardOar=blade;
            }
            hull.ink=Wood;
            foreach(float station in new[]{-1.14f,-.14f,.90f}) hull.Block(V(0,.36f,station),V(1.19f,.095f,.3f));
            Art(t,"Painted lapstrake hull",hull);
            var regalia = new QuarterForms(Blue);
            foreach (int side in new[]{-1,1}) {
                var shoulder=V(side*.215f,.23f,0);
                regalia.ink=Gold;regalia.Lathe(shoulder,10,(-.015f,.104f),(.025f,.12f),(.078f,.086f),(.10f,.038f));
                regalia.ink=Teal;regalia.Lathe(shoulder,10,(.032f,.116f),(.062f,.097f));
                for(int tassel=0;tassel<4;tassel++){
                    var knot=shoulder+V(side*.084f,-.002f,(tassel-1.5f)*.039f);
                    regalia.ink=Gold;regalia.Rod(knot,knot+V(side*.018f,-.081f,0),.014f,.009f,5);
                }
            }
            book.paradeMantle=Art(root,"Courier parade epaulettes",regalia).gameObject;
            var lantern=new QuarterForms(Gold);
            lantern.Rod(V(0,.06f,0),V(0,.2f,0),.013f,.014f);
            lantern.ink=Teal; lantern.Lathe(Vector3.zero,8,(-.19f,.045f),(-.14f,.09f),(.007f,.087f),(.06f,.044f));
            lantern.ink=Gold;
            foreach(float h in new[]{-.17f,.033f}) lantern.Lathe(Vector3.zero,8,(h,.068f),(h+.016f,.078f));
            book.lantern=Art(root,"Nera’s sea-glass lantern",lantern).gameObject;
            var light=book.lantern.AddComponent<Light>();light.type=LightType.Point;light.color=Palette.Hex("A0FFE3");
            light.intensity=3.2f;light.range=6;light.shadows=LightShadows.None;

            var lookout = GameObject.Find("Cliff Quarter/Southern reach/Signal lookout").transform;
            var mast=Node(root,"Tidekeeper’s signal mast",lookout.position);
            var signal=mast.gameObject.AddComponent<IslandSignal>();
            var mastArt=new QuarterForms(Wood); mastArt.Rod(Vector3.zero,V(0,4.05f,0),.12f,.068f);
            mastArt.ink=Gold;mastArt.Rod(V(-.68f,3.88f,0),V(.68f,3.88f,0),.045f,.045f);
            mastArt.ink=Cream;mastArt.Rod(V(.48f,.36f,0),V(.48f,3.88f,0),.014f,.014f,5);
            Art(mast,"Cedar mast and halyard",mastArt);
            var bunting=new QuarterForms(Red);bunting.Silk(V(.48f,0,0),V(1.27f,-.10f,.16f),V(.48f,-.38f,0));
            bunting.ink=Blue;bunting.Silk(V(.48f,-.48f,0),V(1.03f,-.70f,-.05f),V(.48f,-.87f,0));
            signal.flags=Art(mast,"Tide pennants",bunting,"Shutters",Vector3.up*.78f);
        }
    }
}
