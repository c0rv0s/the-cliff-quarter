using System;
using UnityEngine;
using UnityEditor;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        // Stations follow the joint that split the headland. Heights are measured from the lower lip.
        static void Grotto(Transform coast,Func<float,int,Vector3> surface)
        {
            Vector3 low=surface(CaveX,0),high=surface(VentX,2);
            var route=new[]{
                low,V(-136.7f,low.y+.33f,427.3f),V(-139.0f,low.y+1.6f,423.8f),
                V(-143.1f,low.y+3.05f,421.65f),V(-145.2f,low.y+4.42f,417.6f),
                V(-144.7f,low.y+3.57f,413.35f),V(-141.8f,low.y+5.18f,409.6f),
                V(-138,low.y+6.83f,407.65f),V(-133.15f,low.y+6.92f,406.0f),
                V(-128.65f,low.y+8.44f,406.8f),V(-124.7f,low.y+10.02f,409.65f),
                V(-120.35f,low.y+9.44f,411.4f),V(-117.65f,low.y+10.82f,414.15f),high
            };
            float[] left={3,1.65f,2.65f,1.73f,2.0f,2.9f,1.7f,3.4f,4.55f,2.85f,1.9f,2.55f,2,3};
            float[] right={3,2.35f,1.85f,2.6f,1.8f,1.58f,2.75f,2.35f,2.9f,3.6f,2.2f,1.66f,2.37f,3};
            float[] height={3.3f,2.85f,4.9f,4.1f,3.9f,5.7f,5.6f,8.4f,8.8f,6.2f,4.35f,3.8f,4.4f,3.55f};
            var across=new Vector3[route.Length];
            for(int i=0;i<route.Length;i++)
            {
                var a=route[Mathf.Max(i-1,0)];var b=route[Mathf.Min(i+1,route.Length-1)];
                across[i]=Vector3.Cross(b-a,Vector3.up).normalized;
            }
            across[0]=Vector3.right;across[^1]=Vector3.left;
            var path=Node(coast,"Grotto scramble",Vector3.zero);
            foreach(var point in route)Node(path,"Sill "+path.childCount,point);
            Node(coast,"Grotto entrance",low);Node(coast,"Grotto chamber",route[8]);
            Node(coast,"Grotto skylight",high);
            var shell=new QuarterForms(Cream);var flow=new QuarterForms(Teal);var stone=new QuarterForms(Cream);
            var sparks=new QuarterForms(Teal);
            float Grain(float x,float z)=>Mathf.PerlinNoise(x*.37f+31,z*.37f+16)-.5f;
            Color shade=Palette.Hex("7F8287"),chalk=Palette.Hex("D8C4AA"),ochre=Palette.Hex("B59D80");
            Vector3[] previousArch=null,previousBed=null;

            void Section(int i,float u,float bedY,bool sill=false)
            {
                float station=i+u;
                int portal=station==0?0:station==route.Length-1?2:-1;
                var center=Vector3.Lerp(route[i],route[i+1],u);
                var side=Vector3.Slerp(across[i],across[i+1],u).normalized;
                var forward=Vector3.Cross(Vector3.up,side);
                float broadL=Mathf.Lerp(left[i],left[i+1],u),broadR=Mathf.Lerp(right[i],right[i+1],u);
                float crown=Mathf.Lerp(height[i],height[i+1],u);
                var arch=new Vector3[13];var bed=new Vector3[13];
                float daylight=Mathf.Max(Mathf.Clamp01(1-station/3),Mathf.Clamp01(1-(route.Length-1-station)/3));
                for(int k=0;k<13;k++)
                {
                    float t=(k-6)/6f,span=t*(t<0?broadL:broadR);
                    if(portal>=0)
                    {
                        float x=center.x+side.x*t*CaveHalf;
                        arch[k]=surface(x,portal+1);bed[k]=surface(x,portal);
                        continue;
                    }
                    float arc=Mathf.Sqrt(Mathf.Max(0,1-t*t));
                    float fault=Mathf.Sin(station*1.13f+1.3f)*t*.49f+Mathf.Sin(t*7.1f+station*.64f)*.31f;
                    float h=Mathf.Lerp(route[i].y,route[i+1].y,u)+.20f+arc*(crown+fault);
                    // Two shoulders and a shifted crown give the ceiling a fractured cross section.
                    float shoulder=Mathf.Max(0,1-Mathf.Abs(t+.45f)*3);
                    h-=shoulder*(.38f+.55f*Mathf.Sin(station*.9f));
                    var rock=center+side*(span+arc*.49f*Mathf.Sin(station*1.44f+t*2))+forward*(.44f*Mathf.Sin(t*4+station*1.64f)*arc);
                    rock.y=h+(k==0||k==12?0:Grain(rock.x,rock.z)*1.5f);
                    arch[k]=rock;
                    var foot=center+side*span+forward*(.49f*t+.48f*Mathf.Sin(3.2f*t+station)+.16f*Mathf.Sin(t*9-station));
                    foot.y=bedY+.13f*t*Mathf.Sin(station*.73f)+.22f*Grain(foot.x,foot.z)
                        +.29f*t*t+.32f*t*t*Mathf.Sin(t*6+station);
                    bed[k]=foot;
                    // Bottom wall vertices track the terrace, including the vertical riser.
                    if(k==0||k==12)arch[k]=foot+Vector3.up*.23f;
                }
                void FaceToward(Vector3 a,Vector3 b,Vector3 c,Vector3 facing)
                {
                    if(Vector3.Cross(b-a,c-a).sqrMagnitude<.0000001f)return;
                    if(Vector3.Dot(Vector3.Cross(b-a,c-a),facing)>0)shell.Face(a,b,c);else shell.Face(a,c,b);
                }
                if(previousArch!=null)
                {
                    for(int k=0;k<12;k++)
                    {
                        var a=previousArch[k];var b=previousArch[k+1];var c=arch[k+1];var d=arch[k];
                        var inside=center+Vector3.up*.85f-(a+b+c+d)*.25f;
                        shell.ink=Color.Lerp(shade,chalk,.19f+.48f*daylight+.22f*Mathf.PerlinNoise(k*.7f,station*.77f));
                        FaceToward(a,b,c,inside);FaceToward(a,c,d,inside);
                        a=previousBed[k];b=previousBed[k+1];c=bed[k+1];d=bed[k];
                        var face=sill?(previousBed[6].y<bed[6].y?-forward:forward):Vector3.up;
                        shell.ink=Color.Lerp(sill?ochre:Palette.Hex("D0C2AA"),shade,.18f+.28f*Mathf.PerlinNoise(k*.35f,station*.5f));
                        FaceToward(a,b,c,face);FaceToward(a,c,d,face);
                    }
                    foreach(int k in new[]{0,12})
                    {
                        var inwards=k==0?side:-side;shell.ink=ochre;
                        FaceToward(previousArch[k],previousBed[k],bed[k],inwards);
                        FaceToward(previousArch[k],bed[k],arch[k],inwards);
                    }
                }
                previousArch=arch;previousBed=bed;
            }
            Section(0,0,low.y);
            for(int i=0;i<route.Length-1;i++)
            {
                float dy=route[i+1].y-route[i].y;
                if(Mathf.Abs(dy)>.60f)
                {
                    float split=.44f+.085f*Mathf.Sin(i*2.74f);
                    Section(i,split-.11f,route[i].y+dy*.035f);
                    Section(i,split,route[i].y+dy*.18f);
                    Section(i,split+.027f,route[i+1].y-dy*.055f,true);
                }
                else Section(i,.5f,(route[i]+route[i+1]).y*.5f);
                Section(i,1,route[i+1].y);
            }
            var chamber=Art(coast,"Salt grotto vault and floor",shell);
            const string stonePath=Folder+"/Wet limestone.mat";
            var damp=AssetDatabase.LoadAssetAtPath<Material>(stonePath);
            if(!damp){damp=new Material(Mat("Shutters"));AssetDatabase.CreateAsset(damp,stonePath);}
            damp.SetFloat("_Pattern",5);damp.SetFloat("_Gloss",.18f);damp.SetFloat("_RimStrength",.14f);
            EditorUtility.SetDirty(damp);
            chamber.GetComponent<Renderer>().sharedMaterial=damp;
            RockCollision(chamber);
            for(int i=1;i<route.Length-1;i++)
            {
                int sign=i%2==0?1:-1;
                Vector3 side=across[i]*sign,along=Vector3.Cross(Vector3.up,across[i]);
                float width=sign>0?right[i]:left[i];
                var foot=route[i]+side*(width-.27f);
                // Tilted breakdown at the outside of bends, with a clean line through the centre.
                Boulder(stone,foot-Vector3.up*.21f,V(.68f+.29f*(i%3),.75f+.46f*(i%4),.85f+.14f*(i%3)),i+52);
                if(i%3!=0)Boulder(stone,foot+along*.99f,V(.57f,.52f,.68f),i+27);
                for(int j=0;j<9;j++)
                {
                    float run=(j-4)*.34f, sz=.105f+.24f*Mathf.PerlinNoise(i*1.4f,j*.77f);
                    var baseRock=foot+along*run-side*(.32f+.37f*Mathf.Sin(j*1.16f+i))+Vector3.up*.025f;
                    Boulder(stone,baseRock,V(sz*1.4f,sz*.9f,sz*(.8f+.35f*Mathf.Sin(j))),i*13+j);
                }
                if(i!=4&&i!=7&&i!=8&&i!=10)continue;
                // Calcite grows in fans along a wet joint. Longer teeth hang in the high chamber.
                for(int k=0;k<13;k++)
                {
                    float t=k/12f, wav=Mathf.Sin(k*2.39f+i);
                    var root=route[i]+side*(width*(.57f+.09f*Mathf.Sin(t*3)))+along*(t-.5f)*3.25f;
                    root.y+=height[i]*.91f;
                    float length=(.65f+.85f*Mathf.Abs(wav))*(i==8?1.83f:1);
                    flow.ink=Color.Lerp(Palette.Hex("A6AAA0"),Palette.Hex("DDD1B1"),t);
                    var knee=root-side*.16f-Vector3.up*length*.62f;
                    flow.Rod(root+Vector3.up*.6f,knee,.22f+.08f*Mathf.Abs(wav),.088f,6);
                    flow.Rod(knee,knee-side*.08f-Vector3.up*length*.38f,.088f,.007f,6);
                }
            }
            // The shallow spring occupies the broad outside of the deepest bend.
            var spring=route[8]-across[8]*1.9f+Vector3.up*.17f;
            var pool=Node(coast,"Echo pool",spring);
            Pool(pool);
            var echo=pool.GetComponent<GrottoPool>();
            echo.passage=route;
            var drop=new QuarterForms(Palette.Hex("9DD2CC"));
            drop.Lathe(Vector3.zero,6,(-.06f,.005f),(-.027f,.025f),(0,.026f),(.085f,.003f));
            echo.drop=Art(pool,"Seep",drop);
            var reverb=pool.gameObject.AddComponent<AudioReverbZone>();
            reverb.reverbPreset=AudioReverbPreset.StoneCorridor;reverb.minDistance=5;reverb.maxDistance=13;
            for(int i=0;i<22;i++)
            {
                float a=i*2.39996f;float r=1.52f+.18f*Mathf.Sin(i*3.14f+.31f*i);
                var point=spring+V(Mathf.Cos(a)*r,.025f,Mathf.Sin(a)*r*.69f);
                if(i%3==0)Boulder(stone,point,V(.29f,.35f,.26f),i+71);
                else{sparks.ink=i%2==0?Palette.Hex("66ABA2"):Palette.Hex("8C9FAA");sparks.Lathe(point,5,(0,.076f),(.13f,.064f),(.24f+.07f*Mathf.Sin(a),0));}
            }
            var debris=Art(coast,"Grotto breakdown and calcite",stone);
            debris.GetComponent<Renderer>().sharedMaterial=damp;RockCollision(debris);
            Art(coast,"Flowstone curtains",flow);
            Art(coast,"Luminous salt seam",sparks);
            var terrace=new QuarterForms(chalk);
            // Apron follows the upper opening out onto the coastal traverse.
            for(int j=0;j<12;j++)
            {
                var a=surface(VentX-CaveHalf+j*.5f,2);var b=surface(VentX-CaveHalf+(j+1)*.5f,2);
                Vector3 Nose(Vector3 q)=>q+V(0,-.08f,1.8f+.29f*Mathf.Sin(q.x*1.7f));
                var c=Nose(b);var d=Nose(a);
                terrace.Face(a,d,c,b);terrace.ink=ochre;
                terrace.Face(d,d+V(0,-1.65f,-.7f),c+V(0,-1.65f,-.7f),c);terrace.ink=chalk;
            }
            RockCollision(Art(coast,"High sea window",terrace));
        }
    }
}
