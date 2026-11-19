using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyWorld
{
    public sealed class QuarterRace : MonoBehaviour
    {
        public Transform[] hoops;
        public Vector3[] feet;
        public float[] lengths, arcs;
        public int[] gates;
        public int jumpGate, glideGate;
        public QuarterResident rival;
        public GameObject rivalWing;
        public bool Running { get; private set; }
        public string Status { get; private set; } = "";
        public int Cleared=>next;
        public float FinishTime { get; private set; }
        WandererMotor p;
        int next, leg, respawns;
        float elapsed,legTime,finishedAt;
        Vector3 before;
        bool armed,finishing,missed;
        void Start(){p=FindAnyObjectByType<WandererMotor>();Show();}
        public void Begin()
        {
            if(Running)return;
            if(!p)p=FindAnyObjectByType<WandererMotor>();
            p.Teleport(feet[0]+Vector3.left*.53f+Vector3.up*.08f);
            var dir=feet[1]-feet[0];FindAnyObjectByType<WandererCamera>().SetOrbit(Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg,9);
            rival.Race(true,feet[0]+Vector3.right*.5f,dir,0,false);
            Running=true;finishing=false;armed=false;missed=false;next=leg=0;legTime=0;elapsed=-3;respawns=p.Respawns;before=p.transform.position+Vector3.up;
            Show();
        }
        void Show()
        {
            for(int i=0;i<hoops.Length;i++){
                bool show=Running&&i>=next&&i<=next+2;hoops[i].gameObject.SetActive(show);
                hoops[i].localScale=Vector3.one*(i==next?1:.72f);
            }
            if(rivalWing)rivalWing.SetActive(false);
        }
        void Finish(bool win)
        {
            Running=false;finishing=true;finishedAt=Time.time;Show();
            if(win) {
                FinishTime=elapsed;
                var book=QuarterQuests.Current;
                if(!book.Speak("race-win"))book.Announce("Moss   ·   You got me again. "+elapsed.ToString("0.0")+" seconds!",7);
            }
            else QuarterQuests.Current.Announce("Moss wins. Press T or D-pad up for a rematch, or meet him at the quay.",8);
        }
        void Update()
        {
            if((!Running&&!finishing)||!p||!p.ControlsEnabled||Time.deltaTime<=0)return;
            bool retry=(Keyboard.current?.tKey.wasPressedThisFrame??false)||(Gamepad.current?.dpad.up.wasPressedThisFrame??false);
            if(retry && (Running||Time.time-finishedAt<25)){
                Running=false;Begin();return;
            }
            if(finishing){
                if(leg<lengths.Length){elapsed+=Time.deltaTime;AdvanceRival(1.5f);}
                else{
                    var finish=feet[feet.Length-1];rival.Race(true,finish,p.transform.position-finish,0,false);
                    if(Time.time-finishedAt>20 && Vector3.Distance(p.transform.position,finish)>12){
                        finishing=false;rival.Race(false,Vector3.zero,Vector3.forward,0,false);
                    }
                }
                return;
            }
            if(p.Respawns!=respawns){Finish(false);return;}
            elapsed+=Time.deltaTime;
            if(elapsed<0){
                p.Teleport(feet[0]+Vector3.left*.53f+Vector3.up*.06f);
                Status="The courier race   ·   "+Mathf.CeilToInt(-elapsed);return;
            }
            if(!armed){before=p.transform.position+Vector3.up;armed=true;}
            var current=p.transform.position+Vector3.up;
            var h=hoops[next];var d=current-before;
            float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(h.position-before,d)/d.sqrMagnitude):0;
            var offset=before+d*t-h.position;
            float distance=offset.magnitude;
            bool mode=next==glideGate?p.Gliding:next!=jumpGate||(!p.Grounded&&!p.Hanging);
            var gateForward=h.forward;
            if (Vector3.Dot(before-h.position,gateForward)<0 && Vector3.Dot(current-h.position,gateForward)>=0
                && distance<12 && (distance>=1.24f || !mode)) missed=true;
            if(distance<1.24f&&mode && (current-before).sqrMagnitude<36){
                missed=false;next++;if(next==hoops.Length){Finish(true);return;} Show();
            }
            before=current;
            int playerLeg=next==0?0:gates[next-1];
            float rivalPace=1.70f+Mathf.Clamp((playerLeg-leg)*.22f,-.56f,1.65f);
            if(!AdvanceRival(rivalPace)){Finish(false);return;}
            Status="Hoop "+(next+1)+" / "+hoops.Length+"   ·   "+(missed?"Missed hoop · circle back":next==glideGate?"Glide through":next==jumpGate?"Jump through":leg<gates[next]?"Moss is chasing":"Catch Moss")+"   ·   "+elapsed.ToString("0.0")+" s";
        }
        bool AdvanceRival(float rivalPace)
        {
            legTime+=Time.deltaTime*rivalPace;
            while(legTime>=lengths[leg]){
                legTime-=lengths[leg];leg++;
                if(leg>=lengths.Length)return false;
            }
            float f=legTime/lengths[leg];var a=feet[leg];var b=feet[leg+1];
            var pos=Vector3.Lerp(a,b,f)+Vector3.up*(arcs[leg]*4*f*(1-f));
            bool soaring=leg>=gates[glideGate]-1&&leg<gates[glideGate]+1;
            rival.Race(true,pos,b-a,elapsed,soaring);
            if(rivalWing)rivalWing.SetActive(soaring);
            return true;
        }
    }
}
