using System;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static partial class BuildQuarterLandmarks
    {
        public static float WestQuayLimit(float z)
        {
            float t=Mathf.Clamp01((z-400)/80);
            return -248+111*Mathf.Sin(t*Mathf.PI*.5f)+5*Mathf.Sin(t*11)-3.3f*Mathf.Sin(t*22);
        }

        static void CoastalFolds(Transform coast,Func<float,float> height,Func<float,float> ground)
        {
            var folds=new QuarterForms(Cream);var grasses=new QuarterForms(Palette.Olive);
            // Broad spurs, a pair of gateposts at the lower cleft, and remnant sea stacks.
            var spurs=new(float x,float width,float rise,float reach)[]{
                (-247,17,59,18),(-222,18,92,21),(-207,12,42,23),(-187,19,68,19),
                (-170,13,38,14),(-155,13,64,13),(-145.9f,5.7f,22,6),
                (-140.2f,1.6f,7,2.1f),(-131.3f,1.4f,4.2f,1.1f),
                (-104,8,54,5),(-86,6,29,4),(105,9,36,4),(124,13,59,6),(152,12,53,8),
                (173,13,75,8),(198,18,63,12),(224,13,66,11),(249,15,44,10)};
            for(int n=0;n<spurs.Length;n++)
            {
                var s=spurs[n];float baseY=ground(s.x),peak=Mathf.Min(s.rise,height(s.x)-baseY-1);
                if(peak<3)continue;
                Vector3 Crest(int i,int j)
                {
                    float a=i*Mathf.PI*2/9+.36f*n, f=j/6f;
                    float radius=1-f*.61f+.14f*Mathf.Sin(j*2.0f+n*.79f);
                    float x=s.x+s.width*(Mathf.Cos(a)*radius+.16f*f);
                    float h=baseY-2.4f+peak*f+peak*.035f*Mathf.Sin(a*2+n)*f;
                    float t=Mathf.InverseLerp(ground(x),height(x),h);
                    float z=BluffZ(x,t)-1.3f-s.reach*.24f*f+Mathf.Sin(a)*s.reach*radius;
                    return V(x,h,z);
                }
                for(int j=0;j<6;j++)for(int i=0;i<9;i++)
                {
                    float stratum=Mathf.PerlinNoise(n*.48f+i*.12f,j*.62f);
                    folds.ink=Color.Lerp(Palette.Hex("968C82"),Palette.Hex("E1C9A1"),.24f+.62f*stratum);
                    var a=Crest(i,j);var b=Crest(i+1,j);var c=Crest(i+1,j+1);var d=Crest(i,j+1);
                    folds.Face(b,a,d);folds.Face(b,d,c);
                    if(j==5)
                    {
                        var top=(Crest(0,6)+Crest(4,6))*.5f+Vector3.up*.55f;
                        folds.ink=Palette.Hex("B4AD83");folds.Face(c,d,top);
                        if(i%3==0)Grass(grasses,(c+d+top)/3,1.5f,n+i);
                    }
                }
            }
            RockCollision(Art(coast,"Folded limestone buttresses",folds));
            var bank=new QuarterForms(Cream);var stacks=new QuarterForms(Cream);
            Vector3 Shore(int i,int j)
            {
                float z=400+Mathf.Min(i*2,80);float edge=WestQuayLimit(z);
                float w=8.5f+3.4f*Mathf.Sin(z*.19f);float q=Mathf.Sin(z*.28f+j*.7f);
                return j switch{
                    0=>V(edge+w+3,ground(edge)-.10f,z),
                    1=>V(edge+w*.60f,ground(edge)+.70f+.46f*q,z),
                    2=>V(edge-2.2f+q,ground(edge)+.20f+.63f*q,z),
                    3=>V(edge-4.8f+q,1.25f+.72f*q,z),
                    4=>V(edge-7.1f+q*.84f,-.56f+.24f*q,z),
                    _=>V(edge-12,-8,z)};
            }
            for(int i=0;i<40;i++)
            {
                for(int j=0;j<5;j++)
                {
                    var a=Shore(i,j);var b=Shore(i+1,j);var c=Shore(i+1,j+1);var d=Shore(i,j+1);
                    bank.ink=Color.Lerp(j<2?Palette.Hex("B6AF88"):Palette.Hex("BCAA94"),j<2?Palette.Hex("D1BA94"):Palette.Hex("7C807D"),.43f+.25f*Mathf.Sin(i*.7f+j));
                    bank.Face(a,d,c);bank.Face(a,c,b);
                }
                var pos=Shore(i,3);
                if(i%3==0)Boulder(stacks,pos+V(-2.4f,-1.5f,.51f),V(2.7f,3.1f,2.5f),i);
                if(i%2==0)Grass(grasses,Shore(i,1),1.45f+.4f*Mathf.Sin(i*1.1f),i);
            }
            // Close the exposed end below quay level.
            for(int j=0;j<5;j++)
            {
                var a=Shore(40,j);var b=Shore(40,j+1);
                bank.ink=Palette.Sandstone;bank.Face(b,b+Vector3.down*13,a+Vector3.down*13,a);
            }
            foreach(var s in new[]{(-223f,450f,8f,14f),(-213f,458f,4f,6f),(-203f,452f,5f,9f),(-171f,476f,3.3f,5.7f)})
            {
                Boulder(stacks,V(s.Item1,-2.5f,s.Item2),V(s.Item3,s.Item4,s.Item3*.72f),(int)s.Item2);
            }
            RockCollision(Art(coast,"Eroded shore",bank));
            RockCollision(Art(coast,"Sea stacks and tide rocks",stacks));
            Art(coast,"Headland thrift",grasses,"Foliage");
        }
    }
}
