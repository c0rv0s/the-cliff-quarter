using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TinyWorld;
using Object = UnityEngine.Object;

public static class FixPlayable
{
    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first");
        var stairs = Object.FindObjectsByType<Transform>().Where(t => t.name == "Terrace stair" || t.name == "Stair edge inlay").ToArray();
        foreach (var t in stairs)
        {
            float sign = Mathf.Sign(t.localPosition.x), a = Mathf.Abs(t.localPosition.x);
            int tier = a > 17 ? 1 : 0;
            // Infer the tread index from its height, not its current x (safe to re-run).
            float height = t.name == "Terrace stair" ? t.localScale.y : t.localPosition.y - (1.6f + tier * 4.4f) - .008f;
            int index = Mathf.RoundToInt(height / .2f) - 1;
            var p = t.localPosition; p.x = sign * (7.15f + tier * 10 + index * .38f); t.localPosition = p;
        }
        foreach (var t in Object.FindObjectsByType<Transform>().Where(t => t.name == "Cliff terrace"))
        {
            var scale = t.localScale; float top = t.localPosition.y + scale.y / 2;
            float desired = 1.6f + Mathf.Round((top - 1.6f) / 4.4f) * 4.4f - .36f;
            float delta = desired - top;
            var pos = t.localPosition; pos.y += delta / 2; scale.y += delta;
            t.localPosition = pos; t.localScale = scale;
        }
        var ledges = Object.FindObjectsByType<Transform>().Where(t => t.name.StartsWith("Rooftop climbing ledge ")).OrderBy(t => t.name).ToArray();
        var lips = Object.FindObjectsByType<Transform>().Where(t => t.name == "Brass climb lip").OrderBy(t => t.localPosition.y).ToArray();
        var supports = Object.FindObjectsByType<Transform>().Where(t => t.name == "Ledge support").OrderBy(t => t.localPosition.y).ToArray();
        for (int i = 1; i < ledges.Length; i++)
        {
            float x = i == 1 ? -10.4f : -12;
            var pos = ledges[i].localPosition; pos.x = x; pos.z = 3.1f; ledges[i].localPosition = pos;
            pos = lips[i].localPosition; pos.x = x + .82f; pos.z = 3.1f; lips[i].localPosition = pos;
            pos = supports[i].localPosition; pos.x = x - .37f; pos.z = 3.1f; supports[i].localPosition = pos;
        }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Geometry/WandererCloak.asset");
        var front = new[] { new Vector3(-.34f, 0, 0), new Vector3(.34f, 0, 0), new Vector3(-.48f, -.85f, -.12f), new Vector3(.48f, -.85f, -.12f), new Vector3(0, -.94f, -.23f), new Vector3(0, -.12f, -.24f) };
        var verts = front.Concat(front.Select(v => v + Vector3.back * .035f)).ToArray();
        int[] face = { 0, 5, 1, 0, 2, 5, 2, 4, 5, 4, 3, 5, 3, 1, 5 };
        var triangles = face.Concat(face.Reverse().Select(i => i + 6)).Concat(new[] { 0, 1, 6, 1, 7, 6, 0, 6, 2, 2, 6, 8, 2, 8, 4, 4, 8, 10, 4, 10, 3, 3, 10, 9, 3, 9, 1, 1, 9, 7 }).ToArray();
        mesh.Clear(); mesh.vertices = verts; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        // Add a hood rim, shoulder mantle and sewn border for a readable traveller silhouette.
        var head = Object.FindAnyObjectByType<WandererPose>().head;
        if (!head.Find("Hood rim"))
        {
            var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere); hood.name = "Hood rim"; hood.layer = 2; hood.transform.SetParent(head, false); hood.transform.localPosition = new Vector3(0, .045f, .11f); hood.transform.localScale = new Vector3(.5f, .5f, .36f); Object.DestroyImmediate(hood.GetComponent<Collider>()); hood.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Limestone.mat");
            // Keep the face opening visible at the front.
            var opening = head.Find("Shadow within hood"); opening.localPosition = new Vector3(0, -.025f, .25f);
            var scarf = head.Find("Face scarf"); scarf.localPosition = new Vector3(0, -.12f, .3f);
        }
        foreach (var dome in Object.FindObjectsByType<MeshFilter>().Where(f => f.name == "Low terracotta dome"))
            if (!dome.GetComponent<Collider>()) dome.gameObject.AddComponent<MeshCollider>().sharedMesh = dome.sharedMesh;
        var sun = RenderSettings.sun; sun.shadowStrength = .7f;
        RenderSettings.ambientEquatorColor = Palette.Limestone * .68f;
        RenderSettings.ambientSkyColor = Color.Lerp(Palette.Shadow, Palette.Sky, .5f);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return new { stairs = stairs.Length, cloakVertices = mesh.vertexCount, domes = Object.FindObjectsByType<MeshCollider>().Length };
    }
}
