using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Unity.Cinemachine;
using Object = UnityEngine.Object;

public static class VerifyMovement
{
    static WandererMotor p;
    static WandererCamera orbit;
    static List<object> results;
    static async Task Wait(int ms) { await Task.Delay(ms); }
    static void Check(string test, bool passed, object evidence) { results.Add(new { test, passed, evidence }); Debug.Log("[Verify] " + test + ": " + passed + " " + evidence); }
    static void Drive(Vector2 direction, bool sprint = false, bool jump = false, bool hold = false)
    { p.TestInput = true; p.TestMove = direction; p.TestSprint = sprint; p.TestJumpPressed = jump; p.TestJumpHeld = hold; }
    public static async Task<object> CheckAll()
    {
        if (!Application.isPlaying) throw new Exception("Start Play first.");
        results = new List<object>(); p = Object.FindAnyObjectByType<WandererMotor>(); orbit = Object.FindAnyObjectByType<WandererCamera>();
        Object.FindAnyObjectByType<WandererShell>().Begin();
        orbit.SetOrbit(0, 13); p.Teleport(new Vector3(-7.2f, 1.65f, -2.5f));
        Drive(Vector2.zero); await Wait(500);
        Vector3 start = p.transform.position; Drive(Vector2.up); await Wait(700); Drive(Vector2.zero); await Wait(250);
        float walked = Vector3.Distance(start, p.transform.position);
        Check("walk", walked > 1.8f && walked < 4.6f, walked);
        p.Teleport(new Vector3(-7.2f, 1.65f, -2.5f)); await Wait(250); start = p.transform.position;
        Drive(Vector2.up, true); await Wait(700); Drive(Vector2.zero); await Wait(200);
        float ran = Vector3.Distance(start, p.transform.position); Check("sprint faster", ran > walked * 1.25f, ran);
        p.Teleport(new Vector3(-7.2f, 1.65f, -2.5f)); Drive(Vector2.zero); await Wait(300);
        int jumps = p.Jumps; float baseY = p.transform.position.y;
        Drive(Vector2.zero, false, true, true); await Wait(300); float apexSample = p.transform.position.y;
        Check("jump rises", p.Jumps == jumps + 1 && apexSample > baseY + .9f, apexSample - baseY);
        Drive(Vector2.zero); await Wait(900); Check("jump lands", p.Grounded && Mathf.Abs(p.transform.position.y - baseY) < .15f, p.transform.position.ToString());
        // Real world bench: deck top 2.56, approach from the south.
        p.Teleport(new Vector3(-7.25f, 1.65f, 26.7f)); Drive(Vector2.up); await Wait(200);
        int climbs = p.Traversals; Drive(Vector2.up, false, true, true); await Wait(80); Drive(Vector2.zero); await Wait(620);
        Check("bench vault", p.Traversals > climbs && p.transform.position.y > 2.4f, p.transform.position.ToString());
        // Real climbing ledge: approach from river side.
        orbit.SetOrbit(-90, 13); p.Teleport(new Vector3(-7.25f, 1.65f, 3.8f)); Drive(Vector2.up); await Wait(170);
        climbs = p.Traversals; Drive(Vector2.up, false, true, true); await Wait(80); Drive(Vector2.zero); await Wait(620);
        Check("first roof ledge", p.Traversals > climbs && p.transform.position.y > 2.5f, p.transform.position.ToString());
        // Stairs from the promenade to the second terrace.
        orbit.SetOrbit(-90, 13); p.Teleport(new Vector3(-6.6f, 1.65f, -7)); Drive(Vector2.up); await Wait(3000); Drive(Vector2.zero); await Wait(200);
        Check("terrace staircase", p.transform.position.y > 5.9f, p.transform.position.ToString());
        // Solid wall must stop horizontal motion.
        p.Teleport(new Vector3(-7.2f, 1.65f, 8)); Drive(Vector2.up, true); await Wait(900); Drive(Vector2.zero);
        Check("building collision", p.transform.position.x > -9.1f, p.transform.position.ToString());
        int respawns = p.Respawns; p.Teleport(new Vector3(0, -.5f, 0)); await Wait(200);
        Check("river recovery", p.Respawns == respawns + 1 && p.transform.position.y > 1.5f, p.transform.position.ToString());
        int systems = Object.FindObjectsByType<ParticleSystem>().Length;
        for (int i = 0; i < 10; i++) p.feedback.Land(.7f);
        await Wait(50);
        Check("pooled feedback", Object.FindObjectsByType<ParticleSystem>().Length == systems && p.feedback.dust.particleCount > 0, p.feedback.dust.particleCount);
        var third = Object.FindAnyObjectByType<CinemachineThirdPersonFollow>();
        Check("Cinemachine collision", third.AvoidObstacles.Enabled && third.AvoidObstacles.CollisionFilter.value == 1, third.CameraDistance);
        var renderers = Object.FindObjectsByType<Renderer>();
        var defaults = renderers.Where(r => r.sharedMaterials.Any(m => !m || m.name == "Default-Material")).Select(r => r.name).ToArray();
        Check("no default materials", defaults.Length == 0, string.Join(",", defaults));
        Drive(Vector2.zero); p.TestInput = false; orbit.SetOrbit(0, 13); p.Teleport(new Vector3(-7.2f, 1.65f, -2.5f));
        return results;
    }
}
