using UnityEngine;

namespace TinyWorld
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class QuarterResident : QuarterLandmark
    {
        public string firstName;
        [TextArea] public string[] lines;
        public Vector3 end;
        public Transform body, head, armL, armR, legL, legR;
        public float pace = 1.25f;
        public string Speech { get; private set; }
        public override string Verb => firstName=="Moss" && QuarterQuests.Current && QuarterQuests.Current.Stage(1)>0 && !QuarterQuests.Current.race.Running ? "Race Moss again" : "Talk to " + firstName;
        Vector3 home;
        CharacterController walker;
        WandererMotor visitor;
        float wait, stride, speakingUntil, stalled, sideStep, yieldCooldown;
        Vector3 yieldDirection;
        bool racing, meeting;
        int line;
        void PlaceAt(Vector3 at)
        {
            walker.enabled=false;transform.position=at;walker.enabled=true;
        }
        public void Meet(bool active,Vector3 at)
        {
            if(meeting==active)return;
            meeting=active;PlaceAt(active?at:home);
        }
        public void Race(bool active,Vector3 at,Vector3 direction,float time,bool soaring)
        {
            if(!racing && active)walker.enabled=false;
            if(racing && !active){walker.enabled=true;PlaceAt(home);}
            racing=active;
            if(!active)return;
            transform.position=at;
            direction.y=0;if(direction.sqrMagnitude>.001f)body.rotation=Quaternion.LookRotation(direction);
            float gait=soaring?4:Mathf.Sin(time*10)*34;
            legL.localRotation=Quaternion.Euler(gait,0,0);legR.localRotation=Quaternion.Euler(-gait,0,0);
            armL.localRotation=Quaternion.Euler(-gait*.72f,0,soaring?-62:-8);armR.localRotation=Quaternion.Euler(gait*.72f,0,soaring?62:8);
        }
        bool outbound = true;
        protected override void Awake()
        {
            base.Awake();
            walker = GetComponent<CharacterController>();
            visitor = FindAnyObjectByType<WandererMotor>();
            home = transform.position;
        }
        protected override float Activate()
        {
            var quests=QuarterQuests.Current;
            if(quests && quests.Speak(firstName)){Speech=null;speakingUntil=Time.time+1;return 1;}
            speakingUntil = Time.time + 5;
            Speech = lines[line]; line = (line+1)%lines.Length;
            return .65f;
        }
        void Update()
        {
            float dt=Time.deltaTime;
            if (racing || dt<=0 || !visitor || !visitor.ControlsEnabled) return;
            var toPlayer=visitor.transform.position-transform.position;
            if (toPlayer.sqrMagnitude>125*125) return;
            if (Time.time>=speakingUntil) Speech=null;
            if(pace<=0){
                var gaze=Vector3.ProjectOnPlane(toPlayer,Vector3.up);
                if(Speech!=null && gaze.sqrMagnitude>.001f)body.rotation=Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(gaze),dt*140);
                head.localRotation=Quaternion.Euler(Speech==null?0:Mathf.Sin(Time.time*4)*3,0,0);
                return;
            }
            Vector3 goal=(outbound?end:home)-transform.position; goal.y=0;
            Vector3 forward=goal.normalized;
            Vector3 other=Vector3.ProjectOnPlane(toPlayer, Vector3.up);
            bool yielding=other.sqrMagnitude<2.4f*2.4f && Mathf.Abs(toPlayer.y)<1.8f && Vector3.Dot(other,forward)>-.2f;
            if (goal.magnitude<.38f) { outbound=!outbound;wait=1.7f; }
            wait=Mathf.MoveTowards(wait,0,dt);
            yieldCooldown -= dt;
            if (!meeting && yielding && Speech==null && yieldCooldown<=0 && visitor.Speed>1.5f && other.sqrMagnitude<3f)
            {
                var tangent=Vector3.Cross(Vector3.up,forward);
                if(Vector3.Dot(other,tangent)>0)tangent=-tangent;
                var foot=transform.position;
                if(!Physics.CapsuleCast(foot+Vector3.up*.33f,foot+Vector3.up*1.40f,.26f,tangent,.72f,1,QueryTriggerInteraction.Ignore) &&
                    Physics.Raycast(foot+tangent*.72f+Vector3.up*.25f,Vector3.down,.5f,1,QueryTriggerInteraction.Ignore))
                {yieldDirection=tangent;sideStep=.42f;yieldCooldown=3;}
            }
            var direction=Speech!=null||yielding?other:forward;
            if(direction.sqrMagnitude>.001f) body.rotation=Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(direction),dt*220);
            bool walking=!meeting&&wait<=0&&!yielding&&Speech==null;
            var before=transform.position;
            var motion=walking?forward*pace:Vector3.zero;
            if(sideStep>0 && Speech==null){motion=yieldDirection*1.25f;sideStep-=dt;}
            walker.Move((motion+Vector3.down*3f)*dt);
            float moved=Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude;
            if(walking && moved < pace*dt*.12f) stalled+=dt; else stalled=0;
            if(stalled>.9f){outbound=!outbound;wait=.7f;stalled=0;}
            stride += moved*6.1f;
            float gait=walking?Mathf.Sin(stride)*27:0;
            float blend=1-Mathf.Exp(-12*dt);
            legL.localRotation=Quaternion.Slerp(legL.localRotation,Quaternion.Euler(gait,0,0),blend);
            legR.localRotation=Quaternion.Slerp(legR.localRotation,Quaternion.Euler(-gait,0,0),blend);
            armL.localRotation=Quaternion.Slerp(armL.localRotation,Quaternion.Euler(-gait*.64f,0,-6),blend);
            armR.localRotation=Quaternion.Slerp(armR.localRotation,Quaternion.Euler(gait*.37f-16,0,7),blend);
            head.localRotation=Quaternion.Euler(Speech!=null?Mathf.Sin(Time.time*4)*3:0,wait>0?12*Mathf.Sin(Time.time):0,0);
        }
    }
}
