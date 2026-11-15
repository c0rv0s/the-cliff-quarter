using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using TinyWorld;
// Import settings + toon material remap for Assets/Models/Wanderer.fbx.
public static class ImportWanderer
{
    const string Fbx = "Assets/Models/Wanderer.fbx", MatDir = "Assets/Materials/Wanderer";
    static readonly Dictionary<string, (string hex, float rim, float gloss)> Roles = new()
    {
        ["Tunic"] = ("F3E8D2", .3f, 0), ["Cape"] = ("2C4C7C", .45f, 0), ["Trim"] = ("DAAE57", .5f, .8f), ["Sash"] = ("B66547", .3f, 0),
        ["Trousers"] = ("5E5648", .25f, 0), ["Leather"] = ("6B4A35", .3f, .25f), ["Skin"] = ("E2B48C", .35f, 0), ["Eyes"] = ("26262F", 0, .9f), ["Glove"] = ("8A6448", .3f, 0),
    };
    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Wanderer");
        AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = importer.importLights = false; importer.importBlendShapes = false; importer.isReadable = false;
        importer.importNormals = ModelImporterNormals.Import; importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        foreach (var role in Roles)
        {
            string path = $"{MatDir}/Wanderer {role.Key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("TinyWorld/Toon")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("TinyWorld/Toon");
            mat.SetColor("_BaseColor", Palette.Hex(role.Value.hex));
            mat.SetColor("_ShadowTint", Color.Lerp(Palette.Shadow, Palette.Sky, .5f));
            mat.SetFloat("_Variation", 0); mat.SetFloat("_Pattern", 0); mat.SetFloat("_RimStrength", role.Value.rim); mat.SetFloat("_Gloss", role.Value.gloss);
            mat.SetFloat("_RampSoft", .07f); mat.SetFloat("_Ambient", .6f); mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), role.Key), mat);
        }
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var sb = new StringBuilder();
        var root = model.transform;
        sb.Append($"root rot {root.localEulerAngles} scale {root.localScale}\n");
        foreach (var t in root.GetComponentsInChildren<Transform>())
        {
            if (t.name.StartsWith("m_") && t.name != "m_EyeL" && t.name != "m_Boot.L" && t.name != "m_Satchel") continue;
            sb.Append($"{t.name} parent={t.parent?.name} world={t.position:F3} rot={t.rotation.eulerAngles:F0} ls={t.lossyScale:F2}\n");
        }
        var mats = root.GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial ? r.sharedMaterial.name : "null").Distinct();
        sb.Append("materials: " + string.Join(", ", mats) + "\n");
        var b = new Bounds(); foreach (var r in root.GetComponentsInChildren<MeshFilter>()) { var w = r.sharedMesh.bounds; b.Encapsulate(root.InverseTransformPoint(r.transform.TransformPoint(w.min))); b.Encapsulate(root.InverseTransformPoint(r.transform.TransformPoint(w.max))); }
        sb.Append("bounds " + b);
        return sb.ToString();
    }
}
