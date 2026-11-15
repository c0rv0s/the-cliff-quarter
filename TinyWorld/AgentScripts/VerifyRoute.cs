using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object = UnityEngine.Object;

public static class VerifyRoute
{
    static WandererMotor p;
    static WandererCamera orbit;
    static List<object> results;
    static async Task Traverse(Vector3 position, float yaw)
    {
        p.Teleport(position); orbit.SetOrbit(yaw, 13); p.TestInput = true; p.TestMove = Vector2.zero;
        await Task.Delay(220); int before = p.Traversals;
        p.TestMove = Vector2.up; p.TestJumpPressed = true; p.TestJumpHeld = true;
        await Task.Delay(80); p.TestMove = Vector2.zero; p.TestJumpHeld = false;
        await Task.Delay(650);
        results.Add(new { test = "route ledge from " + position, passed = p.Traversals > before && p.Grounded, position = p.transform.position.ToString() });
    }
    public static async Task<object> Check()
    {
        results = new List<object>(); p = Object.FindAnyObjectByType<WandererMotor>(); orbit = Object.FindAnyObjectByType<WandererCamera>();
        Object.FindAnyObjectByType<WandererShell>().Begin();
        await Traverse(new Vector3(-7.65f, 1.64f, 3.8f), -90);
        await Traverse(new Vector3(-9.1f, 2.69f, 3.5f), -90);
        await Traverse(new Vector3(-10.7f, 3.84f, 3.1f), -90);
        await Traverse(new Vector3(-12, 4.99f, 3.3f), 0);
        p.TestMove = Vector2.zero;
        p.Teleport(new Vector3(-7.2f, 1.65f, -2.5f)); orbit.SetOrbit(0, 13); await Task.Delay(200);
        var pose = Object.FindAnyObjectByType<WandererPose>();
        var gait = new List<string>(); p.TestMove = Vector2.up;
        gait.Add(pose.leftLeg.localRotation.ToString("F4") + pose.torso.localPosition.ToString("F4"));
        await Task.Delay(700); gait.Add(pose.leftLeg.localRotation.ToString("F4") + pose.torso.localPosition.ToString("F4"));
        await Task.Delay(1300); gait.Add(pose.leftLeg.localRotation.ToString("F4") + pose.torso.localPosition.ToString("F4"));
        p.TestMove = Vector2.zero; await Task.Delay(700); float idleA = pose.torso.localPosition.y; await Task.Delay(500); float idleB = pose.torso.localPosition.y;
        results.Add(new { test = "gait and idle", passed = gait[0] != gait[1] && gait[1] != gait[2] && Mathf.Abs(idleA - idleB) > .0001f, gait, idleA, idleB });
        // Pause must freeze world position; resume must restore controls.
        var shell = Object.FindAnyObjectByType<WandererShell>(); shell.SetPaused(true); var at = p.transform.position;
        p.TestMove = Vector2.up; await Task.Delay(180); bool froze = p.transform.position == at;
        shell.SetPaused(false); await Task.Delay(180);
        results.Add(new { test = "pause and resume", passed = froze && p.transform.position != at });
        p.TestMove = Vector2.zero; p.TestInput = false; p.Teleport(new Vector3(-7.2f, 1.65f, -2.5f)); p.visual.rotation = Quaternion.identity; orbit.SetOrbit(0, 13);
        foreach (var result in results) Debug.Log("[VerifyRoute] " + Newtonsoft.Json.JsonConvert.SerializeObject(result));
        return results;
    }
}
