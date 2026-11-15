using System.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;
using TinyWorld;
using Object = UnityEngine.Object;
public static class PoseCheck
{
    // mode: idle | walk | sprint | jump | crouch | fall ; camera on the character's side at `distance`.
    public static async Task<object> Stage(string mode, float delayMs = 900, float camYaw = 90, float distance = 3.2f)
    {
        var p = Object.FindAnyObjectByType<WandererMotor>(); var shell = Object.FindAnyObjectByType<WandererShell>();
        Time.timeScale = 1; if (!shell.Playing) shell.Begin();
        var canvas = shell.GetComponentInChildren<Canvas>(); canvas.enabled = false;
        var start = GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        p.TestInput = true; p.TestSprint = p.TestCrouch = p.TestJumpHeld = false; p.TestMove = Vector2.zero;
        p.Teleport(new Vector3(start.x - 1, start.y + .15f, 22f));
        var orbit = Object.FindAnyObjectByType<WandererCamera>(); orbit.SetOrbit(0, 8);
        await Task.Delay(300);
        if (mode == "walk" || mode == "sprint" || mode == "jump" || mode == "fall") p.TestMove = Vector2.up;
        p.TestSprint = mode == "sprint" || mode == "jump";
        p.TestCrouch = mode == "crouch";
        if (mode == "jump") { await Task.Delay(500); p.TestJumpPressed = true; p.TestJumpHeld = true; }
        if (mode == "fall") { p.Teleport(p.transform.position + Vector3.up * 6); }
        if (mode == "swim") { float z = 22, c = Mathf.Sin(z * .017f) * 20 + Mathf.Sin(z * .039f + .4f) * 7; p.Teleport(new Vector3(c, -1.2f, z)); p.TestMove = Vector2.up; }
        var brain = Camera.main.GetComponent<CinemachineBrain>(); brain.enabled = false;
        float t = 0;
        while (t < delayMs)
        {
            await Task.Yield();
            t += Time.unscaledDeltaTime * 1000;
            var vis = p.visual; var target = p.transform.position + Vector3.up * .95f;
            var dir = Quaternion.Euler(0, vis.eulerAngles.y + camYaw, 0) * Vector3.forward;
            Camera.main.transform.position = target + dir * distance + Vector3.up * .35f;
            Camera.main.transform.LookAt(target);
            Camera.main.fieldOfView = 40;
        }
        Time.timeScale = 0;
        return new { mode, p.Action, p.Grounded, speed = p.Speed.ToString("F1"), vy = p.VerticalSpeed.ToString("F1") };
    }
    public static object Release()
    {
        Time.timeScale = 1;
        var p = Object.FindAnyObjectByType<WandererMotor>(); p.TestMove = Vector2.zero; p.TestSprint = p.TestCrouch = p.TestJumpHeld = false;
        Camera.main.GetComponent<CinemachineBrain>().enabled = true; Object.FindAnyObjectByType<WandererShell>().GetComponentInChildren<Canvas>().enabled = true; return "ok";
    }
}
