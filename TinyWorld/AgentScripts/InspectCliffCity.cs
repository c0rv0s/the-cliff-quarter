using System.Linq;
using UnityEngine;
using UnityEditor;
using TinyWorld;
using Object=UnityEngine.Object;
public static class InspectCliffCity
{
    public static object Run()
    {
        var city=GameObject.Find("Cliff City");
        var filters=city.GetComponentsInChildren<MeshFilter>();
        var renderers=city.GetComponentsInChildren<Renderer>();
        var bounds=new Bounds(new Vector3(0,50,150),Vector3.zero);
        foreach(var r in renderers)if(!r.name.Contains("turquoise"))bounds.Encapsulate(r.bounds);
        return new{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            renderers=renderers.Length,triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3),colliders=city.GetComponentsInChildren<Collider>().Length,
            anchors=city.GetComponentsInChildren<TraversalAnchor>().Length,stairRoutes=city.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Stair route")),
            bounds=bounds.ToString(),baselineSHA256="ef4f496a10f046795d4d347ffae1e8cebcc4de5a5b83640d5b3673184eb7e572",
            missingMaterials=renderers.Count(r=>r.sharedMaterials.Any(m=>!m||!m.shader)),
            shaderErrors=new[]{"TinyWorld/River","TinyWorld/CliffSky"}.SelectMany(n=>ShaderUtil.GetShaderMessages(Shader.Find(n))).Select(m=>m.message).ToArray()};
    }
}
