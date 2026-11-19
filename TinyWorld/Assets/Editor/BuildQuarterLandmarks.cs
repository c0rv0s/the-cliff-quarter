using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        const string Folder = "Assets/Geometry/Discoveries";
        const string MeshFile = Folder + "/Quayside crafts.asset";
        public const string Prefab = Folder + "/Quarter landmarks.prefab";
        static readonly Color Cream = Palette.Limestone, Gold = Palette.Brass, Teal = Palette.Water, Blue = Palette.Shutter,
            Red = Palette.Terracotta, Pink = Palette.Bougainvillea, Dark = Palette.Shadow, Wood = Palette.Hex("754432");
        static Vector3 V(float x, float y, float z) => new(x,y,z);
        static Transform Node(Transform parent, string name, Vector3 at)
        {
            var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition = at; return t;
        }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Quarter/" + name + ".mat");
        static AudioClip Clip(string path) => Resources.Load<AudioClip>("Audio/" + path);
        static Transform Art(Transform parent, string name, QuarterForms form, string material = "Shutters", Vector3 at = default)
        {
            var t = Node(parent,name,at);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = form.Bake(name,MeshFile);
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat(material);
            return t;
        }
        static void Solid(Transform t, Vector3 center, Vector3 size)
        {
            var box = t.gameObject.AddComponent<BoxCollider>(); box.center = center; box.size = size;
        }

        [MenuItem("Tiny World/Rebuild quarter discoveries")]
        public static void Bake()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().name != "CliffQuarter") throw new InvalidOperationException("Open CliffQuarter in Edit mode.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Geometry","Discoveries");
            if (!AssetDatabase.LoadMainAssetAtPath(MeshFile)) AssetDatabase.CreateAsset(new Mesh { name = "Quayside crafts" }, MeshFile);
            var previous = GameObject.Find("Quarter landmarks");
            if(previous)previous.SetActive(false);
            var root = Node(null,"Quarter landmarks",Vector3.zero);
            try
            {
                Vector3 stone(string place) => Object.FindObjectsByType<Sunstone>().Single(s => s.place == place).transform.position;
                var court = GameObject.Find("Route · Saffron wall and swing alley").transform.position;
                var chimes = Node(root,"The chime maker", V(court.x+1.1f,court.y,26.9f));
                Chimes(chimes); Arcade(root,court); Windfish(root);
                var bell = Node(root,"Swallow belfry",stone("Swing alley landing") + V(0,-1.05f,.46f)); Belfry(bell);
                var shrine = stone("Shrine of the Current");
                var slip = Node(root,"The lotus slip",V(shrine.x-.05f,.64f,103.6f)); Slip(slip);
                var crown = stone("Crown observatory"); var kite = Node(root,"The sun kite",crown + V(0,-1.05f,-3.8f)); Kite(kite);
                HarbourHeadlands(root); SeaLions(root);
                Physics.SyncTransforms();
                Residents(root, court); Stories(root,court); WallIvy(root);
                var used=root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToHashSet();
                foreach(var item in AssetDatabase.LoadAllAssetRepresentationsAtPath(MeshFile))
                    if(item is Mesh mesh&&!used.Contains(mesh))Object.DestroyImmediate(mesh,true);
                AssetDatabase.SaveAssets();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject,Prefab);
            }
            finally { Object.DestroyImmediate(root.gameObject); if(previous) previous.SetActive(true); }
            AssetDatabase.SaveAssets();
        }

        public static void Place()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "CliffQuarter") throw new InvalidOperationException("Open CliffQuarter.");
            if (scene.GetRootGameObjects().Any(r => r.name == "Quarter landmarks")) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (!prefab) throw new InvalidOperationException("Bake quarter discoveries first.");
            PrefabUtility.InstantiatePrefab(prefab,scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void PaintedUrn(QuarterForms g, Vector3 p, float s, Color glaze)
        {
            g.ink = glaze;
            g.Lathe(p,16,(0,.19f*s),(.09f*s,.26f*s),(.37f*s,.39f*s),(.58f*s,.36f*s),(.71f*s,.22f*s),(.76f*s,.24f*s),(.82f*s,.25f*s),(.82f*s,.19f*s),(.72f*s,.17f*s));
            g.ink = Cream;
            g.Lathe(p,16,(.37f*s,.393f*s),(.41f*s,.39f*s));
            g.Lathe(p,16,(.53f*s,.373f*s),(.56f*s,.366f*s));
            for (int j = 0; j < 16; j++)
            {
                float a = j*Mathf.PI/8;
                Vector3 Side(float angle,float y) => p + V(Mathf.Cos(angle)*.386f,y,Mathf.Sin(angle)*.386f)*s;
                g.Silk(Side(a-.14f,.515f),Side(a+.14f,.515f),Side(a,.424f));
            }
        }
        static void Finial(QuarterForms g, Vector3 p, float scale)
        {
            g.ink = Gold; g.Lathe(p,12,(0,.12f*scale),(.08f*scale,.13f*scale),(.17f*scale,.07f*scale),(.28f*scale,.14f*scale),(.55f*scale,.008f));
        }
        static void Chimes(Transform t)
        {
            var g = new QuarterForms(Wood);
            for (int side = -1; side <= 1; side += 2)
            {
                var p = V(side*1.06f,0,0);
                g.ink = Cream; g.Lathe(p,8,(0,.23f),(.16f,.23f),(.25f,.14f));
                g.ink = Teal; g.Rod(p+Vector3.up*.2f,p+Vector3.up*2.63f,.11f,.078f);
                g.ink = Gold;
                for (int j=0;j<4;j++) g.Lathe(p,10,(.38f+j*.66f,.114f-j*.008f),(.43f+j*.66f,.114f-j*.008f));
                Finial(g,p+Vector3.up*2.62f,1);
                Solid(t,p+Vector3.up*1.25f,V(.18f,2.5f,.18f));
            }
            g.ink = Wood; g.Rod(V(-1.23f,2.42f,0),V(1.23f,2.42f,0),.105f,.105f);
            g.ink = Gold; g.Rod(V(-1,2.57f,0),V(1,2.57f,0),.027f,.027f);
            PaintedUrn(g,V(1.46f,0,.18f),1.05f,Blue);
            PaintedUrn(g,V(1.49f,0,-.46f),.63f,Red);
            for (int i=0;i<11;i++)
            {
                float a = i*2.4f;
                var end = V(1.46f+.31f*Mathf.Cos(a),1.23f+.22f*Mathf.Sin(i*4),.18f+.26f*Mathf.Sin(a));
                g.ink=Palette.Olive; g.Rod(V(1.46f,.65f,.18f),end,.015f,.005f,4);
                for (int j=0;j<4;j++) { g.ink=j==0?Gold:Pink; g.Lathe(end+V(.05f*Mathf.Cos(j*2),.026f*j,.05f*Mathf.Sin(j*2)),6,(-.055f,0),(0,.067f),(.042f,.02f)); }
            }
            Art(t,"Painted chime stand",g);
            var chime = t.gameObject.AddComponent<QuayChimes>(); chime.tubes = new Transform[5]; chime.note = Clip("Sunstone/glock_c_01"); chime.handle=V(0,1.45f,-.2f);
            for (int i=0;i<5;i++)
            {
                float length = 1.0f-i*.12f;
                g=new QuarterForms(Dark); g.Rod(Vector3.zero,V(0,-.22f,0),.012f,.012f,6);
                g.ink=Gold; g.Lathe(Vector3.zero,12,(-.23f-length,.082f),(-.18f-length,.09f),(-.22f,.08f),(-.20f,.03f));
                g.ink=Teal; g.Lathe(Vector3.zero,12,(-.16f-length,.091f),(-.10f-length,.089f));
                g.ink=Red; g.Rod(V(0,-.21f-length,0),V(0,-.47f-length,0),.008f,.008f,4);
                g.ink=i%2==0?Gold:Cream; g.Silk(V(-.07f,-.49f-length,0),V(.07f,-.49f-length,0),V(0,-.65f-length,.035f));
                chime.tubes[i]=Art(t,"Tuned pipe "+(i+1),g,"Shutters",V((i-2)*.36f,2.38f,0));
            }
        }
        static void Arcade(Transform root, Vector3 court)
        {
            var t=Node(root,"Dyers' shade", V(court.x+1.1f,court.y,26.9f));
            var posts=new QuarterForms(Wood); var fabric=new QuarterForms(Teal);
            // A low curve between four high corner ties leaves the central vault line clear.
            for(int j=0;j<2;j++) for(int side=0;side<2;side++)
            {
                var p=V(-4.6f+side*5.5f,0,-3+j*6.2f);
                float h=4.7f+j*.55f+side*.2f;
                posts.ink=Wood;posts.Rod(p,p+Vector3.up*h,.088f,.064f);
                posts.ink=Gold;posts.Lathe(p,8,(.12f,.13f),(.31f,.13f),(.34f,.08f));Finial(posts,p+Vector3.up*h,.65f);
                Solid(t,p+Vector3.up*h*.5f,V(.15f,h,.15f));
            }
            Vector3 Sail(float u,float v) => V(-4.6f+5.5f*u,4.70f+v*.55f+u*.2f-.58f*Mathf.Sin(u*Mathf.PI)-.32f*Mathf.Sin(v*Mathf.PI),-3+v*6.2f);
            var indigo=Palette.Hex("384D83");
            // Each diamond is cut into the cloth and reads from above and below.
            void Diamond(System.Func<float,float,Vector3> patch)
            {
                var center=patch(.5f,.5f);
                for(int k=0;k<4;k++) {
                    Vector2 Turn(float u,float v) => k switch {
                        0=>new Vector2(u,v),1=>new Vector2(1-v,u),2=>new Vector2(1-u,1-v),_=>new Vector2(v,1-u)
                    };
                    Vector3 At(float u,float v){var uv=Turn(u,v);return patch(uv.x,uv.y);}
                    var a=At(.5f,0);var b=At(0,.5f);var innerA=At(.5f,.12f);var innerB=At(.12f,.5f);
                    fabric.ink=Cream;
                    fabric.Silk(At(0,0),a,b);
                    fabric.Silk(a,innerA,b);fabric.Silk(innerA,innerB,b);
                    fabric.ink=indigo;fabric.Silk(center,innerB,innerA);
                }
            }
            fabric.wind=.6f;
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
                Diamond((u,v)=>Sail((col+u)/3,(row+v)/3));
            for(int side=0;side<4;side++)for(int tile=0;tile<5;tile++) {
                Diamond((u,v)=>{
                    float f=(tile+u)/5;
                    var hem=side switch {0=>Sail(f,0),1=>Sail(1,f),2=>Sail(1-f,1),_=>Sail(0,1-f)};
                    return hem+Vector3.down*(v*(.50f+.12f*Mathf.Sin(u*Mathf.PI)));
                });
            }
            Art(t,"Dyer's indigo diamond canopy",fabric,"Cloth");
            Art(t,"Cedar sail masts",posts);
        }
        static void Windfish(Transform root)
        {
            const float z=69;
            float center = Mathf.Sin(z*.017f)*20+Mathf.Sin(z*.039f+.4f)*7;
            float width = 17+Mathf.Sin(z*.023f+1)*5+Mathf.Sin(z*.051f)*2;
            var poles = new Vector3[2];
            var t=Node(root,"The dyers' flying shoal",V(center,0,z));
            var mast=new QuarterForms(Wood);
            for(int i=0;i<2;i++)
            {
                int side=i*2-1;
                float edge = side*(width + Mathf.Sin(z*.025f+side)*6 + Mathf.Sin(z*.067f+side)*2 + 1.3f);
                var p=V(edge,0,0);
                if (!Physics.Raycast(t.position+p+Vector3.up*8,Vector3.down,out var ground,10,1,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Flying shoal needs two quay footings.");
                p.y=ground.point.y;
                var top=p+V(-side*.75f,13-i*1.9f,0);
                mast.ink=Cream;mast.Lathe(p,12,(0,.37f),(.2f,.37f),(.32f,.27f),(.63f,.24f));
                mast.ink=Wood;mast.Rod(p,top,.18f,.09f,10);
                Solid(t,p+Vector3.up*1.25f,V(.32f,2.5f,.32f));
                for(int j=0;j<5;j++){var b=Vector3.Lerp(p,top,.15f+j*.17f);mast.ink=Gold;mast.Rod(b,b+Vector3.up*.13f,.19f-j*.014f,.19f-j*.014f);}
                Finial(mast,top,1.35f);poles[i]=top;
            }
            float sag=2.4f;
            mast.ink=Wood;mast.Strand(poles[0],poles[1],sag,.045f);
            Vector3 OnLine(float u) => Vector3.Lerp(poles[0],poles[1],u)+Vector3.down*(4*sag*u*(1-u));
            var flag=new QuarterForms(Red){wind=1};
            for(int i=1;i<35;i++)
            {
                float u=i/36f;var a=OnLine(u);var b=OnLine(u+.014f);
                flag.ink=i%4==0?Cream:i%3==0?Gold:i%2==0?Teal:Red;
                flag.Silk(a,b,(a+b)*.5f+V(.13f,-.6f,.16f));
            }
            Art(t,"Signal pennants",flag,"Cloth");
            var shoal=t.gameObject.AddComponent<RiverWindsocks>();shoal.fish=new Transform[3];
            for(int i=0;i<3;i++)
            {
                float u=.25f+i*.23f, scale=i==1?1.32f:i==0?.87f:.68f;
                var tie=OnLine(u);var at=tie+Vector3.down*(i==1?1.63f:1.21f);
                mast.ink=Gold;mast.Rod(tie,at,.025f,.025f,6);
                var fish=Fish(i==1?Teal:i==0?Red:Blue);
                var f=Art(t,"Hand-painted flying fish "+(i+1),fish,"Cloth",at);
                f.localScale=Vector3.one*scale;
                shoal.fish[i]=f;
            }
            Art(t,"Harbour festival rigging",mast);
        }
        static QuarterForms Fish(Color dye)
        {
            var g=new QuarterForms(dye){wind=.6f};
            float[] x={0,.32f,.93f,1.65f,2.37f,3.18f,3.75f}, radii={.59f,.70f,.80f,.83f,.68f,.46f,.22f};
            Vector3 Skin(int row,float a,float extra=0) => V(x[row],-.38f+Mathf.Sin(a)*(radii[row]+extra),Mathf.Cos(a)*(radii[row]+extra)*.73f);
            for(int j=0;j<6;j++) for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                var l=Skin(j,a);var m=Skin(j,b);var n=Skin(j+1,b);var o=Skin(j+1,a);
                g.ink=j==0?Gold:Mathf.Sin((a+b)*.5f)<-.52f?Cream:dye;
                g.Face(l,m,n,o);g.Face(o,n,m,l);
                if(j>=2 && j<=4)
                {
                    var p=Skin(j,a,.012f);var q=Skin(j,b,.012f);
                    var tip=Vector3.Lerp((Skin(j+1,a,.018f)+Skin(j+1,b,.018f))*.5f,(p+q)*.5f,.27f);
                    g.ink=(i+j)%3==0?Gold:Color.Lerp(dye,Cream,.55f);g.Silk(p,q,tip);
                }
            }
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 eye=V(.57f,-.24f,side*.565f);
                g.ink=Cream;g.Rod(eye,eye+V(0,0,side*.023f),.208f,.208f,20);
                g.ink=Dark;g.Rod(eye+V(-.026f,0,side*.025f),eye+V(-.026f,0,side*.048f),.118f,.118f,16);
                g.ink=Gold;g.Silk(V(1.34f,-.40f,side*.45f),V(1.52f,-.52f,side*.45f),V(2.65f,-.63f,side*1.46f));
                g.ink=Red;g.Silk(V(1.34f,-.40f,side*.45f),V(2.65f,-.63f,side*1.46f),V(1.74f,-.36f,side*.87f));
            }
            g.ink=Red;g.Silk(V(.97f,.35f,0),V(1.87f,1.06f,.03f),V(2.62f,.21f,0));
            g.ink=Gold;g.Silk(V(3.66f,-.32f,0),V(5.21f,.52f,.16f),V(4.62f,-.41f,.08f));
            g.ink=dye;g.Silk(V(3.66f,-.32f,0),V(4.62f,-.41f,.08f),V(5.44f,-1.22f,-.16f));
            g.ink=Red;g.Silk(V(3.66f,-.32f,0),V(5.44f,-1.22f,-.16f),V(3.65f,-.57f,0));
            g.ink=Dark;
            for(int i=0;i<3;i++){float a=(i+.25f)*Mathf.PI*2/3;g.Rod(Vector3.zero,Skin(0,a),.015f,.015f,4);}
            return g;
        }
        static void Belfry(Transform t)
        {
            var g = new QuarterForms(Cream);
            for (int side=-1;side<=1;side+=2)
            {
                var p=V(side*1.28f,0,0);
                g.ink=Cream;g.Lathe(p,8,(-.05f,.29f),(.14f,.29f),(.26f,.23f),(2.5f,.19f),(2.58f,.28f),(2.73f,.27f));
                g.ink=Teal;g.Lathe(p,8,(.34f,.235f),(.53f,.233f));g.Lathe(p,8,(2.26f,.21f),(2.38f,.21f));
                Finial(g,p+Vector3.up*2.73f,1);
                Solid(t,p+Vector3.up*1.35f,V(.38f,2.7f,.4f));
            }
            Vector3 Arc(float a,float radius,float z) => V(Mathf.Cos(a)*radius,2.58f+Mathf.Sin(a)*radius,z);
            for(int j=0;j<19;j++)
            {
                float a=Mathf.PI*j/19,b=Mathf.PI*(j+1)/19;
                float r=j==9?1.61f:1.5f;
                var l=Arc(a,1.08f,-.2f);var m=Arc(b,1.08f,-.2f);var n=Arc(b,r,-.2f);var o=Arc(a,r,-.2f);
                var back=Vector3.forward*.4f;
                g.ink=j%2==0?Cream:Palette.Sandstone;
                g.Face(l,m,n,o); g.Face(o+back,n+back,m+back,l+back);
                g.Face(l,l+back,m+back,m);g.Face(o,n,n+back,o+back);
                g.ink=Teal;g.Rod(n,o,.046f,.046f);
            }
            g.ink=Wood;g.Rod(V(-1.28f,2.6f,0),V(1.28f,2.6f,0),.115f,.115f);
            g.ink=Gold;g.Strand(V(.62f,2.55f,0),V(.94f,.78f,-.29f),.08f,.025f);
            Art(t,"Swallow arch inlaid with turquoise",g);
            g=new QuarterForms(Gold);
            g.Lathe(Vector3.zero,32,(-1.02f,.39f),(-1.1f,.54f),(-1.02f,.57f),(-.93f,.49f),(-.71f,.36f),(-.30f,.28f),(-.19f,.23f),(-.12f,.10f),(-.02f,.08f));
            g.ink=Dark;g.Lathe(Vector3.zero,24,(-1.08f,.47f),(-1.02f,.42f),(-.86f,.31f));
            g.ink=Teal;g.Lathe(Vector3.zero,32,(-.94f,.50f),(-.91f,.49f));g.Lathe(Vector3.zero,24,(-.36f,.295f),(-.32f,.29f));
            g.ink=Wood;g.Rod(V(0,-.12f,0),V(0,-1.12f,0),.025f,.044f);
            g.ink=Gold;g.Lathe(Vector3.zero,12,(-1.23f,.01f),(-1.18f,.11f),(-1.1f,.1f),(-1.06f,.03f));
            var bell=t.gameObject.AddComponent<SwallowBell>(); bell.bell=Art(t,"Bronze bell and clapper",g,"Shutters",V(0,2.57f,0));
            bell.peal=Clip("Bells/distant_church_01");bell.handle=V(.91f,1.05f,-.32f);
            bell.swallows=new Transform[6];
            Transform master=null;
            for(int i=0;i<6;i++)
            {
                var p=V((i-2.5f)*.42f,2.78f+Mathf.Sin((i+.5f)*Mathf.PI/6)*1.23f,0);
                if(i>0) { var copy=Object.Instantiate(master,t);copy.name="Swallow "+(i+1);copy.localPosition=p;bell.swallows[i]=copy;continue; }
                var bird=Node(t,"Swallow 1",p);master=bird;bell.swallows[i]=bird;
                g=new QuarterForms(Dark);g.Rod(V(0,0,-.18f),V(0,.04f,.19f),.055f,.073f);
                g.ink=Red;g.Rod(V(0,.04f,.16f),V(0,.04f,.27f),.055f,0);
                for(int side=-1;side<=1;side+=2)
                {
                    g.ink=Dark;g.Silk(V(0,0,-.11f),V(side*.08f,.01f,-.15f),V(side*.12f,0,-.36f));
                    var wing=new QuarterForms(Dark);
                    wing.Silk(V(0,0,.085f),V(side*.25f,.055f,.12f),V(side*.20f,0,-.09f));
                    wing.Silk(V(0,0,.085f),V(side*.20f,0,-.09f),V(0,-.01f,-.11f));
                    wing.ink=Blue;wing.Silk(V(side*.25f,.055f,.12f),V(side*.48f,.02f,-.14f),V(side*.20f,0,-.09f));
                    var w=Art(bird,side==-1?"Left wing":"Right wing",wing);w.localRotation=Quaternion.Euler(0,0,-side*67);
                }
                Art(bird,"Fork-tailed swallow",g);
            }
        }
        static void Slip(Transform t)
        {
            var g=new QuarterForms(Wood);
            for(int i=0;i<7;i++)
            {
                float z=(i-3)*.24f;
                g.ink=Color.Lerp(Wood,Palette.Sandstone,(i%3)*.15f);
                g.Rod(V(-.66f,0,z),V(.66f,0,z),.155f,.155f,4);
            }
            Solid(t,V(0,-.02f,0),V(1.31f,.24f,1.66f));
            for(int side=-1;side<=1;side+=2)
            {
                var p=V(side*.54f,-1.8f,.56f);
                g.ink=Wood;g.Rod(p,p+Vector3.up*2.7f,.09f,.08f);
                g.ink=Teal;g.Lathe(p,8,(2.4f,.09f),(2.62f,.09f));Finial(g,p+Vector3.up*2.68f,.65f);
            }
            g.ink=Dark;g.Strand(V(-.54f,.75f,.56f),V(.54f,.75f,.56f),.18f,.018f);
            PaintedUrn(g,V(-.45f,.14f,.23f),.66f,Teal);
            Art(t,"Lotus launch and offering jar",g);
            var lantern=t.gameObject.AddComponent<LanternSlip>();
            g=new QuarterForms(Gold);g.Lathe(Vector3.zero,12,(-.02f,.18f),(.055f,.27f),(.08f,.28f),(.09f,.21f));
            for(int j=0;j<10;j++)
            {
                float a=j*Mathf.PI*.2f;var d=V(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=V(-d.z,0,d.x);
                var baseP=d*.11f+Vector3.up*.04f;var l=d*.25f+tangent*.085f+Vector3.up*.17f;var r=l-tangent*.17f;var tip=d*.36f+Vector3.up*(.29f+.04f*(j%2));
                g.ink=j%2==0?Pink:Palette.Hex("DF887D");g.Silk(baseP,l,tip);g.ink=Red;g.Silk(baseP,tip,r);
            }
            lantern.lantern=Art(t,"Folded lotus petals",g,"Shutters",V(.21f,.18f,-.53f));
            g=new QuarterForms(Cream);g.Lathe(Vector3.zero,10,(.06f,.08f),(.15f,.065f),(.21f,.036f),(.33f,0));
            Art(lantern.lantern,"Candle flame",g,"Lantern glow");
            lantern.handle=V(.12f,.8f,-.23f);lantern.splash=Clip("SplashSmall/surface_01");
        }
        static void Kite(Transform t)
        {
            var rig=t.gameObject.AddComponent<ObservatoryKite>();
            var g=new QuarterForms(Wood);
            for(int s=-1;s<=1;s+=2)
            {
                var p=V(s*.48f,0,0);g.ink=Wood;g.Rod(p,p+Vector3.up*1.22f,.082f,.065f);
                Finial(g,p+Vector3.up*1.20f,.7f);
            }
            g.ink=Teal;g.Lathe(V(1.26f,0,.37f),12,(0,.25f),(.18f,.31f),(.30f,.18f),(1.92f,.09f));
            Art(t,"Kite mooring",g);
            g=new QuarterForms(Wood);g.Rod(V(-.59f,0,0),V(.59f,0,0),.09f,.09f);
            g.ink=Gold;g.Rod(V(-.32f,0,0),V(-.27f,0,0),.26f,.26f);g.Rod(V(.27f,0,0),V(.32f,0,0),.26f,.26f);
            for(int j=0;j<10;j++) {g.ink=j%2==0?Cream:Palette.Sandstone;g.Rod(V(-.26f+j*.052f,0,0),V(-.21f+j*.052f,0,0),.177f,.18f);}
            g.ink=Red;g.Rod(V(.45f,0,0),V(.45f,.35f,0),.036f,.036f);g.Rod(V(.45f,.35f,0),V(.63f,.35f,0),.048f,.048f);
            rig.spool=Art(t,"Hand-wound reel",g,"Shutters",V(0,.95f,0));rig.lineEye=Node(t,"Fairlead",V(.1f,1.12f,0));
            g=new QuarterForms(Teal);g.wind=.7f;
            Vector3 nose=V(0,1.4f,0),keel=V(0,-1.17f,0),crest=V(0,.22f,-.42f);
            for(int s=-1;s<=1;s+=2)
            {
                var tip=V(s*1.7f,.10f,.04f);var inner=V(s*.8f,.12f,-.22f);
                g.ink=Teal;g.Silk(nose,inner,crest);g.ink=Blue;g.Silk(crest,inner,keel);
                g.ink=Gold;g.Silk(nose,tip,inner);g.ink=Red;g.Silk(tip,keel,inner);
                for(int i=0;i<5;i++)
                {
                    var a=Vector3.Lerp(tip,keel,i/5f);var b=Vector3.Lerp(tip,keel,(i+1)/5f);
                    g.ink=i%2==0?Cream:Teal;g.Silk(a,b,(a+b)*.5f+V(s*.12f,-.38f,.05f));
                }
                g.ink=Cream;g.Rod(nose,tip,.018f,.015f,5);g.Rod(tip,keel,.015f,.018f,5);
            }
            g.ink=Gold;g.Rod(nose,keel,.023f,.02f);
            var last=keel;
            for(int i=1;i<=12;i++)
            {
                var p=keel+V(Mathf.Sin(i*.36f)*.36f,-i*.095f,i*.10f);
                g.ink=Dark;g.Rod(last,p,.013f,.012f,4);if(i%3==0){g.ink=Pink;g.Silk(p,p+V(-.18f,-.08f,0),p+V(-.2f,.08f,0));g.Silk(p,p+V(.18f,.06f,0),p+V(.20f,-.10f,0));}last=p;
            }
            rig.kite=Art(t,"Sun kite and knotted tail",g,"Cloth",V(1.26f,2.37f,.37f));
            rig.thread=rig.lineEye.gameObject.AddComponent<LineRenderer>();rig.thread.sharedMaterial=Mat("Cedar");
            rig.thread.useWorldSpace=true;rig.thread.positionCount=9;rig.thread.startWidth=.021f;rig.thread.endWidth=.013f;
            rig.thread.startColor=rig.thread.endColor=Cream;rig.thread.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            rig.canvas=Clip("GlideOpen/canvas_snap_01");rig.handle=V(0,.96f,-.2f);
        }
    }
}
