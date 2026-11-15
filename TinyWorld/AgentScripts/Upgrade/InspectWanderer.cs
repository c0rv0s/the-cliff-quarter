using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using TinyWorld;
public static class InspectWanderer
{
    public static string Run()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/RiverCityCliffside.unity"&&!scene.isDirty) EditorSceneManager.OpenScene("Assets/Scenes/RiverCityCliffside.unity");
        var p=Object.FindAnyObjectByType<WandererMotor>();
        var sb=new StringBuilder("scene "+EditorSceneManager.GetActiveScene().path+"\n");
        void Walk(Transform t,int d){
            var mr=t.GetComponent<MeshRenderer>();var mf=t.GetComponent<MeshFilter>();
            sb.Append(new string(' ',d*2)+t.name+" lp"+t.localPosition.ToString("F2")+" lr"+t.localEulerAngles.ToString("F0")+" ls"+t.localScale.ToString("F2")+(mf?" mesh:"+(mf.sharedMesh?mf.sharedMesh.name:"null"):"")+(mr?" mat:"+mr.sharedMaterial?.name:"")+" ["+string.Join(",",System.Array.ConvertAll(t.GetComponents<Component>(),c=>c.GetType().Name))+"]\n");
            foreach(Transform c in t)Walk(c,d+1);
        }
        Walk(p.transform,0);
        var pose=p.GetComponentInChildren<WandererPose>()??Object.FindAnyObjectByType<WandererPose>();
        sb.Append("pose on "+(pose?pose.name:"none")+"\n");
        var cc=p.GetComponent<CharacterController>();sb.Append($"cc h{cc.height} r{cc.radius} c{cc.center} slope{cc.slopeLimit} step{cc.stepOffset}\n");
        return sb.ToString();
    }
}
