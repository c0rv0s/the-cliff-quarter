using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using TinyWorld;
using Object = UnityEngine.Object;

// Builds Assets/Scenes/CliffQuarter.unity from the cliff-city layout (BuildCliffCity) with the
// stylized art pass: smooth sculpted shapes, vertex-color variety, toon/water/sky shaders,
// vegetation, collectibles and ambient life. Source scenes are not modified.
public static class BuildQuarter
{
    const string SourceScene = "Assets/Scenes/RiverCityCliffside.unity";
    const string ScenePath = "Assets/Scenes/CliffQuarter.unity";
    const string GeoPath = "Assets/Geometry/Quarter";
    const string MatPath = "Assets/Materials/Quarter";
    static Material plaster, trim, paving, roof, clay, recess, shutter, brass, leaf, cypressMat, blossom, wood, rock, bedMat, water, cloth, glow, sunMat, beamMat, fishMat, birdMat;
    static Transform city, group;
    static Matrix4x4 frame = Matrix4x4.identity;
    static Dictionary<string, Batch> batches;
    static System.Random rng, deco;
    static int meshId, buildings, anchors, triangles, ungrounded;
    static List<(Vector3 top, Vector3 outward, float length)> vines;
    static List<(Vector3 p, string place, bool fixedSpot)> spots;
    static Color tint = Color.white, shutterTint;
    static float wind, aoBase = float.NaN, aoSpan = 3;
    static bool smooth, ghost;
    static readonly float[] heights = { 4, 27, 55, 84 };
    // Each tier: a street of streetW, then a row of houses backed by the next tier's retaining wall.
    static readonly float[] streetW = { 11, 7, 7, 7 };
    static List<(Vector3 a, Vector3 b, float r)> keepOut;
    static readonly float[] stairZ = { -22f, 118f, 210f, 296f };
    static readonly float[] waterStairZ = { 0f, 48f, 125f, 201f, 285f, 332f };
    static readonly string[] districts = { "River Gates", "Saffron Ascent", "Olive Ward", "Crown Gardens" };
    static readonly Color[] plasters = { P("F2E3C6"), P("F5EAD6"), P("EBD3AA"), P("EFD2B4"), P("F7F0E2"), P("E6C998"), P("F0DFBF") };
    static readonly Color[] shutters = { Palette.Shutter, P("2E7C7A"), P("6C8A60"), P("98503A"), P("3E6E9C") };
    static Color P(string hex) => Palette.Hex(hex);
    static float R(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
    static float D(float a, float b) => Mathf.Lerp(a, b, (float)deco.NextDouble());
    static bool Chance(float p) => deco.NextDouble() < p;
    static float Center(float z) => Mathf.Sin(z * .017f) * 20 + Mathf.Sin(z * .039f + .4f) * 7;
    static float Width(float z) => 17 + Mathf.Sin(z * .023f + 1) * 5 + Mathf.Sin(z * .051f) * 2;
    static float RowDepth(int side, int tier, float z) => 8.5f + Mathf.Sin(z * .041f + tier * 2.3f + side) * 1.5f;
    static float Offset(int side, int tier, float z) { float o = 0; for (int t = 0; t < tier; t++) o += streetW[t] + RowDepth(side, t, z); return o; }
    // Street front edge; tier 4 is the back of the top row, where the escarpment starts.
    static float Edge(int side, int tier, float z) => Center(z) + side * (Width(z) + Offset(side, tier, z) + Mathf.Sin(z * .025f + side) * 6 + Mathf.Sin(z * .067f + side) * 2);
    static float Level(int side, int tier, float z) => heights[tier] + Mathf.Sin(z * .018f + tier * .8f + side * .7f) * (tier == 0 ? 1.15f : 2.5f + tier * 1.2f) + Mathf.Sin(z * .043f + side) * .6f;
    static Vector3 Point(int side, int tier, float z, float outwards = 3) => new(Edge(side, tier, z) + side * outwards, Level(side, tier, z), z);

    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Save the open scene before building.");
        var scene = EditorSceneManager.OpenScene(SourceScene);
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Cliff City" || root.name == "River water volume") Object.DestroyImmediate(root);
        if (AssetDatabase.IsValidFolder(GeoPath)) AssetDatabase.DeleteAsset(GeoPath);
        AssetDatabase.CreateFolder("Assets/Geometry", "Quarter");
        if (!AssetDatabase.IsValidFolder(MatPath)) AssetDatabase.CreateFolder("Assets/Materials", "Quarter");
        rng = new System.Random(731921); deco = new System.Random(5150);
        meshId = buildings = anchors = triangles = ungrounded = 0; vines = new(); spots = new();
        Materials();
        city = new GameObject("Cliff Quarter").transform;
        InitPads();
        CollectKeepOuts();
        for (int side = -1; side <= 1; side += 2)
        {
            for (int tier = 0; tier < 4; tier++)
            {
                float end = 342 - tier * 14 + (side == 1 ? 12 : 0);
                StartGroup($"{(side < 0 ? "West" : "East")} {districts[tier]} outer gardens");
                GardenTerrace(side, tier, TerrainStart, -32); GardenTerrace(side, tier, end, TerrainEnd);
                FinishGroup();
                for (int segment = 0; segment < 6; segment++)
                {
                    float z0 = -32 + segment * 64, z1 = Mathf.Min(z0 + 64, end);
                    if (z0 >= end) break;
                    StartGroup($"{(side < 0 ? "West" : "East")} {districts[tier]} {segment + 1}");
                    if (!HasStreet(side, tier, z0 + 1)) { GardenTerrace(side, tier, z0, z1); FinishGroup(); continue; }
                    Shelf(side, tier, z0, z1);
                    Row(side, tier, z0, z1);
                    for (float zz = z0 + 4; zz < z1; zz += R(12, 22))
                        if (StairDistance(zz) > 6 && !(side < 0 && tier == 0 && zz > 18 && zz < 61))
                        {
                            Planter(Point(side, tier, zz, 1.4f), R(.6f, 1));
                            if (Chance(.5f)) Lamp(Point(side, tier, zz + 3, .9f));
                        }
                    FinishGroup();
                }
            }
            StartGroup(side < 0 ? "West stair streets" : "East stair streets");
            for (int tier = 0; tier < 3; tier++)
                foreach (float z in stairZ)
                {
                    if (!HasStreet(side, tier, z - 14) || !HasStreet(side, tier + 1, z + 14)) continue;
                    StairStreet(side, tier, z);
                }
            FinishGroup();
            StartGroup(side < 0 ? "Western terraces and escarpment" : "Eastern terraces and escarpment");
            Terraces(side);
            CliffFace(side);
            GrandStair(side);
            FinishGroup();
            StartGroup(side < 0 ? "Western uplands" : "Eastern uplands");
            Upland(side);
            FinishGroup();
        }
        Physics.SyncTransforms();
        StartGroup("Mountain sanctuaries"); Mountains(); FinishGroup();
        StartGroup("Farmlands"); Farms(); FinishGroup();
        StartGroup("Harbour"); Port(); FinishGroup();
        StartGroup("River channel and quays");
        RiverBed();
        for (int side = -1; side <= 1; side += 2) QuayWall(side);
        var waterTint = tint; tint = Color.white;
        Box(new Vector3(0, -.06f, 250), new Vector3(1400, .1f, 1400), water, false);
        // Sea floor in 100 m tiles: PhysX dislikes triangles longer than 500 m.
        tint = P("CDB591"); for (float x = -700; x < 700; x += 100) for (float z = -220; z < 880; z += 100) Box(new Vector3(x + 50, -9.6f, z + 50), new Vector3(100, 1, 100), bedMat, true); tint = Color.white;
        tint = waterTint;
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 26; i++)
            {
                float z = -33 + i * 15 + R(-4, 4), x = Edge(side, 0, z) - side * R(.5f, 2);
                if (WaterStairGap(z, 6) || i % 2 == 1) continue;
                Rock(new Vector3(x, -4, z), new Vector3(R(1.6f, 3), R(3.6f, 5.5f), R(2, 4)), 7);
            }
        for (int i = 0; i < 46; i++)
        {
            float z = D(-40, 380); int side = Chance(.5f) ? 1 : -1;
            float x = Edge(side, 0, z) - side * D(1.5f, 7);
            Rock(new Vector3(x, Bed(x, z) - .6f, z), new Vector3(D(.8f, 2.2f), D(.8f, 1.8f), D(.8f, 2.2f)), 9);
        }
        FinishGroup();
        StartGroup("River gates and cross-gorge routes");
        foreach (var (z, tier, width) in BridgeList) Bridge(z, tier, width);
        FinishGroup();
        StartGroup("Shrine of the current");
        Rock(new Vector3(Center(118), -6, 118), new Vector3(8, 13, 10), 11);
        frame = Matrix4x4.TRS(new Vector3(Center(118), 6, 118), Quaternion.identity, Vector3.one);
        Cylinder(Vector3.zero, 7, .6f, trim, true, 36);
        frame = Matrix4x4.identity;
        Pavilion(new Vector3(Center(118), 6.6f, 118), 4.7f, 7.5f, true, 2);
        Stairs(new Vector3(Center(118), -.8f, 100), new Vector3(Center(118), 6.6f, 114), 3.2f);
        OliveTree(new Vector3(Center(118) - 4, 6.6f, 118), 1.1f);
        for (int i = 0; i < 6; i++) Bougainvillea(new Vector3(Center(118) + Mathf.Cos(i) * 6.6f, 6.4f, 118 + Mathf.Sin(i) * 6.6f), new Vector3(Mathf.Cos(i), 0, Mathf.Sin(i)), D(1, 1.6f));
        Spot(new Vector3(Center(118) + 2, 6.6f, 118), "Shrine of the Current", true);
        FinishGroup();
        StartGroup("Water stairs and moorings");
        for (int side = -1; side <= 1; side += 2)
            foreach (float z in waterStairZ)
            {
                var upper = Point(side, 0, z, 4);
                var lower = new Vector3(upper.x - side * 8, -1.3f, z - 10);
                Stairs(lower, upper, 4);
                frame = Matrix4x4.TRS(lower + new Vector3(-side * 3, .05f, 0), Quaternion.Euler(0, side * 20, 0), Vector3.one);
                Boat(); frame = Matrix4x4.identity;
                Lamp(upper + new Vector3(side * 1.2f, 0, 2.6f));
            }
        FinishGroup();
        StartGroup("Saffron ascent parkour court");
        ParkourCourt();
        FinishGroup();
        StartGroup("Hanging gardens");
        for (int side = -1; side <= 1; side += 2)
            for (int tier = 1; tier < 4; tier++) for (float z = -23; z < 320; z += R(7, 14))
                {
                    if (StairDistance(z) < 12) continue;
                    var p = Point(side, tier, z, .3f);
                    var old = frame; frame = Matrix4x4.TRS(p, Quaternion.Euler(0, side * 90, 0), Vector3.one);
                    Vine(Vector3.zero, R(1.5f, 4)); frame = old;
                    if (HasStreet(side, tier, z) && rng.NextDouble() < .3) Tree(Point(side, tier, z, 2.2f), R(.65f, .9f), rng.Next(1, 3));
                    if (Chance(.35f)) Bougainvillea(p + Vector3.down * .3f, Vector3.left * side, D(1, 1.8f));
                }
        FinishGroup();
        StartGroup("Crown observatory");
        var crown = Crown;
        Pavilion(crown, 9, 15, true, 1);
        for (int i = 0; i < 8; i++)
        {
            var c = crown + new Vector3(Mathf.Cos(i * .78f) * 12, 0, Mathf.Sin(i * .78f) * 12);
            if (Mathf.Abs(c.x - Center(c.z)) < Mathf.Abs(Edge(-1, 4, c.z) - Center(c.z)) - 1 && Mathf.Abs(c.x - Center(c.z)) > Mathf.Abs(Edge(-1, 3, c.z) - Center(c.z)) + 1) Cypress(c, R(7, 10));
        }
        Spot(crown + Vector3.right * 3, "Crown observatory", true);
        FinishGroup();
        Physics.SyncTransforms();
        StartGroup("Hanging vine routes");
        int vineId = 0;
        foreach (var request in vines)
        {
            if (!VineRoute(request.top, request.outward, request.length, out var top, out var outward, out var length)) continue;
            frame = Matrix4x4.TRS(top, Quaternion.LookRotation(-outward), Vector3.one);
            EmitVine(length, vineId++); frame = Matrix4x4.identity;
        }
        FinishGroup();
        Physics.SyncTransforms();
        int stones = PlaceSunstones();
        Life();
        ConfigureScene();
        var volume = new GameObject("River water volume").AddComponent<RiverWaterVolume>();
        volume.transform.position = new Vector3(0, 0, 250); volume.size = new Vector3(1400, 24, 1400);
        var player = Object.FindAnyObjectByType<WandererMotor>();
        var start = GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        player.transform.position = new Vector3(start.x - 1, start.y + .15f, 23.5f);
        player.ropeMaterial = brass;
        var orbit = Object.FindAnyObjectByType<WandererCamera>(); orbit.transform.position = player.transform.position + Vector3.up * 1.25f; orbit.transform.rotation = Quaternion.Euler(3, 20, 0);
        var rig = Object.FindAnyObjectByType<CinemachineCamera>(); rig.Lens.FarClipPlane = 900; rig.Lens.NearClipPlane = .15f;
        Camera.main.farClipPlane = 900; Camera.main.nearClipPlane = .15f;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Physics.SyncTransforms();
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        Debug.Log($"[Quarter] {buildings} buildings, {anchors} rings, {vineId} vines, {stones} sunstones, {meshId} meshes, {triangles} triangles");
        return new { scene = ScenePath, buildings, ungroundedCorners = ungrounded, anchors, vines = vineId, stones, meshes = meshId, triangles, renderers = city.GetComponentsInChildren<Renderer>().Length };
    }

    // Physics check on the saved scene: streets have paving underfoot and a wall, not air, below their front edge.
    public static object Check()
    {
        int samples = 0, holes = 0, overhangs = 0; var bad = new List<string>();
        foreach (int side in new[] { -1, 1 }) for (int tier = 0; tier < 4; tier++)
                for (float z = -30; z < 352; z += 3)
                {
                    if (!HasStreet(side, tier, z)) continue;
                    samples++;
                    var surface = Point(side, tier, z, streetW[tier] * .5f);
                    var hits = Physics.RaycastAll(surface + Vector3.up * 30, Vector3.down, 33, 1, QueryTriggerInteraction.Ignore);
                    if (!hits.Any(h => Mathf.Abs(h.point.y - surface.y) < .35f)) { holes++; if (bad.Count < 12) bad.Add($"hole {side} {tier} {z}"); }
                    if (tier == 0) continue;
                    // Probe 2 m below the street lip from the river side: the retaining wall must be within 0.5 m of the edge.
                    var river = Vector3.left * side; var lip = Point(side, tier, z, 0) + Vector3.down * 2;
                    if (!Physics.Raycast(lip + river * 1.5f, -river, out var wall, 3, 1, QueryTriggerInteraction.Ignore) || wall.distance > 2.05f) { overhangs++; if (bad.Count < 12) bad.Add($"overhang {side} {tier} {z}"); }
                }
        var city = EditorSceneManager.GetActiveScene().GetRootGameObjects().First(r => r.name == "Cliff Quarter");
        var defaults = city.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterials.Any(m => !m || m.name == "Default-Material")).Select(r => r.name).Take(5).ToArray();
        return new { samples, holes, overhangs, defaults, skybox = RenderSettings.skybox ? RenderSettings.skybox.name : "none", bad };
    }

    // ---------- materials ----------
    static void Materials()
    {
        plaster = Toon("Plaster", Color.white, 1, .45f);
        trim = Toon("Carved limestone", P("F4E6CB"), 1, .9f, m => m.SetFloat("_WetLine", .8f));
        paving = Toon("Paving", P("E9D7B5"), 4, .9f);
        roof = Toon("Roof tiles", Palette.Terracotta, 3, 1);
        clay = Toon("Terracotta", P("C0704E"), 0, 0);
        recess = Toon("Window recess", P("3B3F4E"), 0, 0, m => m.SetFloat("_Variation", 0));
        shutter = Toon("Shutters", Color.white, 0, 0);
        brass = Toon("Brass", Palette.Brass, 0, 0, m => { m.SetFloat("_Gloss", .8f); m.SetFloat("_RimStrength", .5f); m.SetColor("_EmissionColor", Palette.Brass.linear * .08f); });
        leaf = Toon("Foliage", Color.white, 0, 0, m => { m.SetFloat("_Wind", 1); m.SetFloat("_Translucency", .35f); m.SetFloat("_Variation", .3f); m.SetFloat("_RampSoft", .2f); m.SetFloat("_RimStrength", .35f); });
        cypressMat = Toon("Cypress", P("3E5A3A"), 0, 0, m => { m.SetFloat("_Wind", .5f); m.SetFloat("_Translucency", .15f); m.SetFloat("_RampSoft", .2f); });
        blossom = Toon("Blossom", Color.white, 0, 0, m => { m.SetFloat("_Wind", 1.2f); m.SetFloat("_Translucency", .45f); m.SetFloat("_RampSoft", .22f); m.SetFloat("_RimStrength", .4f); });
        wood = Toon("Cedar", P("7A5340"), 0, 0);
        rock = Toon("Cliff rock", Color.white, 2, 1, m => { m.SetColor("_TopColor", P("7E8F55")); m.SetFloat("_TopAmount", .85f); m.SetFloat("_WetLine", 1); m.SetFloat("_Variation", .25f); });
        bedMat = Toon("River bed", Color.white, 0, 0, m => m.SetFloat("_Variation", .45f));
        cloth = Toon("Cloth", Color.white, 0, 0, m => { m.SetFloat("_Wind", .7f); m.SetFloat("_Translucency", .3f); m.SetFloat("_RampSoft", .2f); });
        glow = Toon("Lantern glow", P("FFD9A0"), 0, 0, m => m.SetColor("_EmissionColor", new Color(1.6f, 1.05f, .45f)));
        sunMat = Toon("Sunstone", P("FFC960"), 0, 0, m => { m.SetFloat("_Gloss", 1); m.SetColor("_EmissionColor", new Color(2.4f, 1.5f, .45f)); m.SetFloat("_RimStrength", .8f); });
        fishMat = Toon("Fish", P("2C4A58"), 0, 0, m => m.SetFloat("_Variation", 0));
        birdMat = Toon("Bird", P("F7F2E8"), 0, 0, m => m.SetFloat("_RimStrength", .5f));
        water = Asset("Shallow river", "TinyWorld/Water");
        water.SetColor("_Shallow", Color.Lerp(Palette.Shallows, Color.white, .1f)); water.SetColor("_Mid", Palette.Water); water.SetColor("_Deep", Palette.DeepWater);
        water.SetColor("_Sky", P("B9DCEA")); water.SetColor("_Foam", P("FBFAF3"));
        beamMat = Asset("Sunstone beacon", "TinyWorld/Glow"); beamMat.SetColor("_Color", new Color(.55f, .38f, .12f));
        stock = Toon("Livestock", Color.white, 0, 0, m => { m.SetFloat("_RimStrength", .4f); m.SetFloat("_Variation", 0); });
        fall = Asset("Waterfall", "TinyWorld/Waterfall");
        fall.SetColor("_Deep", Palette.Water); fall.SetColor("_Light", Color.Lerp(Palette.Shallows, Color.white, .25f)); fall.SetColor("_Foam", P("FBFAF3"));
    }
    static Material Asset(string name, string shader)
    {
        string path = MatPath + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(mat, path); }
        mat.shader = Shader.Find(shader); EditorUtility.SetDirty(mat); return mat;
    }
    static Material Toon(string name, Color color, int pattern, float strength, Action<Material> extra = null)
    {
        var mat = Asset(name, "TinyWorld/Toon");
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Pattern", pattern); mat.SetFloat("_PatternStrength", strength);
        mat.SetColor("_ShadowTint", Color.Lerp(Palette.Shadow, Palette.Sky, .5f));
        mat.SetFloat("_Variation", .18f); mat.SetFloat("_Wind", 0); mat.SetFloat("_Gloss", 0); mat.SetFloat("_Translucency", 0);
        mat.SetFloat("_TopAmount", 0); mat.SetFloat("_WetLine", 0); mat.SetFloat("_RimStrength", .22f); mat.SetFloat("_RampSoft", .09f);
        mat.SetColor("_EmissionColor", Color.black); mat.enableInstancing = true;
        extra?.Invoke(mat); return mat;
    }

    // ---------- mesh batching ----------
    sealed class Batch { public List<Vector3> v = new(); public List<Color> c = new(); public List<int> t = new(); public Material mat; public bool solid, smooth, ghost; }
    static bool HasStreet(int side, int tier, float z)
    {
        int segment = Mathf.FloorToInt((z + 32) / 64);
        if (tier == 2 && ((side < 0 && segment == 0) || (side > 0 && segment == 4))) return false;
        if (tier == 3 && ((side < 0 && segment >= 4) || (side > 0 && segment < 2))) return false;
        return z >= -32 && z <= 342 - tier * 14 + (side == 1 ? 12 : 0);
    }
    static float StairDistance(float z) => stairZ.Min(v => Mathf.Abs(z - v));
    static bool WaterStairGap(float z, float half) => waterStairZ.Any(s => Mathf.Abs(z - (s - 5)) < half);
    static void StartGroup(string name) { group = new GameObject(name).transform; group.SetParent(city, false); batches = new(); frame = Matrix4x4.identity; }
    static void FinishGroup()
    {
        foreach (var entry in batches.Values)
        {
            if (entry.v.Count == 0) continue;
            List<Vector3> v = entry.v; List<Color> c = entry.c; List<int> t = entry.t;
            if (entry.smooth) Weld(entry, out v, out c, out t);
            var mesh = new Mesh { name = group.name + " " + entry.mat.name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            triangles += t.Count / 3;
            string path = GeoPath + "/Q_" + (meshId++).ToString("D4") + ".asset";
            mesh.UploadMeshData(false);
            AssetDatabase.CreateAsset(mesh, path);
            bool collides = entry.solid || (!entry.ghost && entry.mat != water);
            if (collides)
            {
                var serialized = new SerializedObject(mesh);
                serialized.FindProperty("m_PreBakeTriangleCollisionMesh").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var go = new GameObject(entry.mat.name + (entry.solid ? " · solid" : " · detail") + (entry.smooth ? " · smooth" : ""), typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(group, false); go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = entry.mat; go.isStatic = true;
            if (entry.mat == water || entry.mat == glow) renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (entry.solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            else if (collides)
            {
                var surface = new GameObject("Sticky surface") { layer = 8 };
                surface.transform.SetParent(go.transform, false); surface.isStatic = true;
                surface.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
        frame = Matrix4x4.identity;
    }
    static void Weld(Batch b, out List<Vector3> v, out List<Color> c, out List<int> t)
    {
        var map = new Dictionary<Vector3Int, int>(b.v.Count / 3); v = new(); c = new(); t = new(b.t.Count);
        var remap = new int[b.v.Count];
        for (int i = 0; i < b.v.Count; i++)
        {
            var key = Vector3Int.RoundToInt(b.v[i] * 400);
            if (!map.TryGetValue(key, out int index)) { index = v.Count; map[key] = index; v.Add(b.v[i]); c.Add(b.c[i]); }
            remap[i] = index;
        }
        for (int i = 0; i < b.t.Count; i += 3)
        {
            int x = remap[b.t[i]], y = remap[b.t[i + 1]], z = remap[b.t[i + 2]];
            if (x == y || y == z || x == z) continue;
            t.Add(x); t.Add(y); t.Add(z);
        }
    }
    static Batch Get(Material mat)
    {
        bool g = ghost || mat == leaf || mat == blossom || mat == cloth || mat == glow;
        string key = mat.name + (smooth ? "S" : "F") + (g ? "G" : "") + "|" + (solidFlag ? "1" : "0");
        if (!batches.TryGetValue(key, out var b)) { b = new Batch { mat = mat, solid = solidFlag, smooth = smooth, ghost = g }; batches[key] = b; }
        return b;
    }
    static bool solidFlag;
    static Color VC(Vector3 w)
    {
        var c = tint.linear;
        if (!float.IsNaN(aoBase)) { float k = Mathf.Lerp(.68f, 1, Mathf.Clamp01((w.y - aoBase) / aoSpan)); c.r *= k; c.g *= k; c.b *= k; }
        c.a = wind; return c;
    }
    static void Tri(Vector3 a, Vector3 b, Vector3 c, Material mat, bool solid = false)
    {
        if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-9f) return;
        solidFlag = solid; var batch = Get(mat); int n = batch.v.Count;
        var wa = frame.MultiplyPoint3x4(a); var wb = frame.MultiplyPoint3x4(b); var wc = frame.MultiplyPoint3x4(c);
        batch.v.Add(wa); batch.v.Add(wb); batch.v.Add(wc);
        batch.c.Add(VC(wa)); batch.c.Add(VC(wb)); batch.c.Add(VC(wc));
        batch.t.Add(n); batch.t.Add(n + 1); batch.t.Add(n + 2);
    }
    static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material mat, bool solid = false) { Tri(a, b, c, mat, solid); Tri(a, c, d, mat, solid); }
    static void Smooth(Action draw) { bool was = smooth; smooth = true; draw(); smooth = was; }

    // ---------- primitives ----------
    static void Box(Vector3 p, Vector3 size, Material mat, bool solid = false, bool bottom = true)
    {
        bool was = smooth; smooth = false;
        var s = size * .5f;
        var a = p + new Vector3(-s.x, -s.y, -s.z); var b = p + new Vector3(s.x, -s.y, -s.z); var c = p + new Vector3(s.x, -s.y, s.z); var d = p + new Vector3(-s.x, -s.y, s.z);
        var e = a + Vector3.up * size.y; var f = b + Vector3.up * size.y; var g = c + Vector3.up * size.y; var h = d + Vector3.up * size.y;
        Quad(a, e, f, b, mat, solid); Quad(b, f, g, c, mat, solid); Quad(c, g, h, d, mat, solid); Quad(d, h, e, a, mat, solid);
        Quad(e, h, g, f, mat, solid); if (bottom) Quad(a, b, c, d, mat, solid);
        smooth = was;
    }
    static void Cylinder(Vector3 bottom, float radius, float height, Material mat, bool solid = false, int cuts = 16, float topRadius = -1, bool caps = true)
    {
        if (topRadius < 0) topRadius = radius;
        bool was = smooth;
        for (int i = 0; i < cuts; i++)
        {
            float a = i * 2 * Mathf.PI / cuts, b = (i + 1) * 2 * Mathf.PI / cuts;
            var p = bottom + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
            var q = bottom + new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius);
            var u = bottom + new Vector3(Mathf.Cos(a) * topRadius, height, Mathf.Sin(a) * topRadius);
            var v = bottom + new Vector3(Mathf.Cos(b) * topRadius, height, Mathf.Sin(b) * topRadius);
            smooth = was || cuts >= 8; Quad(p, u, v, q, mat, solid);
            smooth = false; if (caps) { Tri(bottom + Vector3.up * height, v, u, mat, solid); Tri(bottom, p, q, mat, solid); }
        }
        smooth = was;
    }
    static void Lathe(Vector3 p, Vector2[] profile, int cuts, Material mat, bool solid = false, float noise = 0, float twist = 0)
    {
        var rings = new Vector3[profile.Length, cuts];
        for (int j = 0; j < profile.Length; j++) for (int i = 0; i < cuts; i++)
            {
                float a = i * 2 * Mathf.PI / cuts + j * twist, r = profile[j].x * (1 + (noise > 0 ? D(-noise, noise) : 0));
                rings[j, i] = p + new Vector3(Mathf.Cos(a) * r, profile[j].y, Mathf.Sin(a) * r);
            }
        bool down = profile[profile.Length - 1].y < profile[0].y;
        Smooth(() =>
        {
            for (int j = 0; j < profile.Length - 1; j++) for (int i = 0; i < cuts; i++)
                {
                    int k = (i + 1) % cuts;
                    if (down) Quad(rings[j, k], rings[j + 1, k], rings[j + 1, i], rings[j, i], mat, solid);
                    else Quad(rings[j, i], rings[j + 1, i], rings[j + 1, k], rings[j, k], mat, solid);
                }
        });
    }
    static void Dome(Vector3 bottom, float radius, float height, Material mat, bool solid = false, int cuts = 28)
    {
        Smooth(() =>
        {
            for (int j = 0; j < 9; j++)
            {
                float a = j * Mathf.PI / 18, b = (j + 1) * Mathf.PI / 18;
                for (int i = 0; i < cuts; i++)
                {
                    float x = i * Mathf.PI * 2 / cuts, y = (i + 1) * Mathf.PI * 2 / cuts;
                    Vector3 P(float angle, float ring) => bottom + new Vector3(Mathf.Cos(angle) * Mathf.Cos(ring) * radius, Mathf.Sin(ring) * height, Mathf.Sin(angle) * Mathf.Cos(ring) * radius);
                    Quad(P(x, a), P(x, b), P(y, b), P(y, a), mat, solid);
                }
            }
        });
    }
    static void Ellipsoid(Vector3 p, Vector3 size, Material mat, int cuts = 10, int rings = 7, float lumps = 0)
    {
        var points = new Vector3[rings + 1, cuts];
        for (int j = 0; j <= rings; j++) for (int i = 0; i < cuts; i++)
            {
                float r = -Mathf.PI * .5f + j * Mathf.PI / rings, x = i * Mathf.PI * 2 / cuts;
                float bump = j == 0 || j == rings ? 1 : 1 + (lumps > 0 ? D(-lumps, lumps) : 0);
                points[j, i] = p + Vector3.Scale(new Vector3(Mathf.Cos(x) * Mathf.Cos(r), Mathf.Sin(r), Mathf.Sin(x) * Mathf.Cos(r)) * bump, size);
            }
        Smooth(() =>
        {
            for (int j = 0; j < rings; j++) for (int i = 0; i < cuts; i++)
                { int k = (i + 1) % cuts; Quad(points[j, i], points[j + 1, i], points[j + 1, k], points[j, k], mat); }
        });
    }
    static void Torus(Vector3 center, float radius, float tube, Material mat, int cuts = 20, int sides = 8)
    {
        Smooth(() =>
        {
            Vector3 S(float u, float v) => center + new Vector3(Mathf.Cos(u) * (radius + tube * Mathf.Cos(v)), Mathf.Sin(u) * (radius + tube * Mathf.Cos(v)), tube * Mathf.Sin(v));
            for (int i = 0; i < cuts; i++) for (int j = 0; j < sides; j++)
                {
                    float u0 = i * Mathf.PI * 2 / cuts, u1 = (i + 1) * Mathf.PI * 2 / cuts, v0 = j * Mathf.PI * 2 / sides, v1 = (j + 1) * Mathf.PI * 2 / sides;
                    Quad(S(u0, v0), S(u1, v0), S(u1, v1), S(u0, v1), mat);
                }
        });
    }
    static void Arch(Vector3 p, float radius, float rise, float thickness, float depth, Material mat, bool solid = false, int cuts = 20)
    {
        bool was = smooth;
        for (int i = 0; i < cuts; i++)
        {
            float a = Mathf.PI * i / cuts, b = Mathf.PI * (i + 1) / cuts;
            Vector3 P(float t, float outer, float z) => p + new Vector3(Mathf.Cos(t) * (radius + outer), Mathf.Sin(t) * (rise + outer), z);
            var x = P(a, 0, -depth * .5f); var y = P(b, 0, -depth * .5f); var u = P(a, thickness, -depth * .5f); var v = P(b, thickness, -depth * .5f);
            var dz = Vector3.forward * depth;
            smooth = false; Quad(x, y, v, u, mat, solid); Quad(x + dz, u + dz, v + dz, y + dz, mat, solid);
            smooth = true; Quad(x, x + dz, y + dz, y, mat, solid); Quad(u, v, v + dz, u + dz, mat, solid);
        }
        smooth = was;
    }
    static void Beam(Vector3 a, Vector3 b, float width, Material mat, bool solid = false)
    {
        var previous = frame; var d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
        frame = frame * Matrix4x4.TRS((a + b) * .5f, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one);
        Box(Vector3.zero, new Vector3(width, d.magnitude, width), mat, solid); frame = previous;
    }
    static void Limb(Vector3 a, Vector3 b, float r0, float r1, Material mat, int cuts = 7)
    {
        var previous = frame; var d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
        frame = frame * Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one);
        Smooth(() => Cylinder(Vector3.zero, r0, d.magnitude, mat, false, cuts, r1, false));
        frame = previous;
    }
    static void Rock(Vector3 p, Vector3 size, int cuts)
    {
        const int levels = 11;
        var rings = new Vector3[levels, cuts]; float phase = R(0, 6), seed = R(0, 100);
        var old = tint; tint = Color.Lerp(P("E4D3B4"), P("CDB28E"), D(0, 1));
        for (int j = 0; j < levels; j++) for (int i = 0; i < cuts; i++)
            {
                float a = i * Mathf.PI * 2 / cuts, t = j / (float)(levels - 1), taper = 1 - t * .38f;
                float r = taper * (.8f + Mathf.PerlinNoise(seed + Mathf.Cos(a) * 1.3f, seed + Mathf.Sin(a) * 1.3f + t * 2.2f) * .45f);
                rings[j, i] = p + new Vector3(Mathf.Cos(a) * size.x * r + Mathf.Sin(t * 6 + phase) * size.x * .1f, t * size.y, Mathf.Sin(a) * size.z * r);
            }
        Smooth(() =>
        {
            for (int j = 0; j < levels - 1; j++) for (int i = 0; i < cuts; i++)
                { int k = (i + 1) % cuts; Quad(rings[j, i], rings[j + 1, i], rings[j + 1, k], rings[j, k], rock, true); }
        });
        for (int i = 1; i < cuts - 1; i++) Tri(rings[levels - 1, 0], rings[levels - 1, i + 1], rings[levels - 1, i], rock, true);
        tint = old;
    }

    // ---------- terrain ----------
    static float Bed(float x, float z)
    {
        int side = x > Center(z) ? 1 : -1;
        float edge = Mathf.Abs(Edge(side, 0, z) - Center(z));
        float d = Mathf.Abs(x - Center(z)) / edge;
        float deep = -5.6f + (Mathf.PerlinNoise(x * .04f + 3, z * .04f) - .5f) * 2;
        float shelf = -1.15f + (Mathf.PerlinNoise(x * .18f, z * .18f + 5) - .5f) * .7f;
        return Mathf.Lerp(deep, shelf, Mathf.SmoothStep(0, 1, (d - .3f) / .62f));
    }
    static void RiverBed()
    {
        const int cols = 56; float step = 3;
        int rows = Mathf.CeilToInt(460 / step);
        var grid = new Vector3[rows + 1, cols + 1];
        for (int i = 0; i <= rows; i++)
        {
            float z = -60 + i * step, c = Center(z), half = Width(z) + 32;
            for (int j = 0; j <= cols; j++) { float x = c - half + 2 * half * j / cols; grid[i, j] = new Vector3(x, Bed(x, z), z); }
        }
        var old = tint;
        smooth = true;
        for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++)
            {
                var g = grid[i, j];
                float n = Mathf.PerlinNoise(g.x * .09f, g.z * .09f);
                tint = n > .62f ? P("A7A57E") : Color.Lerp(P("E6D2A6"), P("D4BC8E"), Mathf.PerlinNoise(g.x * .3f, g.z * .3f));
                Quad(grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1], bedMat, true);
            }
        smooth = false; tint = old;
    }
    static void QuayWall(int side)
    {
        var old = tint; tint = P("E9D9BB"); aoBase = -1.4f; aoSpan = 3.2f;
        for (float z = TerrainStart; z < TerrainEnd - .01f; z += 2.5f)
        {
            float z1 = z + 2.5f;
            if (WaterStairGap(z + 1.25f, 4.5f)) continue;
            var a = new Vector3(Edge(side, 0, z) + side * .06f, Level(side, 0, z) - .03f, z); var b = new Vector3(Edge(side, 0, z1) + side * .06f, Level(side, 0, z1) - .03f, z1);
            var a0 = new Vector3(a.x, -3.2f, z); var b0 = new Vector3(b.x, -3.2f, z1);
            if (side > 0) Quad(a0, b0, b, a, trim, true); else Quad(b0, a0, a, b, trim, true);
        }
        tint = old; aoBase = float.NaN;
    }
    const float TerrainStart = -60, TerrainEnd = 400, TerrainStep = 2.5f;
    static Vector3 Crown => Point(-1, 3, 244, 8);
    // Quad wound to face `normal`.
    static void QuadN(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Material mat, bool solid = true)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0) Quad(d, c, b, a, mat, solid); else Quad(a, b, c, d, mat, solid);
    }
    // Solid stepped ground: each tier's floor runs back to the retaining wall that carries the tier above.
    static void Terraces(int side)
    {
        var river = Vector3.left * side;
        for (float z = TerrainStart; z < TerrainEnd - .01f; z += TerrainStep)
        {
            float z1 = z + TerrainStep;
            for (int tier = 0; tier < 4; tier++)
            {
                Vector3 F(float zz) => new(Edge(side, tier, zz), Level(side, tier, zz) - .03f, zz);
                Vector3 B(float zz) => new(Edge(side, tier + 1, zz) + side * .6f, Level(side, tier, zz) - .03f, zz);
                tint = Color.Lerp(P("E4D3B4"), P("CDB28E"), Mathf.PerlinNoise(z * .05f, tier * 3.1f + side));
                QuadN(F(z), F(z1), B(z1), B(z), Vector3.up, rock);
                if (tier == 3) continue;
                // Retaining wall under the next street, from this floor up to the next.
                Vector3 W(float zz, int t) => new(Edge(side, tier + 1, zz) + side * .06f, Level(side, t, zz) - (t == tier ? .6f : .03f), zz);
                tint = Color.Lerp(P("F1E2C6"), P("DCC39E"), Mathf.PerlinNoise(z * .07f + 9, tier + side * 5));
                aoBase = Level(side, tier, z); aoSpan = 4;
                QuadN(W(z, tier), W(z1, tier), W(z1, tier + 1), W(z, tier + 1), river, trim);
                aoBase = float.NaN;
                if (Chance(.06f) && StairDistance(z) > 14)
                    IvyCurtain(W(z, tier + 1) + Vector3.down * .3f - side * Vector3.right * .1f, river, D(3, 7), D(4, Level(side, tier + 1, z) - Level(side, tier, z) - 3));
            }
        }
        tint = Color.white;
        EndCaps(side, TerrainStart, Vector3.back); EndCaps(side, TerrainEnd, Vector3.forward);
        WallDwellings(side);
    }
    // Windows and the odd balcony on retaining wall left exposed by houses, stairs and gardens.
    static void WallDwellings(int side)
    {
        Physics.SyncTransforms();
        shutterTint = Palette.Shutter;
        for (int tier = 0; tier < 3; tier++)
            for (float z = -30; z < 352; z += D(3.2f, 5.5f))
            {
                float slope = (Edge(side, tier + 1, z + 1) - Edge(side, tier + 1, z - 1)) * .5f;
                var river = new Vector3(-side, 0, side * slope).normalized;
                float bottom = Level(side, tier, z), top = Level(side, tier + 1, z);
                for (float y = bottom + D(3, 4.5f); y < top - 2.8f; y += D(3.6f, 4.6f))
                {
                    var wall = new Vector3(Edge(side, tier + 1, z) + side * .06f, y, z);
                    // The wall itself has no collider yet, so any hit means something stands in front of it.
                    if (Physics.SphereCast(wall + river * 12 + Vector3.up * .8f, .9f, -river, out _, 11.6f, 1, QueryTriggerInteraction.Ignore)) continue;
                    if (!Chance(.35f)) continue;
                    var old = frame; frame = Matrix4x4.TRS(wall, Quaternion.LookRotation(-river), Vector3.one);
                    if (Chance(.15f)) { Window(new Vector3(0, .15f, 0), .95f, 2.1f, true); Balcony(Vector3.zero, D(1.8f, 2.8f)); }
                    else Window(Vector3.zero, .8f, 1.45f, true);
                    frame = old;
                }
            }
    }
    static void EndCaps(int side, float z, Vector3 normal)
    {
        tint = P("D8C19B");
        for (int tier = 0; tier < 4; tier++)
        {
            float x0 = Edge(side, tier, z), x1 = Edge(side, tier + 1, z) + side * .6f, y = Level(side, tier, z) - .03f;
            QuadN(new(x0, -8, z), new(x0, y, z), new(x1, y, z), new(x1, -8, z), normal, rock);
        }
        tint = Color.white;
    }
    static Vector3[] CliffProfile(int side, float z, int yCuts)
    {
        float baseY = Level(side, 3, z) - .03f, baseX = Edge(side, 4, z) + side * .5f;
        float peak = Mathf.Max(baseY + 14, 104 + Mathf.Sin(z * .029f + side) * 16 + Mathf.Sin(z * .081f) * 7);
        var profile = new Vector3[yCuts + 3];
        for (int j = 0; j <= yCuts; j++)
        {
            float t = j / (float)yCuts, y = baseY + t * (peak - baseY), fade = Mathf.SmoothStep(0, 1, t * 4);
            float buttress = (Mathf.PerlinNoise(z * .05f + side * 31, y * .035f) - .5f) * 4.4f;
            float crag = (Mathf.PerlinNoise(z * .17f + side * 7, y * .15f) - .5f) * 1.8f;
            float strata = Mathf.Sin(y * .52f + Mathf.PerlinNoise(z * .03f, side * 3) * 6) * .45f;
            profile[j] = new Vector3(baseX + side * ((y - baseY) * .55f + fade * (buttress + crag + strata)), y, z);
        }
        profile[yCuts + 1] = profile[yCuts]; profile[yCuts + 2] = profile[yCuts];
        return profile;
    }
    static void CliffFace(int side)
    {
        const int yCuts = 30; int zCuts = Mathf.RoundToInt((TerrainEnd - TerrainStart) / TerrainStep);
        var rows = new Vector3[zCuts + 1][];
        for (int i = 0; i <= zCuts; i++) rows[i] = CliffProfile(side, TerrainStart + i * TerrainStep, yCuts);
        var river = Vector3.left * side;
        smooth = true;
        for (int i = 0; i < zCuts; i++) for (int j = 0; j < yCuts; j++)
            {
                var a = rows[i][j]; var b = rows[i + 1][j]; var c = rows[i + 1][j + 1]; var d = rows[i][j + 1];
                tint = Color.Lerp(P("EBDCBD"), P("C9A985"), Mathf.SmoothStep(.3f, .75f, Mathf.PerlinNoise(a.z * .02f + side * 11, a.y * .02f)));
                QuadN(a, b, c, d, river + Vector3.up * .3f, rock);
            }
        smooth = false;
        foreach (var (i, normal) in new[] { (0, Vector3.back), (zCuts, Vector3.forward) })
            for (int j = 0; j < yCuts; j++)
            {
                var a = rows[i][j]; var b = rows[i][j + 1];
                tint = P("D8C19B");
                QuadN(a, b, new Vector3(b.x, -10, b.z), new Vector3(a.x, -10, a.z), normal, rock);
            }
        tint = Color.white;
        for (int i = 0; i < 26; i++)
        {
            var row = rows[deco.Next(zCuts)]; var surface = row[deco.Next(4, yCuts - 4)];
            IvyCurtain(surface - Vector3.right * side * .2f, river, D(4, 11), D(3, 8));
        }
    }

    // ---------- uplands: west mountains, east farmland ----------
    const float UStep = 3, UMax = 174;
    // GLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).
    static float Edge01(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
    static Vector3 Crest(int side, float z) => CliffProfile(side, z, 30)[30];
    static readonly (float u, float z, float r, float h)[] WestPeaks = { (150, 60, 40, 70), (150, 165, 45, 85), (140, 335, 40, 60), (120, 212, 22, 58), (45, 385, 30, 30), (110, 20, 30, 35) };
    static readonly (float u, float z, float r, float h)[] EastPeaks = { (38, 22, 22, 10), (128, 250, 26, 12), (150, 120, 40, 14) };
    static List<(float u, float z, float r, float h)>[] pads;
    static float RiverU(float z) => 95 + 28 * Mathf.Sin(z * .011f + .6f) + 9 * Mathf.Sin(z * .034f);
    static float WaterY(float z) => 108.5f - (z + 60) * .013f;
    static float UplandBase(int side, float u, float z, float crestY)
    {
        float h;
        if (side < 0)
        {
            h = 116 + (Mathf.PerlinNoise(u * .012f + 3, z * .012f) - .5f) * 46 + (Mathf.PerlinNoise(u * .045f, z * .045f + 9) - .5f) * 9;
            foreach (var pk in WestPeaks) h += pk.h * Mathf.Exp(-((u - pk.u) * (u - pk.u) + (z - pk.z) * (z - pk.z)) / (2 * pk.r * pk.r));
        }
        else
        {
            h = 109 - (z + 60) * .012f + (Mathf.PerlinNoise(u * .015f, z * .015f + 5) - .5f) * 6 + Mathf.Sin(u * .05f + z * .02f) * 1.2f;
            foreach (var pk in EastPeaks) h += pk.h * Mathf.Exp(-((u - pk.u) * (u - pk.u) + (z - pk.z) * (z - pk.z)) / (2 * pk.r * pk.r));
        }
        return Mathf.Lerp(crestY, h, Mathf.SmoothStep(0, 1, u / 26));
    }
    static float UplandH(int side, float u, float z, float crestY)
    {
        float h = UplandBase(side, u, z, crestY);
        foreach (var pad in pads[side < 0 ? 0 : 1])
        {
            float d = Mathf.Sqrt((u - pad.u) * (u - pad.u) + (z - pad.z) * (z - pad.z));
            h = Mathf.Lerp(h, pad.h, 1 - Edge01(pad.r, pad.r + 14, d));
        }
        if (side > 0 && z > -58)
        {
            // Stream: banks held above the water, a flat bed below it; a spring pond at the head.
            float d = Mathf.Abs(u - RiverU(z)), w = WaterY(z);
            if (z < -40) d = Mathf.Min(d, Mathf.Sqrt((u - RiverU(-40)) * (u - RiverU(-40)) + (z + 48) * (z + 48)) * .6f);
            h = Mathf.Lerp(h, Mathf.Max(h, w + .35f), 1 - Edge01(9, 16, d));
            h = Mathf.Lerp(h, w - 1.5f, 1 - Edge01(4.5f, 9, d));
        }
        return Mathf.Lerp(h, -10, Edge01(UMax - 24, UMax, u));
    }
    static void InitPads()
    {
        pads = new[] { new List<(float, float, float, float)>(), new List<(float, float, float, float)>() };
        void Pad(int side, float u, float z, float r, float h = float.NaN)
        {
            if (float.IsNaN(h)) h = UplandBase(side, u, z, Crest(side, z).y) + .2f;
            pads[side < 0 ? 0 : 1].Add((u, z, r, h));
        }
        Pad(-1, 80, 118, 58, 168); Pad(-1, 92, 292, 60, 146); Pad(-1, 62, 22, 30, 132); Pad(-1, 124, 212, 13, 194); Pad(-1, 16, 236, 13);
        Pad(-1, 10, GrandStairTop(-1).z, 12, GrandStairTop(-1).y - .15f); Pad(1, 10, GrandStairTop(1).z, 12, GrandStairTop(1).y - .15f);
        Pad(1, 55, 100, 26); Pad(1, 70, 190, 16); Pad(1, 38, 22, 8); Pad(1, 128, 250, 8); Pad(1, 66, 300, 14);
    }
    static Vector3 UplandPoint(int side, float u, float z)
    {
        var crest = Crest(side, z);
        return new Vector3(crest.x + side * u, UplandH(side, u, z, crest.y), z);
    }
    static Vector3 UP(int side, float u, float z) => UplandPoint(side, u, z);
    static void Upland(int side)
    {
        int zCuts = Mathf.RoundToInt((TerrainEnd - TerrainStart) / TerrainStep), uCuts = Mathf.RoundToInt(UMax / UStep);
        var grid = new Vector3[zCuts + 1, uCuts + 1];
        for (int i = 0; i <= zCuts; i++)
        {
            float z = TerrainStart + i * TerrainStep; var crest = Crest(side, z);
            for (int j = 0; j <= uCuts; j++) grid[i, j] = new Vector3(crest.x + side * j * UStep, j == 0 ? crest.y : UplandH(side, j * UStep, z, crest.y), z);
        }
        smooth = true;
        for (int i = 0; i < zCuts; i++) for (int j = 0; j < uCuts; j++)
            {
                var a = grid[i, j];
                float dry = Mathf.PerlinNoise(a.x * .02f, a.z * .02f);
                tint = side < 0 ? Color.Lerp(P("E6D2B0"), P("C4A57E"), dry) : Color.Lerp(P("D9CFA2"), P("BFAE7E"), dry);
                QuadN(a, grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1], Vector3.up, rock);
            }
        smooth = false;
        foreach (var (i, normal) in new[] { (0, Vector3.back), (zCuts, Vector3.forward) })
            for (int j = 0; j < uCuts; j++)
            {
                var a = grid[i, j]; var b = grid[i, j + 1]; tint = P("D8C19B");
                QuadN(a, b, new Vector3(b.x, -10, b.z), new Vector3(a.x, -10, a.z), normal, rock);
            }
        tint = Color.white;
    }
    // Monumental stair viaduct from the top street, along the cliff, then a bridge onto the crest.
    static float GrandStairZ(int side) => side < 0 ? 128 : 150;
    static Vector3 GrandStairStart(int side) { float z = GrandStairZ(side); return new Vector3(Edge(side, 3, z) + side * (streetW[3] + 1.6f), Level(side, 3, z), z); }
    static Vector3 GrandStairTop(int side)
    {
        var s0 = GrandStairStart(side); float z1 = s0.z + 60;
        for (int k = 0; k < 3; k++) z1 = s0.z + (Crest(side, z1).y - s0.y) / .55f;
        var crest = Crest(side, z1);
        return new Vector3(Edge(side, 4, z1) - side * 2.5f, crest.y + .2f, z1);
    }
    static void GrandStair(int side)
    {
        var s0 = GrandStairStart(side); var s1 = GrandStairTop(side); var crest = Crest(side, s1.z) + Vector3.right * side * 9;
        crest.y = s1.y;
        Stairs(s0, s1 - Vector3.forward * 4, 5);
        Piers(s0, s1 - Vector3.forward * 4, p => Level(side, 3, p.z) - .3f);
        Cylinder(s1 + Vector3.down * .5f, 4.5f, .5f, paving, true, 32);
        foreach (float a in new[] { 0f, 2.1f, 4.2f }) { var foot = s1 + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.2f; foot.y = Level(side, 3, foot.z) - .3f; Column(foot, s1.y - .5f - foot.y, .55f); }
        // Bridge to the crest over the upper escarpment.
        var old = frame; var d = crest - s1; float length = new Vector2(d.x, d.z).magnitude;
        frame = Matrix4x4.TRS((s1 + crest) * .5f, Quaternion.LookRotation(new Vector3(d.x, 0, d.z)), Vector3.one);
        Box(new Vector3(0, -.4f, 0), new Vector3(4.6f, .8f, length + 2), paving, true);
        // Rails start clear of the landing so the stair can step onto the bridge.
        foreach (int s in new[] { -1, 1 }) Rail(new Vector3(s * 2.15f, 0, -length * .5f + 4.6f), new Vector3(s * 2.15f, 0, length * .5f));
        frame = old;
        for (int i = 0; i < 2; i++) Lamp(s1 + new Vector3(i == 0 ? 3 : -3, 0, 2.5f));
        Spot(s1 + Vector3.up * .05f, side < 0 ? "Stair of the Sanctuaries" : "Stair to the Farmlands", true);
        var route = new GameObject(side < 0 ? "Grand stair west" : "Grand stair east").transform; route.SetParent(city, false);
        foreach (var (name, pos) in new[] { ("A", s0), ("B", s1), ("C", crest) }) { var n = new GameObject(name).transform; n.SetParent(route, false); n.position = pos; }
    }

    static void Shelf(int side, int tier, float z0, float z1)
    {
        int cuts = Mathf.CeilToInt((z1 - z0) / 2.5f);
        for (int i = 0; i < cuts; i++)
        {
            float a = Mathf.Lerp(z0, z1, i / (float)cuts), b = Mathf.Lerp(z0, z1, (i + 1f) / cuts);
            float width = streetW[tier];
            Vector3 p = Point(side, tier, a, 0), q = Point(side, tier, b, 0), r = q + Vector3.right * side * width, s = p + Vector3.right * side * width;
            tint = Color.white;
            if (side > 0) Quad(p, q, r, s, paving, true); else Quad(s, r, q, p, paving, true);
            Vector3 down = Vector3.down * .5f;
            if (side > 0) Quad(p + down, q + down, q, p, trim, true); else Quad(q + down, p + down, p, q, trim, true);
            if (i % 2 == 0 && StairDistance(a) > 16)
            {
                var railP = p + Vector3.right * side * .4f + Vector3.up * .65f;
                var railQ = q + Vector3.right * side * .4f + Vector3.up * .65f;
                Beam(railP, railQ, .18f, trim, true); Beam(p + Vector3.right * side * .4f, railP, .22f, trim, true);
                ghost = true;
                for (float t = .2f; t < 1; t += .2f) Baluster(Vector3.Lerp(p, q, t) + Vector3.right * side * .4f, .6f);
                ghost = false;
            }
            if (i % 7 == 0 && tier > 0 && StairDistance(a) > 16) Vine(p + Vector3.right * side * .2f, R(2, 4), Vector3.left * side);
        }
    }
    static void Corbel(Vector3 p, int side)
    {
        // Stepped stone bracket under the street lip.
        for (int k = 0; k < 3; k++) Box(p + new Vector3(-side * (.6f + k * .35f), -k * .45f - .2f, 0), new Vector3(1.4f - k * .35f, .4f, .7f), trim);
    }
    static void Baluster(Vector3 foot, float height)
    {
        float h = height;
        Lathe(foot, new[] { new Vector2(.07f, 0), new Vector2(.09f, .06f * h), new Vector2(.045f, .25f * h), new Vector2(.085f, .5f * h), new Vector2(.045f, .8f * h), new Vector2(.07f, .92f * h), new Vector2(.07f, h) }, 5, trim);
    }

    // ---------- architecture ----------
    // ---------- keep-out volumes (stairs, bridges, landmarks) ----------
    static void CollectKeepOuts()
    {
        keepOut = new();
        foreach (int side in new[] { -1, 1 }) for (int tier = 0; tier < 3; tier++) foreach (float z in stairZ)
                {
                    if (!HasStreet(side, tier, z - 14) || !HasStreet(side, tier + 1, z + 14)) continue;
                    var r = StairRoute(side, tier, z);
                    keepOut.Add((r.a, r.firstEnd, 2.6f)); keepOut.Add((r.turnIn, r.turnOut, 4)); keepOut.Add((r.secondStart, r.b, 2.6f));
                    keepOut.Add((r.b, r.b, 4.6f)); keepOut.Add((r.mast, r.mast + Vector3.up * 9, 1.6f)); keepOut.Add((r.anchor, r.anchor, 3.5f));
                    for (int i = 1; i < 9; i++) { var c = r.Lip(i); keepOut.Add((c, c, 1.8f)); }
                }
        foreach (var (z, tier, width) in BridgeList)
        {
            var g = BridgeGeometry(z, tier);
            for (int i = 0; i <= 40; i++)
            {
                float s = i / 40f; var deck = Vector3.Lerp(g.a, g.b, s);
                float arch = deck.y - g.rise - .7f + g.rise * Mathf.Sqrt(Mathf.Max(0, 1 - (2 * s - 1) * (2 * s - 1)));
                keepOut.Add((new Vector3(deck.x, arch - .5f, deck.z), new Vector3(deck.x, deck.y + 1.5f, deck.z), width * .5f + 1.4f));
            }
            if (tier > 0) foreach (var end in new[] { g.a + Vector3.left * 4, g.b + Vector3.right * 4 }) keepOut.Add((end, end + Vector3.up * 11, 4.8f));
        }
        keepOut.Add((Crown, Crown + Vector3.up * 24, 11.5f));
        foreach (int side in new[] { -1, 1 }) { var s0 = GrandStairStart(side); var s1 = GrandStairTop(side); keepOut.Add((s0 - Vector3.forward * 3, s1, 4.2f)); keepOut.Add((s1, s1 + Vector3.right * side * 14, 5.5f)); }
    }
    static bool Blocked(Matrix4x4 m, float width, float depth, float y0, float y1)
    {
        var inv = m.inverse;
        foreach (var k in keepOut)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(k.a, k.b)));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(k.a, k.b, i / (float)n);
                if (p.y < y0 - 3 || p.y > y1 + k.r) continue;
                var l = inv.MultiplyPoint3x4(p);
                if (Mathf.Abs(l.x) < width * .5f + k.r && l.z > -k.r && l.z < depth + k.r) return true;
            }
        }
        return false;
    }
    static bool BlockedCircle(Vector3 p, float radius, float height) => Blocked(Matrix4x4.Translate(p - Vector3.forward * radius), radius * 2, radius * 2, p.y, p.y + height);

    // ---------- architecture ----------
    // Attached houses along the back of a street, each reaching back into the next retaining wall.
    static void Row(int side, int tier, float z0, float z1)
    {
        float z = z0 + R(.4f, 1.5f); bool openLeft = true;
        while (z < z1 - 4)
        {
            float width = Mathf.Min(R(6.5f, 12), z1 - z - .4f);
            if (width < 5) break;
            float zc = z + width * .5f;
            float slope = (Edge(side, tier, zc + 1) - Edge(side, tier, zc - 1)) * .5f;
            float yaw = (side < 0 ? Mathf.Atan2(-1, slope) : Mathf.Atan2(1, -slope)) * Mathf.Rad2Deg;
            var foot = new Vector3(Edge(side, tier, zc) + side * streetW[tier], Level(side, tier, zc), zc);
            float depth = Mathf.Abs(Edge(side, tier + 1, zc) - foot.x) + 1.2f;
            float riser = tier < 3 ? Level(side, tier + 1, zc) - foot.y : 0;
            double roll = rng.NextDouble();
            float height = tier == 3 ? R(9, 19) : roll < .5 ? riser - R(.9f, 3) : roll < .86 ? riser * R(.5f, .78f) : R(7.5f, 11.5f);
            height = Mathf.Max(height, 7);
            bool gapAfter = rng.NextDouble() < .14;
            bool tower = width < 8.5f && rng.NextDouble() < .12;
            if (tower) height = tier < 3 ? riser + R(5, 11) : R(19, 27);
            if (Blocked(Matrix4x4.TRS(foot, Quaternion.Euler(0, yaw, 0), Vector3.one), width, depth, foot.y, foot.y + height + 4))
            {
                if (tier == 0 && !(side < 0 && zc > 18 && zc < 61) && Chance(.4f) && !BlockedCircle(Point(side, tier, zc, 8.5f), 2, 4))
                    Market(Point(side, tier, zc, 8.5f), side * 90, R(2.3f, 3.6f));
                z += 3; openLeft = true; continue;
            }
            Building(foot, yaw, width, depth, height, riser, tier, openLeft, gapAfter, tower);
            z += width + (gapAfter ? R(1.8f, 3.2f) : R(-.15f, .1f));
            openLeft = gapAfter;
        }
    }
    static void GardenTerrace(int side, int tier, float z0, float z1)
    {
        for (float z = z0 + R(1, 4); z < z1 - 1; z += R(4.5f, 8))
        {
            float span = Mathf.Abs(Edge(side, tier + 1, z) - Edge(side, tier, z));
            var back = Point(side, tier, z, span - 1.8f);
            if (Chance(.75f) && !BlockedCircle(back, 1.2f, 9)) Cypress(back, D(5, 9));
            var mid = Point(side, tier, z + D(-1, 1), D(3, span - 4.5f));
            if (Chance(.55f)) OliveTree(mid, D(.75f, 1.05f));
            else if (!BlockedCircle(mid, 1, 2)) Shrubs(mid, D(1.2f, 2.2f));
            if (Chance(.5f)) Shrubs(Point(side, tier, z + D(2, 4), D(1.2f, span - 2)), D(.8f, 1.6f));
        }
    }
    // ---------- house styles ----------
    enum Look { Island, Venetian, Moorish, Tuscan, Merchant }
    sealed class Style
    {
        public Look look; public Color wall, accent; public int window; public bool tower, quoins, pilasters, rustic, bands, tiles, lines, pediment;
    }
    static Style style;
    static readonly Look[][] districtLooks =
    {
        new[] { Look.Tuscan, Look.Merchant, Look.Venetian, Look.Merchant, Look.Island, Look.Tuscan },
        new[] { Look.Moorish, Look.Venetian, Look.Moorish, Look.Island, Look.Tuscan },
        new[] { Look.Island, Look.Tuscan, Look.Moorish, Look.Venetian, Look.Island },
        new[] { Look.Island, Look.Venetian, Look.Island, Look.Moorish, Look.Tuscan },
    };
    static Color Pick(params string[] hex) => P(hex[deco.Next(hex.Length)]);
    static Style MakeStyle(int tier, bool tower)
    {
        var looks = districtLooks[Mathf.Clamp(tier, 0, 3)];
        var st = new Style { look = looks[deco.Next(looks.Length)], tower = tower, lines = Chance(.6f) };
        switch (st.look)
        {
            case Look.Island:
                st.wall = plasters[deco.Next(plasters.Length)]; st.accent = Pick("3E6E9C", "2E7C7A", "5B87B8"); st.window = Chance(.85f) ? 0 : 5; break;
            case Look.Venetian:
                st.wall = Pick("E9B7A2", "E7C49A", "DE9F86", "F0D9C4", "E4AE93"); st.accent = Pick("F4E6CB", "C9725A"); st.window = Chance(.5f) ? 4 : 2;
                st.bands = Chance(.7f); st.rustic = Chance(.4f); st.quoins = Chance(.3f); break;
            case Look.Moorish:
                st.wall = Pick("F3E7D3", "E8CFA0", "EBD9BC", "F1DEC2"); st.accent = Pick("2E9C9C", "268F98", "3E8F8A"); st.window = 3; st.tiles = true; break;
            case Look.Tuscan:
                st.wall = Pick("E3B26C", "D9905F", "E8C48A", "CF8A63", "E0A872"); st.accent = Pick("F4E6CB", "EAD8B6"); st.window = 1;
                st.pediment = Chance(.5f); st.quoins = Chance(.7f); st.rustic = Chance(.6f); st.pilasters = Chance(.35f); break;
            default:
                st.wall = Pick("EEDFC4", "E2C8A0", "F2E6CF", "E8D3B0"); st.accent = P("7A5340"); st.window = 1; st.pilasters = Chance(.3f); break;
        }
        shutterTint = st.look == Look.Island ? st.accent : st.look == Look.Moorish ? P("7A5340") : shutters[deco.Next(shutters.Length)];
        return st;
    }
    static void Building(Vector3 foot, float yaw, float width, float depth, float height, float riser, int tier, bool openLeft, bool openRight, bool tower = false, Look? look = null, bool free = false)
    {
        // Local frame: origin at the street-level centre of the facade; -z faces the river, +z runs into the hill.
        buildings++; var previous = frame; frame = Matrix4x4.TRS(foot, Quaternion.Euler(0, yaw, 0), Vector3.one);
        // Front corners stand on this tier's terrace; back corners reach the retaining wall; the plinth spans the ground line.
        int side = foot.x > Center(foot.z) ? 1 : -1;
        if (!free) foreach (var corner in new[] { new Vector3(-width * .5f, 0, 0), new Vector3(width * .5f, 0, 0), new Vector3(-width * .5f, 0, depth), new Vector3(width * .5f, 0, depth) })
        {
            var w = frame.MultiplyPoint3x4(corner); float off = (w.x - Edge(side, tier, w.z)) * side, wall = (Edge(side, tier + 1, w.z) - Edge(side, tier, w.z)) * side;
            float floor = Level(side, tier, w.z);
            bool placed = corner.z == 0 ? off > -.3f && off < wall : off > wall + .05f;
            if (!placed || floor < foot.y - 2.4f || floor > foot.y + 1) ungrounded++;
        }
        var oldTint = tint; style = MakeStyle(tier, tower); var st = style;
        if (look.HasValue && st.look != look.Value) { for (int i = 0; i < 12 && style.look != look.Value; i++) style = MakeStyle(tier, tower); st = style; }
        int ground = st.look == Look.Merchant ? 1 : st.look == Look.Venetian && Chance(.4f) ? 2 : deco.Next(10) < (tier == 0 ? 3 : 1) ? 2 : deco.Next(2);   // 0 doors, 1 shopfront, 2 portico
        float groundH = D(3.7f, 4.4f), floorH = D(3.3f, 3.7f);
        int stacks = tower ? (tier < 3 && !free ? 2 : 1) : height > 20 ? 3 : height > 12.5f ? 2 : 1;
        var tops = new float[stacks]; var fronts = new float[stacks];
        for (int k = 0; k < stacks; k++)
        {
            tops[k] = k == stacks - 1 ? height : tower ? riser - .3f : height * (stacks == 2 ? D(.5f, .68f) : k == 0 ? D(.3f, .42f) : D(.62f, .76f));
            fronts[k] = k == 0 || tower ? 0 : Mathf.Min(fronts[k - 1] + (Chance(.25f) || st.look == Look.Merchant ? 0 : D(.8f, 2.2f)), depth - 4);
        }
        int cols = Mathf.Max(1, Mathf.FloorToInt((width - 1) / (st.look == Look.Venetian ? 2.9f : 2.5f)));
        float Col(int i) => -width * .5f + (i + .5f) * width / cols;
        // A tower shaft above the next street stands clear of it.
        float Back(int k) => tower && k == 1 ? depth - 1.45f : depth;
        for (int k = 0; k < stacks; k++)
        {
            float yb = k == 0 ? -2.5f : tops[k - 1], yt = tops[k], f = fronts[k], back = Back(k), mid = (f + back) * .5f;
            tint = Color.Lerp(st.wall, Color.white, k * .06f); aoBase = k == 0 ? foot.y : float.NaN; aoSpan = 3.5f;
            Box(new Vector3(0, (yb + yt) * .5f, mid), new Vector3(width, yt - yb, back - f), plaster, true, false);
            tint = Color.white; aoBase = float.NaN;
            if (st.tiles) { tint = st.accent; Box(new Vector3(0, yt - .55f, f - .03f), new Vector3(width, .4f, .08f), trim); tint = Color.white; }
            Box(new Vector3(0, yt - .14f, mid - .2f), new Vector3(width + .3f, .3f, back - f + .4f), trim, true);
            if (k == 0) Box(new Vector3(0, .15f, mid - .12f), new Vector3(width + .16f, .55f, depth + .24f), trim, true);
            else
            {
                float ledge = f - fronts[k - 1];
                if (ledge > .5f)
                {
                    Parapet(width, yb, fronts[k - 1] + .15f);
                    if (Chance(.5f)) Planter(new Vector3(D(-width * .35f, width * .35f), yb, fronts[k - 1] + ledge * .55f), Mathf.Min(.6f, ledge * .3f));
                    if (Chance(.25f)) Bougainvillea(new Vector3(D(-width * .3f, width * .3f), yb + .7f, fronts[k - 1] - .1f), Vector3.back, D(.9f, 1.4f));
                }
            }
            Ornament(width, yb < 0 ? 0 : yb, yt, f, k == 0 ? groundH : 0, cols);
        }
        var floors = new List<float> { 0 };
        for (float y = groundH; y < height - 2.6f; y += floorH) floors.Add(y);
        int oriel = st.look == Look.Merchant || (st.look == Look.Moorish && Chance(.6f)) ? deco.Next(cols) : -1;
        foreach (float y in floors)
        {
            int k = 0; while (k < stacks - 1 && y >= tops[k] - .5f) k++;
            float f = fronts[k] - .02f;
            if (y == 0) { GroundFloor(ground, width, groundH, cols, f); continue; }
            if (st.lines && y - (k == 0 ? 0 : tops[k - 1]) > .5f) { tint = st.bands ? st.accent : Color.white; Box(new Vector3(0, y - .05f, f - .05f), new Vector3(width, .16f, .14f), trim, true); tint = Color.white; }
            bool balcony = Chance(st.look == Look.Venetian ? .3f : .2f) && y < height - 3.2f;
            float bw = width * D(.45f, .85f), bx = D(-(width - bw) * .5f, (width - bw) * .5f);
            for (int i = 0; i < cols; i++)
            {
                if (i == oriel && y < height - 3) { Oriel(new Vector3(Col(i), y, f), Mathf.Min(width / cols + .3f, 2.4f), Mathf.Min(floorH - .4f, 2.8f)); continue; }
                bool door = balcony && Mathf.Abs(Col(i) - bx) < bw * .5f - .4f;
                Window(new Vector3(Col(i), y + (door ? .15f : .85f), f), door ? .95f : .85f, door ? 2.15f : 1.5f, !door || Chance(.5f), door && st.window != 3 ? 0 : -1);
            }
            if (balcony) Balcony(new Vector3(bx, y, f), bw);
            else if (Chance(.12f)) Bougainvillea(new Vector3(Col(deco.Next(cols)), y + .6f, f - .1f), Vector3.back, D(.8f, 1.3f));
            foreach (int s in new[] { -1, 1 })
            {
                if (s < 0 ? !openLeft : !openRight) continue;
                var saved = frame;
                frame *= Matrix4x4.TRS(new Vector3(s * width * .5f, 0, 0), Quaternion.Euler(0, s * -90, 0), Vector3.one);
                Window(new Vector3(s * (fronts[k] + 1.7f), y + .85f, -.02f), .8f, 1.45f, true);
                if (depth - fronts[k] > 7.5f) Window(new Vector3(s * (fronts[k] + 4.6f), y + .85f, -.02f), .8f, 1.45f, true);
                frame = saved;
            }
        }
        if (st.look == Look.Island && !tower && Chance(.35f) && floors.Count > 1) OutsideStair(width, groundH, Chance(.5f) ? 1 : -1);
        if (tower) Belfry(width, Back(stacks - 1), height, Mathf.Min(width, Back(stacks - 1) - 1.2f));
        else Roof(width, depth, height, riser, tier, fronts[stacks - 1], foot);
        // Retaining wall showing above the roof reads as cliff dwellings rather than bare stone.
        if (free)
        {
            // Freestanding: the back gets windows too.
            var saved = frame; frame *= Matrix4x4.TRS(new Vector3(0, 0, depth), Quaternion.Euler(0, 180, 0), Vector3.one);
            foreach (float y in floors) if (y > 0) for (int i = 0; i < cols; i++) Window(new Vector3(-Col(i), y + .85f, -.02f), .85f, 1.5f, true);
            frame = saved;
        }
        else if (tier < 3 && riser - height > 6 && !tower)
        {
            float wall = depth - 1.26f; style = null;
            for (float y = height + 2.6f; y < riser - 3; y += D(3.6f, 4.4f))
            {
                int n = Chance(.4f) ? 2 : 1;
                for (int i = 0; i < n; i++) Window(new Vector3(D(-width * .32f, width * .32f), y, wall), .8f, 1.45f, true);
            }
            if (Chance(.3f)) IvyCurtain(new Vector3(D(-width * .3f, width * .3f), riser - .5f, wall - .05f), Vector3.back, D(2.5f, 5), D(2.5f, riser - height - 1.5f));
        }
        if (Chance(.45f)) Vine(new Vector3(-width * .3f, height, -.3f), R(2.5f, 5));
        tint = oldTint; aoBase = float.NaN; style = null;
        frame = previous;
    }
    // Facade dressing per style: quoins, pilasters, rusticated base, marble bands.
    static void Ornament(float width, float yb, float yt, float f, float groundH, int cols)
    {
        var st = style; var old = tint;
        if (st.quoins)
            for (float y = yb + .2f; y < yt - .5f; y += .62f)
                foreach (int s in new[] { -1, 1 })
                {
                    tint = Color.Lerp(st.accent, Color.white, .3f); bool wide = Mathf.RoundToInt(y / .62f) % 2 == 0;
                    Box(new Vector3(s * (width * .5f - (wide ? .4f : .25f)), y + .27f, f - .04f), new Vector3(wide ? .8f : .5f, .52f, .1f), trim);
                }
        if (st.pilasters)
            for (int i = 0; i <= cols; i++)
            {
                float x = -width * .5f + i * width / cols; tint = Color.Lerp(st.wall, Color.white, .35f);
                Box(new Vector3(Mathf.Clamp(x, -width * .5f + .2f, width * .5f - .2f), (Mathf.Max(yb, groundH) + yt) * .5f, f - .06f), new Vector3(.36f, yt - Mathf.Max(yb, groundH) - .3f, .14f), trim);
            }
        if (st.rustic && groundH > 0)
        {
            tint = Color.Lerp(st.wall, P("8A6F55"), .25f);
            for (float y = .7f; y < groundH - .3f; y += .5f) Box(new Vector3(0, y, f - .02f), new Vector3(width, .05f, .06f), trim);
        }
        if (st.bands)
            for (float y = Mathf.Max(yb, groundH) + .3f; y < yt - 1; y += 1.15f) { tint = Color.Lerp(st.wall, st.accent, .55f); Box(new Vector3(0, y, f - .015f), new Vector3(width, .18f, .03f), trim); }
        tint = old;
    }
    static void Parapet(float width, float y, float z)
    {
        var st = style; var old = tint; tint = Color.white;
        if (st != null && (st.look == Look.Moorish || st.look == Look.Venetian))
        {
            Box(new Vector3(0, y + .3f, z), new Vector3(width, .6f, .3f), trim, true);
            for (float x = -width * .5f + .3f; x < width * .5f - .2f; x += .9f)
            {
                Box(new Vector3(x, y + .85f, z), new Vector3(.45f, .5f, .3f), trim, true);
                if (st.look == Look.Moorish) Box(new Vector3(x, y + 1.25f, z), new Vector3(.25f, .3f, .3f), trim, true);
                else Box(new Vector3(x, y + 1.2f, z), new Vector3(.15f, .2f, .15f), trim);
            }
        }
        else if (st != null && st.look == Look.Island && Chance(.5f))
        {
            Box(new Vector3(0, y + .2f, z), new Vector3(width, .4f, .3f), trim, true);
            Rail(new Vector3(-width * .5f + .1f, y + .38f, z), new Vector3(width * .5f - .1f, y + .38f, z));
        }
        else Box(new Vector3(0, y + .45f, z), new Vector3(width, .9f, .3f), trim, true);
        tint = old;
    }
    static void GroundFloor(int type, float width, float h, int cols, float f)
    {
        int door = cols / 2; var st = style;
        float Col(int i) => -width * .5f + (i + .5f) * width / cols;
        if (type == 2) Arcade(new Vector3(0, 0, f - 1.25f), width - .3f, 3.1f, 2.4f);
        for (int i = 0; i < cols; i++)
        {
            if (i == door)
            {
                if (st.look == Look.Moorish) { Window(new Vector3(Col(i), .42f, f - .04f), 1.4f, 2.2f, false, 3); tint = shutterTint; Box(new Vector3(Col(i), 1.5f, f - .02f), new Vector3(1.3f, 2.1f, .06f), shutter); tint = Color.white; }
                else Opening(new Vector3(Col(i), .42f, f - .04f), 1.3f, 2.4f, true);
                WallLamp(new Vector3(Col(i) + .95f, 2.4f, f - .08f));
            }
            else if (type == 1) Opening(new Vector3(Col(i), .42f, f - .04f), Mathf.Min(1.7f, width / cols - .7f), 2.2f, true);
            else Window(new Vector3(Col(i), 1.05f, f), .85f, 1.5f, true);
        }
        if (type == 1)
        {
            Awn(new Vector3(0, 3.15f, f - .06f), width * .8f, 2);
            if (Chance(.6f)) Sign(new Vector3(width * .5f - .6f, 3.6f, f));
        }
        else if (type == 0 && Chance(.4f)) Planter(new Vector3(Col(door) + (Chance(.5f) ? 1.4f : -1.4f), 0, f - .6f), .45f);
    }
    static void Sign(Vector3 p)
    {
        var old = tint; tint = Palette.Brass;
        Beam(p, p + new Vector3(0, 0, -1.1f), .05f, brass);
        tint = Pick("B66547", "2F5F8F", "63774C", "DAAE57");
        Box(p + new Vector3(0, -.45f, -.75f), new Vector3(.06f, .6f, .7f), wood);
        tint = old;
    }
    // Window by style: 0 round arch, 1 framed (pedimented), 2 lancet, 3 horseshoe in tile frame, 4 paired lancets, 5 oculus.
    static void Window(Vector3 p, float w, float h, bool shutters, int kind = -1)
    {
        var st = style; if (kind < 0) kind = st == null ? 0 : st.window;
        var old = tint; tint = Color.white; float r = w * .5f; float z = p.z - .03f;
        if (kind == 5) { p.y += h * .3f; h = w; }
        void Recess(float x0, float x1, float y0, float y1) => Quad(new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z), recess);
        void Fan(Vector3 c, float radius, float a0, float a1, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n), b = Mathf.Lerp(a0, a1, (i + 1f) / n);
                Tri(c, c + new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, 0), c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0), recess);
            }
        }
        void Lancet(float x, float half)
        {
            Recess(x - half, x + half, p.y, p.y + h);
            var apex = new Vector3(x, p.y + h + half * 1.5f, z);
            Tri(new Vector3(x - half, p.y + h, z), apex, new Vector3(x + half, p.y + h, z), recess);
            Beam(new Vector3(x - half - .06f, p.y + h - .05f, p.z), apex + Vector3.up * .08f, .11f, trim); Beam(new Vector3(x + half + .06f, p.y + h - .05f, p.z), apex + Vector3.up * .08f, .11f, trim);
            Box(new Vector3(x - half - .06f, p.y + h * .5f, p.z), new Vector3(.11f, h, .12f), trim); Box(new Vector3(x + half + .06f, p.y + h * .5f, p.z), new Vector3(.11f, h, .12f), trim);
        }
        switch (kind)
        {
            case 1:
                Recess(p.x - r, p.x + r, p.y, p.y + h);
                foreach (int s in new[] { -1, 1 }) Box(new Vector3(p.x + s * (r + .07f), p.y + h * .5f, p.z - .04f), new Vector3(.14f, h + .1f, .12f), trim);
                Box(new Vector3(p.x, p.y + h + .08f, p.z - .08f), new Vector3(w + .5f, .16f, .26f), trim, true);
                if (st != null && st.pediment)
                {
                    var a = new Vector3(p.x - r - .25f, p.y + h + .16f, p.z - .1f); var b = new Vector3(p.x + r + .25f, a.y, a.z); var c = new Vector3(p.x, a.y + .42f, a.z);
                    Tri(a, c, b, trim); Beam(a, c, .08f, trim); Beam(b, c, .08f, trim);
                }
                break;
            case 2: Lancet(p.x, r); break;
            case 3:
                {
                    var c = new Vector3(p.x, p.y + h - r * .15f, z); float rr = r * 1.12f;
                    Recess(p.x - r, p.x + r, p.y, c.y); Fan(c, rr, -.6f, Mathf.PI + .6f, 8);
                    tint = st != null && st.tiles ? st.accent : P("2E9C9C");
                    Box(new Vector3(p.x, c.y + rr + .14f, p.z - .02f), new Vector3(w + .7f, .2f, .07f), trim);
                    foreach (int s in new[] { -1, 1 }) Box(new Vector3(p.x + s * (r + .3f), (p.y + c.y + rr + .2f) * .5f, p.z - .02f), new Vector3(.2f, c.y + rr + .2f - p.y, .07f), trim);
                    tint = Color.white;
                    if (shutters && Chance(.4f))
                    {
                        // Carved cedar screen box.
                        tint = P("7A5340");
                        Box(new Vector3(p.x, p.y + h * .45f, p.z - .32f), new Vector3(w + .3f, h * .9f, .5f), wood, true);
                        for (int i = 1; i < 4; i++) Box(new Vector3(p.x, p.y + h * .45f - h * .45f + i * h * .225f, p.z - .58f), new Vector3(w + .3f, .05f, .03f), wood);
                        tint = Color.white; shutters = false;
                    }
                    else shutters = false;
                    break;
                }
            case 4:
                {
                    float half = w * .26f;
                    Lancet(p.x - half - .07f, half); Lancet(p.x + half + .07f, half);
                    Cylinder(new Vector3(p.x, p.y, p.z - .06f), .06f, h, trim, false, 6);
                    Arch(new Vector3(p.x, p.y + h, p.z), r + .3f, r + .55f, .12f, .14f, trim, false, 8);
                    shutters = false; break;
                }
            case 5:
                {
                    var c = new Vector3(p.x, p.y + r, z); Fan(c, r, 0, Mathf.PI * 2, 10);
                    Arch(new Vector3(p.x, p.y + r, p.z), r, r, .12f, .14f, trim, false, 6);
                    var saved = frame; frame *= Matrix4x4.TRS(new Vector3(p.x, p.y + r, p.z), Quaternion.Euler(0, 0, 180), Vector3.one);
                    Arch(Vector3.zero, r, r, .12f, .14f, trim, false, 6); frame = saved;
                    shutters = false; break;
                }
            default:
                Recess(p.x - r, p.x + r, p.y, p.y + h); Fan(new Vector3(p.x, p.y + h, z), r, 0, Mathf.PI, 6);
                Arch(new Vector3(p.x, p.y + h, p.z), r, r, .12f, .14f, trim, false, 6); break;
        }
        if (kind != 5) Box(new Vector3(p.x, p.y - .07f, p.z - .1f), new Vector3(w + .3f, .14f, .24f), trim, true);
        if (shutters)
        {
            tint = shutterTint;
            foreach (int s in new[] { -1, 1 }) Box(new Vector3(p.x + s * (r + w * .26f), p.y + h * .5f, p.z - .05f), new Vector3(w * .48f, h * .96f, .05f), shutter);
        }
        tint = old;
    }
    // Jettied timber bay window; its hood is a handhold.
    static void Oriel(Vector3 p, float width, float h)
    {
        var old = tint; tint = style.look == Look.Moorish ? P("7A5340") : P("8C5E44");
        Box(new Vector3(p.x, p.y + .45f + h * .5f, p.z - .45f), new Vector3(width, h, .9f), wood, true);
        Corbel(new Vector3(p.x - width * .3f, p.y + .35f, p.z - .4f), 0); Corbel(new Vector3(p.x + width * .3f, p.y + .35f, p.z - .4f), 0);
        tint = Color.white;
        for (int i = 0; i < 2; i++)
        {
            float x = p.x + (i - .5f) * width * .48f, zz = p.z - .92f;
            Quad(new Vector3(x - width * .18f, p.y + .9f, zz), new Vector3(x - width * .18f, p.y + .45f + h - .3f, zz), new Vector3(x + width * .18f, p.y + .45f + h - .3f, zz), new Vector3(x + width * .18f, p.y + .9f, zz), recess);
        }
        tint = Color.Lerp(Color.white, P("E7A07E"), .3f);
        var a = new Vector3(p.x - width * .5f - .15f, p.y + .45f + h, p.z - 1.05f); var b = a + Vector3.right * (width + .3f);
        Quad(a, a + new Vector3(0, .5f, 1.05f), b + new Vector3(0, .5f, 1.05f), b, roof, true);
        tint = Pick("C0704E", "B66547"); Box(new Vector3(p.x, p.y + .82f, p.z - 1.02f), new Vector3(width * .9f, .22f, .22f), clay);
        for (int k = 0; k < 4; k++) { tint = Chance(.5f) ? Palette.Bougainvillea : P("6E8B4A"); Ellipsoid(new Vector3(p.x + D(-width * .4f, width * .4f), p.y + 1f, p.z - 1.05f), new Vector3(.2f, .17f, .17f), blossom, 6, 4); }
        tint = old;
    }
    // Island-style stair climbing the facade to a first-floor door.
    static void OutsideStair(float width, float rise, int dir)
    {
        var old = tint; tint = Color.Lerp(style.wall, Color.white, .2f);
        int steps = Mathf.CeilToInt(rise / .26f); float run = Mathf.Min(width - 1.2f, steps * .3f), x0 = -dir * width * .45f;
        for (int i = 0; i < steps; i++)
        {
            float x = x0 + dir * run * (i + .5f) / steps, y = rise * (i + 1f) / steps;
            Box(new Vector3(x, y * .5f, -.55f), new Vector3(run / steps + .02f, y, 1.1f), plaster, true);
        }
        Box(new Vector3(x0 + dir * (run + .6f), rise * .5f, -.7f), new Vector3(1.4f, rise, 1.4f), plaster, true);
        Box(new Vector3(x0 + dir * run * .5f, rise + .45f, -1.1f), new Vector3(run + 1.4f, .9f, .16f), plaster, true);
        tint = Color.white; Opening(new Vector3(x0 + dir * (run + .6f), rise, -.04f), 1.1f, 2.2f, true);
        tint = old;
    }
    static void Balcony(Vector3 p, float width)
    {
        var old = tint; tint = Color.white;
        Box(new Vector3(p.x, p.y - .1f, p.z - .6f), new Vector3(width, .22f, 1.2f), trim, true);
        Corbel(new Vector3(p.x - width * .35f, p.y - .25f, p.z - .7f), 0); Corbel(new Vector3(p.x + width * .35f, p.y - .25f, p.z - .7f), 0);
        var look = style?.look ?? Look.Island;
        if (look == Look.Venetian)
        {
            // Gothic loggia rail: colonnettes under a stone handrail.
            Box(new Vector3(p.x, p.y + .9f, p.z - 1.13f), new Vector3(width, .12f, .18f), trim, true);
            for (float x = -width * .5f + .1f; x <= width * .5f; x += .35f) Cylinder(new Vector3(p.x + x, p.y, p.z - 1.13f), .05f, .85f, trim, false, 6);
        }
        else if (look == Look.Moorish || (look != Look.Tuscan && Chance(.45f)))
        {
            tint = look == Look.Moorish ? P("7A5340") : P("3E4A57");
            Beam(new Vector3(p.x - width * .5f, p.y + .9f, p.z - 1.13f), new Vector3(p.x + width * .5f, p.y + .9f, p.z - 1.13f), .06f, wood, true);
            for (float x = -width * .5f + .05f; x <= width * .5f; x += .42f) Box(new Vector3(p.x + x, p.y + .45f, p.z - 1.13f), new Vector3(.04f, .9f, .04f), wood);
        }
        else
        {
            Box(new Vector3(p.x, p.y + .45f, p.z - 1.13f), new Vector3(width, .9f, .14f), trim, true);
            foreach (int s in new[] { -1, 1 }) Box(new Vector3(p.x + s * (width * .5f - .07f), p.y + .45f, p.z - .6f), new Vector3(.14f, .9f, 1.1f), trim, true);
        }
        tint = old;
        if (Chance(.3f)) Bougainvillea(new Vector3(p.x - width * .35f, p.y + .8f, p.z - 1.2f), Vector3.back, D(.9f, 1.4f));
        else if (Chance(.3f)) Planter(new Vector3(p.x + width * .3f, p.y + .01f, p.z - .65f), .32f);
        else if (Chance(.25f)) Laundry(new Vector3(p.x - width * .45f, p.y + 1.6f, p.z - 1.15f), new Vector3(p.x + width * .45f, p.y + 1.6f, p.z - 1.15f));
    }
    static void Laundry(Vector3 a, Vector3 b)
    {
        var old = tint; tint = P("6A5A44"); Beam(a, b, .02f, wood);
        int n = Mathf.FloorToInt(Vector3.Distance(a, b) / .55f); wind = .8f;
        for (int i = 0; i < n; i++)
        {
            var c = Vector3.Lerp(a, b, (i + .5f) / n); float w = D(.3f, .45f), h = D(.4f, .8f);
            tint = Pick("F4EBDD", "B66547", "5B87B8", "DAAE57", "F3E9D4", "63774C");
            Quad(c + new Vector3(-w * .5f, 0, 0), c + new Vector3(w * .5f, 0, 0), c + new Vector3(w * .5f, -h, 0), c + new Vector3(-w * .5f, -h, 0), cloth);
            Quad(c + new Vector3(-w * .5f, -h, 0), c + new Vector3(w * .5f, -h, 0), c + new Vector3(w * .5f, 0, 0), c + new Vector3(-w * .5f, 0, 0), cloth);
        }
        wind = 0; tint = old;
    }
    static void Roof(float width, float depth, float height, float riser, int tier, float front, Vector3 foot)
    {
        var st = style;
        float mid = (front + depth - 1.2f) * .5f, visible = depth - 1.2f - front;
        tint = P("D9B48A");
        Box(new Vector3(0, height + .08f, mid + .6f), new Vector3(width - .4f, .16f, depth - front - .4f), paving, true);
        tint = Color.white;
        bool terrace = tier < 3 && riser - height < 3.6f;
        Vector3 Spot2(float fz) => new Vector3(D(-width * .2f, width * .2f), height + .16f, front + Mathf.Max(2.2f, visible * fz));
        if (terrace || (st.look != Look.Tuscan && st.look != Look.Merchant && Chance(.4f)))
        {
            Parapet(width, height, front + .15f);
            var saved = frame; frame *= Matrix4x4.Translate(new Vector3(0, 0, mid));
            if (Chance(.6f)) RoofGarden(width, visible, height);
            else if (Chance(.4f)) Cypress(new Vector3(D(-width * .3f, width * .3f), height + .16f, visible * .2f), D(3.5f, 6));
            frame = saved;
            if (st.look == Look.Venetian && !terrace) for (int i = 0; i < 2; i++) Chimney(Spot2(D(.55f, .9f)), true);
            if (Spotty(.3f)) Spot(frame.MultiplyPoint3x4(new Vector3(width * .2f, height + .6f, mid)), districts[tier] + " roof terrace", false);
            return;
        }
        switch (st.look)
        {
            case Look.Tuscan: case Look.Merchant:
                {
                    // Tile roof into the wall behind: hipped with rafter-tail eaves, or a steep gable.
                    bool gable = st.look == Look.Merchant;
                    float y = height + .25f, z0 = front - (gable ? .5f : 1.1f), z1 = depth, over = gable ? .4f : .9f;
                    var a = new Vector3(-width * .5f - over, y, z0); var b = new Vector3(width * .5f + over, y, z0);
                    var c = new Vector3(width * .5f + over, y, z1); var d = new Vector3(-width * .5f - over, y, z1);
                    float rise = gable ? Mathf.Min(4.2f, (z1 - z0) * .55f) : Mathf.Min(2.4f, (z1 - z0) * .3f);
                    var ridgeA = new Vector3(gable ? -width * .5f - over : -width * .5f + 1.4f, y + rise, (z0 + z1) * .5f); var ridgeB = new Vector3(gable ? width * .5f + over : width * .5f - 1.4f, y + rise, (z0 + z1) * .5f);
                    tint = Color.Lerp(Color.white, P("E7A07E"), D(0, .5f));
                    Quad(a, ridgeA, ridgeB, b, roof, true); Quad(c, ridgeB, ridgeA, d, roof, true);
                    if (gable) { tint = st.wall; Tri(new Vector3(-width * .5f, y, z0), new Vector3(-width * .5f, y, z1), new Vector3(-width * .5f, y + rise - .1f, (z0 + z1) * .5f), plaster, true); Tri(new Vector3(width * .5f, y, z1), new Vector3(width * .5f, y, z0), new Vector3(width * .5f, y + rise - .1f, (z0 + z1) * .5f), plaster, true); }
                    else { Tri(a, d, ridgeA, roof, true); Tri(b, ridgeB, c, roof, true); }
                    tint = Color.white; Beam(ridgeA + Vector3.up * .05f, ridgeB + Vector3.up * .05f, .22f, clay);
                    if (!gable) { tint = P("7A5340"); for (float x = -width * .5f + .3f; x < width * .5f; x += .7f) Box(new Vector3(x, y - .12f, front - .45f), new Vector3(.12f, .14f, 1), wood); }
                    else if (Chance(.6f))
                    {
                        // Dormer.
                        var dz = (z0 + z1) * .5f - rise * .1f; tint = st.wall; Box(new Vector3(0, y + rise * .45f, dz - 1.2f), new Vector3(1.4f, 1.5f, 1.2f), plaster, true);
                        Window(new Vector3(0, y + rise * .25f, dz - 1.82f), .7f, .8f, false, 1);
                        tint = Color.Lerp(Color.white, P("E7A07E"), .3f); Box(new Vector3(0, y + rise * .45f + .85f, dz - 1.2f), new Vector3(1.7f, .15f, 1.5f), roof, true);
                    }
                    tint = Color.white; Chimney(new Vector3(width * D(-.3f, .3f), y + rise * .4f, (z0 + z1) * .5f + 1), false);
                    break;
                }
            case Look.Island:
                if (Chance(.5f))
                {
                    // Barrel vault running across the facade.
                    float r = Mathf.Min(visible * .45f, 2.6f), zc = front + r + .4f;
                    tint = st.wall;
                    for (int i = 0; i < 10; i++)
                    {
                        float a0 = i * Mathf.PI / 10, a1 = (i + 1) * Mathf.PI / 10;
                        Vector3 V(float x, float a) => new Vector3(x, height + .16f + Mathf.Sin(a) * r, zc + Mathf.Cos(a) * r);
                        smooth = true; Quad(V(-width * .5f + .3f, a0), V(-width * .5f + .3f, a1), V(width * .5f - .3f, a1), V(width * .5f - .3f, a0), plaster, true); smooth = false;
                        foreach (float x in new[] { -width * .5f + .3f, width * .5f - .3f }) Tri(new Vector3(x, height + .16f, zc), V(x, x < 0 ? a0 : a1), V(x, x < 0 ? a1 : a0), plaster, true);
                    }
                    tint = Color.white;
                }
                else { var c = Spot2(.45f); float r = Mathf.Min(width, visible) * .28f; Cylinder(c, r + .25f, 1.2f, plaster, true, 20); Cylinder(c + Vector3.up * 1.2f, r + .45f, .25f, trim, true, 22); DomeStyled(c + Vector3.up * 1.45f, r, r * .8f); }
                Parapet(width, height, front + .15f);
                break;
            case Look.Moorish:
                {
                    Parapet(width, height, front + .15f);
                    var c = Spot2(.5f); float r = Mathf.Min(width, visible) * .26f;
                    tint = st.wall; Cylinder(c, r + .3f, 1.6f, plaster, true, 8);
                    tint = st.accent; Box(c + Vector3.up * 1.4f, new Vector3((r + .3f) * 2, .3f, (r + .3f) * 2), trim);
                    tint = Chance(.5f) ? st.accent : Color.white;
                    Lathe(c + Vector3.up * 1.6f, new[] { new Vector2(r * 1.05f, 0), new Vector2(r * 1.25f, r * .5f), new Vector2(r * 1.05f, r * 1.05f), new Vector2(r * .45f, r * 1.55f), new Vector2(.04f, r * 2.1f) }, 16, tint == Color.white ? brass : trim, true);
                    tint = Color.white; break;
                }
            default:
                {
                    // Venetian roof: crenellated parapet, funnel chimneys and a timber altana.
                    Parapet(width, height, front + .15f);
                    for (int i = 0; i < 2; i++) Chimney(Spot2(D(.6f, .9f)), true);
                    var c = Spot2(.3f); float w = Mathf.Min(width * .6f, 4.5f), d = Mathf.Min(visible * .6f, 3.5f);
                    tint = P("7A5340");
                    for (int i = 0; i < 4; i++) Beam(c + new Vector3((i % 2 == 0 ? -1 : 1) * w * .45f, 0, (i < 2 ? -1 : 1) * d * .45f), c + new Vector3((i % 2 == 0 ? -1 : 1) * w * .45f, 2.1f, (i < 2 ? -1 : 1) * d * .45f), .14f, wood, true);
                    Box(c + Vector3.up * 2.15f, new Vector3(w, .14f, d), wood, true);
                    Beam(c + new Vector3(-w * .5f, 3, -d * .5f), c + new Vector3(w * .5f, 3, -d * .5f), .07f, wood); 
                    for (float x = -w * .5f; x <= w * .5f; x += .5f) Beam(c + new Vector3(x, 2.2f, -d * .5f), c + new Vector3(x, 3, -d * .5f), .04f, wood);
                    if (Chance(.6f)) Laundry(c + new Vector3(-w * .45f, 3.6f, d * .3f), c + new Vector3(w * .45f, 3.6f, d * .3f));
                    tint = Color.white; break;
                }
        }
    }
    static void Chimney(Vector3 p, bool funnel)
    {
        var old = tint; tint = Color.Lerp(Color.white, P("E7A07E"), .4f);
        if (funnel) Lathe(p, new[] { new Vector2(.22f, 0), new Vector2(.2f, 1.4f), new Vector2(.45f, 1.9f), new Vector2(.5f, 2.2f), new Vector2(.35f, 2.25f) }, 8, clay, true);
        else { Box(p + Vector3.up * .8f, new Vector3(.6f, 1.6f, .6f), plaster, true); tint = Color.white; Box(p + Vector3.up * 1.65f, new Vector3(.8f, .12f, .8f), trim); }
        tint = old;
    }
    // Slim bell tower crowning a narrow house: belfry openings and a spire, dome or onion.
    static void Belfry(float width, float depth, float height, float size)
    {
        var st = style; float zc = Mathf.Min(depth - 1.2f, size) * .5f, top = height;
        tint = Color.white; Box(new Vector3(0, top - 3.6f, zc), new Vector3(width + .35f, .3f, size + .35f), trim, true);
        for (int s = 0; s < 4; s++)
        {
            var saved = frame; frame *= Matrix4x4.TRS(new Vector3(0, 0, zc), Quaternion.Euler(0, s * 90, 0), Vector3.one);
            float half = (s % 2 == 0 ? size : width) * .5f;
            Window(new Vector3(0, top - 3.2f, -half - .02f), Mathf.Min(1.3f, half), 1.9f, false, st.look == Look.Moorish ? 3 : st.look == Look.Venetian ? 4 : 0);
            frame = saved;
        }
        Box(new Vector3(0, top + .05f, zc), new Vector3(width + .4f, .3f, size + .4f), trim, true);
        var c = new Vector3(0, top + .2f, zc); float r = Mathf.Min(width, size) * .5f;
        switch (st.look)
        {
            case Look.Moorish: tint = st.accent; Lathe(c, new[] { new Vector2(r * .9f, 0), new Vector2(r * 1.1f, r * .5f), new Vector2(r * .9f, r), new Vector2(r * .35f, r * 1.6f), new Vector2(.03f, r * 2.3f) }, 16, trim, true); break;
            case Look.Island: tint = st.accent; Dome(c, r, r * .9f, trim, true); break;
            default:
                tint = Color.Lerp(Color.white, P("E7A07E"), .3f);
                Lathe(c, new[] { new Vector2(r * 1.15f, 0), new Vector2(r * .9f, .3f), new Vector2(.04f, r * 3.2f) }, 4, roof, true); break;
        }
        tint = Palette.Brass; Cylinder(c + Vector3.up * (r * (st.look == Look.Tuscan || st.look == Look.Merchant || st.look == Look.Venetian ? 3.2f : 2.2f)), .05f, 1.2f, brass, false, 6);
        tint = Color.white;
    }
    static bool Spotty(float p) => Chance(p);
    static void Spot(Vector3 p, string place, bool fixedSpot) => spots.Add((p, place, fixedSpot));
    static void DomeStyled(Vector3 bottom, float radius, float height)
    {
        int style = deco.Next(4);
        Material mat = style == 0 ? brass : roof; var old = tint;
        tint = style == 2 ? P("5B87B8") : style == 3 ? P("E0A487") : Color.white;
        if (style == 0) Dome(bottom, radius, height, brass, true);
        else { smooth = false; Dome(bottom, radius, height, roof, true); }
        tint = Color.white;
        Cylinder(bottom + Vector3.up * (height - .05f), radius * .12f, .5f, brass, false, 10);
        Ellipsoid(bottom + Vector3.up * (height + .62f), Vector3.one * radius * .1f, brass, 8, 5);
        tint = old;
    }
    static void Opening(Vector3 p, float width, float height, bool door, bool flowers = false)
    {
        var old = tint; tint = Color.white;
        Box(p + new Vector3(0, height * .5f, .02f), new Vector3(width, height, .08f), recess);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI / 10, b = (i + 1) * Mathf.PI / 10;
            Tri(p + Vector3.up * height, p + new Vector3(Mathf.Cos(a) * width * .5f, height + Mathf.Sin(a) * width * .5f, -.025f), p + new Vector3(Mathf.Cos(b) * width * .5f, height + Mathf.Sin(b) * width * .5f, -.025f), recess);
        }
        Arch(p + Vector3.up * height, width * .5f, width * .5f, .14f, .2f, trim, false, 12);
        Box(p + new Vector3(-width * .5f - .07f, height * .5f, -.06f), new Vector3(.14f, height, .2f), trim);
        Box(p + new Vector3(width * .5f + .07f, height * .5f, -.06f), new Vector3(.14f, height, .2f), trim);
        Box(p + new Vector3(0, .04f, -.12f), new Vector3(width + .45f, .18f, .38f), trim);
        tint = shutterTint;
        if (door) Box(p + new Vector3(0, height * .5f, -.01f), new Vector3(width * .92f, height * .98f, .06f), shutter);
        else
            for (int s = -1; s <= 1; s += 2)
            {
                var saved = frame;
                frame *= Matrix4x4.TRS(p + new Vector3(s * (width * .5f + .14f), 0, -.1f), Quaternion.Euler(0, s * -18, 0), Vector3.one);
                Box(new Vector3(s * width * .25f, height * .5f, 0), new Vector3(width * .48f, height * .95f, .05f), shutter);
                for (int k = 1; k < 4; k++) Box(new Vector3(s * width * .25f, height * k / 4f, -.03f), new Vector3(width * .44f, .04f, .02f), shutter);
                frame = saved;
            }
        tint = Color.white;
        if (flowers)
        {
            Box(p + new Vector3(0, -.12f, -.28f), new Vector3(width + .3f, .25f, .32f), clay);
            for (int k = 0; k < 6; k++)
            {
                tint = k % 3 == 0 ? P("6E8B4A") : Chance(.5f) ? Palette.Bougainvillea : Chance(.5f) ? P("E8708A") : P("F4EBDD");
                Ellipsoid(p + new Vector3(D(-width * .5f, width * .5f), .1f, -.3f + D(-.05f, .08f)), new Vector3(.18f, .16f, .16f), blossom, 6, 4);
            }
        }
        tint = old;
    }
    static void Rail(Vector3 a, Vector3 b)
    {
        Beam(a + Vector3.up * .82f, b + Vector3.up * .82f, .17f, trim, true);
        int n = Mathf.CeilToInt(Vector3.Distance(a, b) / .38f);
        ghost = true;
        for (int i = 0; i <= n; i++) Baluster(Vector3.Lerp(a, b, i / (float)n), .78f);
        ghost = false;
    }
    static void Arcade(Vector3 p, float width, float height, float depth)
    {
        int n = Mathf.Max(2, Mathf.RoundToInt(width / 3.3f)); float span = width / n;
        Box(p + new Vector3(0, height + span * .5f + .2f, 0), new Vector3(width + .4f, .4f, depth), trim, true);
        for (int i = 0; i <= n; i++)
        {
            var c = p + new Vector3(-width * .5f + i * span, 0, 0); Column(c, height, .22f);
            if (i < n) Arch(c + new Vector3(span * .5f, height, 0), span * .5f - .2f, span * .48f, .22f, .5f, trim, true, 16);
        }
    }
    static void Column(Vector3 p, float height, float radius)
    {
        Cylinder(p, radius * 1.6f, .3f, trim, true, 12);
        Cylinder(p + Vector3.up * .3f, radius, height - .55f, trim, true, 12, radius * .85f);
        Cylinder(p + Vector3.up * (height - .3f), radius * .9f, .18f, trim, false, 12, radius * 1.5f);
        Box(p + Vector3.up * (height - .05f), new Vector3(radius * 3, .2f, radius * 3), trim);
    }
    static void Pavilion(Vector3 p, float radius, float height, bool dome, int style = -1)
    { var old = frame; frame = Matrix4x4.TRS(p, Quaternion.Euler(0, R(0, 90), 0), Vector3.one); PavilionLocal(Vector3.zero, radius, height, dome, style < 0 ? deco.Next(3) : style); frame = old; }
    static void PavilionLocal(Vector3 p, float radius, float height, bool dome, int style)
    {
        var old = tint; tint = Color.white;
        Cylinder(p, radius + 1, .4f, trim, true, 32); Cylinder(p + Vector3.up * .4f, radius + .6f, .45f, paving, true, 32);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI / 5; var c = p + new Vector3(Mathf.Cos(a) * radius, .85f, Mathf.Sin(a) * radius);
            Column(c, height - .85f, .27f);
        }
        Cylinder(p + Vector3.up * height, radius + .5f, .45f, trim, true, 32);
        Cylinder(p + Vector3.up * (height + .45f), radius + .6f, .15f, brass, false, 32);
        if (dome)
        {
            if (style == 1) Dome(p + Vector3.up * (height + .6f), radius + .2f, radius * .7f, brass, true);
            else { tint = style == 2 ? P("5B87B8") : Color.white; Dome(p + Vector3.up * (height + .6f), radius + .2f, radius * .62f, roof, true); tint = Color.white; }
            Cylinder(p + Vector3.up * (height + .6f + radius * .6f), .2f, 1.2f, brass, false, 8);
        }
        else Cylinder(p + Vector3.up * (height + .6f), radius * .85f, .35f, trim, true, 32);
        if (Chance(.5f)) for (int i = 0; i < 3; i++) Wisteria(p + new Vector3(Mathf.Cos(i * 2.1f) * (radius + .4f), height - .05f, Mathf.Sin(i * 2.1f) * (radius + .4f)), 1.6f, 5);
        tint = old;
    }
    static void RoofGarden(float width, float depth, float height)
    {
        float y = height + .42f;
        if (Chance(.6f))
        {
            // Pergola with wisteria.
            float w = width * .5f, d = depth * .45f;
            for (int i = 0; i < 4; i++) Beam(new Vector3((i % 2 == 0 ? -1 : 1) * w * .5f, y, (i < 2 ? -1 : 1) * d * .5f), new Vector3((i % 2 == 0 ? -1 : 1) * w * .5f, y + 2.5f, (i < 2 ? -1 : 1) * d * .5f), .16f, wood);
            for (float x = -w * .5f; x <= w * .5f + .01f; x += .6f) Beam(new Vector3(x, y + 2.55f, -d * .55f), new Vector3(x, y + 2.55f, d * .55f), .1f, wood);
            Wisteria(new Vector3(0, y + 2.6f, 0), Mathf.Max(w, d) * .55f, 14);
        }
        else
        {
            for (int i = 0; i < 3; i++) Planter(new Vector3(D(-width * .3f, width * .3f), y, D(-depth * .3f, depth * .3f)), D(.45f, .7f));
            Parasol(new Vector3(width * .2f, y, -depth * .15f));
        }
    }
    static void Parasol(Vector3 p)
    {
        Cylinder(p, .05f, 2.3f, wood, false, 6);
        var old = tint; tint = Chance(.5f) ? P("F2E8D6") : Palette.Terracotta;
        Cylinder(p + Vector3.up * 2.05f, 1.5f, .45f, cloth, false, 12, .05f);
        tint = old;
        Box(p + new Vector3(0, .38f, 0), new Vector3(.9f, .06f, .9f), wood);
        Cylinder(p, .06f, .38f, wood, false, 6);
    }
    static void Awn(Vector3 p, float width, float depth)
    {
        int stripes = Mathf.Max(3, Mathf.RoundToInt(width / .55f)); var old = tint;
        Color main = Chance(.5f) ? Palette.Terracotta : Chance(.5f) ? Palette.Shutter : P("C9A04B");
        for (int i = 0; i < stripes; i++)
        {
            float x0 = -width * .5f + width * i / stripes, x1 = -width * .5f + width * (i + 1) / stripes;
            tint = i % 2 == 0 ? main : P("F3E9D4");
            var a = p + new Vector3(x0, 0, 0); var b = p + new Vector3(x1, 0, 0); var c = b + new Vector3(0, -.55f, -depth); var d = a + new Vector3(0, -.55f, -depth);
            Quad(a, b, c, d, cloth); Quad(d, c, b, a, cloth);
            Quad(d, c, c + Vector3.down * .3f, d + Vector3.down * .3f, cloth); Quad(d + Vector3.down * .3f, c + Vector3.down * .3f, c, d, cloth);
        }
        tint = old;
        var e = p + new Vector3(-width * .5f, -.55f, -depth); var f = p + new Vector3(width * .5f, -.55f, -depth);
        Beam(e - Vector3.up * 2.6f, e, .08f, wood); Beam(f - Vector3.up * 2.6f, f, .08f, wood); Beam(e, f, .07f, brass);
    }
    static void Market(Vector3 p, float yaw, float width)
    {
        var old = frame; frame = Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
        Box(new Vector3(0, .5f, 0), new Vector3(width, 1, 1), wood, true); Awn(new Vector3(0, 2.8f, .5f), width + .4f, 2.4f);
        var oldTint = tint;
        for (int i = 0; i < 3; i++)
        {
            Lathe(new Vector3(-width * .3f + i * width * .3f, 1, 0), new[] { new Vector2(.22f, 0), new Vector2(.36f, .12f), new Vector2(.38f, .2f) }, 10, clay);
            Color fruit = i == 0 ? P("F0A030") : i == 1 ? P("F2D44E") : P("B6463E");
            tint = fruit;
            for (int k = 0; k < 5; k++) Ellipsoid(new Vector3(-width * .3f + i * width * .3f + D(-.18f, .18f), 1.22f + D(0, .08f), D(-.18f, .18f)), Vector3.one * .1f, blossom, 6, 4);
            tint = oldTint;
        }
        Box(new Vector3(width * .5f + .5f, .3f, .4f), new Vector3(.6f, .6f, .6f), wood);
        frame = old;
    }
    static void Stairs(Vector3 a, Vector3 b, float width)
    {
        var old = frame; var delta = b - a; var flat = new Vector3(delta.x, 0, delta.z);
        int count = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta.y) / .23f)); float tread = flat.magnitude / count;
        frame = Matrix4x4.TRS(a, Quaternion.LookRotation(flat), Vector3.one);
        for (int i = 0; i < count; i++)
        {
            float y = delta.y * (i + 1f) / count;
            Box(new Vector3(0, y - .14f, (i + .5f) * tread), new Vector3(width, .28f, tread + .025f), paving);
            if (i % 5 == 0) Box(new Vector3(width * .5f - .18f, y + .18f, (i + .5f) * tread), new Vector3(.3f, .36f, tread), trim, true);
            if (i % 9 == 4 && Chance(.5f)) Planter(new Vector3(-width * .5f + .3f, y, (i + .5f) * tread), .28f);
        }
        var stairFrame = frame;
        frame *= Matrix4x4.TRS(new Vector3(0, delta.y * .5f + .06f, flat.magnitude * .5f), Quaternion.Euler(-Mathf.Atan2(delta.y, flat.magnitude) * Mathf.Rad2Deg, 0, 0), Vector3.one);
        Box(Vector3.zero, new Vector3(width, .12f, delta.magnitude), paving, true);
        frame = stairFrame;
        for (int side = -1; side <= 1; side += 2)
        {
            Beam(new Vector3(side * (width * .5f - .1f), -.4f, 0), new Vector3(side * (width * .5f - .1f), delta.y - .4f, flat.magnitude), .38f, trim);
            Beam(new Vector3(side * (width * .5f - .12f), .7f, 0), new Vector3(side * (width * .5f - .12f), delta.y + .7f, flat.magnitude), .14f, trim, true);
            for (int i = 0; i <= count; i += 7)
            {
                float t = i / (float)count; var pos = new Vector3(side * (width * .5f - .12f), delta.y * t, flat.magnitude * t);
                Beam(pos, pos + Vector3.up * .7f, .16f, trim);
            }
        }
        frame = old;
    }
    sealed class Route
    {
        public int side; public Vector3 a, b, mid, turnIn, turnOut, firstEnd, secondStart, mast, anchor;
        public Vector3 Lip(int i) => Vector3.Lerp(a, b, i / 9f) + new Vector3(-side * 2.6f, 1, 0);
    }
    static Route StairRoute(int side, int tier, float z)
    {
        var r = new Route { side = side, a = Point(side, tier, z - 14, 3), b = Point(side, tier + 1, z + 14, -1) };
        r.b.y += .14f;
        float rise = r.b.y - r.a.y;
        r.mid = Vector3.Lerp(r.a, r.b, .5f);
        r.mid.z = r.b.z + Mathf.Max(18, rise * .9f);
        r.mid.x = (Edge(side, tier, r.mid.z) + Edge(side, tier + 1, r.mid.z)) * .5f - side * 3.5f;
        r.turnIn = r.mid - Vector3.right * side * 2.8f; r.turnOut = r.mid + Vector3.right * side * 2.8f;
        r.firstEnd = r.turnIn - new Vector3(r.turnIn.x - r.a.x, 0, r.turnIn.z - r.a.z).normalized * 3f;
        var travel = new Vector3(r.b.x - r.turnOut.x, 0, r.b.z - r.turnOut.z).normalized;
        r.secondStart = r.turnOut + travel * 3f;
        r.mast = new Vector3(r.mid.x + side * 7, Level(side, tier, r.mid.z + 4), r.mid.z + 4);
        r.anchor = r.mid + Vector3.up * 7 + Vector3.right * (-side * 2);
        return r;
    }
    static void StairStreet(int side, int tier, float z)
    {
        // Long switchbacks run along the retaining wall on piers, with open landings at the turns.
        var r = StairRoute(side, tier, z);
        float Ground(Vector3 p) => Level(side, tier, p.z) - .3f;
        var route = new GameObject($"Stair route {side} {tier} {z}").transform; route.SetParent(city, false);
        foreach (var pair in new[] { ("A", r.a), ("Turn", r.turnIn), ("Out", r.turnOut), ("B", r.b) }) { var node = new GameObject(pair.Item1).transform; node.SetParent(route, false); node.position = pair.Item2; }
        Stairs(r.a, r.firstEnd, 3.2f);
        Cylinder(r.turnIn + Vector3.down * .4f, 3, .4f, paving, true, 32);
        Cylinder(r.turnOut + Vector3.down * .4f, 3, .4f, paving, true, 32);
        Box(r.mid + Vector3.down * .2f, new Vector3(5.6f, .4f, 4), paving, true);
        var travel = (r.secondStart - r.turnOut).normalized;
        Stairs(r.secondStart, r.b - travel * 3.6f, 3.2f);
        Cylinder(r.b + Vector3.down * .4f, 3.6f, .4f, paving, true, 32);
        Piers(r.a, r.firstEnd, Ground); Piers(r.secondStart, r.b - travel * 3.6f, Ground);
        foreach (var c in new[] { r.turnIn, r.turnOut })
            for (int i = 0; i < 3; i++)
            {
                var foot = c + new Vector3(Mathf.Cos(i * 2.1f + .5f), 0, Mathf.Sin(i * 2.1f + .5f)) * 2.1f; foot.y = Ground(foot);
                Column(foot, c.y - .4f - foot.y, .32f);
            }
        foreach (float dz in new[] { -1.8f, 1.8f })
        {
            var foot = r.b + new Vector3(-side * 2.4f, 0, dz); foot.y = Ground(foot);
            Column(foot, r.b.y - .4f - foot.y, .32f);
        }
        Lamp(r.turnIn + Vector3.right * side * 2.2f); Lamp(r.b + Vector3.forward * 2.8f);
        Spot(r.mid + Vector3.up * .05f, districts[tier + 1] + " switchback", false);
        for (int i = 1; i < 9; i++)
        {
            // Stepping columns climb beside the stair.
            var c = r.Lip(i); float g = Ground(c);
            Column(new Vector3(c.x, g, c.z), c.y - .3f - g, .5f);
            Box(c - Vector3.up * .15f, new Vector3(1.6f, .3f, 2.1f), trim, true);
            Beam(c + new Vector3(-side * .65f, .02f, -.8f), c + new Vector3(-side * .65f, .02f, .8f), .07f, brass);
        }
        var top = new Vector3(r.mast.x, r.mid.y + 8, r.mast.z);
        Beam(r.mast, top, .3f, wood, true);
        Beam(top, r.anchor + Vector3.up * 1.5f, .3f, wood, true); Anchor(r.anchor);
        Spot(r.anchor + Vector3.down * 2.6f + Vector3.forward * 2.2f, "Swing ring above the " + districts[tier + 1], true);
    }
    static void Piers(Vector3 from, Vector3 to, Func<Vector3, float> ground)
    {
        var flat = new Vector3(to.x - from.x, 0, to.z - from.z); var across = Vector3.Cross(Vector3.up, flat.normalized);
        int n = Mathf.FloorToInt(flat.magnitude / 6);
        for (int i = 1; i <= n; i++)
        {
            var p = Vector3.Lerp(from, to, i / (n + 1f)); float g = ground(p);
            if (p.y - g < 2.4f) continue;
            foreach (int s in new[] { -1, 1 })
            {
                var q = p + across * s * 1.25f;
                Box(new Vector3(q.x, (g + p.y - .4f) * .5f, q.z), new Vector3(.7f, p.y - .4f - g, .7f), trim, true);
            }
        }
    }
    static readonly (float z, int tier, float width)[] BridgeList = { (-12, 0, 4.2f), (87, 1, 4.8f), (188, 2, 5.7f), (272, 3, 6.5f), (316, 0, 3.5f) };
    // Deck ramps between the two street levels; the arch is tilted with it.
    static (Vector3 a, Vector3 b, float length, float rise) BridgeGeometry(float z, int tier)
    {
        var a = Point(-1, tier, z, 3) + Vector3.up * .2f; var b = Point(1, tier, z + 8, 3) + Vector3.up * .2f;
        var d = b - a; float length = d.magnitude;
        return (a, b, length, Mathf.Min(Mathf.Min(a.y, b.y) - 1, length * .31f));
    }
    static void Bridge(float z, int tier, float width)
    {
        var (a, b, length, rise) = BridgeGeometry(z, tier); var d = b - a;
        var old = frame; frame = Matrix4x4.TRS((a + b) * .5f, Quaternion.Euler(0, -Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, 0) * Quaternion.Euler(0, 0, Mathf.Asin(d.y / length) * Mathf.Rad2Deg), Vector3.one);
        Box(new Vector3(0, -.35f, 0), new Vector3(length + .8f, .7f, width), paving, true);
        Arch(new Vector3(0, -rise - .7f, 0), length * .5f, rise, 1.1f, width * .8f, trim, true, 48);
        for (int side = -1; side <= 1; side += 2)
        {
            Rail(new Vector3(-length * .5f, .03f, side * (width * .5f - .1f)), new Vector3(length * .5f, .03f, side * (width * .5f - .1f)));
            Arch(new Vector3(0, -rise - .65f, side * width * .42f), length * .5f, rise, .18f, .12f, brass, false, 48);
            for (int k = -2; k <= 2; k++)
            {
                if (k == 0) continue;
                Banner(new Vector3(k * length * .18f, -.3f, side * (width * .5f + .08f)), side, k % 2 == 0 ? Palette.Terracotta : Palette.Shutter);
            }
            if (Chance(.6f)) for (int k = 0; k < 4; k++) Wisteria(new Vector3(D(-length * .4f, length * .4f), -.7f, side * (width * .5f + .2f)), 1.5f, 6);
        }
        for (int i = -1; i <= 1; i++)
        {
            var c = new Vector3(i * length * .24f, -2, 0);
            Anchor(frame.MultiplyPoint3x4(c));
        }
        Lamp(new Vector3(-length * .5f + 1.2f, 0, width * .5f - .6f)); Lamp(new Vector3(length * .5f - 1.2f, 0, -width * .5f + .6f));
        Spot(frame.MultiplyPoint3x4(new Vector3(length * .08f, .05f, 0)), districts[tier] + " bridge", true);
        frame = old;
        if (tier > 0)
        {
            var pa = a + Vector3.left * 4; pa.y = Level(-1, tier, pa.z); var pb = b + Vector3.right * 4; pb.y = Level(1, tier, pb.z);
            Pavilion(pa, 3, 7, false); Pavilion(pb, 3, 7, true);
        }
    }
    static void Banner(Vector3 top, int side, Color color)
    {
        var old = tint; tint = color; wind = .6f;
        Vector3 A(float x, float y) => top + new Vector3(x, y, 0);
        for (int s = 0; s < 4; s++)
        {
            float y0 = -s * .55f, y1 = -(s + 1) * .55f, w = .45f;
            Quad(A(-w, y0), A(w, y0), A(w, y1), A(-w, y1), cloth); Quad(A(-w, y1), A(w, y1), A(w, y0), A(-w, y0), cloth);
        }
        Tri(A(-.45f, -2.2f), A(.45f, -2.2f), A(0, -2.6f), cloth); Tri(A(0, -2.6f), A(.45f, -2.2f), A(-.45f, -2.2f), cloth);
        wind = 0; tint = Palette.Brass;
        Box(A(0, -.33f), new Vector3(.12f, .12f, .03f), brass);
        tint = old;
    }
    static void Anchor(Vector3 p)
    {
        var root = new GameObject("Brass swing ring " + (++anchors)); root.transform.SetParent(city, false); root.transform.position = p;
        root.AddComponent<TraversalAnchor>();
        var old = frame; frame = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one);
        Torus(Vector3.zero, .42f, .065f, brass, 22, 8);
        Beam(new Vector3(0, .42f, 0), new Vector3(0, 1.6f, 0), .06f, wood); frame = old;
    }
    static void Boat()
    {
        // Rowing boat: curved hull from cross sections.
        const int n = 9; var rings = new Vector3[n, 5];
        for (int i = 0; i < n; i++)
        {
            float t = i / (n - 1f), z = (t - .5f) * 4.2f, w = Mathf.Sin(t * Mathf.PI) * .78f + .04f, lift = Mathf.Pow(Mathf.Abs(t - .5f) * 2, 3) * .35f;
            rings[i, 0] = new Vector3(-w, .45f + lift, z); rings[i, 1] = new Vector3(-w * .8f, .05f + lift * .6f, z);
            rings[i, 2] = new Vector3(0, -.12f + lift * .5f, z); rings[i, 3] = new Vector3(w * .8f, .05f + lift * .6f, z); rings[i, 4] = new Vector3(w, .45f + lift, z);
        }
        var old = tint; tint = P("8C5E44");
        Smooth(() =>
        {
            for (int i = 0; i < n - 1; i++) for (int j = 0; j < 4; j++)
                {
                    Quad(rings[i, j], rings[i, j + 1], rings[i + 1, j + 1], rings[i + 1, j], wood);
                    Vector3 In(Vector3 v) => new Vector3(v.x * .9f, v.y + .05f, v.z * .97f);
                    Quad(In(rings[i + 1, j]), In(rings[i + 1, j + 1]), In(rings[i, j + 1]), In(rings[i, j]), wood);
                }
        });
        tint = P("2F5F8F");
        for (int i = 0; i < n - 1; i++) { Beam(rings[i, 0], rings[i + 1, 0], .07f, wood); Beam(rings[i, 4], rings[i + 1, 4], .07f, wood); }
        tint = P("D9B48A");
        for (int i = -1; i <= 1; i++) Box(new Vector3(0, .3f, i * 1.1f), new Vector3(1.3f, .07f, .32f), wood);
        tint = P("C9A27A");
        Beam(new Vector3(-1.1f, .7f, -.4f), new Vector3(1.4f, .2f, .9f), .06f, wood);
        tint = old;
    }
    static void ParkourCourt()
    {
        // Dedicated alley off the lower west quay: walls, lips, launch and swing landing.
        Vector3 p = Point(-1, 0, 35, 5); float y = p.y;
        Box(new Vector3(p.x - 1.2f, y - .25f, 34), new Vector3(4.5f, .5f, 23), paving, true);
        tint = P("EBD3AA");
        Box(new Vector3(p.x - 3, y + 3, 35), new Vector3(.7f, 6, 15), plaster, true);
        tint = P("F5EAD6");
        Box(new Vector3(p.x + .7f, y + 4.5f, 37), new Vector3(.7f, 9, 12), plaster, true);
        tint = Color.white; shutterTint = Palette.Shutter;
        for (int side = -1; side <= 1; side += 2)
        {
            var old = frame; frame = Matrix4x4.TRS(new Vector3(p.x + (side < 0 ? -2.62f : .32f), y, 35), Quaternion.Euler(0, side < 0 ? -90 : 90, 0), Vector3.one);
            for (int j = -1; j <= 1; j++) Opening(new Vector3(j * 3.5f, .8f, 0), .75f, 1.7f, false, j == 0);
            Box(new Vector3(0, 5.5f, 0), new Vector3(12, .24f, .3f), trim);
            frame = old;
        }
        for (int i = 0; i < 5; i++)
        {
            float top = y + 1.65f * (i + 1);
            Box(new Vector3(p.x - 2.5f, top - .15f, 30 + i * 2.1f), new Vector3(1.6f, .3f, 2), trim, true);
            Box(new Vector3(p.x - 1.67f, top - .03f, 30 + i * 2.1f), new Vector3(.07f, .1f, 1.7f), brass);
        }
        Box(new Vector3(p.x - 1, y + .48f, 26), new Vector3(2.4f, .96f, .65f), trim, true);
        var launch = new Vector3(p.x - 1, y + 9, 42);
        Box(launch - Vector3.up * .2f, new Vector3(4, .4f, 3), paving, true);
        var land = launch + new Vector3(0, 0, 12);
        Box(land - Vector3.up * .2f, new Vector3(4, .4f, 4), paving, true);
        Beam(launch + new Vector3(-3, 0, 6), launch + new Vector3(-3, 8, 6), .35f, wood, true);
        Beam(launch + new Vector3(-3, 8, 6), launch + new Vector3(0, 8, 6), .3f, wood, true);
        Anchor(launch + new Vector3(0, 6.5f, 6));
        Wisteria(new Vector3(p.x - 2.6f, y + 6, 33), 1.4f, 8);
        Spot(launch + Vector3.right * .8f, "Swing alley launch", true);
        Spot(land + Vector3.forward * .6f, "Swing alley landing", true);
        Spot(new Vector3(p.x - 2.5f, y + 1.65f * 5 + .1f, 30 + 4 * 2.1f), "Top of the brass lips", true);
        var marker = new GameObject("Route · Saffron wall and swing alley"); marker.transform.SetParent(city, false); marker.transform.position = p;
    }

    // ---------- vegetation and props ----------
    static void Planter(Vector3 p, float radius)
    {
        var old = tint; tint = Color.Lerp(Color.white, P("E0A487"), D(0, .6f));
        Lathe(p, new[] { new Vector2(radius * .75f, 0), new Vector2(radius, radius * .55f), new Vector2(radius * 1.06f, radius * .85f), new Vector2(radius * 1.12f, radius * .95f), new Vector2(radius * .95f, radius * .97f) }, 12, clay, true);
        int kind = deco.Next(3);
        wind = .7f;
        if (kind == 0)
        {
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.4f, r = radius * .55f * Mathf.Sqrt(i / 9f);
                var b = p + new Vector3(Mathf.Cos(a) * r, radius * .9f, Mathf.Sin(a) * r);
                tint = P("6F8550"); Limb(b, b + new Vector3(D(-.08f, .08f), radius * 1.1f, D(-.08f, .08f)), .03f, .02f, leaf, 4);
                tint = Color.Lerp(P("8E79C8"), P("A893DD"), D(0, 1));
                Ellipsoid(b + Vector3.up * radius * 1.2f, new Vector3(.06f, radius * .28f, .06f), blossom, 5, 4);
            }
        }
        else
        {
            tint = Color.Lerp(P("6E8B4A"), P("8FA45E"), D(0, 1));
            Ellipsoid(p + Vector3.up * radius * 1.05f, new Vector3(radius * .95f, radius * .7f, radius * .95f), leaf, 10, 6, .1f);
            if (kind == 1)
                for (int i = 0; i < 7; i++)
                {
                    tint = Chance(.5f) ? Palette.Bougainvillea : Chance(.5f) ? P("F08A5D") : P("F6EEE0");
                    float a = D(0, 6.3f);
                    Ellipsoid(p + new Vector3(Mathf.Cos(a) * radius * .6f, radius * 1.4f + D(0, .2f), Mathf.Sin(a) * radius * .6f), Vector3.one * radius * .2f, blossom, 6, 4);
                }
        }
        wind = 0; tint = old;
    }
    static void Tree(Vector3 p, float size, int type)
    {
        if (BlockedCircle(p, 1.8f * size, 6 * size)) return;
        if (type == 2) { Cypress(p, 5 * size); return; }
        if (type == 1) { Palm(p, size); return; }
        OliveTree(p, size);
    }
    static void OliveTree(Vector3 p, float size)
    {
        if (BlockedCircle(p, 1.8f * size, 6 * size)) return;
        var old = frame; frame = Matrix4x4.TRS(p, Quaternion.Euler(0, D(0, 360), 0), Vector3.one * size);
        var oldTint = tint;
        tint = P("7C6A55");
        var points = new List<Vector3> { Vector3.zero };
        for (int i = 1; i <= 3; i++) points.Add(points[i - 1] + new Vector3(D(-.5f, .5f), 1.05f, D(-.5f, .5f)));
        for (int i = 0; i < 3; i++) Limb(points[i], points[i + 1], .32f - i * .06f, .26f - i * .06f, wood, 8);
        var top = points[3];
        int crowns = deco.Next(5, 8);
        for (int i = 0; i < crowns; i++)
        {
            float a = i * Mathf.PI * 2 / crowns + D(-.3f, .3f), r = D(1.1f, 2.1f);
            var end = top + new Vector3(Mathf.Cos(a) * r, D(.2f, 1.4f), Mathf.Sin(a) * r);
            tint = P("7C6A55"); Limb(top, end, .14f, .07f, wood, 6);
            tint = Color.Lerp(P("7D9160"), P("A3B183"), D(0, 1)); wind = 1;
            Ellipsoid(end + Vector3.up * .3f, new Vector3(D(1.2f, 1.7f), D(.75f, 1.05f), D(1.2f, 1.7f)), leaf, 11, 7, .12f);
            wind = 0;
        }
        tint = Color.Lerp(P("7D9160"), P("A3B183"), .4f); wind = 1;
        Ellipsoid(top + Vector3.up * 1.2f, new Vector3(1.7f, 1.1f, 1.7f), leaf, 11, 7, .1f);
        wind = 0; tint = oldTint; frame = old;
    }
    static void Cypress(Vector3 p, float height)
    {
        var old = tint; tint = Color.Lerp(P("4F6D45"), P("3A5537"), D(0, 1)); wind = .5f;
        float w = height * .13f;
        Lathe(p, new[] { new Vector2(.12f, 0), new Vector2(w * .7f, height * .08f), new Vector2(w, height * .28f), new Vector2(w * .95f, height * .5f), new Vector2(w * .7f, height * .72f), new Vector2(w * .35f, height * .9f), new Vector2(.02f, height) }, 10, cypressMat, false, .12f, .35f);
        wind = 0; tint = old;
    }
    static void Shrubs(Vector3 p, float size)
    {
        var old = tint; wind = .6f;
        for (int i = 0; i < 4; i++)
        {
            tint = Color.Lerp(P("6E8B4A"), P("93A862"), D(0, 1));
            Ellipsoid(p + new Vector3(D(-size, size) * .6f, size * .35f, D(-size, size) * .6f), new Vector3(D(.6f, 1), D(.45f, .7f), D(.6f, 1)) * size * .55f, leaf, 8, 5, .12f);
        }
        if (Chance(.4f)) Bougainvillea(p + Vector3.up * size * .7f, Vector3.up, size * .6f);
        wind = 0; tint = old;
    }
    static void Palm(Vector3 p, float size)
    {
        var old = frame; frame = Matrix4x4.TRS(p, Quaternion.Euler(0, D(0, 360), 0), Vector3.one * size);
        var oldTint = tint; tint = P("9C8263");
        var lean = new Vector3(D(.4f, .9f), 0, 0); Vector3 last = Vector3.zero;
        for (int i = 1; i <= 5; i++) { var next = new Vector3(lean.x * i * i / 25f, i * 1.2f, 0); Limb(last, next, .24f - i * .02f, .22f - i * .02f, wood, 7); last = next; }
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 2 / 9 + D(-.2f, .2f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var side = Vector3.Cross(Vector3.up, dir) * .32f;
            tint = Color.Lerp(P("6D8E4A"), P("90AA5C"), D(0, 1));
            Vector3 prev = last;
            for (int s = 1; s <= 5; s++)
            {
                float t = s / 5f;
                var pnt = last + dir * (t * 3.2f) + Vector3.up * (Mathf.Sin(t * 2.6f) * .9f - t * t * 1.2f);
                float wdt = Mathf.Sin(t * Mathf.PI) + .1f;
                wind = t;
                Quad(prev - side * wdt, pnt - side * wdt, pnt + side * wdt, prev + side * wdt, leaf);
                Quad(prev + side * wdt, pnt + side * wdt, pnt - side * wdt, prev - side * wdt, leaf);
                prev = pnt;
            }
            wind = 0;
        }
        tint = oldTint; frame = old;
    }
    static void Wisteria(Vector3 p, float spread, int count)
    {
        var old = tint;
        for (int i = 0; i < count; i++)
        {
            var q = p + new Vector3(D(-spread, spread), D(-.1f, .05f), D(-spread, spread) * .6f);
            float length = D(.6f, 1.5f);
            tint = Color.Lerp(P("9C80D2"), P("C2ADEB"), D(0, 1)); wind = 1;
            Lathe(q, new[] { new Vector2(.2f, 0), new Vector2(.24f, -length * .2f), new Vector2(.16f, -length * .6f), new Vector2(.06f, -length * .9f), new Vector2(.01f, -length) }, 6, blossom, false, .15f);
            tint = P("7C9A55"); wind = .6f;
            Ellipsoid(q + Vector3.up * .05f, new Vector3(.32f, .14f, .3f), leaf, 6, 4);
        }
        wind = 0; tint = old;
    }
    static void Bougainvillea(Vector3 p, Vector3 outward, float size)
    {
        var old = tint; var side = Vector3.Cross(Vector3.up, outward.normalized);
        for (int i = 0; i < 9; i++)
        {
            var q = p + side * D(-size, size) + Vector3.down * D(0, size * 1.6f) + outward.normalized * D(.05f, .35f);
            tint = Chance(.7f) ? Color.Lerp(Palette.Bougainvillea, P("E66AA0"), D(0, 1)) : P("6E8B4A"); wind = .8f;
            Ellipsoid(q, new Vector3(D(.35f, .6f), D(.3f, .5f), D(.25f, .4f)) * size * .8f, blossom, 7, 5, .15f);
        }
        wind = 0; tint = old;
    }
    static void IvyCurtain(Vector3 top, Vector3 outward, float width, float length)
    {
        var old = tint; var side = Vector3.Cross(Vector3.up, outward);
        for (int i = 0; i < width * 1.6f; i++)
        {
            float s = D(-width * .5f, width * .5f), drop = D(.4f, 1) * length;
            tint = Color.Lerp(P("5F7A44"), P("7F9658"), D(0, 1)); wind = .5f;
            Ellipsoid(top + side * s + Vector3.down * drop * .5f + outward * .4f, new Vector3(.9f, drop * .5f, .45f), leaf, 7, 5, .15f);
        }
        if (Chance(.4f)) Bougainvillea(top + outward * .5f, outward, 1.4f);
        wind = 0; tint = old;
    }
    static void Lamp(Vector3 p)
    {
        var old = tint; tint = P("3E4A57");
        Cylinder(p, .09f, 2.6f, wood, false, 6, .06f);
        Cylinder(p, .2f, .25f, wood, false, 8, .14f);
        Beam(p + new Vector3(0, 2.55f, 0), p + new Vector3(.35f, 2.65f, 0), .05f, wood);
        tint = Palette.Brass;
        Cylinder(p + new Vector3(.4f, 2.05f, 0), .14f, .45f, brass, false, 6, .1f);
        Cylinder(p + new Vector3(.4f, 2.5f, 0), .17f, .14f, brass, false, 6, .02f);
        tint = Color.white;
        Ellipsoid(p + new Vector3(.4f, 2.27f, 0), new Vector3(.1f, .16f, .1f), glow, 6, 4);
        tint = old;
    }
    static void WallLamp(Vector3 p)
    {
        var old = tint; tint = Palette.Brass;
        Box(p + new Vector3(0, 0, .08f), new Vector3(.08f, .3f, .08f), brass);
        Cylinder(p + new Vector3(0, -.1f, -.15f), .11f, .32f, brass, false, 6, .08f);
        tint = Color.white; Ellipsoid(p + new Vector3(0, .05f, -.15f), new Vector3(.07f, .11f, .07f), glow, 6, 4);
        tint = old;
    }
    static void Vine(Vector3 p, float length, Vector3 outward = default)
    {
        if (outward == default) outward = Vector3.back;
        vines.Add((frame.MultiplyPoint3x4(p), frame.MultiplyVector(outward).normalized, length));
    }
    static bool VineRoom(Vector3 foot, float height) => !Physics.CheckCapsule(foot + Vector3.up * .34f, foot + Vector3.up * (height - .3f), .3f, 1, QueryTriggerInteraction.Ignore);
    static bool VineSweep(Vector3 from, Vector3 to, float height)
    {
        if (!VineRoom(from, height) || !VineRoom(to, height)) return false;
        var d = to - from;
        return d.sqrMagnitude < .001f || !Physics.CapsuleCast(from + Vector3.up * .34f, from + Vector3.up * (height - .3f), .3f, d.normalized, d.magnitude, 1, QueryTriggerInteraction.Ignore);
    }
    static bool VineRoute(Vector3 source, Vector3 facing, float requestedLength, out Vector3 top, out Vector3 outward, out float length)
    {
        top = source; outward = facing; length = requestedLength;
        var tangent = Vector3.Cross(Vector3.up, facing);
        foreach (float side in new[] { 0f, .8f, -.8f, 1.6f, -1.6f, 3f, -3f, 5f, -5f }) foreach (float inset in new[] { .5f, 1f, 2f, 3.5f })
            {
                var sample = source + tangent * side - facing * inset;
                if (!Physics.Raycast(sample + Vector3.up * 3, Vector3.down, out var floor, 6, 1, QueryTriggerInteraction.Ignore) || floor.normal.y < .62f) continue;
                var outside = floor.point + facing * 8 - Vector3.up * .15f;
                if (!Physics.Raycast(outside, -facing, out var wall, 8.5f, 1, QueryTriggerInteraction.Ignore) || Mathf.Abs(wall.normal.y) > .3f || Vector3.Dot(wall.normal, facing) < .6f) continue;
                var normal = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
                var edge = wall.point + Vector3.up * .15f;
                if (!Physics.Raycast(edge - normal * .12f + Vector3.up * .6f, Vector3.down, out var lip, 1.4f, 1, QueryTriggerInteraction.Ignore) || lip.normal.y < .62f) continue;
                edge.y = lip.point.y;
                if (Vector3.Distance(edge, source) > 8) continue;
                if (!Physics.Raycast(edge - normal * .8f + Vector3.up * 1.3f, Vector3.down, out var landing, 2.6f, 1, QueryTriggerInteraction.Ignore) || landing.normal.y < .62f) continue;
                var end = landing.point + Vector3.up * (.06f + .3f * (1 / landing.normal.y - 1));
                bool path = false;
                foreach (float height in new[] { 1.8f, 1.02f })
                {
                    float lift = Mathf.Max(edge.y, end.y) + .35f;
                    var high = edge + normal * .65f; high.y = lift;
                    var across = new Vector3(end.x, lift, end.z);
                    if (VineSweep(high, across, height) && VineSweep(across, end, height)) { path = true; break; }
                }
                if (!path) continue;
                for (float span = requestedLength; span >= 1.2f; span -= .4f)
                {
                    var low = edge + normal * .65f - Vector3.up * (span * 6 / 7 + 1.4f);
                    var high = edge + normal * .65f + Vector3.up * .4f;
                    if (!VineSweep(low, high, 1.8f)) continue;
                    top = edge + normal * .15f; outward = normal; length = span; return true;
                }
            }
        return false;
    }
    static void EmitVine(float length, int id)
    {
        var vine = new GameObject("Climbable hanging vine " + id).AddComponent<ClimbableVine>();
        vine.transform.SetParent(group, false); vine.transform.position = frame.MultiplyPoint3x4(Vector3.zero);
        vine.transform.rotation = Quaternion.LookRotation(frame.MultiplyVector(Vector3.back), Vector3.up);
        vine.length = length * 6 / 7 + .45f;
        var old = tint;
        int blobs = Mathf.CeilToInt(length * 3.2f);
        for (int i = 0; i < blobs; i++)
        {
            float t = i / (float)blobs;
            var v = new Vector3(Mathf.Sin(i * 1.7f) * .42f, -t * length - .1f, -.12f);
            bool flower = Chance(.18f);
            tint = flower ? Color.Lerp(P("9C80D2"), P("C2ADEB"), D(0, 1)) : Color.Lerp(P("5F7A44"), P("88A060"), D(0, 1)); wind = .4f;
            Ellipsoid(v, new Vector3(.42f, .38f, .26f) * D(.8f, 1.15f), flower ? blossom : leaf, 7, 5, .12f);
        }
        tint = P("6A5A44"); wind = 0;
        Limb(new Vector3(-.15f, 0, -.05f), new Vector3(.1f, -length, -.08f), .035f, .025f, wood, 4);
        Limb(new Vector3(.2f, 0, -.05f), new Vector3(-.05f, -length * .9f, -.1f), .03f, .02f, wood, 4);
        tint = old;
    }

    // ---------- mountain sanctuaries (west uplands) ----------
    static Vector3 PadCenter(int side, int index) { var pd = pads[side < 0 ? 0 : 1][index]; return UP(side, pd.u, pd.z); }
    static void Mountains()
    {
        const int W = -1;
        var top = GrandStairTop(W); var landing = UP(W, 14, top.z);
        var temple = PadCenter(W, 0); var palace = PadCenter(W, 1); var theatre = PadCenter(W, 2); var peak = PadCenter(W, 3); var colossus = PadCenter(W, 4);
        // Processional way from the stair to the acropolis, lined with obelisks and cypress.
        var templeFront = temple + Vector3.back * 56;
        Road(W, landing, templeFront, 6, true);
        Road(W, temple + Vector3.forward * 44, palace + Vector3.back * 44, 4.5f, false);
        Road(W, landing, theatre + Vector3.forward * 30, 4, false);
        Road(W, landing, colossus + Vector3.back * 12, 4, false);
        Acropolis(temple);
        Palace(palace);
        Theatre(theatre);
        Armillary(peak);
        Colossus(colossus);
        Aqueduct(W, temple + new Vector3(-8, -1, 44), palace + new Vector3(-6, 0, -42));
        // Groves on the slopes.
        for (int i = 0; i < 140; i++)
        {
            float u = D(24, 160), z = D(-50, 395); var p = UP(W, u, z);
            if (Near(W, u, z, 12)) continue;
            if (Chance(.65f)) Cypress(p - Vector3.up * .3f, D(5, 11)); else UmbrellaPine(p - Vector3.up * .2f, D(.9f, 1.4f));
        }
    }
    static bool Near(int side, float u, float z, float margin)
    {
        foreach (var pd in pads[side < 0 ? 0 : 1]) if ((u - pd.u) * (u - pd.u) + (z - pd.z) * (z - pd.z) < (pd.r + margin) * (pd.r + margin)) return true;
        return side > 0 && Mathf.Abs(u - RiverU(z)) < 10 + margin;
    }
    // Paved road draped over the uplands; avenue roads get obelisks and cypress pairs.
    static void Road(int side, Vector3 a, Vector3 b, float width, bool avenue, string color = null)
    {
        var dir = new Vector3(b.x - a.x, 0, b.z - a.z); float length = dir.magnitude; dir /= length;
        var across = Vector3.Cross(Vector3.up, dir) * width * .5f;
        int n = Mathf.CeilToInt(length / 3);
        Vector3 G(Vector3 p) { var crest = Crest(side, p.z); return new Vector3(p.x, UplandH(side, (p.x - crest.x) * side, p.z, crest.y) + .12f, p.z); }
        for (int i = 0; i < n; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)n); var q = Vector3.Lerp(a, b, (i + 1f) / n);
            tint = P(color ?? (avenue ? "EDE0C4" : "E2CFA8"));
            Quad(G(p - across), G(q - across), G(q + across), G(p + across), paving, true);
            Quad(G(p + across), G(q + across), G(q + across) + Vector3.down * .6f, G(p + across) + Vector3.down * .6f, trim, true);
            Quad(G(q - across), G(p - across), G(p - across) + Vector3.down * .6f, G(q - across) + Vector3.down * .6f, trim, true);
            if (avenue && i % 8 == 4)
                foreach (int s in new[] { -1, 1 })
                {
                    var o = G(p + across * s * 1.9f);
                    if (i % 16 == 4) Obelisk(o, D(11, 14)); else Cypress(o - Vector3.up * .2f, D(7, 10));
                }
            else if (!avenue && i % 12 == 6) Lamp(G(p + across * 1.3f));
        }
        tint = Color.white;
    }
    static void Obelisk(Vector3 foot, float height)
    {
        var old = tint; tint = P("F1E3C8");
        Box(foot + Vector3.up * .6f, new Vector3(2.2f, 1.2f, 2.2f), trim, true);
        Lathe(foot + Vector3.up * 1.2f, new[] { new Vector2(.75f, 0), new Vector2(.5f, height) }, 4, trim, true);
        tint = Palette.Brass; Lathe(foot + Vector3.up * (1.2f + height), new[] { new Vector2(.5f, 0), new Vector2(.02f, 1.1f) }, 4, brass, true);
        tint = old;
    }
    static void UmbrellaPine(Vector3 p, float size)
    {
        var old = tint; tint = P("7C6A55");
        var top = p + new Vector3(D(-1, 1), 6 * size, D(-1, 1));
        Limb(p, top, .35f * size, .22f * size, wood, 7);
        tint = Color.Lerp(P("4F6D45"), P("6E8B4A"), D(0, 1)); wind = .5f;
        Ellipsoid(top + Vector3.up * .6f * size, new Vector3(4.2f, 1.3f, 4.2f) * size, leaf, 12, 6, .12f);
        wind = 0; tint = old;
    }
    // Colossal peripteral temple on a walled podium, approached by a full-width stair.
    static void Acropolis(Vector3 c)
    {
        c += Vector3.up * 6;
        var old = frame; frame = Matrix4x4.Translate(c);
        tint = P("E9D9BB");
        // Raised podium; its walls drop into the hill so the platform reads as built, not floating.
        Box(new Vector3(0, -5.9f, 0), new Vector3(46, 12, 92), trim, true);
        for (int s = -1; s <= 1; s += 2) for (int i = -4; i <= 4; i++) Box(new Vector3(s * 23.4f, -5, i * 10), new Vector3(1.2f, 10, 1.6f), trim, true);
        tint = P("F3E8D2");
        for (int k = 0; k < 3; k++) Box(new Vector3(0, .1f + k * 1.2f + .6f, 0), new Vector3(34 - k * 2.4f, 1.2f, 72 - k * 2.4f), trim, true);
        float baseY = 3.7f, colH = 13.5f; int front = 8, flank = 17; float w = 28.6f, l = 66.6f;
        for (int i = 0; i < front; i++) foreach (int s in new[] { -1, 1 }) Column(new Vector3(-w * .5f + i * w / (front - 1), baseY, s * l * .5f), colH, 1.05f);
        for (int i = 1; i < flank - 1; i++) foreach (int s in new[] { -1, 1 }) Column(new Vector3(s * w * .5f, baseY, -l * .5f + i * l / (flank - 1)), colH, 1.05f);
        // Cella with a great bronze door.
        tint = P("EFE2C8"); Box(new Vector3(0, baseY + colH * .5f, 2), new Vector3(16, colH, 42), plaster, true);
        tint = Palette.Brass; Box(new Vector3(0, baseY + 4.5f, -19.05f), new Vector3(5, 9, .2f), brass, true);
        tint = Color.white; Box(new Vector3(0, baseY + 9.2f, -19.1f), new Vector3(6.4f, .5f, .4f), trim, true);
        // Entablature: architrave, brass-studded frieze, cornice.
        float e = baseY + colH;
        tint = P("F3E8D2"); Box(new Vector3(0, e + .7f, 0), new Vector3(w + 2.4f, 1.4f, l + 2.4f), trim, true);
        tint = P("E6D2AE"); Box(new Vector3(0, e + 2, 0), new Vector3(w + 2.2f, 1.2f, l + 2.2f), trim, true);
        tint = Palette.Brass; for (float z = -l * .5f; z <= l * .5f; z += 3.3f) foreach (int s in new[] { -1, 1 }) Box(new Vector3(s * (w * .5f + 1.15f), e + 2, z), new Vector3(.1f, .7f, .7f), brass);
        tint = Color.white; Box(new Vector3(0, e + 2.9f, 0), new Vector3(w + 3.4f, .6f, l + 3.4f), trim, true);
        // Marble roof with pediments and brass acroteria.
        float rY = e + 3.2f, ridge = 5.2f; var hw = w * .5f + 1.7f; var hl = l * .5f + 1.7f;
        tint = P("F6EEDD");
        Quad(new Vector3(-hw, rY, -hl), new Vector3(0, rY + ridge, -hl), new Vector3(0, rY + ridge, hl), new Vector3(-hw, rY, hl), roof, true);
        Quad(new Vector3(hw, rY, hl), new Vector3(0, rY + ridge, hl), new Vector3(0, rY + ridge, -hl), new Vector3(hw, rY, -hl), roof, true);
        foreach (int s in new[] { -1, 1 })
        {
            tint = P("EFE2C8"); var z = s * hl;
            QuadN(new Vector3(-hw, rY, z), new Vector3(0, rY + ridge, z), new Vector3(0, rY + ridge, z), new Vector3(hw, rY, z), Vector3.forward * s, trim);
            Tri(new Vector3(-hw, rY, z), new Vector3(0, rY + ridge, z), new Vector3(hw, rY, z), trim, true); Tri(new Vector3(hw, rY, z), new Vector3(0, rY + ridge, z), new Vector3(-hw, rY, z), trim, true);
            tint = Palette.Brass;
            Ellipsoid(new Vector3(0, rY + ridge + 1.2f, z), new Vector3(1.1f, 1.6f, .5f), brass, 10, 6);
            foreach (int t in new[] { -1, 1 }) Ellipsoid(new Vector3(t * hw, rY + .8f, z), new Vector3(.7f, 1, .4f), brass, 8, 5);
            // Pediment sculpture: a brass sun.
            Cylinder(new Vector3(0, rY + ridge * .42f, z + s * .1f), 1.3f, .2f, brass, false, 18);
        }
        // Grand stair down the front of the podium.
        tint = P("F3E8D2");
        for (int i = 0; i < 15; i++) Box(new Vector3(0, -(i + .5f) * .4f, -46.3f - i * .55f), new Vector3(20, .4f, .55f), paving, true);
        for (int s = -1; s <= 1; s += 2) { Box(new Vector3(s * 10.6f, -3.5f, -50.2f), new Vector3(1.2f, 7.2f, 8.4f), trim, true); Obelisk(new Vector3(s * 10.6f, .1f, -47), 8); }
        Spot(c + new Vector3(0, rY + ridge + .2f, 0), "Roof of the Great Temple", true);
        Spot(c + new Vector3(0, baseY + .1f, -26), "Temple colonnade", true);
        for (int i = 0; i < 4; i++) Anchor(c + new Vector3(i % 2 == 0 ? -12 : 12, e - 2.5f, i < 2 ? -38 : 38));
        frame = old; tint = Color.white;
    }
    // Palace of the Winds: stacked arcaded terraces, a great tiled dome, four minarets and a reflecting pool.
    static void Palace(Vector3 c)
    {
        var old = frame; frame = Matrix4x4.Translate(c);
        var saveStyle = style; style = new Style { look = Look.Moorish, wall = P("F3E7D3"), accent = P("2E9C9C"), window = 3, tiles = true };
        shutterTint = P("7A5340");
        float[] w = { 64, 46, 30 }, d = { 52, 38, 26 }, h = { 7, 7, 8 }; float y = 0;
        tint = P("E9D9BB"); Box(new Vector3(0, -4, 0), new Vector3(70, 8.2f, 58), trim, true);
        for (int k = 0; k < 3; k++)
        {
            tint = style.wall; Box(new Vector3(0, y + h[k] * .5f, 0), new Vector3(w[k], h[k], d[k]), plaster, true);
            tint = style.accent; Box(new Vector3(0, y + h[k] - .7f, 0), new Vector3(w[k] + .1f, .5f, d[k] + .1f), trim);
            tint = Color.white; Box(new Vector3(0, y + h[k] + .15f, 0), new Vector3(w[k] + .6f, .3f, d[k] + .6f), trim, true);
            // Arcades on all four faces of each terrace.
            for (int f = 0; f < 4; f++)
            {
                var saved = frame; frame *= Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.Euler(0, f * 90, 0), Vector3.one);
                float span = f % 2 == 0 ? w[k] : d[k], depthHalf = f % 2 == 0 ? d[k] * .5f : w[k] * .5f;
                int n = Mathf.FloorToInt(span / 4.2f);
                for (int i = 0; i < n; i++) Window(new Vector3(-span * .5f + (i + .5f) * span / n, 1.2f, -depthHalf - .02f), 1.6f, h[k] - 3.4f, false, 3);
                if (k < 2) { Parapet(span, h[k], -depthHalf + .2f); }
                frame = saved;
            }
            y += h[k];
            if (k < 2) for (int i = 0; i < 10; i++) { float a = i * Mathf.PI * .2f; Bougainvillea(new Vector3(Mathf.Cos(a) * w[k + 1] * .62f, y + .4f, Mathf.Sin(a) * d[k + 1] * .62f), new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), D(1.4f, 2.2f)); }
            if (k < 2) for (int i = 0; i < 6; i++) Tree(new Vector3(D(-w[k] * .45f, w[k] * .45f), y, (i % 2 == 0 ? -1 : 1) * (d[k + 1] * .5f + 2.5f)), D(.8f, 1.1f), 1);
        }
        // Drum and great dome.
        tint = style.wall; Cylinder(new Vector3(0, y, 0), 12, 5, plaster, true, 32);
        for (int i = 0; i < 16; i++) { var saved = frame; frame *= Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.Euler(0, i * 22.5f, 0), Vector3.one); Window(new Vector3(0, 1.3f, -12.02f), 1.1f, 2.2f, false, 0); frame = saved; }
        tint = Color.white; Cylinder(new Vector3(0, y + 5, 0), 12.6f, .5f, trim, true, 32);
        tint = style.accent; Dome(new Vector3(0, y + 5.5f, 0), 12.2f, 13, trim, true, 36);
        tint = Palette.Brass; Cylinder(new Vector3(0, y + 18.2f, 0), .5f, 2.5f, brass, true, 10); Ellipsoid(new Vector3(0, y + 21.3f, 0), Vector3.one * .9f, brass, 10, 6);
        Lathe(new Vector3(0, y + 22, 0), new[] { new Vector2(.12f, 0), new Vector2(.02f, 2.5f) }, 6, brass);
        Spot(c + new Vector3(0, y + .2f, 13.5f), "Dome terrace of the Palace of Winds", true);
        // Minarets with balconies and onion caps.
        foreach (var m in new[] { new Vector3(-33, 0, -27), new Vector3(33, 0, -27), new Vector3(-33, 0, 27), new Vector3(33, 0, 27) })
        {
            tint = style.wall; Cylinder(m + Vector3.down * 4, 2.2f, 4, plaster, true, 12); Cylinder(m, 1.7f, 30, plaster, true, 12, 1.4f);
            foreach (float b in new[] { 12f, 24f }) { tint = Color.white; Cylinder(m + Vector3.up * b, 2.6f, .4f, trim, true, 16); tint = style.accent; Cylinder(m + Vector3.up * (b + .4f), 2.5f, .9f, trim, false, 16); }
            tint = style.accent; Lathe(m + Vector3.up * 30, new[] { new Vector2(1.5f, 0), new Vector2(2.2f, 1.4f), new Vector2(1.6f, 2.8f), new Vector2(.5f, 4.2f), new Vector2(.04f, 5.6f) }, 16, trim, true);
            tint = Palette.Brass; Cylinder(m + Vector3.up * 35.5f, .08f, 1.6f, brass, false, 6);
            Anchor(c + m + new Vector3(0, 22, m.z < 0 ? -4 : 4));
        }
        // Reflecting pool and gateway (pishtaq) in front.
        tint = style.wall; Box(new Vector3(0, -.2f, -44), new Vector3(14, .6f, 30), trim, true);
        var waterTint = tint; tint = Color.white; Box(new Vector3(0, .02f, -44), new Vector3(11, .1f, 27), water, false); tint = waterTint;
        for (int i = 0; i < 4; i++) { tint = Color.white; Cylinder(new Vector3(0, 0, -54 + i * 7), .6f, .9f, trim, true, 12); tint = Palette.Brass; Ellipsoid(new Vector3(0, 1.2f, -54 + i * 7), new Vector3(.35f, .5f, .35f), brass, 8, 5); }
        foreach (int s in new[] { -1, 1 }) for (int i = 0; i < 5; i++) Tree(new Vector3(s * 9.5f, 0, -56 + i * 6), D(.85f, 1.1f), i % 2 == 0 ? 2 : 1);
        tint = style.wall; Box(new Vector3(0, 7, -61), new Vector3(16, 14, 4), plaster, true);
        var gate = frame; frame *= Matrix4x4.Translate(new Vector3(0, 0, -63.02f)); Window(Vector3.up * .3f, 7, 8, false, 3); frame = gate;
        tint = style.accent; Box(new Vector3(0, 13.4f, -61), new Vector3(16.4f, .8f, 4.4f), trim, true);
        Parapet(16, 14, -61);
        style = saveStyle; frame = old; tint = Color.white;
    }
    // Semicircular theatre facing the gorge, with a two-storey stage front.
    static void Theatre(Vector3 c)
    {
        var old = frame; frame = Matrix4x4.TRS(c, Quaternion.Euler(0, -90, 0), Vector3.one);
        tint = P("EDE0C4"); Cylinder(new Vector3(0, -.3f, 0), 11, .45f, paving, true, 40);
        for (int i = 0; i < 16; i++)
        {
            float r0 = 12 + i * 1.3f, y = .45f * (i + 1);
            for (int k = 0; k < 24; k++)
            {
                float a0 = Mathf.PI * k / 24, a1 = Mathf.PI * (k + 1) / 24;
                Vector3 V(float r, float a, float yy) => new Vector3(Mathf.Cos(a) * r, yy, Mathf.Sin(a) * r);
                tint = (i % 5 == 4) ? P("E2CFA8") : P("F1E5CB");
                Quad(V(r0, a0, y), V(r0 + 1.3f, a0, y), V(r0 + 1.3f, a1, y), V(r0, a1, y), paving, true);
                Quad(V(r0, a1, y - .45f), V(r0, a1, y), V(r0, a0, y), V(r0, a0, y - .45f), trim, true);
                if (i == 15) Quad(V(r0 + 1.3f, a0, -3), V(r0 + 1.3f, a0, y), V(r0 + 1.3f, a1, y), V(r0 + 1.3f, a1, -3), trim, true);
            }
        }
        // Scaenae frons: two storeys of columns and niches.
        tint = P("E9D9BB"); Box(new Vector3(0, 5, -3), new Vector3(30, 10, 2), trim, true);
        for (int storey = 0; storey < 2; storey++) for (int i = 0; i < 9; i++) Column(new Vector3(-13.5f + i * 3.375f, storey * 5, -1.6f), 4.6f, .32f);
        tint = Color.white; Box(new Vector3(0, 4.9f, -1.4f), new Vector3(30, .4f, 1.4f), trim, true); Box(new Vector3(0, 10.1f, -2.2f), new Vector3(31, .5f, 2.6f), trim, true);
        Spot(c + Vector3.up * 7.6f, "Top row of the theatre", false);
        frame = old; tint = Color.white;
    }
    // Brass armillary sphere on the high peak.
    static void Armillary(Vector3 c)
    {
        var old = frame; frame = Matrix4x4.Translate(c);
        tint = P("E9D9BB"); Box(new Vector3(0, -2, 0), new Vector3(16, 4.2f, 16), trim, true);
        for (int k = 0; k < 3; k++) Box(new Vector3(0, .25f + k * .5f, 0), new Vector3(14 - k * 2.5f, .5f, 14 - k * 2.5f), trim, true);
        tint = Color.white; Column(new Vector3(0, 1.5f, 0), 6, .9f);
        var center = new Vector3(0, 16.5f, 0); tint = Palette.Brass;
        foreach (var rot in new[] { Quaternion.identity, Quaternion.Euler(90, 0, 0), Quaternion.Euler(0, 90, 0), Quaternion.Euler(23, 0, 60), Quaternion.Euler(60, 45, 0) })
        { var saved = frame; frame *= Matrix4x4.TRS(center, rot, Vector3.one); Torus(Vector3.zero, 9, .32f, brass, 40, 6); frame = saved; }
        tint = Color.white; Ellipsoid(center, Vector3.one * 2.2f, glow, 16, 10);
        tint = Palette.Brass; Beam(center + Vector3.down * 9.3f, center + Vector3.up * 11, .25f, brass);
        Anchor(c + center + Vector3.down * 3.2f + Vector3.right * 5);
        Spot(c + Vector3.up * 1.6f, "Armillary summit", true);
        frame = old; tint = Color.white;
    }
    // Robed colossus at the cliff edge, holding a sun disc over the gorge.
    static void Colossus(Vector3 c)
    {
        var old = frame; frame = Matrix4x4.TRS(c, Quaternion.Euler(0, 90, 0), Vector3.one);
        tint = P("E9D9BB"); Box(new Vector3(0, -4, 0), new Vector3(14, 8.2f, 14), trim, true);
        Box(new Vector3(0, 3, 0), new Vector3(11, 6, 11), trim, true);
        tint = Color.white; Box(new Vector3(0, 6.2f, 0), new Vector3(12, .5f, 12), trim, true);
        for (int i = 0; i < 6; i++) Box(new Vector3(0, i * .5f + .25f, -5.5f - (6 - i) * .55f), new Vector3(6, .5f, .55f), paving, true);
        var marble = P("F4EADB"); float b = 6.45f;
        tint = marble;
        Lathe(new Vector3(0, b, 0), new[] { new Vector2(4.6f, 0), new Vector2(4.2f, 4), new Vector2(3.3f, 11), new Vector2(3.1f, 15), new Vector2(3.6f, 19), new Vector2(3.9f, 22), new Vector2(2.6f, 24.5f), new Vector2(1.2f, 25.3f) }, 18, trim, true, .07f);
        tint = Palette.Brass; Cylinder(new Vector3(0, b + 14.5f, 0), 3.35f, .7f, brass, true, 20);
        tint = marble; Cylinder(new Vector3(0, b + 25, 0), 1.1f, 1.6f, trim, true, 12);
        Ellipsoid(new Vector3(0, b + 28.2f, 0), new Vector3(2.1f, 2.6f, 2.3f), trim, 14, 9);
        Ellipsoid(new Vector3(0, b + 28.6f, .35f), new Vector3(2.35f, 2.7f, 2.2f), trim, 14, 9);
        tint = Palette.Brass; Torus(new Vector3(0, b + 29.6f, 0), 2.15f, .22f, brass, 22, 6);
        tint = marble;
        // Raised right arm with the sun disc; left arm holds a staff.
        var shoulderR = new Vector3(3.5f, b + 23, 0); var handR = new Vector3(5.2f, b + 34, 1.5f);
        Limb(shoulderR, handR, 1.1f, .8f, trim, 12); Ellipsoid(handR, Vector3.one * 1.1f, trim, 10, 6);
        var shoulderL = new Vector3(-3.5f, b + 23, 0); var handL = new Vector3(-4.6f, b + 15, 2.2f);
        Limb(shoulderL, handL, 1.1f, .8f, trim, 12); Ellipsoid(handL, Vector3.one * 1, trim, 10, 6);
        tint = Palette.Brass; Cylinder(new Vector3(-4.6f, b, 2.2f), .3f, 30, brass, true, 8);
        Ellipsoid(new Vector3(-4.6f, b + 30.3f, 2.2f), Vector3.one * .9f, brass, 10, 6);
        var disc = handR + Vector3.up * 4.2f;
        var saved = frame; frame *= Matrix4x4.TRS(disc, Quaternion.Euler(90, 0, 0), Vector3.one);
        Cylinder(new Vector3(0, -.35f, 0), 4.2f, .7f, brass, true, 32);
        tint = Color.white; Cylinder(new Vector3(0, -.45f, 0), 2.4f, .9f, sunMat, false, 24);
        tint = Palette.Brass; for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; Beam(new Vector3(Mathf.Cos(a) * 4.2f, 0, Mathf.Sin(a) * 4.2f), new Vector3(Mathf.Cos(a) * 6.2f, 0, Mathf.Sin(a) * 6.2f), .3f, brass); }
        frame = saved;
        Anchor(c + Quaternion.Euler(0, 90, 0) * (disc + Vector3.down * 5.2f + Vector3.forward * 1.8f));
        Spot(c + Quaternion.Euler(0, 90, 0) * new Vector3(0, b + 31.4f, 0), "Head of the Colossus", true);
        frame = old; tint = Color.white;
    }
    // Two-tier aqueduct between the temple and palace hills; its channel top is a runway.
    static void Aqueduct(int side, Vector3 a, Vector3 b)
    {
        var dir = new Vector3(b.x - a.x, 0, b.z - a.z); float length = dir.magnitude; dir /= length;
        var rot = Quaternion.LookRotation(dir); int spans = Mathf.CeilToInt(length / 8);
        for (int i = 0; i <= spans; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)spans); var crest = Crest(side, p.z);
            float ground = UplandH(side, (p.x - crest.x) * side, p.z, crest.y) - 1, deck = p.y;
            if (deck - ground < 2) continue;
            var old = frame; frame = Matrix4x4.TRS(new Vector3(p.x, 0, p.z), rot, Vector3.one);
            tint = P("E2CFA8"); Box(new Vector3(0, (ground + deck - 1.4f) * .5f, 0), new Vector3(3, deck - 1.4f - ground, 2.2f), trim, true);
            if (i < spans)
            {
                Arch(new Vector3(0, deck - 5.2f, 4), 2.6f, 3.6f, .9f, 2.2f, trim, true, 12);
                if (deck - ground > 22) { Box(new Vector3(0, deck - 14, 4), new Vector3(2.2f, 1.2f, 8), trim, true); Arch(new Vector3(0, deck - 18.4f, 4), 2.6f, 3.6f, .7f, 2, trim, true, 12); }
            }
            frame = old;
        }
        var oldFrame = frame; frame = Matrix4x4.TRS((a + b) * .5f, Quaternion.LookRotation(b - a), Vector3.one);
        float len3 = Vector3.Distance(a, b);
        tint = P("E9D9BB"); Box(new Vector3(0, -.8f, 0), new Vector3(3.4f, 1.6f, len3 + 1), trim, true);
        foreach (int s in new[] { -1, 1 }) Box(new Vector3(s * 1.45f, .35f, 0), new Vector3(.5f, .7f, len3 + 1), trim, true);
        tint = Color.white; Box(new Vector3(0, .02f, 0), new Vector3(2.3f, .08f, len3), water, false);
        frame = oldFrame; tint = Color.white;
        Spot(Vector3.Lerp(a, b, .5f) + Vector3.up * .8f + Vector3.Cross(Vector3.up, dir) * 1.45f, "Aqueduct walk", true);
    }

    // ---------- farmlands (east uplands) ----------
    static Material stock, fall;
    static float Ground(int side, Vector3 p) { var crest = Crest(side, p.z); return UplandH(side, (p.x - crest.x) * side, p.z, crest.y); }
    static Vector3 OnGround(int side, Vector3 p) => new Vector3(p.x, Ground(side, p), p.z);
    static void Farms()
    {
        const int E = 1;
        var top = GrandStairTop(E); var landing = UP(E, 14, top.z);
        var hamlet = PadCenter(E, 1); var barn = PadCenter(E, 2); var mill1 = PadCenter(E, 3); var mill2 = PadCenter(E, 4); var watermill = PadCenter(E, 5);
        const string dirt = "C9A77A";
        Road(E, landing, hamlet, 3.6f, false, dirt); Road(E, hamlet, barn, 3.2f, false, dirt); Road(E, hamlet, mill1, 2.6f, false, dirt);
        Road(E, barn, watermill, 3, false, dirt); Road(E, landing, barn, 3, false, dirt);
        // Patchwork fields across the plateau.
        int kinds = 0;
        for (float z = -48; z < 385; z += D(30, 42))
            for (float u = 24; u < 148; u += D(26, 36))
            {
                float w = D(18, 26), d = D(22, 32);
                if (Near(E, u + w * .5f, z + d * .5f, 6 + Mathf.Max(w, d) * .5f)) continue;
                if (Mathf.Abs(u + w * .5f - RiverU(z + d * .5f)) < 14 + w * .5f) continue;
                Field(E, u, z, w, d, kinds++ % 7 == 4 ? 4 : deco.Next(7));
            }
        // Hamlet: cottages around a square with a well, a chapel and chickens.
        tint = P("E2CFA8"); var sq = OnGround(E, hamlet); Cylinder(sq + Vector3.down * .3f, 9, .45f, paving, true, 28); tint = Color.white;
        Well(sq);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2 / 8 + D(-.15f, .15f), r = D(16, 21);
            var foot = OnGround(E, hamlet + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            float yaw = Mathf.Atan2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Rad2Deg + 180;
            if (i == 0) Building(foot + Vector3.down * .3f, yaw, 6.5f, 7, 16, 40, 2, true, true, true, Look.Island, true);
            else Building(foot + Vector3.down * .3f, yaw, D(6.5f, 9), D(6, 7.5f), D(4.6f, 7.6f), 40, 2, true, true, false, Chance(.5f) ? Look.Island : Look.Tuscan, true);
            if (Chance(.6f)) Tree(foot + Quaternion.Euler(0, yaw, 0) * new Vector3(D(-5, 5), 0, -3.5f), D(.8f, 1), 0);
        }
        Herd("Chicken", ChickenMesh(), sq, 14, 12, 1.3f, 1, true, true, .02f);
        Barn(barn);
        Windmill(mill1); Windmill(mill2);
        Watermill(watermill);
        Stream(E);
        // Bridges where the farm tracks cross the stream.
        foreach (float z in new[] { 250f, 60f }) StreamBridge(E, z);
        Road(E, UP(E, RiverU(250) + 12, 250), mill2, 2.6f, false, dirt);
        Waterfall(E);
        // Hedgerow trees and haystacks.
        for (int i = 0; i < 90; i++)
        {
            float u = D(22, 150), z = D(-55, 395);
            if (Near(E, u, z, 6)) continue;
            var p = UP(E, u, z);
            if (Chance(.5f)) OliveTree(p, D(.8f, 1.15f)); else if (Chance(.6f)) Cypress(p - Vector3.up * .3f, D(5, 9)); else Haystack(p);
        }
        Spot(OnGround(E, hamlet) + new Vector3(0, 16.5f, 0), "Chapel belfry", false);
    }
    // One parcel: 0 wheat, 1 lavender, 2 vineyard, 3 orchard, 4 pasture, 5 kitchen garden, 6 sunflowers.
    static void Field(int side, float u0, float z0, float w, float d, int kind)
    {
        Vector3 At(float u, float z) => UP(side, u, z);
        void Row(float u, Material mat, Color color, float width, float height, bool pointed)
        {
            for (float z = z0; z < z0 + d - .1f; z += 3)
            {
                float z1 = Mathf.Min(z + 3, z0 + d);
                var a = At(u, z); var b = At(u, z1); var across = Vector3.right * width * .5f;
                tint = Color.Lerp(color, Color.white, D(0, .12f)); wind = .6f;
                var at = a + Vector3.up * height; var bt = b + Vector3.up * height;
                if (pointed) { QuadN(a - across, b - across, bt, at, new Vector3(-1, 1, 0), mat); QuadN(a + across, at, bt, b + across, new Vector3(1, 1, 0), mat); }
                else { QuadN(at - across, bt - across, bt + across, at + across, Vector3.up, mat); QuadN(a - across, b - across, bt - across, at - across, Vector3.left, mat); QuadN(a + across, at + across, bt + across, b + across, Vector3.right, mat); }
                wind = 0;
            }
        }
        switch (kind)
        {
            case 0: for (float u = u0 + 1; u < u0 + w; u += 1.5f) Row(u, leaf, Pick("E6C35A", "D9B04A", "E8CB6E"), 1.2f, .75f, false); break;
            case 1: for (float u = u0 + 1; u < u0 + w; u += 1.6f) Row(u, blossom, Pick("9C80D2", "8E79C8", "A893DD"), 1, .6f, true); break;
            case 2:
                for (float u = u0 + 1.2f; u < u0 + w; u += 2.6f)
                {
                    Row(u, leaf, Pick("6E8B4A", "7F9658"), .6f, 1.45f, false);
                    tint = P("7A5340"); for (float z = z0; z < z0 + d; z += 5) { var post = At(u, z); Box(post + Vector3.up * .8f, new Vector3(.12f, 1.6f, .12f), wood, true); }
                    tint = P("6B4F8A"); for (float z = z0 + 1; z < z0 + d; z += 2.2f) if (Chance(.5f)) Ellipsoid(At(u, z) + new Vector3(.32f, .9f, 0), new Vector3(.12f, .18f, .12f), blossom, 6, 4);
                }
                break;
            case 3: for (float u = u0 + 3; u < u0 + w; u += 7) for (float z = z0 + 3; z < z0 + d; z += 7) OliveTree(At(u + D(-1, 1), z + D(-1, 1)), D(.8f, 1.05f)); break;
            case 4:
                {
                    Fence(side, u0, z0, w, d);
                    var c = At(u0 + w * .5f, z0 + d * .5f);
                    if (Chance(.6f)) Herd("Cow", CowMesh(), c, Mathf.Min(w, d) * .38f, deco.Next(3, 6), .7f, 1, false, false, .02f);
                    else Herd("Sheep", SheepMesh(), c, Mathf.Min(w, d) * .38f, deco.Next(5, 9), .6f, 1, false, false, .03f);
                    for (int i = 0; i < 2; i++) Tree(At(u0 + D(2, w - 2), z0 + D(2, d - 2)), D(.8f, 1.1f), 0);
                    break;
                }
            case 5:
                for (float u = u0 + 1; u < u0 + w; u += 1.3f) Row(u, leaf, Pick("6E8B4A", "8FA45E", "E07A3A", "B6463E", "5E7B3A"), .7f, .4f, true);
                tint = P("8C5E44"); var shed = At(u0 + w - 2, z0 + 2); Box(shed + Vector3.up * 1.2f, new Vector3(2.4f, 2.4f, 2), wood, true);
                tint = Color.Lerp(Color.white, P("E7A07E"), .3f); Box(shed + Vector3.up * 2.5f, new Vector3(2.8f, .2f, 2.4f), roof, true);
                break;
            default:
                for (float u = u0 + 1; u < u0 + w; u += 1.7f) for (float z = z0 + .5f; z < z0 + d; z += 1.4f)
                    {
                        var f = At(u, z); float h = D(1.6f, 2.2f);
                        tint = P("6E8B4A"); Beam(f, f + Vector3.up * h, .07f, leaf);
                        tint = P("F2C230"); var face = f + new Vector3(0, h, -.12f);
                        Cylinder(face, .32f, .08f, blossom, false, 8); tint = P("7A5340"); Cylinder(face + new Vector3(0, .02f, 0), .14f, .1f, blossom, false, 6);
                    }
                break;
        }
        // Dry-stone wall along the near edge.
        if (kind != 4 && Chance(.6f)) { tint = P("D8C6A2"); for (float u = u0; u < u0 + w; u += 2) { var a = At(u, z0 - 1); Box(a + Vector3.up * .35f, new Vector3(2.05f, .9f, .6f), trim, true); } }
        tint = Color.white;
    }
    static void Fence(int side, float u0, float z0, float w, float d)
    {
        tint = P("8C5E44");
        var corners = new[] { UP(side, u0, z0), UP(side, u0 + w, z0), UP(side, u0 + w, z0 + d), UP(side, u0, z0 + d) };
        for (int e = 0; e < 4; e++)
        {
            var a = corners[e]; var b = corners[(e + 1) % 4]; int n = Mathf.CeilToInt(Vector3.Distance(a, b) / 2.6f);
            for (int i = 0; i < n; i++)
            {
                var p = OnGround(side, Vector3.Lerp(a, b, i / (float)n)); var q = OnGround(side, Vector3.Lerp(a, b, (i + 1f) / n));
                Box(p + Vector3.up * .6f, new Vector3(.14f, 1.2f, .14f), wood, true);
                if (e == 0 && i == n / 2) continue;
                Beam(p + Vector3.up * .5f, q + Vector3.up * .5f, .08f, wood, true); Beam(p + Vector3.up * 1, q + Vector3.up * 1, .08f, wood, true);
            }
        }
        tint = Color.white;
    }
    static void Herd(string name, Mesh mesh, Vector3 center, float radius, int count, float speed, float scale, bool skittish, bool hops, float bob)
    {
        var herd = new GameObject(name + " flock").AddComponent<Livestock>(); herd.transform.SetParent(group, false); herd.transform.position = center;
        herd.mesh = mesh; herd.material = stock; herd.radius = radius; herd.count = count; herd.walkSpeed = speed; herd.scale = scale; herd.skittish = skittish; herd.hops = hops; herd.bob = bob;
    }
    static readonly Dictionary<string, Mesh> baked = new();
    static Mesh Bake(string name, Action draw)
    {
        if (baked.TryGetValue(name, out var cached) && cached) return cached;
        var savedBatches = batches; var savedFrame = frame; var savedTint = tint; batches = new(); frame = Matrix4x4.identity;
        draw();
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        foreach (var b in batches.Values) { int o = v.Count; v.AddRange(b.v); c.AddRange(b.c); foreach (var i in b.t) t.Add(i + o); }
        batches = savedBatches; frame = savedFrame; tint = savedTint;
        var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, GeoPath + "/" + name + ".asset"); baked[name] = mesh; return mesh;
    }
    static Mesh CowMesh() => Bake("Cow", () =>
    {
        tint = P("F4EEE2"); Ellipsoid(new Vector3(0, 1.1f, 0), new Vector3(.55f, .5f, 1), stock, 12, 8);
        tint = P("3B3F4E"); Ellipsoid(new Vector3(.3f, 1.25f, .2f), new Vector3(.32f, .3f, .4f), stock, 8, 6); Ellipsoid(new Vector3(-.28f, 1f, -.35f), new Vector3(.32f, .28f, .35f), stock, 8, 6);
        tint = P("F4EEE2"); Ellipsoid(new Vector3(0, 1.38f, 1.08f), new Vector3(.26f, .27f, .36f), stock, 10, 7);
        tint = P("E8A8A0"); Ellipsoid(new Vector3(0, 1.26f, 1.38f), new Vector3(.2f, .15f, .14f), stock, 8, 5); Ellipsoid(new Vector3(0, .66f, -.25f), new Vector3(.2f, .14f, .2f), stock, 8, 5);
        tint = P("EBDCBD"); foreach (int s in new[] { -1, 1 }) Limb(new Vector3(s * .15f, 1.6f, 1), new Vector3(s * .32f, 1.75f, .98f), .05f, .02f, stock, 5);
        tint = P("3B3F4E"); foreach (int s in new[] { -1, 1 }) Ellipsoid(new Vector3(s * .28f, 1.48f, .95f), new Vector3(.14f, .06f, .09f), stock, 6, 4);
        foreach (var leg in new[] { new Vector3(.3f, 0, .65f), new Vector3(-.3f, 0, .65f), new Vector3(.3f, 0, -.65f), new Vector3(-.3f, 0, -.65f) })
        { tint = P("F4EEE2"); Limb(leg + Vector3.up * .9f, leg + Vector3.up * .1f, .12f, .09f, stock, 6); tint = P("3B3F4E"); Limb(leg + Vector3.up * .12f, leg, .1f, .1f, stock, 6); }
        tint = P("F4EEE2"); Limb(new Vector3(0, 1.35f, -.98f), new Vector3(0, .65f, -1.1f), .04f, .03f, stock, 5); tint = P("3B3F4E"); Ellipsoid(new Vector3(0, .6f, -1.1f), new Vector3(.07f, .12f, .07f), stock, 6, 4);
    });
    static Mesh SheepMesh() => Bake("Sheep", () =>
    {
        tint = P("F4EBDD");
        for (int i = 0; i < 7; i++) Ellipsoid(new Vector3(D(-.2f, .2f), .78f + D(-.08f, .1f), D(-.45f, .45f)), new Vector3(.4f, .36f, .42f), stock, 8, 6);
        tint = P("3B3F4E"); Ellipsoid(new Vector3(0, .9f, .68f), new Vector3(.16f, .19f, .26f), stock, 8, 6);
        foreach (int s in new[] { -1, 1 }) Ellipsoid(new Vector3(s * .17f, .98f, .62f), new Vector3(.12f, .05f, .07f), stock, 6, 4);
        foreach (var leg in new[] { new Vector3(.18f, 0, .35f), new Vector3(-.18f, 0, .35f), new Vector3(.18f, 0, -.35f), new Vector3(-.18f, 0, -.35f) }) Limb(leg + Vector3.up * .55f, leg, .055f, .05f, stock, 5);
    });
    static Mesh ChickenMesh() => Bake("Chicken", () =>
    {
        tint = P("F7F2E8"); Ellipsoid(new Vector3(0, .32f, 0), new Vector3(.17f, .17f, .23f), stock, 10, 7);
        Ellipsoid(new Vector3(0, .42f, -.2f), new Vector3(.1f, .14f, .08f), stock, 6, 4);
        Ellipsoid(new Vector3(0, .52f, .17f), new Vector3(.09f, .1f, .09f), stock, 8, 6);
        tint = P("C9392E"); Ellipsoid(new Vector3(0, .62f, .17f), new Vector3(.02f, .05f, .06f), stock, 5, 4); Ellipsoid(new Vector3(0, .46f, .24f), new Vector3(.025f, .04f, .02f), stock, 5, 4);
        tint = P("F2B134"); Lathe(new Vector3(0, .52f, .25f), new[] { new Vector2(.03f, 0), new Vector2(.002f, .06f) }, 5, stock);
        foreach (int s in new[] { -1, 1 }) Limb(new Vector3(s * .06f, .2f, 0), new Vector3(s * .07f, 0, .02f), .015f, .015f, stock, 4);
    });
    static void Well(Vector3 p)
    {
        tint = P("D8C6A2"); Cylinder(p, 1.3f, 1, trim, true, 16); tint = Color.white; Box(p + Vector3.up * .5f, new Vector3(1.6f, .1f, 1.6f), water, false);
        tint = P("7A5340"); foreach (int s in new[] { -1, 1 }) Beam(p + new Vector3(s * 1.1f, 1, 0), p + new Vector3(s * 1.1f, 3, 0), .14f, wood, true);
        Beam(p + new Vector3(-1.3f, 3, 0), p + new Vector3(1.3f, 3, 0), .12f, wood, true);
        tint = Color.Lerp(Color.white, P("E7A07E"), .3f); var a = p + new Vector3(-1.6f, 3.1f, -1.2f); var b = p + new Vector3(1.6f, 3.1f, -1.2f);
        Quad(a, a + new Vector3(0, .8f, 1.2f), b + new Vector3(0, .8f, 1.2f), b, roof, true); Quad(b + new Vector3(0, 0, 2.4f), b + new Vector3(0, .8f, 1.2f), a + new Vector3(0, .8f, 1.2f), a + new Vector3(0, 0, 2.4f), roof, true);
        tint = Color.white;
    }
    static void Haystack(Vector3 p)
    {
        tint = P("E6C35A"); Lathe(p - Vector3.up * .2f, new[] { new Vector2(1.6f, 0), new Vector2(1.7f, .8f), new Vector2(1.2f, 2.2f), new Vector2(.1f, 3) }, 12, leaf, true, .06f); tint = Color.white;
    }
    static void Barn(Vector3 c)
    {
        var old = frame; frame = Matrix4x4.TRS(c, Quaternion.Euler(0, D(0, 360), 0), Vector3.one);
        tint = P("B66547"); Box(new Vector3(0, 2.6f, 0), new Vector3(14, 6.2f, 22), plaster, true);
        tint = P("F4E6CB"); Box(new Vector3(0, .2f, 0), new Vector3(14.3f, .6f, 22.3f), trim, true);
        // Gambrel roof.
        float y = 5.7f; tint = Color.Lerp(Color.white, P("E7A07E"), .4f);
        Vector3 V(float x, float h, float z) => new Vector3(x, y + h, z);
        foreach (int s in new[] { -1, 1 })
        {
            QuadN(V(s * 7.6f, 0, -11.6f), V(s * 4.6f, 3.2f, -11.6f), V(s * 4.6f, 3.2f, 11.6f), V(s * 7.6f, 0, 11.6f), new Vector3(s, .5f, 0), roof);
            QuadN(V(s * 4.6f, 3.2f, -11.6f), V(0, 4.8f, -11.6f), V(0, 4.8f, 11.6f), V(s * 4.6f, 3.2f, 11.6f), new Vector3(s, 2, 0), roof);
        }
        tint = P("B66547");
        foreach (int s in new[] { -1, 1 }) { float z = s * 11.1f; QuadN(V(-7, 0, z), V(-4.6f, 3.2f, z), V(4.6f, 3.2f, z), V(7, 0, z), Vector3.forward * s, plaster); Tri(V(-4.6f, 3.2f, z), V(0, 4.8f, z), V(4.6f, 3.2f, z), plaster, true); Tri(V(4.6f, 3.2f, z), V(0, 4.8f, z), V(-4.6f, 3.2f, z), plaster, true); }
        tint = P("F4E6CB");
        foreach (int s in new[] { -1, 1 })
        {
            Box(new Vector3(0, 2.4f, s * 11.05f), new Vector3(5.2f, 4.4f, .2f), wood);
            Beam(new Vector3(-2.5f, .4f, s * 11.15f), new Vector3(2.5f, 4.4f, s * 11.15f), .16f, trim); Beam(new Vector3(2.5f, .4f, s * 11.15f), new Vector3(-2.5f, 4.4f, s * 11.15f), .16f, trim);
            Box(new Vector3(0, 7.4f, s * 11.1f), new Vector3(1.8f, 1.6f, .2f), wood);
        }
        tint = Color.white; frame = old;
        for (int i = 0; i < 5; i++) Haystack(OnGround(1, c + new Vector3(D(-14, 14), 0, D(-16, 16)) + Vector3.right * 12));
        Spot(c + new Vector3(0, 10.6f, 0), "Barn ridge", false);
    }
    static void Windmill(Vector3 c)
    {
        var old = frame; frame = Matrix4x4.Translate(c);
        tint = P("F4EEE2"); Cylinder(new Vector3(0, -1, 0), 3.6f, 12.5f, plaster, true, 20, 2.7f);
        tint = Color.Lerp(Color.white, P("E7A07E"), .3f); Lathe(new Vector3(0, 11.4f, 0), new[] { new Vector2(3.1f, 0), new Vector2(2.4f, 1.6f), new Vector2(.1f, 4.2f) }, 20, roof, true);
        tint = Color.white; shutterTint = P("2F5F8F"); style = null;
        Opening(new Vector3(0, -.4f, -3.5f), 1.2f, 2.2f, true); Window(new Vector3(0, 6, -3.05f), .7f, 1.2f, true);
        frame = old;
        var hub = c + new Vector3(0, 11.6f, -3.3f);
        var sails = new GameObject("Windmill sails", typeof(MeshFilter), typeof(MeshRenderer)); sails.transform.SetParent(group, false); sails.transform.position = hub;
        sails.GetComponent<MeshFilter>().sharedMesh = Bake("Windmill sails", () =>
        {
            tint = P("7A5340"); Cylinder(new Vector3(0, 0, -.2f), .35f, .5f, cloth, false, 10);
            for (int i = 0; i < 4; i++)
            {
                var saved = frame; frame = Matrix4x4.Rotate(Quaternion.Euler(0, 0, i * 90 + 15));
                tint = P("7A5340"); Box(new Vector3(0, 4.6f, -.3f), new Vector3(.22f, 9, .12f), cloth);
                for (float y = 1.4f; y < 9; y += 1.1f) Box(new Vector3(.75f, y, -.32f), new Vector3(1.5f, .07f, .06f), cloth);
                tint = P("F3E9D4"); Quad(new Vector3(.1f, 1.2f, -.35f), new Vector3(.1f, 9, -.35f), new Vector3(1.55f, 9, -.4f), new Vector3(1.55f, 1.2f, -.4f), cloth);
                Quad(new Vector3(1.55f, 1.2f, -.4f), new Vector3(1.55f, 9, -.4f), new Vector3(.1f, 9, -.35f), new Vector3(.1f, 1.2f, -.35f), cloth);
                frame = saved;
            }
        });
        sails.GetComponent<MeshRenderer>().sharedMaterial = cloth;
        var spin = sails.AddComponent<Spinner>(); spin.axis = Vector3.forward; spin.speed = D(14, 22);
        Spot(c + new Vector3(0, 15.8f, 0), "Windmill cap", false);
    }
    static void Watermill(Vector3 c)
    {
        float rz = c.z, ru = RiverU(rz); var bank = UP(1, ru + 10, rz);
        Building(bank + Vector3.down * .3f, 90, 8, 7, 7, 30, 1, true, true, false, Look.Tuscan, true);
        var axle = UP(1, ru + 5.6f, rz); axle.y = WaterY(rz) + 1.6f;
        var wheel = new GameObject("Water wheel", typeof(MeshFilter), typeof(MeshRenderer)); wheel.transform.SetParent(group, false); wheel.transform.position = axle;
        wheel.GetComponent<MeshFilter>().sharedMesh = Bake("Water wheel", () =>
        {
            tint = P("7A5340");
            for (int i = 0; i < 12; i++)
            {
                var saved = frame; frame = Matrix4x4.Rotate(Quaternion.Euler(i * 30, 0, 0));
                Box(new Vector3(0, 1.4f, 0), new Vector3(.12f, 2.8f, .12f), wood);
                Box(new Vector3(0, 2.75f, 0), new Vector3(1.4f, .5f, .1f), wood);
                Box(new Vector3(.62f, 2.75f, .4f), new Vector3(.1f, .1f, .9f), wood); Box(new Vector3(-.62f, 2.75f, .4f), new Vector3(.1f, .1f, .9f), wood);
                frame = saved;
            }
            Cylinder(new Vector3(-1, 0, 0), .2f, 2, wood, false, 8);
        });
        wheel.GetComponent<MeshRenderer>().sharedMaterial = wood;
        var spin = wheel.AddComponent<Spinner>(); spin.axis = Vector3.right; spin.speed = 40;
        tint = P("7A5340"); Beam(axle + Vector3.right * 1.1f, UP(1, ru + 8.2f, rz) + Vector3.up * 3, .3f, wood, true); tint = Color.white;
    }
    // The stream: a flat water ribbon in its carved bed, from a spring pond to the end of the cliffs.
    static void Stream(int side)
    {
        tint = Color.white;
        for (float z = -52; z < TerrainEnd - .01f; z += 3)
        {
            float z1 = Mathf.Min(z + 3, TerrainEnd);
            Vector3 W(float zz, float du) { var crest = Crest(side, zz); return new Vector3(crest.x + side * (RiverU(zz) + du), WaterY(zz), zz); }
            QuadN(W(z, -7.5f), W(z1, -7.5f), W(z1, 7.5f), W(z, 7.5f), Vector3.up, water);
        }
        var pond = W0(side, -48); Cylinder(new Vector3(pond.x, WaterY(-48), -48), 12, .05f, water, false, 24);
        for (int i = 0; i < 14; i++) { float a = i * Mathf.PI / 7; Tree(OnGround(side, new Vector3(pond.x + Mathf.Cos(a) * 15, 0, -48 + Mathf.Sin(a) * 15)), D(.7f, 1), i % 3 == 0 ? 2 : 0); }
        for (float z = -40; z < 395; z += D(5, 9))
            foreach (int s in new[] { -1, 1 })
                if (Chance(.35f)) { tint = Color.Lerp(P("6E8B4A"), P("93A862"), D(0, 1)); var p = W0(side, z) + Vector3.right * side * s * D(8, 10); p.y = Ground(side, p); wind = .8f; Ellipsoid(p + Vector3.up * .4f, new Vector3(.5f, 1.1f, .5f), leaf, 6, 5, .2f); wind = 0; }
        tint = Color.white;
    }
    static Vector3 W0(int side, float z) { var crest = Crest(side, z); return new Vector3(crest.x + side * RiverU(z), WaterY(z), z); }
    static void StreamBridge(int side, float z)
    {
        var c = W0(side, z); var old = frame; frame = Matrix4x4.Translate(c);
        float a = Ground(side, c + Vector3.left * 11) - c.y, b = Ground(side, c + Vector3.right * 11) - c.y, deck = Mathf.Max(a, b) + .3f;
        tint = P("D8C6A2"); Box(new Vector3(0, deck - .3f, 0), new Vector3(24, .6f, 3.2f), paving, true);
        frame *= Matrix4x4.Rotate(Quaternion.Euler(0, 90, 0)); Arch(new Vector3(0, -1.2f, 0), 8, deck + .4f, .6f, 2.8f, trim, true, 18);
        frame = Matrix4x4.Translate(c);
        foreach (int s in new[] { -1, 1 }) Rail(new Vector3(-11.5f, deck, s * 1.45f), new Vector3(11.5f, deck, s * 1.45f));
        frame = old; tint = Color.white;
    }
    // The stream leaves the plateau at the end of the cliffs and falls into the harbour.
    static void Waterfall(int side)
    {
        var lip = W0(side, TerrainEnd); float top = lip.y + .05f, width = 15;
        int rows = 24; var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int i = 0; i <= rows; i++)
        {
            float f = i / (float)rows, y = Mathf.Lerp(top, .2f, f), out_ = .4f + 6 * Mathf.Sqrt(f) - f * 2.5f, spread = 1 + f * .35f;
            for (int k = 0; k <= 6; k++)
            {
                float a = (k / 6f - .5f) * width * spread;
                v.Add(new Vector3(lip.x + a, y, TerrainEnd + out_ + Mathf.Sin(k * 1.7f + i * .3f) * .25f)); uv.Add(new Vector2(k / 6f * width / 4, (top - y)));
                if (i < rows && k < 6) { int n = i * 7 + k; t.AddRange(new[] { n, n + 7, n + 8, n, n + 8, n + 1 }); }
            }
        }
        var mesh = new Mesh { name = "Waterfall" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, GeoPath + "/Waterfall.asset");
        var go = new GameObject("Waterfall", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(group, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = fall; r.shadowCastingMode = ShadowCastingMode.Off;
        fall.SetFloat("_Height", top - .2f);
        // Churned water and drifting spray clouds at the foot.
        var foot = new Vector3(lip.x, .2f, TerrainEnd + 4.5f);
        for (int i = 0; i < 26; i++)
        {
            float a = D(0, Mathf.PI * 2), rr = D(2, 12); tint = Color.Lerp(P("FBFAF3"), P("CFE8EA"), D(0, .5f)); wind = .9f;
            Ellipsoid(foot + new Vector3(Mathf.Cos(a) * rr, D(0, 1.5f), Mathf.Sin(a) * rr * .6f + 3), new Vector3(D(1.5f, 3.2f), D(1, 2.4f), D(1.5f, 3)), cloth, 9, 6, .15f);
        }
        for (int i = 0; i < 9; i++) { tint = Color.Lerp(P("FBFAF3"), P("D8ECEE"), .4f); Ellipsoid(foot + new Vector3(D(-9, 9), D(3, 12), D(3, 9)), Vector3.one * D(2.5f, 4.5f), cloth, 9, 6, .2f); }
        wind = 0; tint = Color.white;
        Spot(new Vector3(lip.x + side * 9, Ground(side, new Vector3(lip.x + side * 9, 0, TerrainEnd - 3)) + .1f, TerrainEnd - 3), "Top of the falls", true);
    }

    // ---------- harbour (beyond the gorge mouth) ----------
    const float PortZ0 = 400, PortZ1 = 480;
    static float QuayEdge(int side, float z) => Edge(side, 0, PortZ0) + side * Mathf.Max(0, z - PortZ0) * .85f;
    static float QuayY(int side) => Level(side, 0, PortZ0) - .03f;
    static void Port()
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float y = QuayY(side), outer = side < 0 ? -260 : Mathf.Min(260, W0(1, TerrainEnd).x - 16);
            // Quay platform: paved top, quay walls to the sea floor.
            for (float z = PortZ0; z < PortZ1 - .01f; z += 4)
            {
                float z1 = z + 4; Vector3 I(float zz, float yy) => new Vector3(QuayEdge(side, zz), yy, zz);
                tint = Color.Lerp(P("E9D7B5"), P("DCC6A0"), Mathf.PerlinNoise(z * .1f, side));
                QuadN(I(z, y), I(z1, y), new Vector3(outer, y, z1), new Vector3(outer, y, z), Vector3.up, paving);
                tint = P("E2CFAA"); aoBase = -1; aoSpan = 3;
                QuadN(I(z, -9), I(z1, -9), I(z1, y), I(z, y), Vector3.left * side, trim); aoBase = float.NaN;
                if (Mathf.RoundToInt(z) % 8 == 0) { tint = P("3E4A57"); Cylinder(I(z, y) + Vector3.right * side * .8f, .25f, .7f, wood, true, 8); }
            }
            tint = P("E2CFAA");
            QuadN(new Vector3(QuayEdge(side, PortZ1), -9, PortZ1), new Vector3(QuayEdge(side, PortZ1), y, PortZ1), new Vector3(outer, y, PortZ1), new Vector3(outer, -9, PortZ1), Vector3.forward, trim);
            QuadN(new Vector3(outer, -9, PortZ0), new Vector3(outer, y, PortZ0), new Vector3(outer, y, PortZ1), new Vector3(outer, -9, PortZ1), Vector3.right * side, trim);
            tint = Color.white;
            HarbourTown(side, y);
            foreach (float pz in new[] { 422f, 458f }) Pier(side, pz, pz < 440 ? 18 : 30, y);
            Breakwater(side);
        }
        // Moored galleons, one anchored mid-basin; plank walks and rings link them.
        var a = Ship(new Vector3(QuayEdge(-1, 458) + 17, 0, 465.5f), 90, 30, 2, "Saffron Wind");
        var b = Ship(new Vector3(QuayEdge(-1, 458) + 17, 0, 450.5f), 90, 28, 2, "Olive Star");
        var c = Ship(new Vector3(QuayEdge(1, 458) - 17, 0, 465.5f), -90, 30, 3, "Turquoise Crown");
        var d = Ship(new Vector3(4, 0, 505), 0, 38, 3, "Brass Sun");
        PlankWalk(a[0], b[0]); PlankWalk(a[1], b[1]);
        foreach (float z in new[] { 422f, 458f }) foreach (int s in new[] { -1, 1 }) Gangplank(s, z);
        Lighthouse(new Vector3(-22, 0, 586));
        Spot(new Vector3(QuayEdge(-1, 440) - 4, QuayY(-1) + .1f, 440), "Harbour quay", false);
    }
    static void HarbourTown(int side, float y)
    {
        float yaw = side < 0 ? -90 : 90;
        // Front row on the promenade, gaps left for the pier heads.
        for (float z = 403; z < 476;)
        {
            float w = D(7, 11);
            if (Mathf.Abs(z + w * .5f - 422) < 7 || Mathf.Abs(z + w * .5f - 458) < 7) { if (Chance(.6f)) CrateStack(new Vector3(QuayEdge(side, z + w * .5f) + side * 6, y, z + w * .5f)); z += w; continue; }
            float zc = z + w * .5f, slope = side * .85f;
            var foot = new Vector3(QuayEdge(side, zc) + side * 10, y, zc);
            float yawTilt = Mathf.Atan(slope) * Mathf.Rad2Deg * -side;
            Building(foot + Vector3.down * .3f, yaw + yawTilt * .0f, w, D(9, 12), D(9, 16), 60, 0, true, true, w < 8.5f && Chance(.2f), Chance(.5f) ? Look.Merchant : Chance(.5f) ? Look.Venetian : Look.Tuscan, true);
            if (Chance(.5f)) CrateStack(foot - Vector3.right * side * 3.5f + Vector3.forward * D(-2, 2));
            if (Chance(.4f)) Lamp(foot - Vector3.right * side * 6.5f);
            z += w + D(.3f, 2.5f);
        }
        // Warehouses behind; the customs house closes the west row with its clock tower.
        for (float z = 406; z < 474; z += 24)
        {
            var foot = new Vector3(QuayEdge(side, z + 12) + side * 34, y, z + 12);
            Warehouse(foot, yaw, 20, 14, D(8, 10));
        }
        if (side < 0)
        {
            var customs = new Vector3(QuayEdge(side, 470) + side * 10, y - .3f, 470);
            Building(customs, -90, 7, 7, 30, 60, 0, true, true, true, Look.Venetian, true);
            Spot(customs + new Vector3(0, 30.6f, 3.5f), "Customs clock tower", true);
        }
        foreach (float z in new[] { 412f, 440f, 470f }) Crane(new Vector3(QuayEdge(side, z) + side * 2.5f, y, z), side);
        for (int i = 0; i < 6; i++) Market(new Vector3(QuayEdge(side, 430 + i * 3.4f) + side * 5, y, 430 + i * 3.4f), side * 90, D(2.4f, 3.4f));
    }
    static void CrateStack(Vector3 p)
    {
        int n = deco.Next(2, 6);
        for (int i = 0; i < n; i++)
        {
            float s = D(1, 1.5f); var at = p + new Vector3(D(-1.2f, 1.2f), 0, D(-1.2f, 1.2f));
            int level = i < 3 ? 0 : 1; tint = Color.Lerp(P("A87A55"), P("C9A27A"), D(0, 1));
            if (Chance(.35f)) { tint = P("8C5E44"); Lathe(at + Vector3.up * level * 1.4f, new[] { new Vector2(.5f, 0), new Vector2(.62f, .6f), new Vector2(.5f, 1.2f) }, 10, wood, true); }
            else Box(at + Vector3.up * (level * 1.4f + s * .5f), Vector3.one * s, wood, true);
        }
        tint = Color.white;
    }
    static void Warehouse(Vector3 foot, float yaw, float w, float d, float h)
    {
        var old = frame; frame = Matrix4x4.TRS(foot, Quaternion.Euler(0, yaw, 0), Vector3.one);
        tint = Pick("E3B26C", "D9905F", "E8C48A", "E2C8A0");
        Box(new Vector3(0, h * .5f - .5f, d * .5f), new Vector3(w, h + 1, d), plaster, true, false);
        tint = Color.white; Box(new Vector3(0, h - .1f, d * .5f), new Vector3(w + .4f, .3f, d + .4f), trim, true);
        // Gable across the front, with a hoist beam at the apex.
        float ridge = d * .3f; tint = Color.Lerp(Color.white, P("E7A07E"), D(0, .4f));
        var a = new Vector3(-w * .5f - .4f, h, -.4f); var b = new Vector3(w * .5f + .4f, h, -.4f);
        QuadN(a, b, new Vector3(w * .5f + .4f, h + ridge, d * .5f), new Vector3(-w * .5f - .4f, h + ridge, d * .5f), new Vector3(0, 1, -1), roof);
        QuadN(new Vector3(-w * .5f - .4f, h, d + .4f), new Vector3(-w * .5f - .4f, h + ridge, d * .5f), new Vector3(w * .5f + .4f, h + ridge, d * .5f), new Vector3(w * .5f + .4f, h, d + .4f), new Vector3(0, 1, 1), roof);
        tint = Color.white;
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * w * .3f;
            tint = P("7A5340"); Box(new Vector3(x, 2, -.05f), new Vector3(3, 4, .12f), wood, true);
            tint = Color.white; Arch(new Vector3(x, 4, -.02f), 1.6f, 1, .3f, .3f, trim, true, 10);
            style = null; Window(new Vector3(x, h - 3, -.02f), .9f, 1.4f, true, 1);
        }
        tint = P("7A5340"); Beam(new Vector3(0, h + ridge * .4f, 0), new Vector3(0, h + ridge * .4f, -2.2f), .3f, wood, true);
        Beam(new Vector3(0, h + ridge * .4f, -2.1f), new Vector3(0, 4.5f, -2.1f), .03f, wood);
        Box(new Vector3(0, 4.2f, -2.1f), new Vector3(1, .7f, 1), wood, true);
        frame = old; tint = Color.white;
    }
    static void Crane(Vector3 p, int side)
    {
        var old = frame; frame = Matrix4x4.TRS(p, Quaternion.Euler(0, side < 0 ? 90 : -90, 0), Vector3.one);
        tint = P("7A5340");
        Box(new Vector3(0, .5f, 0), new Vector3(3, 1, 3), wood, true);
        Beam(new Vector3(0, 1, 0), new Vector3(0, 9, 0), .55f, wood, true);
        Beam(new Vector3(0, 8, -.5f), new Vector3(0, 11, 7), .4f, wood, true);
        Beam(new Vector3(0, 3, 0), new Vector3(0, 8.6f, 5.5f), .2f, wood);
        Torus(new Vector3(-1.4f, 2.6f, 0), 1.5f, .18f, wood, 16, 6);
        Beam(new Vector3(0, 11, 7), new Vector3(0, 6, 7), .03f, wood);
        tint = Color.Lerp(P("A87A55"), P("C9A27A"), D(0, 1)); Box(new Vector3(0, 5.4f, 7), new Vector3(1.2f, 1.2f, 1.2f), wood, true);
        frame = old;
        Anchor(p + Vector3.left * side * 7 + Vector3.up * 9.6f);
        tint = Color.white;
    }
    static void Pier(int side, float z, float length, float quay)
    {
        float x0 = QuayEdge(side, z), deck = quay - .6f;
        tint = P("A88462");
        var a = new Vector3(x0, deck, z); var b = new Vector3(x0 - side * length, deck, z);
        Box((a + b) * .5f + Vector3.down * .15f, new Vector3(length + .4f, .3f, 4.2f), wood, true);
        for (float t = 1; t < length; t += 3) foreach (int s in new[] { -1, 1 })
            { tint = P("6E4A35"); Cylinder(new Vector3(x0 - side * t, -9, z + s * 2), .22f, deck + 9.3f, wood, true, 8); }
        for (float t = 4; t < length; t += 8) { tint = P("3E4A57"); Cylinder(new Vector3(x0 - side * t, deck, z + 2.3f), .22f, .7f, wood, true, 8); Lamp(new Vector3(x0 - side * t, deck, z - 1.8f)); }
        // Steps from the quay down to the pier deck.
        tint = P("E2CFAA"); Box(new Vector3(x0 + side * .5f, (deck + quay) * .5f - .1f, z + 3.2f), new Vector3(1.2f, quay - deck, 2.2f), paving, true);
        tint = Color.white;
        var boat = frame; frame = Matrix4x4.TRS(new Vector3(x0 - side * length * .6f, .05f, z - 4.2f), Quaternion.Euler(0, 90, 0), Vector3.one); Boat(); frame = boat;
    }
    static void Gangplank(int side, float z)
    {
        if (z < 440) return;
        float x0 = QuayEdge(side, z) - side * 12, deck = QuayY(side) - .6f;
        tint = P("A88462");
        foreach (int s in new[] { -1, 1 })
        {
            var from = new Vector3(x0, deck, z + s * 2.2f); var to = new Vector3(x0, 4.1f, z + s * 3.6f);
            var old = frame; frame = Matrix4x4.TRS((from + to) * .5f, Quaternion.LookRotation(to - from), Vector3.one);
            Box(Vector3.zero, new Vector3(1.4f, .15f, Vector3.Distance(from, to) + .4f), wood, true); frame = old;
        }
        tint = Color.white;
    }
    static void Breakwater(int side)
    {
        var p0 = new Vector3(QuayEdge(side, PortZ1) + side * 10, 0, PortZ1 - 2); var p1 = new Vector3(side * 100, 0, 572); var p2 = new Vector3(side < 0 ? -22 : 32, 0, 584);
        Vector3 B(float t) => (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
        for (int i = 0; i < 40; i++)
        {
            var a = B(i / 40f); var b = B((i + 1) / 40f); var dir = (b - a); dir.y = 0;
            var old = frame; frame = Matrix4x4.TRS((a + b) * .5f, Quaternion.LookRotation(dir), Vector3.one);
            tint = Color.Lerp(P("E2CFAA"), P("D2BC94"), Mathf.PerlinNoise(i * .3f, side));
            Box(new Vector3(0, -3, 0), new Vector3(9, 12.4f, dir.magnitude + .2f), trim, true);
            tint = P("E9D7B5"); Box(new Vector3(0, 3.35f, 0), new Vector3(7.6f, .3f, dir.magnitude + .2f), paving, true);
            tint = Color.white; Box(new Vector3(-side * 4.2f, 4, 0), new Vector3(.8f, 1.3f, dir.magnitude + .2f), trim, true);
            frame = old;
            if (i % 2 == 0) Rock(a + new Vector3(side * D(5, 7), -6, 0), new Vector3(D(1.6f, 2.8f), D(6, 8), D(1.6f, 2.8f)), 7);
            if (i % 8 == 4) Lamp(a + Vector3.up * 3.5f + Vector3.right * side * 2.5f);
        }
        if (side > 0) { tint = P("F3E8D2"); Cylinder(p2, 3.4f, 9, trim, true, 16, 2.8f); tint = P("B66547"); Lathe(p2 + Vector3.up * 9, new[] { new Vector2(3.1f, 0), new Vector2(.1f, 3) }, 16, roof, true); tint = Color.white; }
    }
    static void Lighthouse(Vector3 p)
    {
        tint = P("F3E8D2"); Cylinder(p, 5.5f, 34, plaster, true, 24, 3.6f);
        for (int i = 0; i < 6; i++) { tint = i % 2 == 0 ? P("B66547") : P("F3E8D2"); Cylinder(p + Vector3.up * (5 + i * 5), 5.45f - i * .32f, .7f, trim, true, 24, 5.3f - i * .32f); }
        tint = Color.white; Cylinder(p + Vector3.up * 34, 5.2f, .6f, trim, true, 24);
        Rail(p + new Vector3(-4.6f, 34.6f, 0), p + new Vector3(4.6f, 34.6f, 0));
        tint = Palette.Brass; for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Beam(p + new Vector3(Mathf.Cos(a) * 2.4f, 34.6f, Mathf.Sin(a) * 2.4f), p + new Vector3(Mathf.Cos(a) * 2.4f, 39, Mathf.Sin(a) * 2.4f), .18f, brass, true); }
        tint = Color.white; Ellipsoid(p + Vector3.up * 36.8f, new Vector3(1.6f, 2, 1.6f), glow, 14, 9);
        tint = Palette.Brass; Dome(p + Vector3.up * 39, 3, 2.4f, brass, true, 20); Cylinder(p + Vector3.up * 41.3f, .1f, 2, brass, false, 6);
        style = null; shutterTint = Palette.Shutter;
        for (int i = 0; i < 5; i++) { var old = frame; frame = Matrix4x4.TRS(p, Quaternion.Euler(0, i * 72, 0), Vector3.one); Window(new Vector3(0, 6 + i * 5.5f, -(5.4f - (6 + i * 5.5f) / 34 * 1.9f) - .05f), .8f, 1.5f, true); frame = old; }
        Spot(p + Vector3.up * 34.7f + Vector3.right * 3.5f, "Lighthouse gallery", true);
        for (int i = 0; i < 3; i++) Anchor(p + new Vector3(Mathf.Cos(i * 2.1f) * 9, 24, Mathf.Sin(i * 2.1f) * 9));
    }
    // A galleon at its waterline. Returns crow's-nest centres for plank walks.
    static Vector3[] Ship(Vector3 at, float yaw, float length, int masts, string name)
    {
        var old = frame; var rot = Quaternion.Euler(0, yaw, 0); frame = Matrix4x4.TRS(at, rot, Vector3.one);
        float beam = length * .27f; const int n = 15;
        float Half(float t) => beam * .5f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Lerp(.14f, 1, t))), .55f);
        float DeckY(float t) => 3 + 1.4f * Mathf.Pow(1 - t, 3) + .9f * Mathf.Pow(t, 3);
        float Z(float t) => (t - .5f) * length;
        var paint = Pick("2F5F8F", "B66547", "2E7C7A", "DAAE57");
        Vector3 S(float t, int j, int sideSign)
        {
            float w = Half(t), y = DeckY(t);
            var pts = new[] { new Vector2(w * .98f, y + 1), new Vector2(w, y), new Vector2(w * 1.02f, 1.6f), new Vector2(w * .96f, 0), new Vector2(w * .7f, -1.8f), new Vector2(0, -3 + .6f * (t - .5f) * (t - .5f)) };
            return new Vector3(pts[j].x * sideSign, pts[j].y, Z(t));
        }
        smooth = true;
        for (int i = 0; i < n - 1; i++)
        {
            float t0 = i / (n - 1f), t1 = (i + 1f) / (n - 1f);
            for (int j = 0; j < 5; j++)
            {
                tint = j == 0 ? paint : j == 2 ? P("2E3A40") : j >= 3 ? P("8C5E44") : P("6E4A35");
                foreach (int sd in new[] { -1, 1 }) QuadN(S(t0, j, sd), S(t1, j, sd), S(t1, j + 1, sd), S(t0, j + 1, sd), Vector3.right * sd, wood, true);
            }
        }
        smooth = false;
        // Transom and stern windows.
        tint = P("6E4A35");
        for (int j = 0; j < 5; j++) foreach (int sd in new[] { -1, 1 }) Tri(new Vector3(0, S(0, j, 1).y, Z(0)), S(0, j, sd), S(0, j + 1, sd), wood, true);
        for (int j = 0; j < 5; j++) foreach (int sd in new[] { -1, 1 }) Tri(new Vector3(0, S(0, j, 1).y, Z(0)), S(0, j + 1, sd), S(0, j, sd), wood, true);
        // Deck, bulwark caps.
        for (int i = 0; i < n - 1; i++)
        {
            float t0 = i / (n - 1f), t1 = (i + 1f) / (n - 1f);
            tint = P("C9A27A");
            QuadN(new Vector3(-Half(t0) * .97f, DeckY(t0), Z(t0)), new Vector3(-Half(t1) * .97f, DeckY(t1), Z(t1)), new Vector3(Half(t1) * .97f, DeckY(t1), Z(t1)), new Vector3(Half(t0) * .97f, DeckY(t0), Z(t0)), Vector3.up, wood, true);
            tint = paint;
            foreach (int sd in new[] { -1, 1 })
            {
                QuadN(new Vector3(sd * Half(t0) * .9f, DeckY(t0), Z(t0)), new Vector3(sd * Half(t1) * .9f, DeckY(t1), Z(t1)), new Vector3(sd * Half(t1) * .9f, DeckY(t1) + 1, Z(t1)), new Vector3(sd * Half(t0) * .9f, DeckY(t0) + 1, Z(t0)), Vector3.left * sd, wood, true);
                QuadN(new Vector3(sd * Half(t0) * .9f, DeckY(t0) + 1, Z(t0)), new Vector3(sd * Half(t1) * .9f, DeckY(t1) + 1, Z(t1)), S(t1, 0, sd), S(t0, 0, sd), Vector3.up, wood, true);
            }
        }
        // Sterncastle with windows and lanterns, forecastle, bowsprit.
        float castleZ = Z(.11f), castleY = DeckY(.11f);
        tint = P("6E4A35"); Box(new Vector3(0, castleY + 1.6f, castleZ), new Vector3(Half(.11f) * 1.8f, 3.2f, length * .2f), wood, true);
        tint = paint; Box(new Vector3(0, castleY + 3.3f, castleZ), new Vector3(Half(.11f) * 1.85f, .3f, length * .2f + .2f), wood, true);
        style = null; shutterTint = paint;
        var save = frame; frame *= Matrix4x4.TRS(new Vector3(0, castleY, Z(0) - .05f), Quaternion.Euler(0, 180, 0), Vector3.one);
        for (int i = -1; i <= 1; i++) Window(new Vector3(i * 1.6f, .6f, -.25f), .8f, 1.3f, false, 1);
        frame = save;
        tint = Color.white; foreach (int sd in new[] { -1, 1 }) Ellipsoid(new Vector3(sd * 1.4f, castleY + 4.1f, Z(0) + .2f), new Vector3(.25f, .4f, .25f), glow, 8, 5);
        Stairs(new Vector3(Half(.3f) * .4f, DeckY(.3f), Z(.3f)), new Vector3(Half(.3f) * .4f, castleY + 3.2f, Z(.21f)), 1.4f);
        tint = P("6E4A35"); Box(new Vector3(0, DeckY(.86f) + 1.1f, Z(.86f)), new Vector3(Half(.86f) * 1.7f, 2.2f, length * .15f), wood, true);
        tint = P("7A5340"); Beam(new Vector3(0, DeckY(1) + 1, Z(.97f)), new Vector3(0, DeckY(1) + 4.5f, Z(1) + 8), .45f, wood, true);
        // Masts, yards, sails, crow's nests with rope ladders.
        var nests = new List<Vector3>();
        float[] spots = masts == 3 ? new[] { .25f, .52f, .76f } : new[] { .4f, .7f };
        float[] heights = masts == 3 ? new[] { length * .52f, length * .68f, length * .6f } : new[] { length * .62f, length * .56f };
        for (int m = 0; m < masts; m++)
        {
            float t = spots[m], h = heights[m], dy = DeckY(t), z = Z(t);
            tint = P("8C5E44"); Cylinder(new Vector3(0, dy, z), .45f, h, wood, true, 10, .25f);
            float nestY = dy + h * .62f;
            Cylinder(new Vector3(0, nestY - .35f, z), 1.6f, .35f, wood, true, 16);
            for (int i = 0; i < 10; i++) { float a = i * Mathf.PI / 5; Box(new Vector3(Mathf.Cos(a) * 1.5f, nestY + .4f, z + Mathf.Sin(a) * 1.5f), new Vector3(.9f, .8f, .12f), wood, true); }
            nests.Add(frame.MultiplyPoint3x4(new Vector3(0, nestY, z)));
            foreach (var (yy, span) in new[] { (dy + h * .42f, beam * 1.9f), (dy + h * .82f, beam * 1.4f) })
            {
                tint = P("7A5340"); Box(new Vector3(0, yy, z), new Vector3(span, .45f, .45f), wood, true);
                if (yy < nestY)
                {
                    tint = P("F3E9D4"); wind = .5f;
                    for (int k = 0; k < 6; k++)
                    {
                        float x0 = -span * .45f + span * .9f * k / 6, x1 = -span * .45f + span * .9f * (k + 1) / 6;
                        for (int r = 0; r < 4; r++)
                        {
                            float y0 = yy - .2f - r * 1.6f, y1 = y0 - 1.6f, b0 = Mathf.Sin(r / 4f * Mathf.PI) * 1.4f, b1 = Mathf.Sin((r + 1) / 4f * Mathf.PI) * 1.4f;
                            Quad(new Vector3(x0, y0, z + b0), new Vector3(x1, y0, z + b0), new Vector3(x1, y1, z + b1), new Vector3(x0, y1, z + b1), cloth);
                            Quad(new Vector3(x0, y1, z + b1), new Vector3(x1, y1, z + b1), new Vector3(x1, y0, z + b0), new Vector3(x0, y0, z + b0), cloth);
                        }
                    }
                    wind = 0;
                }
                else { tint = P("F3E9D4"); var f = frame; frame *= Matrix4x4.TRS(new Vector3(-span * .45f, yy - .45f, z), Quaternion.Euler(0, 0, -90), Vector3.one); Cylinder(Vector3.zero, .38f, span * .9f, cloth, false, 8); frame = f; }
                Anchor(frame.MultiplyPoint3x4(new Vector3(span * .5f - .3f, yy - 1.4f, z)));
            }
            tint = paint; Banner(new Vector3(0, dy + h + .2f, z), 1, paint);
            // Rope ladder from the deck to the crow's nest.
            RopeLadder(frame.MultiplyPoint3x4(new Vector3(0, nestY + .05f, z - 1.62f)), frame.MultiplyVector(Vector3.back), nestY - dy - .2f);
            // Shrouds.
            tint = P("3B3F4E"); foreach (int sd in new[] { -1, 1 }) for (int k = -1; k <= 1; k++) Beam(new Vector3(sd * Half(t) * .95f, dy + 1, z + k * .8f), new Vector3(0, nestY - .3f, z), .04f, wood);
        }
        // Hull ladder for swimmers, amidships on both sides.
        foreach (int sd in new[] { -1, 1 }) RopeLadder(frame.MultiplyPoint3x4(new Vector3(sd * (Half(.5f) * 1.05f), DeckY(.5f) + 1, Z(.5f))), frame.MultiplyVector(Vector3.right * sd), DeckY(.5f) + 1.2f);
        Spot(nests[nests.Count - 1] + Vector3.up * .1f, "Crow's nest of the " + name, true);
        frame = old; tint = Color.white;
        return nests.ToArray();
    }
    static void RopeLadder(Vector3 top, Vector3 outward, float length)
    {
        outward.y = 0; outward.Normalize();
        var vine = new GameObject("Rope ladder").AddComponent<ClimbableVine>();
        vine.transform.SetParent(group, false); vine.transform.position = top; vine.transform.rotation = Quaternion.LookRotation(outward);
        vine.length = length; vine.width = .9f;
        var old = frame; frame = Matrix4x4.TRS(top, Quaternion.LookRotation(outward), Vector3.one);
        tint = P("C9A27A");
        foreach (int s in new[] { -1, 1 }) Beam(new Vector3(s * .35f, 0, .12f), new Vector3(s * .35f, -length, .12f), .05f, wood);
        for (float y = .3f; y < length; y += .45f) Box(new Vector3(0, -y, .12f), new Vector3(.7f, .05f, .05f), wood);
        frame = old; tint = Color.white;
    }
    static void PlankWalk(Vector3 a, Vector3 b)
    {
        var d = b - a; var old = frame; frame = Matrix4x4.TRS((a + b) * .5f + Vector3.up * .05f, Quaternion.LookRotation(d), Vector3.one);
        tint = P("A88462"); Box(Vector3.zero, new Vector3(.55f, .14f, d.magnitude - 2.6f), wood, true);
        tint = P("3B3F4E"); foreach (int s in new[] { -1, 1 }) Beam(new Vector3(s * .5f, 1, -d.magnitude * .5f + 1.4f), new Vector3(s * .5f, 1, d.magnitude * .5f - 1.4f), .035f, wood);
        frame = old; tint = Color.white;
        Spot(Vector3.Lerp(a, b, .5f) + Vector3.up * .3f, "Plank walk between masts", true);
    }

    // ---------- collectibles and life ----------
    static int PlaceSunstones()
    {
        var chosen = new List<(Vector3 p, string place)>();
        var candidates = new List<(Vector3 p, string place, bool fixedSpot)>();
        foreach (var s in spots)
        {
            if (s.place.StartsWith("Swing ring")) { candidates.Add((s.p, s.place, true)); continue; }
            if (!Physics.Raycast(s.p + Vector3.up * 1.2f, Vector3.down, out var floor, 3.5f, 1 | 1 << 8, QueryTriggerInteraction.Ignore) || floor.normal.y < .7f) continue;
            var foot = floor.point;
            if (Physics.CheckCapsule(foot + Vector3.up * .4f, foot + Vector3.up * 1.6f, .3f, 1, QueryTriggerInteraction.Ignore)) continue;
            candidates.Add((foot + Vector3.up * 1.05f, s.place, s.fixedSpot));
        }
        foreach (var c in candidates.Where(c => c.fixedSpot)) if (chosen.All(o => Vector3.Distance(o.p, c.p) > 6)) chosen.Add((c.p, c.place));
        var rest = candidates.Where(c => !c.fixedSpot).ToList();
        while (chosen.Count < 48 && rest.Count > 0)
        {
            var best = rest.OrderByDescending(c => chosen.Count == 0 ? 0 : chosen.Min(o => Vector3.Distance(o.p, c.p))).First();
            rest.Remove(best);
            if (chosen.Count > 0 && chosen.Min(o => Vector3.Distance(o.p, best.p)) < 14) continue;
            chosen.Add((best.p, best.place));
        }
        // Two underwater stones reward diving.
        foreach (float z in new[] { 60f, 240f })
        {
            float x = Center(z); chosen.Add((new Vector3(x, Bed(x, z) + .9f, z), "Riverbed"));
        }
        var root = new GameObject("Sunstones").transform; root.SetParent(city, false);
        var gemMesh = Gem(); var beamMesh = BeamMesh();
        AssetDatabase.CreateAsset(gemMesh, GeoPath + "/Sunstone gem.asset"); AssetDatabase.CreateAsset(beamMesh, GeoPath + "/Sunstone beacon.asset");
        for (int i = 0; i < chosen.Count; i++)
        {
            var go = new GameObject($"Sunstone {i + 1:00} · {chosen[i].place}"); go.transform.SetParent(root, false); go.transform.position = chosen[i].p;
            var stone = go.AddComponent<Sunstone>(); stone.id = i; stone.place = chosen[i].place;
            var gem = new GameObject("Gem", typeof(MeshFilter), typeof(MeshRenderer)); gem.transform.SetParent(go.transform, false);
            gem.GetComponent<MeshFilter>().sharedMesh = gemMesh; gem.GetComponent<MeshRenderer>().sharedMaterial = sunMat;
            var beam = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer)); beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = Vector3.down * .9f;
            beam.GetComponent<MeshFilter>().sharedMesh = beamMesh; var br = beam.GetComponent<MeshRenderer>(); br.sharedMaterial = beamMat; br.shadowCastingMode = ShadowCastingMode.Off;
        }
        return chosen.Count;
    }
    static Mesh Gem()
    {
        var v = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
        Vector3 top = Vector3.up * .32f, bottom = Vector3.down * .38f;
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3, b = (i + 1) * Mathf.PI / 3;
            var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .22f; var q = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * .22f;
            foreach (var tri in new[] { (p, top, q), (q, bottom, p) }) { int n = v.Count; v.Add(tri.Item1); v.Add(tri.Item2); v.Add(tri.Item3); t.Add(n); t.Add(n + 1); t.Add(n + 2); }
        }
        foreach (var _ in v) c.Add(Color.white);
        var mesh = new Mesh { name = "Sunstone gem" }; mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.SetColors(c); mesh.RecalculateNormals(); return mesh;
    }
    static Mesh BeamMesh()
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); var n = new List<Vector3>();
        const int cuts = 12;
        for (int i = 0; i <= cuts; i++)
        {
            float a = i * Mathf.PI * 2 / cuts; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v.Add(dir * .45f); v.Add(dir * .25f + Vector3.up * 16); uv.Add(new Vector2(i / (float)cuts, 0)); uv.Add(new Vector2(i / (float)cuts, 1)); n.Add(dir); n.Add(dir);
            if (i < cuts) { int k = i * 2; t.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 }); }
        }
        var mesh = new Mesh { name = "Sunstone beacon" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); return mesh;
    }
    static void Life()
    {
        var root = new GameObject("River life").transform; root.SetParent(city, false);
        for (int i = 0; i < 12; i++)
        {
            float z = -20 + i * 32 + D(-6, 6), x = Center(z) + D(-6, 6);
            var school = new GameObject("Fish school " + i).AddComponent<RiverLife>(); school.transform.SetParent(root, false);
            school.transform.position = new Vector3(x, 0, z); school.material = fishMat; school.count = deco.Next(5, 10); school.radius = D(3, 6); school.depth = D(.7f, 1.4f);
        }
        for (int i = 0; i < 4; i++)
        {
            var flock = new GameObject("Birds " + i).AddComponent<RiverLife>(); flock.transform.SetParent(root, false);
            flock.transform.position = new Vector3(D(-20, 20), D(55, 110), D(0, 320)); flock.birds = true; flock.material = birdMat; flock.count = deco.Next(4, 8); flock.radius = D(14, 30);
        }
    }

    // ---------- lighting, sky, post ----------
    static void ConfigureScene()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        pipeline.shadowDistance = 140; pipeline.shadowCascadeCount = 3; pipeline.cascade3Split = new Vector2(.12f, .38f);
        pipeline.msaaSampleCount = 4; pipeline.useSRPBatcher = true; pipeline.supportsHDR = true; EditorUtility.SetDirty(pipeline);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = P("A9CBE2");
        RenderSettings.ambientEquatorColor = P("C7CFCB") * .85f;
        RenderSettings.ambientGroundColor = P("B39878") * .6f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = P("C9DCE4");
        RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 640;
        var sun = RenderSettings.sun; sun.transform.rotation = Quaternion.Euler(44, -38, 0); sun.intensity = 1.5f; sun.color = P("FFEBD0"); sun.shadowStrength = .92f;
        sun.shadows = LightShadows.Soft;
        Camera.main.backgroundColor = Palette.Sky;
        var sky = Asset("Quarter sky", "TinyWorld/CliffSky");
        sky.SetColor("_Sky", P("3F88C5")); sky.SetColor("_Horizon", P("AAD3E6")); sky.SetColor("_Haze", P("F1E5CC"));
        sky.SetColor("_Cloud", P("FFF9EE")); sky.SetColor("_CloudShadow", P("A9BCD6")); sky.SetVector("_SunDir", -sun.transform.forward);
        RenderSettings.skybox = sky; Camera.main.clearFlags = CameraClearFlags.Skybox;
        var data = Camera.main.GetComponent<UniversalAdditionalCameraData>(); if (data) { data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.None; }
        var volume = Object.FindAnyObjectByType<Volume>();
        const string profilePath = "Assets/Settings/QuarterDaylight.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
        T Get<T>() where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, profile); }
            c.active = true; return c;
        }
        var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.Neutral);
        var bloom = Get<Bloom>(); bloom.intensity.Override(.5f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.68f); bloom.tint.Override(P("FFE6C2"));
        var vignette = Get<Vignette>(); vignette.intensity.Override(.2f); vignette.smoothness.Override(.45f); vignette.color.Override(P("2A3A4C"));
        var color = Get<ColorAdjustments>(); color.saturation.Override(14); color.contrast.Override(10); color.postExposure.Override(.1f); color.colorFilter.Override(Color.white);
        var balance = Get<WhiteBalance>(); balance.temperature.Override(7); balance.tint.Override(2);
        var smh = Get<ShadowsMidtonesHighlights>(); smh.shadows.Override(new Vector4(.93f, .98f, 1.08f, 0)); smh.highlights.Override(new Vector4(1.04f, 1.01f, .96f, 0));
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
        foreach (var feature in renderer.rendererFeatures)
            if (feature && feature.name == "ScreenSpaceAmbientOcclusion")
            {
                var so = new SerializedObject(feature);
                so.FindProperty("m_Settings.Intensity").floatValue = .75f;
                so.FindProperty("m_Settings.Radius").floatValue = .5f;
                so.FindProperty("m_Settings.DirectLightingStrength").floatValue = .2f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        EditorUtility.SetDirty(renderer);
    }
}
