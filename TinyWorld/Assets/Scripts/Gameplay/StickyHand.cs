using UnityEngine;

namespace TinyWorld
{
    public sealed class StickyHand : MonoBehaviour
    {
        public float maxReach = 10.8f;
        public float shotSpeed = 110;
        public const float MaxHoldSeconds = 10;
        public float HoldSeconds { get; private set; }
        public bool Attached => target && !flying;
        public Collider AttachedCollider => Attached?target:null;
        public bool Flying => flying;
        public float RopeLength { get; private set; }
        public bool AimValid { get; private set; }
        public Vector3 Point => target ? target.transform.TransformPoint(localPoint) : tip;
        public Vector3 Normal => target ? target.transform.TransformDirection(localNormal).normalized : Vector3.up;
        public float Stretch => target ? Vector3.Distance(Origin, Point) : 0;
        public Vector3 Origin => transform.position + Vector3.up * (motor.Crouched ? .72f : 1.3f) + motor.visual.right * .2f;
        public int Shots { get; private set; }
        public int Attachments { get; private set; }
        public string Notice { get; private set; } = "";
        WandererMotor motor;
        Collider target;
        Vector3 localPoint, localNormal, tip, direction;
        bool flying;
        const int SurfaceMask=~(1<<2);
        float traveled, noticeTime, retract;
        LineRenderer cord;
        Transform hand;
        Material green;

        void Awake()
        {
            motor = GetComponent<WandererMotor>();
            green = new Material(motor.ropeMaterial) { name = "Sticky hand green" };
            green.SetColor("_BaseColor", new Color(.25f, .95f, .18f));
            green.SetFloat("_Smoothness", .65f);
            green.EnableKeyword("_EMISSION");green.SetColor("_EmissionColor", new Color(.06f, .23f, .025f));
            var root = new GameObject("Stretchy green hand");root.transform.SetParent(transform, false);
            cord = root.AddComponent<LineRenderer>();cord.positionCount = 12;cord.numCapVertices = 4;
            cord.startWidth = .075f;cord.endWidth = .11f;cord.sharedMaterial = green;
            hand = new GameObject("Sticky palm").transform;hand.SetParent(root.transform, false);
            Shape("Palm", new Vector3(0,0,.015f), new Vector3(.34f,.34f,.1f));
            for(int i=0;i<4;i++)
                Shape("Finger", new Vector3((i-1.5f)*.09f,.21f-Mathf.Abs(i-1.5f)*.025f,0),new Vector3(.085f,.26f,.085f));
            var thumb=Shape("Thumb",new Vector3(-.21f,.015f,.01f),new Vector3(.1f,.22f,.09f));
            thumb.localRotation=Quaternion.Euler(0,0,55);
            cord.enabled=false;hand.gameObject.SetActive(false);
        }
        Transform Shape(string name,Vector3 position,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.layer=2;
            go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(hand,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=green;return go.transform;
        }
        Vector3 AimDirection()
        {
            var camera=Camera.main;
            if(!camera)return motor.visual.forward;
            var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));
            bool hit=Physics.Raycast(ray,out var surface,200,SurfaceMask,QueryTriggerInteraction.Ignore)&&Vector3.Dot(surface.point-Origin,ray.direction)>.1f;
            if(hit&&Vector3.Distance(Origin,surface.point)<=maxReach)return (surface.point-Origin).normalized;
            // A shot that would miss snaps to a swing ring close to the reticle.
            TraversalAnchor best=null;float bestAngle=9;
            foreach(var ring in TraversalAnchor.Active)
            {
                var to=ring.transform.position-Origin;float angle=Vector3.Angle(ray.direction,to);
                if(to.magnitude<=maxReach&&angle<bestAngle){best=ring;bestAngle=angle;}
            }
            if(best)return (best.transform.position-Origin).normalized;
            return hit?(surface.point-Origin).normalized:ray.direction;
        }
        public void SetTrigger(bool pressed,bool held)
        {
            if(pressed)Fire();
            if(!held)Release();
        }
        public bool Fire(){Launch(AimDirection());return true;}
        public bool FireAt(Vector3 point){Launch((point-Origin).normalized);return true;}
        void Launch(Vector3 aim)
        {
            target=null;direction=aim.sqrMagnitude>.1f?aim:motor.visual.forward;
            tip=Origin;traveled=0;flying=true;retract=0;HoldSeconds=0;
            Shots++;motor.feedback?.StickyShot();Notice="";
            foreach(var collider in Physics.OverlapSphere(tip,.06f,SurfaceMask,QueryTriggerInteraction.Ignore))
            {
                Attach(collider,tip,-direction);break;
            }
        }
        void Attach(Collider collider,Vector3 point,Vector3 normal)
        {
            target=collider;localPoint=target.transform.InverseTransformPoint(point);
            localNormal=target.transform.InverseTransformDirection(normal);tip=point;
            flying=false;HoldSeconds=0;RopeLength=Mathf.Max(4.2f,Vector3.Distance(motor.TetherOrigin,Point)*.82f);
            Attachments++;motor.BeginGrapple();motor.feedback?.StickyAttach();
        }
        public void Pull(ref Vector3 velocity,float dt)
        {
            float length=RopeLength;
            velocity=GrappleMotion.Pull(velocity,Point-motor.TetherOrigin,ref length,dt);
            RopeLength=length;
        }
        public void Release()
        {
            if(target){tip=Point;motor.EndGrapple();motor.feedback?.StickyRelease();}
            if(target||flying)retract=.32f;
            target=null;flying=false;
        }
        public void ResetHand(){target=null;flying=false;traveled=retract=HoldSeconds=0;}
        public void Tick(float dt)
        {
            if(target&&(!target.enabled||!target.gameObject.activeInHierarchy))Release();
            if(Attached)
            {
                HoldSeconds+=dt;
                if(HoldSeconds>=MaxHoldSeconds)Release();
            }
            if(Flying)
            {
                float step=Mathf.Min(shotSpeed*dt,maxReach-traveled);
                if(Physics.SphereCast(tip,.065f,direction,out var hit,step,SurfaceMask,QueryTriggerInteraction.Ignore))
                    Attach(hit.collider,hit.point,hit.normal);
                else
                {
                    tip+=direction*step;traveled+=step;
                    if(traveled>=maxReach)Release();
                }
            }
            else if(Attached)tip=Point;
            else if(retract>0){retract-=dt;tip=Vector3.MoveTowards(tip,Origin,40*dt);}
            noticeTime-=dt;if(noticeTime<=0)Notice="";
        }
        void LateUpdate()
        {
            AimValid=motor.ControlsEnabled&&Physics.SphereCast(Origin,.065f,AimDirection(),out _,maxReach,SurfaceMask,QueryTriggerInteraction.Ignore);
            bool show=target||flying||retract>0;cord.enabled=show;hand.gameObject.SetActive(show);
            if(!show)return;
            hand.position=tip+(target?Normal*.035f:Vector3.zero);
            Vector3 facing=target?Normal:flying?-direction:Origin-tip;
            if(facing.sqrMagnitude>.0001f)hand.rotation=Quaternion.LookRotation(facing);
            hand.localScale=Vector3.one*(Attached?1.15f:1);
            float slack=Attached?Mathf.Max(0,RopeLength-Vector3.Distance(motor.TetherOrigin,Point)):0;
            float bow=Attached?Mathf.Clamp(slack*.25f,.025f,.65f):.18f;
            for(int i=0;i<12;i++)
            {
                float t=i/11f;
                cord.SetPosition(i,Vector3.Lerp(Origin,hand.position,t)+Vector3.down*(Mathf.Sin(t*Mathf.PI)*bow));
            }
        }
        void OnDestroy(){if(green)Destroy(green);}
    }
}
