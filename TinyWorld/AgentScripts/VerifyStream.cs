using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;
using TinyWorld;
using Object = UnityEngine.Object;

// Farm stream swimming and the terraced cascade. Probe() in Edit mode; Swim() and View() in Play mode.
public static class VerifyStream
{
    static Transform[] Volumes => GameObject.Find("Stream water volumes").GetComponentsInChildren<RiverWaterVolume>().Select(v => v.transform).ToArray();
    public static object Probe()
    {
        var vols = Volumes; int solidWater = 0, shallow = 0; float minDepth = 99, maxDepth = 0;
        foreach (var v in vols)
        {
            var top = v.position + Vector3.up * 5;
            if (!Physics.Raycast(top, Vector3.down, out var hit, 20, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.collider.name.Contains("river") || hit.collider.name.Contains("Shallow")) solidWater++;
            float depth = v.position.y - hit.point.y; minDepth = Mathf.Min(minDepth, depth); maxDepth = Mathf.Max(maxDepth, depth); if (depth < 1.2f) shallow++;
        }
        var cascade = GameObject.Find("Stream cascade");
        return new { volumes = vols.Length, solidWater, shallow, minDepth, maxDepth, falls = cascade ? cascade.GetComponentsInChildren<MeshRenderer>().Count(r => r.name.Contains("fall")) : 0,
            waterColliders = Object.FindObjectsByType<MeshCollider>().Count(c => c.GetComponent<MeshRenderer>() && c.GetComponent<MeshRenderer>().sharedMaterial && c.GetComponent<MeshRenderer>().sharedMaterial.shader.name.Contains("Water")) };
    }
    public static async Task<object> Swim()
    {
        var p = Object.FindAnyObjectByType<WandererMotor>(); Object.FindAnyObjectByType<WandererShell>().Begin();
        p.TestInput = true; p.TestMove = Vector2.zero; p.ControlsEnabled = true;
        var v = Volumes[30]; var surface = v.position.y;
        p.Teleport(v.position + Vector3.up * 2); await Task.Delay(1800);
        var settled = new { p.Swimming, p.Action, depth = surface - p.transform.position.y };
        var start = p.transform.position; p.TestMove = Vector2.up; await Task.Delay(1500); p.TestMove = Vector2.zero;
        var moved = Vector3.Distance(new Vector3(start.x, 0, start.z), new Vector3(p.transform.position.x, 0, p.transform.position.z));
        Debug.Log($"[VerifyStream] settled swimming={settled.Swimming} action={settled.Action} depth={settled.depth:F2} moved={moved:F1} still={p.Swimming}");
        return new { settled, moved, swimmingAfterMove = p.Swimming, action = p.Action };
    }
    public static object View(string view)
    {
        var cam = Camera.main; cam.GetComponent<CinemachineBrain>().enabled = false; cam.fieldOfView = 60;
        var canvas = Object.FindAnyObjectByType<WandererShell>().GetComponentInChildren<Canvas>(); if (canvas) canvas.enabled = false;
        if (view == "river") { cam.transform.position = new Vector3(-24, 14, 286); cam.transform.LookAt(new Vector3(40, 46, 338)); }
        else if (view == "crest") { cam.transform.position = new Vector3(128, 113, 296); cam.transform.LookAt(new Vector3(82, 96, 338)); }
        else if (view == "quay") { cam.transform.position = new Vector3(-6, 6, 318); cam.transform.LookAt(new Vector3(24, 14, 338)); }
        return cam.transform.position.ToString();
    }
}
