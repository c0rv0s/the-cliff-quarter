using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object = UnityEngine.Object;

// Play-mode check for the glider. Run: unity command script run --file AgentScripts/VerifyGlider.cs --entry VerifyGlider.Run --timeout 120
public static class VerifyGlider
{
    static WandererMotor p;
    static readonly List<object> results = new();
    static async Task Wait(int ms)
    {
        var shell = Object.FindAnyObjectByType<WandererShell>();
        float until = Time.realtimeSinceStartup + ms * .001f;
        while (Time.realtimeSinceStartup < until) { if (shell.Paused) shell.SetPaused(false); await Task.Delay(20); }
    }
    static void Check(string name, bool pass, object evidence)
    { results.Add(new { test = name, passed = pass, evidence }); Debug.Log("[VerifyGlider] " + name + " " + pass + " " + evidence); }
    static void Drive(Vector2 move, bool sprint = false, bool jump = false, bool drop = false)
    { p.TestInput = true; p.TestMove = move; p.TestSprint = sprint; p.TestJumpPressed = jump; p.TestJumpHeld = jump; p.TestDrop = drop; }
    static Transform Wing => p.visual.Find("Glider");
    static void Place(Vector3 point, float yaw = 0)
    {
        Drive(Vector2.zero); p.Teleport(point);
        p.visual.rotation = Quaternion.Euler(0, yaw, 0);
        Object.FindAnyObjectByType<WandererCamera>().SetOrbit(yaw, 13);
    }
    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new Exception("Start Play mode first.");
        results.Clear(); p = Object.FindAnyObjectByType<WandererMotor>();
        Object.FindAnyObjectByType<WandererShell>().Begin();
        var spawn = p.transform.position;
        var sky = new Vector3(0, 40, 70);

        Place(sky, 180); Drive(Vector2.up); await Wait(350);
        float fallBefore = p.VerticalSpeed; int glides = p.Glides;
        Drive(Vector2.up, jump: true); await Wait(80);
        Check("opens in open air", p.Gliding && p.Glides == glides + 1, new { fallBefore, p.Action });
        await Wait(250);
        Check("wing unfolds", Wing && Wing.gameObject.activeSelf && p.Glider.Openness > .9f, new { p.Glider.Openness, scale = Wing ? Wing.localScale.ToString() : "missing" });
        await Wait(900);
        float y0 = p.transform.position.y; await Wait(1000); float sink = y0 - p.transform.position.y;
        Check("steady glide", p.Gliding && sink > 1.5f && sink < 3.5f && p.Speed > 8.5f, new { sink, p.Speed, p.VerticalSpeed });

        var heading = new Vector3(p.Velocity.x, 0, p.Velocity.z).normalized;
        Drive(Vector2.right); await Wait(350);
        float bank = Wing ? Mathf.DeltaAngle(0, Wing.localEulerAngles.z) : 0;
        await Wait(650);
        float turned = Vector3.Angle(heading, new Vector3(p.Velocity.x, 0, p.Velocity.z));
        Check("steers and banks", p.Gliding && turned > 45 && bank < -8, new { turned, bank });

        Place(sky, 180); Drive(Vector2.up); await Wait(300); Drive(Vector2.up, jump: true); await Wait(900);
        Drive(Vector2.up, sprint: true); await Wait(1200);
        Check("Shift dives faster", p.Gliding && p.Action == "Glide dive" && p.Speed > 12.5f && p.VerticalSpeed < -4.5f, new { p.Speed, p.VerticalSpeed });

        Drive(Vector2.zero, jump: true); await Wait(80);
        Check("Space folds", !p.Gliding, p.Action);
        await Wait(250);
        Check("wing hides", Wing && !Wing.gameObject.activeSelf, p.Glider.Openness);
        Drive(Vector2.zero, jump: true); await Wait(80);
        Check("reopens", p.Gliding, p.Glides);
        Drive(Vector2.zero, drop: true); await Wait(80);
        Check("C folds", !p.Gliding, p.Action);

        Place(spawn + Vector3.up * 1.4f); await Wait(60); glides = p.Glides;
        Drive(Vector2.zero, jump: true); await Wait(400);
        Check("no glider near the ground", !p.Gliding && p.Glides == glides, p.Action);

        Place(spawn + Vector3.up * 4.5f); await Wait(250); Drive(Vector2.zero, jump: true); await Wait(80); Drive(Vector2.zero);
        bool opened = p.Gliding; float waited = 0;
        while (!p.Grounded && !p.Swimming && waited < 15000) { await Wait(100); waited += 100; }
        await Wait(100);
        Check("landing folds", opened && !p.Gliding && (p.Grounded || p.Swimming), new { opened, p.Action, waited });

        Place(spawn); p.TestInput = false;
        return results.ToArray();
    }
    // Leaves the wanderer gliding high over the city for a capture.
    public static async Task<object> Pose()
    {
        p = Object.FindAnyObjectByType<WandererMotor>();
        Object.FindAnyObjectByType<WandererShell>().Begin();
        Place(new Vector3(0, 40, 70), 200); Drive(new Vector2(.3f, 1)); await Wait(300);
        Drive(new Vector2(.3f, 1), jump: true); await Wait(1300);
        return new { p.Gliding, p.Action, position = p.transform.position.ToString() };
    }
}
