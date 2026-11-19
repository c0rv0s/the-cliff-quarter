using UnityEngine;

namespace TinyWorld
{
    public sealed class QuarterErrandPoint : QuarterLandmark
    {
        public string role, prompt;
        public GameObject item;
        public bool permanent;
        public AudioClip note;
        public float pitch=1;
        public override string Verb => role == "bird" && QuarterQuests.Current && QuarterQuests.Current.Stage(3) == 2
            ? "Mend the swallow’s nest with the blue thread" : prompt;
        public void Refresh(QuarterQuests tales){
            bool available=tales.Needs(role);
            if(item)item.SetActive(available || permanent);
            enabled=available;
        }
        protected override float Activate()
        {
            if(QuarterQuests.Current && QuarterQuests.Current.Speak(role))Sound(note,.8f,pitch);
            return .45f;
        }
    }
}
