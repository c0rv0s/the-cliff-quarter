using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TinyWorld;
// Replaces the primitive wanderer in the open playable scene with Assets/Models/Wanderer.fbx,
// rebuilding joints at identity rotation so WandererPose can drive them.
public static class SetupWanderer
{
    static readonly (string joint, string parent)[] Joints =
    {
        ("Hips", "Rig"), ("Chest", "Hips"), ("Head", "Chest"), ("CapeUpper", "Chest"), ("CapeLower", "CapeUpper"), ("ScarfTail", "Chest"),
        ("UpperArm.L", "Chest"), ("Forearm.L", "UpperArm.L"), ("Hand.L", "Forearm.L"), ("UpperArm.R", "Chest"), ("Forearm.R", "UpperArm.R"), ("Hand.R", "Forearm.R"),
        ("Thigh.L", "Hips"), ("Shin.L", "Thigh.L"), ("Foot.L", "Shin.L"), ("Thigh.R", "Hips"), ("Shin.R", "Thigh.R"), ("Foot.R", "Shin.R"),
    };
    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new System.Exception("Stop Play mode.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/RiverCityCliffside.unity")
        {
            if (scene.isDirty) throw new System.Exception("Save the open scene first.");
            scene = EditorSceneManager.OpenScene("Assets/Scenes/RiverCityCliffside.unity");
        }
        var motor = Object.FindAnyObjectByType<WandererMotor>();
        var visual = motor.visual;
        var pose = visual.GetComponent<WandererPose>();
        foreach (Transform child in visual.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Wanderer.fbx"));
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.SetPositionAndRotation(visual.position, visual.rotation * model.transform.rotation);
        var source = model.GetComponentsInChildren<Transform>().ToDictionary(t => t.name, t => t);

        var rig = new GameObject("Rig").transform; rig.SetParent(visual, false);
        var built = new Dictionary<string, Transform> { ["Rig"] = rig };
        foreach (var (joint, parent) in Joints)
        {
            var t = new GameObject(joint).transform; t.SetParent(built[parent], false);
            t.position = source[joint].position; t.localRotation = Quaternion.identity; built[joint] = t;
        }
        var renderers = model.GetComponentsInChildren<MeshRenderer>().ToList();
        foreach (var r in renderers)
        {
            var target = built[r.transform.parent.name];
            r.transform.SetParent(target, true);
            r.name = r.name.Substring(2);
            r.shadowCastingMode = r.name.StartsWith("Eye") || r.name.StartsWith("Cheek") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.gameObject.layer = 2;
        }
        Object.DestroyImmediate(model);
        Transform Eye(string name)
        {
            var mesh = built["Head"].Find(name);
            var pivot = new GameObject(name + " pivot").transform; pivot.SetParent(built["Head"], false);
            pivot.position = mesh.GetComponent<Renderer>().bounds.center; mesh.SetParent(pivot, true); return pivot;
        }
        pose.player = motor; pose.rig = rig;
        pose.hips = built["Hips"]; pose.chest = built["Chest"]; pose.head = built["Head"];
        pose.capeUpper = built["CapeUpper"]; pose.capeLower = built["CapeLower"]; pose.scarfTail = built["ScarfTail"];
        pose.upperArmL = built["UpperArm.L"]; pose.forearmL = built["Forearm.L"]; pose.handL = built["Hand.L"];
        pose.upperArmR = built["UpperArm.R"]; pose.forearmR = built["Forearm.R"]; pose.handR = built["Hand.R"];
        pose.thighL = built["Thigh.L"]; pose.shinL = built["Shin.L"]; pose.footL = built["Foot.L"];
        pose.thighR = built["Thigh.R"]; pose.shinR = built["Shin.R"]; pose.footR = built["Foot.R"];
        pose.eyeL = Eye("EyeL"); pose.eyeR = Eye("EyeR");
        EditorUtility.SetDirty(pose);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return new { scene = scene.path, joints = built.Count, renderers = renderers.Count, eyeFront = visual.InverseTransformPoint(pose.eyeL.position).ToString("F3") };
    }
}
