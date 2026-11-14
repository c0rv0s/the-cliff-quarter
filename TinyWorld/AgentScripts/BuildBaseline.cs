using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TinyWorld;
using Object = UnityEngine.Object;

public static class BuildBaseline
{
    const string ScenePath = "Assets/Scenes/RiverCityBaseline.unity";
    static Material stone, sand, clay, water, leaf, shade, gold;
    static Mesh arch, windowArch;
    static Transform city;

    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the open scene first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) throw new InvalidOperationException("Baseline already exists; do not overwrite it.");
        foreach (var folder in new[] { "Assets/Scenes", "Assets/Materials", "Assets/Geometry", "Assets/Settings" })
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", folder.Substring(7));

        QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "PC"), true);
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (!pipeline) throw new InvalidOperationException("An active URP asset is required.");
        pipeline.supportsHDR = true;
        pipeline.msaaSampleCount = 2;
        pipeline.renderScale = 1;
        pipeline.shadowDistance = 65;
        pipeline.shadowCascadeCount = 2;
        EditorUtility.SetDirty(pipeline);
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.macRetinaSupport = false;
        PlayerSettings.productName = "Tiny World";
        PlayerSettings.companyName = "Tiny World";
        QualitySettings.vSyncCount = 1;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        stone = Material("Limestone", Palette.Limestone, .18f);
        sand = Material("Sandstone", Palette.Sandstone, .12f);
        clay = Material("Terracotta", Palette.Terracotta, .15f);
        water = Material("River turquoise", Palette.Water, .72f, .18f);
        leaf = Material("Olive foliage", Palette.Olive, .08f);
        shade = Material("Deep blue shutters", Palette.Shadow, .22f);
        gold = Material("Aged brass", Palette.Brass, .42f, .35f);
        arch = ArchMesh("BridgeArch", 4.8f, .65f, 2.8f, 25);
        windowArch = ArchMesh("WindowArch", 1, .16f, .18f, 13);
        city = new GameObject("River Quarter — visual baseline").transform;

        Cube("River bed", new Vector3(0, -1.3f, 28), new Vector3(13, 1, 110), sand, city);
        Cube("River", new Vector3(0, -.22f, 28), new Vector3(10.8f, .12f, 110), water, city, false);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int tier = 0; tier < 3; tier++)
            {
                float top = 1.6f + tier * 4.4f;
                float x = side * (10.5f + tier * 10);
                Cube("Cliff terrace", new Vector3(x, top / 2 - 1, 31), new Vector3(10, top + 2, 88), sand, city);
                Cube("Limestone promenade", new Vector3(x, top - .18f, 31), new Vector3(10.1f, .36f, 88), stone, city);
                Cube("River edge coping", new Vector3(side * (5.7f + tier * 10), top + .18f, 31), new Vector3(.42f, .36f, 88), stone, city);
                for (int row = 0; row < 6; row++)
                {
                    float z = 5 + row * 13 + ((tier + side + 2) % 2) * 3;
                    float height = 4.2f + ((row + tier + (side + 1)) % 3) * 1.1f;
                    Building(new Vector3(side * (12.2f + tier * 10), top, z), height, side, (row + tier) % 3 == 0);
                    Tree(new Vector3(side * (7.5f + tier * 10), top, z + 4), 1 + tier * .08f);
                }
            }
            for (int i = 0; i < 13; i++)
            {
                float z = -5 + i * 7;
                var rock = Primitive(PrimitiveType.Sphere, "Weathered river rock", new Vector3(side * 5.5f, -.4f, z), new Vector3(1.8f, 2.2f + i % 3, 2.3f), sand, city);
                rock.transform.rotation = Quaternion.Euler(15, i * 31, 8);
            }
        }
        Bridge(17, 5.05f);
        Bridge(53, 5.05f);
        Pavilion(new Vector3(-18, 10.4f, 61));
        for (int i = 0; i < 11; i++)
            Cube("Water light", new Vector3(Mathf.Sin(i * 2.1f) * 3.4f, -.149f, i * 7 - 9), new Vector3(.05f, .008f, 1.5f + i % 3), water, city, false);

        var sky = Palette.Sky;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(Palette.Shadow, Palette.Water, .35f);
        RenderSettings.ambientEquatorColor = Palette.Limestone * .55f;
        RenderSettings.ambientGroundColor = Palette.Terracotta * .23f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = sky;
        RenderSettings.fogStartDistance = 48;
        RenderSettings.fogEndDistance = 125;
        var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = Color.Lerp(Palette.Limestone, Color.white, .55f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .8f;
        sun.transform.rotation = Quaternion.Euler(42, -38, 0);
        RenderSettings.sun = sun;

        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(9, 15, -20);
        camera.transform.LookAt(new Vector3(0, 6, 28));
        camera.fieldOfView = 57;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 170;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = sky;
        camera.allowHDR = true;
        camera.gameObject.AddComponent<AudioListener>();
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.volumeLayerMask = 1;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "River Quarter Daylight";
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(.3f);
        bloom.threshold.Override(1.1f);
        profile.Add<Vignette>(true).intensity.Override(.21f);
        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(8);
        color.contrast.Override(5);
        AssetDatabase.CreateAsset(profile, "Assets/Settings/RiverQuarterDaylight.asset");
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        var volume = new GameObject("Daylight grading — global").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save baseline scene.");
        AssetDatabase.SaveAssets();
        SceneView.lastActiveSceneView?.AlignViewToObject(camera.transform);
        return new { scene = ScenePath, renderers = Object.FindObjectsByType<Renderer>().Length, quality = QualitySettings.names[QualitySettings.GetQualityLevel()], pipeline = pipeline.name };
    }

    static Material Material(string name, Color color, float smoothness, float metallic = 0)
    {
        var material = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(material, "Assets/Materials/" + name + ".mat");
        return material;
    }

    static Mesh ArchMesh(string name, float radius, float width, float depth, int cuts)
    {
        var source = ShapeGenerator.GenerateArch(PivotLocation.Center, 180, radius, width, depth, cuts, true, true, true, true, true);
        source.ToMesh();
        source.Refresh();
        var mesh = Object.Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, "Assets/Geometry/" + name + ".asset");
        Object.DestroyImmediate(source.gameObject);
        return mesh;
    }

    static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool collision = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = true;
        return go;
    }

    static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool collision = true)
        => Primitive(PrimitiveType.Cube, name, position, scale, material, parent, collision);

    static GameObject Arch(string name, Mesh mesh, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.isStatic = true;
        return go;
    }

    static void Building(Vector3 position, float height, int side, bool dome)
    {
        var root = new GameObject(dome ? "Domed residence" : "Terrace residence").transform;
        root.SetParent(city, false);
        root.localPosition = position;
        root.localRotation = Quaternion.Euler(0, side * 90, 0);
        Cube("Plaster walls", new Vector3(0, height / 2, 0), new Vector3(7.5f, height, 5.8f), stone, root);
        Cube("Foundation band", new Vector3(0, .18f, 0), new Vector3(7.9f, .36f, 6.2f), sand, root);
        Cube("Roof cornice", new Vector3(0, height, 0), new Vector3(8, .32f, 6.3f), stone, root);
        Cube("Roof inset", new Vector3(0, height + .25f, 0), new Vector3(7.4f, .22f, 5.7f), dome ? stone : clay, root);
        if (dome)
            Primitive(PrimitiveType.Sphere, "Low terracotta dome", new Vector3(0, height + .22f, 0), new Vector3(5, 2.9f, 5), clay, root);
        Opening(root, new Vector3(0, 0, -2.97f), 1.25f, 2.1f);
        Opening(root, new Vector3(-2.4f, 1.4f, -2.97f), .65f, 1.15f);
        Opening(root, new Vector3(2.4f, 1.4f, -2.97f), .65f, 1.15f);
        if (height > 5)
            Opening(root, new Vector3(0, 3.8f, -2.97f), .75f, 1.25f);
        Cube("Balcony", new Vector3(0, 3.4f, -3.35f), new Vector3(4.3f, .22f, 1), stone, root);
        Cube("Balcony edge", new Vector3(0, 3.8f, -3.83f), new Vector3(4.3f, .55f, .12f), sand, root);
    }

    static void Opening(Transform parent, Vector3 foot, float width, float height)
    {
        Cube("Blue recess", foot + new Vector3(0, height / 2, -.02f), new Vector3(width * 1.65f, height, .1f), shade, parent, false);
        Arch("Carved arch", windowArch, foot + new Vector3(0, height + width * .5f, -.09f), new Vector3(width, width, 1), sand, parent);
        for (int side = -1; side <= 1; side += 2)
            Cube("Door reveal", foot + new Vector3(side * width * .92f, height / 2, -.08f), new Vector3(width * .16f, height, .18f), sand, parent);
    }

    static void Tree(Vector3 foot, float size)
    {
        var root = new GameObject("Olive garden").transform;
        root.SetParent(city, false);
        root.localPosition = foot;
        root.localScale = Vector3.one * size;
        Primitive(PrimitiveType.Cylinder, "Terracotta planter", new Vector3(0, .35f, 0), new Vector3(1.5f, .35f, 1.5f), clay, root);
        Primitive(PrimitiveType.Cylinder, "Trunk", new Vector3(0, 1.5f, 0), new Vector3(.24f, 1.2f, .24f), sand, root);
        var canopy = Primitive(PrimitiveType.Sphere, "Olive crown", new Vector3(0, 3.1f, 0), new Vector3(2.6f, 2.3f, 2.4f), leaf, root);
        canopy.transform.localRotation = Quaternion.Euler(9, foot.z * 7, -12);
        Primitive(PrimitiveType.Sphere, "Olive crown branch", new Vector3(.75f, 2.8f, -.1f), new Vector3(1.8f, 1.6f, 1.8f), leaf, root);
    }

    static void Bridge(float z, float top)
    {
        var root = new GameObject("River arch bridge").transform;
        root.SetParent(city, false);
        root.localPosition = new Vector3(0, 0, z);
        Arch("Limestone vault", arch, new Vector3(0, top - 2.6f, 0), Vector3.one, stone, root);
        Cube("Bridge deck", new Vector3(0, top - .15f, 0), new Vector3(11.3f, .3f, 3.4f), stone, root);
        for (int edge = -1; edge <= 1; edge += 2)
        {
            Cube("Parapet", new Vector3(0, top + .47f, edge * 1.6f), new Vector3(11.4f, .85f, .24f), sand, root);
            Cube("Parapet cap", new Vector3(0, top + .93f, edge * 1.6f), new Vector3(11.6f, .12f, .38f), stone, root);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Cube("Bridge pier", new Vector3(side * 5.1f, 1.6f, 0), new Vector3(1.5f, 5, 3), sand, root);
            for (int step = 0; step < 12; step++)
            {
                float height = top - 1.6f - step * .28f;
                Cube("Approach stair", new Vector3(side * (5.85f + step * .35f), 1.6f + height / 2, 0), new Vector3(.36f, height, 3.2f), stone, root);
            }
        }
    }

    static void Pavilion(Vector3 foot)
    {
        var root = new GameObject("Hilltop belvedere").transform;
        root.SetParent(city, false);
        root.localPosition = foot;
        Primitive(PrimitiveType.Cylinder, "Circular plinth", Vector3.zero, new Vector3(9, .35f, 9), stone, root);
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI / 5;
            Primitive(PrimitiveType.Cylinder, "Column", new Vector3(Mathf.Cos(angle) * 3.5f, 3, Mathf.Sin(angle) * 3.5f), new Vector3(.45f, 2.8f, .45f), stone, root);
        }
        Primitive(PrimitiveType.Cylinder, "Entablature", new Vector3(0, 5.8f, 0), new Vector3(8.9f, .28f, 8.9f), stone, root);
        Primitive(PrimitiveType.Sphere, "Copper dome", new Vector3(0, 5.85f, 0), new Vector3(8.2f, 3.6f, 8.2f), clay, root);
        Primitive(PrimitiveType.Sphere, "Brass finial", new Vector3(0, 7.9f, 0), Vector3.one * .4f, gold, root);
    }
}
