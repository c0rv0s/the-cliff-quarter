using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Edit-mode helper: places a temporary DontSave camera named AgentCam for `capture game_view --camera AgentCam`.
public static class QuarterViews
{
    public static object Open(string path)
    {
        var s = EditorSceneManager.GetActiveScene();
        if (s.path != path) EditorSceneManager.OpenScene(path);
        s = EditorSceneManager.GetActiveScene();
        return new { s.path, roots = s.GetRootGameObjects().Select(r => r.name + " (" + r.transform.childCount + ")").ToArray() };
    }

    public static object Place(float x, float y, float z, float tx, float ty, float tz, float fov)
    {
        var go = GameObject.Find("AgentCam");
        if (!go)
        {
            go = new GameObject("AgentCam") { hideFlags = HideFlags.DontSave };
            var src = Camera.main;
            var cam = go.AddComponent<Camera>();
            if (src) { cam.CopyFrom(src); }
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var srcData = src ? src.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            if (srcData) { data.renderPostProcessing = srcData.renderPostProcessing; data.antialiasing = srcData.antialiasing; data.renderShadows = true; }
        }
        var c = go.GetComponent<Camera>();
        c.farClipPlane = 2000; c.fieldOfView = fov; c.enabled = true;
        go.transform.position = new Vector3(x, y, z);
        go.transform.LookAt(new Vector3(tx, ty, tz));
        return go.transform.position.ToString();
    }

    public static object Clear()
    {
        var go = GameObject.Find("AgentCam");
        if (go) Object.DestroyImmediate(go);
        return "cleared";
    }

    public static object Stats()
    {
        var city = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(r => r.name == "Cliff Quarter");
        var filters = city.GetComponentsInChildren<MeshFilter>();
        long tris = 0; foreach (var f in filters) if (f.sharedMesh) tris += f.sharedMesh.triangles.Length / 3;
        return new { renderers = city.GetComponentsInChildren<Renderer>().Length, colliders = city.GetComponentsInChildren<Collider>().Length, triangles = tris };
    }

    // Camera at a scene object's position plus an offset, looking along a direction.
    public static object PlaceAt(string path, float ox, float oy, float oz, float dx, float dy, float dz, float fov)
    {
        var city = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(r => r.name == "Cliff Quarter").transform;
        var t = path.StartsWith("/") ? GameObject.Find(path.Substring(1)).transform : city.Find(path);
        var p = t.position + new Vector3(ox, oy, oz);
        Place(p.x, p.y, p.z, p.x + dx, p.y + dy, p.z + dz, fov);
        return p.ToString();
    }
}
