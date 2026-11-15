using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TinyWorld;
using Object = UnityEngine.Object;

// Play mode: spawn settles on the ground, then the wanderer climbs every stair route in the scene.
public static class VerifyDenseRoutes
{
    static WandererMotor p; static WandererCamera cam; static WandererShell shell;
    static async Task<bool> Walk(Vector3 target)
    {
        float until = Time.realtimeSinceStartup + 30;
        while (Time.realtimeSinceStartup < until)
        {
            if (shell.Paused) shell.SetPaused(false);
            var delta = target - p.transform.position;
            if (new Vector2(delta.x, delta.z).magnitude < 1.05f && Mathf.Abs(delta.y) < .7f) { p.TestMove = Vector2.zero; return true; }
            cam.SetOrbit(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 13); p.TestMove = Vector2.up; p.TestSprint = false;
            await Task.Delay(35);
        }
        p.TestMove = Vector2.zero; return false;
    }
    public static async Task<object> Run(string only = "")
    {
        p = Object.FindAnyObjectByType<WandererMotor>(); cam = Object.FindAnyObjectByType<WandererCamera>(); shell = Object.FindAnyObjectByType<WandererShell>();
        shell.Begin(); var origin = p.transform.position; var results = new List<object>();
        p.TestInput = true; p.TestJumpHeld = p.TestJumpPressed = p.TestSwing = p.TestDrop = p.TestDash = false; p.TestMove = Vector2.zero;
        await Task.Delay(1500);
        var settled = p.transform.position;
        results.Add(new { test = "spawn settles on ground", passed = Mathf.Abs(settled.y - origin.y) < .6f && Physics.Raycast(settled + Vector3.up * .5f, Vector3.down, 1.2f), position = settled.ToString() });
        var routes = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Where(t => t.name.StartsWith("Stair route ") && t.name.Contains(only)).OrderBy(t => t.name).ToList();
        foreach (var route in routes)
        {
            Vector3 a = route.Find("A").position, turn = route.Find("Turn").position, out_ = route.Find("Out").position, b = route.Find("B").position;
            shell.Begin(); p.TestMove = Vector2.zero; p.Teleport(a + Vector3.up * .6f); await Task.Delay(300);
            bool first = await Walk(turn);
            if (!first) p.Teleport(turn + Vector3.up * .6f);
            bool second = await Walk(out_) && await Walk(b);
            results.Add(new { test = route.name, first, second, end = p.transform.position.ToString() });
            Debug.Log($"[DenseRoutes] {route.name} first={first} second={second}");
        }
        // Grand stairs: street → stair viaduct → bridge onto the uplands.
        foreach (var route in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Where(t => t.name.StartsWith("Grand stair") && t.name.Contains(only)))
        {
            Vector3 a = route.Find("A").position, b = route.Find("B").position, c = route.Find("C").position;
            shell.Begin(); p.TestMove = Vector2.zero; p.Teleport(a + Vector3.up * .6f); await Task.Delay(300);
            bool up = await Walk(b); var stop = p.transform.position; if (!up) p.Teleport(b + Vector3.up * .6f);
            bool across = await Walk(c);
            results.Add(new { test = route.name, first = up, second = across, stop = stop.ToString(), target = b.ToString(), end = p.transform.position.ToString() });
        }
        p.TestMove = Vector2.zero; p.TestInput = false; p.Teleport(origin);
        return new { passed = results.Count(r => r.ToString().Contains("False")) == 0, results };
    }
}
