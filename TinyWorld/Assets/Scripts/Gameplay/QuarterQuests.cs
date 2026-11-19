using System.Collections.Generic;
using UnityEngine;

namespace TinyWorld
{
    public sealed class QuarterQuests : MonoBehaviour
    {
        public static QuarterQuests Current { get; private set; }
        public QuarterRace race;
        public QuarterSkiff skiff;
        public GameObject lantern, paradeMantle;
        bool paradeOutfit = true;
        bool allRewardsUnlocked;
        public bool Earned(int quest) => allRewardsUnlocked || stages[quest] == Tales[quest].Length;
        public void GrantAllRewards()
        {
            if (allRewardsUnlocked) return;
            allRewardsUnlocked = true;
            RefreshWorld();
            Announce("All six quest rewards unlocked.", 4);
        }
        public bool CanGlide => Earned(1) || (race && race.Running);
        static readonly (string name, string use)[] Prizes = {
            ("Sea-glass lantern", "Lights your way"),
            ("Courier glider", "Jump as you fall to open it"),
            ("Parade outfit", "Change outfits in the journal"),
            ("Swallow flip", "Jump again in midair"),
            ("Bell keeper’s boots", "Sprint to run faster"),
            ("Painted skiff", "B / R3 calls it to the nearest water")
        };
        public int CelebrationQuest { get; private set; } = -1;
        public string RewardName => Prizes[CelebrationQuest].name;
        public string RewardUse => Prizes[CelebrationQuest].use;
        public const float CelebrationDuration = 4.2f;
        public float CelebrationAge => Time.time - (until - CelebrationDuration);
        int prizeAfterDialogue = -1;
        readonly Queue<(string line, float seconds, int prize)> waiting = new();
        public GameObject paradeHorse;
        public QuarterResident suitor, courier;
        public Vector3 barnMeeting;
        public struct Beat
        {
            public string actor, line, next;
            public Beat(string who,string says,string objective) { actor=who;line=says;next=objective; }
        }
        public static readonly string[] Titles={"The Blue Glass Ring","Catch the Courier","All That Glitters","A Swallow's Hem","Three Notes for Noon","The Fish Thieves"};
        public static readonly Beat[][] Tales={
            new Beat[]{new("Nera","I lost my ring in a cave. Blue glass, brass band. Worth about two onions. It was my mother's. Will you look?","Find Nera’s blue glass ring."),new("ring","A chip in the blue glass catches the light. The inside is engraved with two tiny onions.","Return the ring to Nera on the east wharf."),new("Nera","There they are. Dad couldn't afford flowers, so he proposed with onions. Thank you.","")},
            new Beat[]{new("Moss","Borrow my glider. Gold hoops in order, up to the bell roof, then jump again as you fall to open it. Beat me home and it’s yours.","Beat Moss through the gold hoops."),new("race-win","Moss doubles over, laughing. “Keep the glider. I’ll carry the onions.”","")},
            new Beat[]{new("Davi","Could you take this to Pia on the east river walk? The heart on the seal is a turnip. Definitely a turnip.","Deliver Davi’s letter to Pia on the east river walk."),new("Pia","“Your eyes are two wet olives.” Oh, Davi. Tell him I value our professional relationship as neighbours.","Carry Pia’s reply to Davi."),new("Davi","Fair enough. One last note? I need my measuring tape back. I'm fitting my horse for the parade.","Take the folded note back to Pia."),new("Pia","The flap’s open. Does that say GOLD HORSE ARMOUR? I was, coincidentally, just going to the barn.","Meet Davi and Pia inside the farm barn."),new("Davi","Mind the buckle. A year's wages, and the horse has already tried to eat it twice.","Hear what Pia has to say at the barn."),new("Pia","Davi! Those wet olives of yours. Suddenly I'm free for dinner. Is that solid gold?","Talk to Davi beside the horse."),new("Davi","Pia, you're a gold digger. He’s a hay digger. I've only got room in my life for one digger. My tape, please.","")},
            new Beat[]{new("bird","The swallow tugs a loose stitch from its nest. Blue thread! Down on the quay, bits of the same blue flutter under the dyer’s cream awning with indigo diamonds.","Find blue thread under the dyer’s diamond awning on the quay."),new("thread","The bobbin unspools a long blue thread. Just enough to bind a nest.","Bring the thread back to the swallow on the green-shuttered roof beside the west end of Olive Ward bridge."),new("bird","You tie the blue thread around the nest. The swallow tests the rim, then somersaults twice in the air. This time you can follow.","")},
            new Beat[]{new("Ines","Will you ring noon inside the temple? Earth first, sea second, sun last. I tied ochre, blue and gold cords to them.","Sound the ochre earth bell inside the temple."),new("earth","A low note rolls along the stone floor.","Sound the blue sea bell."),new("sea","The second note hangs above the first.","Sound the golden sun bell."),new("sun","The high bell answers. For a moment the whole room sings.","Return to Ines at the temple."),new("Ines","Noon! A little late, but the sun has never once complained.","")},
            new Beat[]{new("Belen","Two fish off my scale, two suspects on the low landing. Check the big copper fellow, then the pup. Look at their whiskers.","Inspect Old Copper on the fish pier’s low landing."),new("copper","Old Copper yawns. A single sardine scale shines on his tongue.","Inspect Button, the little sea lion."),new("pup","Button sneezes up a fish head. It lands directly on your boot.","Report the evidence to Belen at the docks."),new("Belen","Open and shut. Their sentence is another fish apiece. I am a terrible judge.","")}
        };
        readonly int[] stages=new int[Titles.Length];
        public string Message { get; private set; }
        float until;
        public int DiscoveredCount { get; private set; }
        public int TotalCount => Tales.Length;
        public string Journal { get; private set; }
        bool atBarn;
        WandererMotor player;
        QuarterErrandPoint[] pickups;
        void Awake()
        {
            Current=this;player=FindAnyObjectByType<WandererMotor>(); pickups=GetComponentsInChildren<QuarterErrandPoint>(true);
        }
        void Start()
        {
            if (player && lantern) {
                var hips = player.GetComponentInChildren<WandererPose>().hips;
                lantern.transform.SetParent(hips, false);
                lantern.transform.localPosition = new Vector3(-.38f, -.195f, -.12f);
            }
            if (player && paradeMantle)
                paradeMantle.transform.SetParent(player.GetComponentInChildren<WandererPose>().chest, false);
            RefreshWorld();
        }
        public void ToggleOutfit()
        {
            if (!Earned(2)) return;
            paradeOutfit = !paradeOutfit;
            RefreshWorld();
        }
        void WearOutfit()
        {
            var block = new MaterialPropertyBlock();
            foreach (var cloth in player.visual.GetComponentsInChildren<Renderer>(true)) {
                var mat = cloth.sharedMaterial;
                if (!mat) continue;
                Color dye;
                switch (mat.name) {
                    case "Wanderer Cape": dye = Palette.Hex("117A79"); break;
                    case "Wanderer Tunic": dye = Palette.Hex("A23C4A"); break;
                    case "Wanderer Trousers": dye = Palette.Hex("363054"); break;
                    case "Wanderer Trim": dye = Palette.Hex("F1C263"); break;
                    default: continue;
                }
                cloth.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Earned(2) && paradeOutfit ? dye : mat.GetColor("_BaseColor"));
                cloth.SetPropertyBlock(block);
            }
        }
        void Update()
        {
            AdvanceDialogue();
            if(stages[2]<4 || !player)return;
            float distance=(player.transform.position-barnMeeting).sqrMagnitude;
            bool join=stages[2]<Tales[2].Length && distance<48*48;
            if(atBarn==join || (!join && distance<65*65))return;
            atBarn=join;
            suitor.Meet(join,barnMeeting+Vector3.left*1.3f);
            courier.Meet(join,barnMeeting+Vector3.right*1.2f);
        }
        void OnDestroy(){ if(Current==this)Current=null; }
        public int Stage(int i)=>stages[i];
        public bool Needs(string role){
            for(int i=0;i<Tales.Length;i++)if(!(i==2&&stages[i]>=4&&!atBarn) && stages[i]<Tales[i].Length && Tales[i][stages[i]].actor==role)return true;
            return false;
        }
        public bool HasOffer(string who){for(int i=0;i<Tales.Length;i++)if(stages[i]==0&&Tales[i][0].actor==who)return true;return false;}
        public bool Speak(string who)
        {
            if(who=="Moss"&&stages[1]>0&&!race.Running){race.Begin();return true;}
            for(int i=0;i<Tales.Length;i++){
                if((i==2&&stages[i]>=4&&!atBarn) || stages[i]>=Tales[i].Length || Tales[i][stages[i]].actor!=who)continue;
                var beat=Tales[i][stages[i]++];
                string line = (char.IsUpper(who[0]) ? who + "   ·   " : "") + beat.line;
                bool finished = stages[i] == Tales[i].Length;
                float seconds = finished ? Mathf.Clamp(1 + line.Length * .05f, 3.5f, 8) : 9;
                Announce(line, seconds, finished ? i : -1);
                RefreshWorld();
                if(i==1 && stages[i]==1)race.Begin();
                return true;
            }
            return false;
        }
        public void Announce(string line, float seconds) => Announce(line, seconds, -1);
        void Announce(string line, float seconds, int prize)
        {
            if (prizeAfterDialogue >= 0 || CelebrationQuest >= 0 || waiting.Count > 0)
                waiting.Enqueue((line, seconds, prize));
            else ShowLine(line, seconds, prize);
        }
        void ShowLine(string line, float seconds, int prize)
        {
            Message = line;
            until = Time.time + seconds;
            prizeAfterDialogue = prize;
        }
        void AdvanceDialogue()
        {
            if (Time.time < until) return;
            if (prizeAfterDialogue >= 0) {
                CelebrationQuest = prizeAfterDialogue;
                prizeAfterDialogue = -1;
                Message = "";
                until = Time.time + CelebrationDuration;
                return;
            }
            CelebrationQuest = -1;
            if (waiting.Count > 0) {
                var next = waiting.Dequeue();
                ShowLine(next.line, next.seconds, next.prize);
            }
        }
        public string Caption=>Time.time<until?Message:"";
        void RefreshWorld()
        {
            var entries = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Tales.Length; i++)
            {
                int stage = stages[i];
                if (stage == 0) continue;
                var detail = stage == Tales[i].Length ? "Earned · " + Prizes[i].name + " · " + Prizes[i].use : Tales[i][stage - 1].next;
                entries.Add("<b>" + Titles[i] + "</b>\n" + detail);
            }
            DiscoveredCount = entries.Count;
            Journal = entries.Count == 0 ? "Look for a brass ! above someone in town." : string.Join("\n\n", entries);
            if (Earned(2)) Journal += "\n\nO / D-right · " + (paradeOutfit ? "Wear travelling clothes" : "Wear parade outfit");
            if (skiff) skiff.gameObject.SetActive(Earned(5));
            if (lantern) lantern.SetActive(Earned(0));
            if (paradeMantle) paradeMantle.SetActive(Earned(2) && paradeOutfit);
            if (player) WearOutfit();
            foreach(var point in pickups)point.Refresh(this);
            paradeHorse.SetActive(stages[2]>=4);
        }
    }
}
