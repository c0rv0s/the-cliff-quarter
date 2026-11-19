using UnityEngine;

namespace TinyWorld
{
    public sealed class GrottoPool : QuarterLandmark
    {
        public Transform[] rings;
        public AudioClip drip;
        public Transform drop;
        public Vector3[] passage;
        float nextDrip;
        bool falling;
        public float Shelter(Vector3 foot)
        {
            if(passage==null||(foot-transform.position).sqrMagnitude>38*38)return 0;
            float shelter=0;
            for(int i=1;i<passage.Length;i++)
            {
                Vector3 a=passage[i-1],d=passage[i]-a;
                float u=Mathf.Clamp01(Vector3.Dot(foot-a,d)/d.sqrMagnitude);
                float dist=Vector3.Distance(foot,a+u*d);
                float depth=Mathf.Min(i-1+u,passage.Length-i-u);
                shelter=Mathf.Max(shelter,Mathf.Clamp01(depth*.72f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,8,dist))));
            }
            return shelter;
        }
        public override string Verb => "Touch the echo pool";
        float touched=-20;
        int echoes;
        protected override void Awake()
        {
            base.Awake();nextDrip=Time.time+3.8f;
            if(drop){drop.localPosition=new Vector3(.22f,4.1f,.16f);drop.gameObject.SetActive(false);}
        }
        protected override float Activate() { touched=Time.time;echoes=0;return 2.4f; }
        void Update()
        {
            float t=Time.time-touched;
            if(t<2.4f && echoes<3 && t>=echoes*.54f){Sound(drip,.65f*Mathf.Pow(.47f,echoes),1-.12f*echoes);echoes++;}
            for(int i=0;i<rings.Length;i++)
            {
                var r=rings[i];float u=(t-i*.26f)/1.5f;bool seen=u>=0&&u<1;
                if(r.gameObject.activeSelf!=seen)r.gameObject.SetActive(seen);
                if(seen)r.localScale=new Vector3(1.48f,.02f,1)*Mathf.Lerp(.08f,.97f,u);
            }
            if(drop)
            {
                if(Time.time>=nextDrip)
                {
                    nextDrip=Time.time+Random.Range(4.1f,7.5f);falling=true;
                }
                if(falling)
                {
                    drop.gameObject.SetActive(true);
                    drop.localPosition+=Vector3.down*(1.6f+Mathf.Max(0,4.1f-drop.localPosition.y)*2.7f)*Time.deltaTime;
                    if(drop.localPosition.y<.04f)
                    {
                        if(t>2.4f)Sound(drip,.16f,1.24f);
                        drop.localPosition=new Vector3(.22f,4.1f,.16f);
                        drop.gameObject.SetActive(false);falling=false;
                    }
                }
            }
            FollowVolume();
        }
    }
}
