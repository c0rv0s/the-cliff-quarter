using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object = UnityEngine.Object;

// Play mode, CliffQuarter: free-running flow (speed vault, roll, carried air speed, facade climb, zip).
public static class VerifyFlow
{
    static WandererMotor p; static WandererCamera orbit; static List<object> results;
    static readonly FieldInfo velocity = typeof(WandererMotor).GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic);
    static void Drive(Vector2 move, bool sprint = false, bool jump = false, bool held = false)
    { p.TestInput = true; p.TestMove = move; p.TestSprint = sprint; p.TestJumpPressed = jump; p.TestJumpHeld = held; }
    static void Place(Vector3 at, float yaw) { Drive(Vector2.zero); p.Teleport(at); orbit.SetOrbit(yaw, 10); }
    static void Check(string test, bool passed, object evidence) => results.Add(new { test, passed, evidence });
    static Task Wait(int ms) => Task.Delay(ms);

    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new Exception("Start Play mode first.");
        p = Object.FindAnyObjectByType<WandererMotor>(); orbit = Object.FindAnyObjectByType<WandererCamera>(); results = new();
        Object.FindAnyObjectByType<WandererShell>().Begin();
        var court = GameObject.Find("Route · Saffron wall and swing alley").transform.position;

        // Sprint at the alley bench without pressing jump: clean vault, still running after.
        Place(new Vector3(court.x - 1, court.y + .12f, 19.5f), 0); await Wait(300);
        int traversals = p.Traversals; var actions = new HashSet<string>(); float after = 0;
        Drive(Vector2.up, true);
        for (int i = 0; i < 40; i++) { await Wait(30); actions.Add(p.Action); if (p.Traversals > traversals && !p.Traversing) after = Mathf.Max(after, p.Speed); }
        Check("sprint auto-vaults bench", p.Traversals > traversals && actions.Contains("Speed vault"), new { actions = string.Join(",", actions), position = p.transform.position.ToString() });
        Check("vault keeps running pace", after > p.walkSpeed, after);

        // Hard landing while moving becomes a roll that keeps speed.
        var street = new Vector3(court.x - 1, court.y + 9, 12); Place(street, 0); await Wait(50);
        velocity.SetValue(p, new Vector3(0, -6, 7)); Drive(Vector2.up, true); actions.Clear(); float rollSpeed = 0;
        for (int i = 0; i < 50; i++) { await Wait(25); actions.Add(p.Action); if (p.Action == "Roll") rollSpeed = Mathf.Max(rollSpeed, p.Speed); }
        Check("hard landing rolls", actions.Contains("Roll") && rollSpeed > 3.5f, new { rollSpeed, actions = string.Join(",", actions) });

        // Speed carried into the air is steered, not braked back to run speed.
        Place(new Vector3(court.x - 1, court.y + 30, 20), 0); await Wait(50);
        velocity.SetValue(p, new Vector3(0, 6, 16)); Drive(Vector2.up); await Wait(450);
        Check("air keeps carried speed", p.Speed > 13, p.Speed);
        Drive(Vector2.zero); await Wait(50);

        // Wall run up a facade, catch, and keep climbing with W held.
        var startFoot = new Vector3(court.x - 4, court.y + .2f, 70);
        Vector3 facade = Vector3.zero, normal = Vector3.zero; bool found = false;
        for (float z = 70; z < 110 && !found; z += 3)
        {
            // A facade still standing 15 m up: a tall house with several storeys of handholds.
            var from = new Vector3(court.x - 2, court.y + 5, z);
            if (Physics.Raycast(from, Vector3.left, out var hit, 14, 1, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.normal.y) < .2f
                && Physics.Raycast(from + Vector3.up * 10, Vector3.left, out var high, 14, 1, QueryTriggerInteraction.Ignore) && Mathf.Abs(high.point.x - hit.point.x) < .5f)
            {
                var foot = new Vector3(hit.point.x + hit.normal.x * .7f, court.y + .2f, z);
                if (Physics.Raycast(foot + Vector3.up, Vector3.down, out var floor, 3, 1, QueryTriggerInteraction.Ignore)) { startFoot = floor.point + Vector3.up * .05f; facade = hit.point; normal = hit.normal; found = true; }
            }
        }
        Place(startFoot, Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg); await Wait(300);
        float startY = p.transform.position.y, best = startY; int grabs = p.Grabs; traversals = p.Traversals; actions.Clear();
        Drive(Vector2.up, true, true, true);
        for (int i = 0; i < 160; i++) { await Wait(30); if (i == 2) Drive(Vector2.up, true, false, true); best = Mathf.Max(best, p.transform.position.y); actions.Add(p.Action); }
        Check("facade climb chain", best - startY > 6 && p.Grabs > grabs, new { gained = best - startY, grabs = p.Grabs - grabs, actions = string.Join(",", actions), facade = facade.ToString() });
        Drive(Vector2.zero); await Wait(50);

        // Sticky hand zip: fire up the facade, press Space, end on a ledge or above it.
        Place(startFoot, Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg); await Wait(300);
        startY = p.transform.position.y;
        p.TestInput = true; p.TestFireHeld = true; p.Hand.FireAt(facade + Vector3.up * 7); for (int i = 0; i < 20 && !p.Grappling; i++) await Wait(20);
        bool attached = p.Grappling; Drive(Vector2.up, false, true, true); actions.Clear();
        for (int i = 0; i < 80; i++) { await Wait(25); actions.Add(p.Action); if (i == 1) Drive(Vector2.up, false, false, true); }
        p.TestFireHeld = false;
        Check("zip reaches the hand", attached && actions.Contains("Zip") && p.transform.position.y - startY > 4, new { attached, gained = p.transform.position.y - startY, actions = string.Join(",", actions) });

        // Ship rope ladder: walk into it, climb, pull over onto the crow's nest.
        var ladder = Object.FindObjectsByType<ClimbableVine>(FindObjectsInactive.Exclude).Where(v => v.name == "Rope ladder" && v.length > 8).OrderBy(v => v.Top.z).FirstOrDefault();
        if (ladder)
        {
            var start = ladder.Top + ladder.Outward * 1.6f; start.y = ladder.Bottom.y + .3f;
            Place(start, Mathf.Atan2(-ladder.Outward.x, -ladder.Outward.z) * Mathf.Rad2Deg); await Wait(300);
            float y0 = p.transform.position.y; bool climbed = false; Drive(Vector2.up);
            for (int i = 0; i < 200 && !(p.Grounded && p.transform.position.y > ladder.Top.y - .5f); i++) { await Wait(30); climbed |= p.VineClimbing; }
            Check("rope ladder to crow's nest", climbed && p.transform.position.y > ladder.Top.y - .5f, new { from = y0, to = p.transform.position.y, top = ladder.Top.y, p.Action });
            Drive(Vector2.zero);
        }
        // Livestock: herds spawned, animals on the ground, chickens scatter from the player.
        var herds = Object.FindObjectsByType<Livestock>(FindObjectsInactive.Exclude);
        int animals = 0, floating = 0;
        foreach (var herd in herds) foreach (Transform a in herd.transform)
            {
                animals++;
                if (!Physics.Raycast(a.position + Vector3.up * 2, Vector3.down, out var hit, 6, 1, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - a.position.y) > .35f) floating++;
            }
        Check("livestock grounded", herds.Length >= 3 && animals >= 20 && floating == 0, new { herds = herds.Length, animals, floating });
        var flock = herds.FirstOrDefault(h => h.skittish);
        if (flock && flock.transform.childCount > 0)
        {
            var hen = flock.transform.GetChild(0); var before = hen.position;
            p.Teleport(before + Vector3.right * 1.2f); await Wait(1500);
            Check("chickens scatter", Vector3.Distance(hen.position, p.transform.position) > 2.5f, new { start = before.ToString(), now = hen.position.ToString() });
        }
        Drive(Vector2.zero); p.TestInput = false;
        int passed = 0; foreach (var r in results) if (r.ToString().Contains("passed = True")) passed++;
        return new { passed, total = results.Count, results };
    }
}
