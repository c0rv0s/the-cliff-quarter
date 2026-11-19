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
    const int WildlandsShift = TinyWorld.EditorArt.BuildQuarterLandmarks.WildlandsShiftSouth;
    static Rect TravelBounds => Rect.MinMaxRect(-640, -1160-WildlandsShift, 640, 870);
    static void SetSeaLimits(RiverWaterVolume sea)
    {
        sea.transform.position = new Vector3(0, 0, -150-WildlandsShift*.5f);
        sea.size = new Vector3(1400, 24, 2200+WildlandsShift);
    }
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
    static readonly float[] waterStairZ = { 12f, 48f, 125f, 201f, 285f, 332f };
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
            if (root.name == "Cliff City" || root.name == "River water volume" || root.name == "Stream water volumes") Object.DestroyImmediate(root);
        if (AssetDatabase.IsValidFolder(GeoPath)) AssetDatabase.DeleteAsset(GeoPath);
        AssetDatabase.CreateFolder("Assets/Geometry", "Quarter");
        if (!AssetDatabase.IsValidFolder(MatPath)) AssetDatabase.CreateFolder("Assets/Materials", "Quarter");
        rng = new System.Random(731921); deco = new System.Random(5150); streamPath = null;
        meshId = buildings = anchors = triangles = ungrounded = roads = 0; vines = new(); spots = new();
        Materials();
        city = new GameObject("Cliff Quarter").transform;
        InitPads();
        PlanSanctuaryWalks();
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
                        if (StairDistance(zz) > 6 && !NearCascade(side, zz, 7) && !(side < 0 && tier == 0 && zz > 18 && zz < 61))
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
        StartGroup("Southern reach"); SouthernReach(); FinishGroup();
        StartGroup("Stream cascade"); Cascade(); FinishGroup();
        StartGroup("Harbour"); Port(); FinishGroup();
        StartGroup("Canal market"); CanalMarket(); FinishGroup();
        StartGroup("Ship companionways"); ShipCompanionways(); FinishGroup();
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
        Rock(new Vector3(Center(118), -6, 118), new Vector3(8, 12.9f, 10), 11);
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
        WaterStairs();
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
        SetSeaLimits(volume);
        // The farm stream: short boxes along its deep channel, each holding its own surface height.
        var stream = new GameObject("Stream water volumes").transform;
        var path = StreamPath();
        for (int i = 0; i + 2 < path.Length - 6; i += 2)
        {
            Vector3 a = StreamWorld(i, 0), b = StreamWorld(i + 2, 0);
            var box = new GameObject("Stream water").AddComponent<RiverWaterVolume>(); box.transform.SetParent(stream, false);
            box.transform.SetPositionAndRotation((a + b) * .5f, Quaternion.LookRotation(new Vector3(b.x - a.x, 0, b.z - a.z)));
            box.size = new Vector3(11, 2.5f, Vector3.Distance(a, b) + 1);
        }
        var spring = new GameObject("Stream water").AddComponent<RiverWaterVolume>(); spring.transform.SetParent(stream, false);
        spring.transform.position = W0(1, -48); spring.size = new Vector3(15, 2.5f, 15);
        var player = Object.FindAnyObjectByType<WandererMotor>();
        var start = GameObject.Find("Route · Saffron wall and swing alley").transform.position;
        player.transform.position = new Vector3(start.x - 1, start.y + .15f, 23.5f);
        player.ropeMaterial = brass;
        player.travelBounds=TravelBounds;
        var orbit = Object.FindAnyObjectByType<WandererCamera>(); orbit.transform.position = player.transform.position + Vector3.up * 1.25f; orbit.transform.rotation = Quaternion.Euler(3, 20, 0);
        var rig = Object.FindAnyObjectByType<CinemachineCamera>(); rig.Lens.FarClipPlane = 900; rig.Lens.NearClipPlane = .15f;
        Camera.main.farClipPlane = 900; Camera.main.nearClipPlane = .15f;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Physics.SyncTransforms();
        var discoveries = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Geometry/Discoveries/Quarter landmarks.prefab");
        if (discoveries) PrefabUtility.InstantiatePrefab(discoveries, scene);
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
        foreach (var surface in new[] { roof, trim, plaster, paving, brass, wood }) surface.SetFloat("_Cull", (float)CullMode.Off);
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
        const int cols = 56, seaRows = 56, harbourRows = 24;
        const float step = 3;
        int cityRows = Mathf.CeilToInt((TerrainEnd - TerrainStart) / step);
        int rows = seaRows + cityRows + harbourRows;
        var grid = new Vector3[rows + 1, cols + 1];
        // Extend the very same cross section at both mouths. The outer rows dive below the sea floor.
        for (int i = 0; i <= rows; i++)
        {
            int cityRow = Mathf.Clamp(i - seaRows, 0, cityRows);
            float join = TerrainStart + cityRow * step;
            float distance = (i - seaRows - cityRow) * step, z = join + distance;
            float center = Center(join), half = Width(join) + 32;
            float left, right, length;
            if (distance < 0)
            {
                float Toe(int side) {
                    var tip = Shore(side, -7, -4, 1);
                    return Shore(side, -7, -4, Mathf.InverseLerp(join, tip.z, z)).x;
                }
                left = Toe(-1); right = Toe(1); length = seaRows * step;
            }
            else { left = QuayEdge(-1, z); right = QuayEdge(1, z); length = harbourRows * step; }
            float run = Mathf.Abs(distance), blend = Edge01(0, 24, run);
            float middle = (left + right) * .5f, channel = (right - left) * .5f;
            for (int j = 0; j <= cols; j++)
            {
                float u = 2f * j / cols - 1;
                float joinX = center + half * u;
                float x = Mathf.Lerp(joinX, middle + (channel + 24) * u, blend);
                float bank = Mathf.Abs(x - middle);
                float silt = Mathf.PerlinNoise(x * .09f + 13, z * .07f) - .5f;
                float bottom = Mathf.Lerp(-6.5f, -3.75f, Edge01(0, channel, bank)) + silt * .62f;
                bottom = Mathf.Lerp(bottom, -9.55f, Mathf.Max(Edge01(channel + 8, channel + 23, bank), Edge01(length * .54f, length, run)));
                grid[i, j] = new Vector3(x, Mathf.Lerp(Bed(joinX, join), bottom, blend), z);
            }
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
        SubmergedShoreEdges();
        smooth = false; tint = old;
    }

    static void SubmergedShoreEdges()
    {
        const float footY = -9.55f;
        Vector3 Foot(Vector3 p, Vector3 offset) => new(p.x + offset.x, Mathf.Min(footY, p.y - .1f), p.z + offset.z);
        void Apron(Vector3 a, Vector3 b, Vector3 offset) {
            tint = Color.Lerp(P("C7AA83"), P("DCC8A3"), Mathf.PerlinNoise(a.x * .08f, a.z * .09f));
            QuadN(a, b, Foot(b,offset), Foot(a,offset), Vector3.up, bedMat);
        }

        foreach (int side in new[]{-1,1})
        {
            var section = CoastSamples(side);
            var inner = Vector3.left * (side * 4);
            var outer = Vector3.right * (side * 6);
            var offshore = Vector3.back * 10;
            Vector3 At(Vector2 sample, int row) => Shore(side, sample.x, sample.y, row / (float)CoastRows);
            for (int row = 0; row < CoastRows; row++) {
                Apron(At(section[0], row), At(section[0], row+1), inner);
                Apron(At(section[section.Count-1], row), At(section[section.Count-1], row+1), outer);
            }
            for (int j = 1; j < section.Count; j++)
                Apron(At(section[j-1], CoastRows), At(section[j], CoastRows), offshore);
            var near = At(section[0], CoastRows); var far = At(section[section.Count-1], CoastRows);
            void Corner(Vector3 p, Vector3 offset) {
                var a=Foot(p,offset); var b=Foot(p,offshore);
                if(Vector3.Cross(a-p,b-p).y>0)Tri(p,a,b,bedMat,true);else Tri(p,b,a,bedMat,true);
            }
            Corner(near,inner); Corner(far,outer);
        }

        // Continue the island's last sand ring until it crosses the offshore floor.
        for (int i=0;i<96;i++) for (int j=48;j<51;j++) {
            float a=i*Mathf.PI/48, b=(i+1)*Mathf.PI/48, r=j*.025f, next=(j+1)*.025f;
            var p=KeyPoint(a,r);
            tint=Color.Lerp(P("D7BE92"),P("F0DEB0"),Mathf.PerlinNoise(p.x*.2f,p.z*.16f));
            QuadN(p, KeyPoint(b,r), KeyPoint(b,next), KeyPoint(a,next), Vector3.up, bedMat);
        }
    }

    public static object RefreshRiverBed()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name!="CliffQuarter" || scene.isDirty || EditorApplication.isPlaying)
            throw new Exception("Open the saved CliffQuarter in Edit mode.");
        var floor = GameObject.Find("Cliff Quarter/River channel and quays/River bed · solid · smooth");
        bedMat = floor.GetComponent<Renderer>().sharedMaterial;
        batches=new(); frame=Matrix4x4.identity; tint=Color.white; smooth=ghost=false; wind=0; aoBase=float.NaN;
        InitPads(); PlanSanctuaryWalks(); RiverBed();
        var mesh = floor.GetComponent<MeshFilter>().sharedMesh;
        Weld(batches.Values.Single(), out var v, out var c, out var ix);
        mesh.Clear(); mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(ix,0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var collider=floor.GetComponent<MeshCollider>(); collider.sharedMesh=null; collider.sharedMesh=mesh;
        EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
        return new{vertices=v.Count, triangles=ix.Count/3};
    }
    static void QuayWall(int side)
    {
        var old = tint; tint = P("E9D9BB"); aoBase = -1.4f; aoSpan = 3.2f;
        for (float z = TerrainStart; z < TerrainEnd - .01f; z += 2.5f)
        {
            float z1 = z + 2.5f;
            var a = new Vector3(Edge(side, 0, z) + side * .06f, Level(side, 0, z) - .03f, z); var b = new Vector3(Edge(side, 0, z1) + side * .06f, Level(side, 0, z1) - .03f, z1);
            var a0 = new Vector3(a.x, -9.6f, z); var b0 = new Vector3(b.x, -9.6f, z1);
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
            float x0 = Edge(side, tier, z), x1 = Edge(side, tier + 1, z) + (tier == 3 ? side * .5f : 0), y = Level(side, tier, z) - .03f;
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
    static float UplandWidth(int side) => side > 0 ? 261 : UMax;
    // GLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).
    static float Edge01(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
    static Vector3 Crest(int side, float z) => CliffProfile(side, z, 30)[30];
    static readonly (float u, float z, float r, float h)[] WestPeaks = { (150, 60, 40, 70), (150, 165, 45, 85), (140, 335, 40, 60), (120, 212, 22, 58), (45, 385, 30, 30), (110, 20, 30, 35) };
    static readonly (float u, float z, float r, float h)[] EastPeaks = { (38, 22, 22, 10), (128, 250, 26, 12), (150, 120, 40, 14) };
    static List<(float u, float z, float r, float h)>[] pads;
    static float RiverU(float z) => 95 + 28 * Mathf.Sin(z * .011f + .6f) + 9 * Mathf.Sin(z * .034f);
    static float WaterY(float z) => 108.5f - (z + 60) * .013f;
    // The farm stream leaves its plateau course at BendZ and spills over the east crest at CascadeZ,
    // where the crest dips below stream level, then falls terrace by terrace into the river.
    const float BendZ = 312, CascadeZ = 336;
    static Vector3[] streamPath;
    // Centreline as (u, water height, z), u measured from the east crest.
    static Vector3[] StreamPath()
    {
        if (streamPath != null) return streamPath;
        var p = new List<Vector3>();
        for (float z = -52; z < BendZ - 2; z += 3) p.Add(new Vector3(RiverU(z), WaterY(z), z));
        Vector3 a = new(RiverU(BendZ), WaterY(BendZ), BendZ), c = new(RiverU(BendZ), 0, CascadeZ), b = new(0, Crest(1, CascadeZ).y + .3f, CascadeZ);
        for (int i = 0; i <= 24; i++)
        {
            float t = i / 24f; var q = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
            q.y = Mathf.Lerp(a.y, b.y, Mathf.SmoothStep(0, 1, (t - .3f) / .7f)); p.Add(q);
        }
        return streamPath = p.ToArray();
    }
    // Horizontal distance to the stream centreline and the water height there.
    static (float d, float w) StreamAt(float u, float z)
    {
        var path = StreamPath(); float best = float.MaxValue, w = 0; var p = new Vector2(u, z);
        for (int i = 0; i + 1 < path.Length; i++)
        {
            Vector2 a = new(path[i].x, path[i].z), b = new(path[i + 1].x, path[i + 1].z), ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude), d = (a + ab * t - p).sqrMagnitude;
            if (d < best) { best = d; w = Mathf.Lerp(path[i].y, path[i + 1].y, t); }
        }
        return (Mathf.Sqrt(best), w);
    }
    // Stream-space point (u, y, z) offset `across` the flow, in world space.
    static Vector3 StreamWorld(int i, float across)
    {
        var path = StreamPath(); var t = path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(i - 1, 0)];
        var n = new Vector2(t.z, -t.x).normalized * across; float u = path[i].x + n.x, z = path[i].z + n.y;
        return new Vector3(Crest(1, z).x + u, path[i].y, z);
    }
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
            var (d, w) = StreamAt(u, z);
            if (z < -40) d = Mathf.Min(d, Mathf.Sqrt((u - RiverU(-40)) * (u - RiverU(-40)) + (z + 48) * (z + 48)) * .6f);
            h = Mathf.Lerp(h, Mathf.Max(h, w + .35f), 1 - Edge01(9, 16, d));
            // Deep enough to swim, shoaling to a shallow lip at the crest.
            h = Mathf.Lerp(h, w - Mathf.Lerp(.3f, 1.8f, Edge01(2, 16, u)), 1 - Edge01(4.5f, 9, d));
        }
        if (side < 0 && walks != null) {
            float worldX = WestCrestX(z) - u;
            float rear = (WestCrestX(118)-80) - worldX, flank = Mathf.Abs(z-118);
            float edge = Mathf.Max(flank-49, Mathf.Max(-17-rear,rear-85));
            h = Mathf.Lerp(Mathf.Min(h,167.4f), h, Edge01(0,7,edge));
            h = CutWalks(new Vector2(worldX,z),h);
        }
        return Mathf.Lerp(h, -10, Edge01(UplandWidth(side) - 24, UplandWidth(side), u));
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
        int zCuts = Mathf.RoundToInt((TerrainEnd - TerrainStart) / TerrainStep), uCuts = Mathf.RoundToInt(UplandWidth(side) / UStep);
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
        Box(new Vector3(0, -.41f, 0), new Vector3(4.6f, .8f, length + 2), paving, true);
        // Rails start clear of the landing so the stair can step onto the bridge.
        foreach (int s in new[] { -1, 1 }) Rail(new Vector3(s * 2.15f, 0, -length * .5f + 4.6f), new Vector3(s * 2.15f, 0, length * .5f));
        frame = old;
        for (int i = 0; i < 2; i++) Lamp(s1 + new Vector3(i == 0 ? 3 : -3, 0, 2.5f));
        Spot(s1 + Vector3.up * .05f, side < 0 ? "Stair of the Sanctuaries" : "Stair to the Farmlands", true);
        var route = new GameObject(side < 0 ? "Grand stair west" : "Grand stair east").transform; route.SetParent(city, false);
        foreach (var (name, pos) in new[] { ("A", s0), ("B", s1), ("C", crest) }) { var n = new GameObject(name).transform; n.SetParent(route, false); n.position = pos; }
    }

    static bool CrossingMouth(int side,int tier,float a,float b)
    {
        foreach(var bridge in BridgeList){
            if(tier!=bridge.tier)continue;
            float z=bridge.z+(side>0?8:0);
            if(a<z+bridge.width*.55f+.65f && b>z-bridge.width*.55f-.65f)return true;
        }
        return tier==0 && waterStairZ.Any(z=>a<z+2.5f && b>z-2.8f);
    }

    static void OpenExistingCrossings()
    {
        for(int tier=0;tier<4;tier++)foreach(int side in new[]{-1,1})for(int segment=0;segment<6;segment++){
            var street=city.Find((side<0?"West ":"East ")+districts[tier]+" "+(segment+1));
            if(!street)continue;
            float start=-32+64*segment, end=Mathf.Min(start+64,342-tier*14+(side>0?12:0));
            int cuts=Mathf.CeilToInt((end-start)/2.5f);
            var openings=new List<Vector2>();
            for(int i=0;i<cuts;i+=2){
                float a=Mathf.Lerp(start,end,i/(float)cuts),b=Mathf.Lerp(start,end,(i+1f)/cuts);
                if(CrossingMouth(side,tier,a,b))openings.Add(new Vector2(a-.16f,b+.16f));
            }
            if(openings.Count==0)continue;
            foreach(var f in street.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Carved limestone"))){
                var m=f.sharedMesh;var v=m.vertices;var tr=m.triangles;var kept=new List<int>(tr.Length);
                for(int j=0;j<tr.Length;j+=3){
                    Vector3 p=(v[tr[j]]+v[tr[j+1]]+v[tr[j+2]])/3;
                    float h=p.y-Level(side,tier,p.z);
                    bool rail=h>-.035f && h<.83f && Mathf.Abs(p.x-Edge(side,tier,p.z)-side*.4f)<.26f && openings.Any(o=>p.z>=o.x && p.z<=o.y);
                    if(!rail){kept.Add(tr[j]);kept.Add(tr[j+1]);kept.Add(tr[j+2]);}
                }
                if(tr.Length==kept.Count)continue;
                m.SetTriangles(kept,0);EditorUtility.SetDirty(m);
                foreach(var col in f.GetComponentsInChildren<MeshCollider>()){col.sharedMesh=null;col.sharedMesh=m;}
            }
        }
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
            if (i % 2 == 0 && StairDistance(a) > 16 && !NearCascade(side, a, 4) && !CrossingMouth(side,tier,a,b))
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
        for (int tier = 0; tier < 4; tier++) { float y = Level(1, tier, CascadeZ); keepOut.Add((new Vector3(Edge(1, tier + 1, CascadeZ), y, CascadeZ), new Vector3(Edge(1, tier, CascadeZ), y, CascadeZ), 4)); }
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
            if (Chance(.55f)) { if (!NearCascade(side, mid.z, 6)) OliveTree(mid, D(.75f, 1.05f)); }
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
            // Cornice and plinth overhang the back wall too, so neither shares its plane.
            Box(new Vector3(0, yt - .14f, mid - .125f), new Vector3(width + .3f, .3f, back - f + .55f), trim, true);
            if (k == 0) Box(new Vector3(0, .15f, mid), new Vector3(width + .16f, .55f, depth + .24f), trim, true);
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
                        smooth = true; QuadN(V(-width * .5f + .3f, a0), V(-width * .5f + .3f, a1), V(width * .5f - .3f, a1), V(width * .5f - .3f, a0), Vector3.up, plaster); smooth = false;
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
    static void WaterStairs()
    {
        for (int side = -1; side <= 1; side += 2)
            foreach (float z in waterStairZ)
            {
                var upper = Point(side, 0, z, .22f);
                var lower = new Vector3(Edge(side, 0, z - 8) - side * 5.5f, -.45f, z - 8);
                Stairs(lower, upper, 4);
                frame = Matrix4x4.TRS(new Vector3(lower.x-side*2.3f, 0, lower.z), Quaternion.Euler(0, side * 20, 0), Vector3.one);
                Boat(); frame = Matrix4x4.identity;
                Lamp(upper + new Vector3(side * 1.2f, 0, 2.6f));
            }
    }
    static void Stairs(Vector3 a, Vector3 b, float width)
    {
        var old = frame; var delta = b - a; var flat = new Vector3(delta.x, 0, delta.z);
        int count = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta.y) / .23f)); float tread = flat.magnitude / count;
        var oldTint = tint; tint = Color.white;
        frame = old * Matrix4x4.TRS(a, Quaternion.LookRotation(flat), Vector3.one);
        for (int i = 0; i < count; i++)
        {
            float y = delta.y * (i + 1f) / count;
            Box(new Vector3(0, y - .14f, (i + .5f) * tread), new Vector3(width + i % 2 * .02f, .28f, tread + .025f), paving);
            if (i % 5 == 0) Box(new Vector3(width * .5f - .18f, y + .18f, (i + .5f) * tread), new Vector3(.3f, .36f, tread), trim, true);
            if (i % 9 == 4 && Chance(.5f)) Planter(new Vector3(-width * .5f + .3f, y, (i + .5f) * tread), .28f);
        }
        var stairFrame = frame;
        frame *= Matrix4x4.TRS(new Vector3(0, delta.y * .5f + .06f, flat.magnitude * .5f), Quaternion.Euler(-Mathf.Atan2(delta.y, flat.magnitude) * Mathf.Rad2Deg, 0, 0), Vector3.one);
        Box(Vector3.zero, new Vector3(width - .04f, .12f, delta.magnitude), paving, true);
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
        frame = old; tint = oldTint;
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
        Cylinder(r.turnOut + Vector3.down * .42f, 3, .4f, paving, true, 32);
        Box(r.mid + Vector3.down * .21f, new Vector3(5.6f, .4f, 4), paving, true);
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
        var oldTint = tint; tint = Color.white;
        var old = frame; frame = Matrix4x4.TRS((a + b) * .5f, Quaternion.Euler(0, -Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, 0) * Quaternion.Euler(0, 0, Mathf.Asin(d.y / length) * Mathf.Rad2Deg), Vector3.one);
        Box(new Vector3(0, -.35f, 0), new Vector3(length + .8f, .7f, width), paving, true);
        Arch(new Vector3(0, -rise - 1.76f, 0), length * .5f, rise, 1.1f, width * .8f, trim, true, 48);
        for (int side = -1; side <= 1; side += 2)
        {
            for(int i=0;i<32;i++){
                float x0=length*(i/32f-.5f),x1=length*((i+1)/32f-.5f),zFace=side*width*.386f;
                float ArchY(float x)=>-rise-1.76f+(rise+1.1f)*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(x/(length*.5f+1.1f),2)));
                QuadN(new Vector3(x0,ArchY(x0),zFace),new Vector3(x1,ArchY(x1),zFace),new Vector3(x1,-.68f,zFace),new Vector3(x0,-.68f,zFace),Vector3.forward*side,trim,false);
            }
            Rail(new Vector3(-length * .5f, .03f, side * (width * .5f - .1f)), new Vector3(length * .5f, .03f, side * (width * .5f - .1f)));
            Arch(new Vector3(0, -rise - 1.0f, side * width * .42f), length * .5f, rise, .18f, .12f, brass, false, 48);
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
        tint = oldTint;
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
        if (!refreshingMountains || group.name=="Mountain sanctuaries")
        {
            var root = new GameObject("Brass swing ring " + (++anchors)); root.transform.SetParent(group.name=="Mountain sanctuaries"?group:city, false); root.transform.position = p;
            root.AddComponent<TraversalAnchor>();
        }
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
        tint = Color.white;
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
        if (BlockedCircle(frame.MultiplyPoint3x4(p), 1.8f * size, 6 * size)) return;
        if (type == 2) { Cypress(p, 5 * size); return; }
        if (type == 1) { Palm(p, size); return; }
        OliveTree(p, size);
    }
    static void OliveTree(Vector3 p, float size)
    {
        if (BlockedCircle(frame.MultiplyPoint3x4(p), 1.8f * size, 6 * size)) return;
        if (group.name=="Farmlands" && !TinyWorld.EditorArt.QuarterPlanting.TryGround(p,out p)) return;
        var old = frame; frame = old * Matrix4x4.TRS(p, Quaternion.Euler(0, D(0, 360), 0), Vector3.one * size);
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
        if ((group.name=="Crown observatory" || group.name=="Farmlands") && !TinyWorld.EditorArt.QuarterPlanting.TryGround(p,out p)) return;
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
        var old = frame; frame = old * Matrix4x4.TRS(p, Quaternion.Euler(0, D(0, 360), 0), Vector3.one * size);
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
    static Vector3 PadCenter(int side, int index) { var pd = pads[side < 0 ? 0 : 1][index]; var p=UP(side,pd.u,pd.z); if(side<0)p.y=pd.h; return p; }
    enum WalkKind { Path, Stairs, Bridge }
    sealed class Walk
    {
        public Vector3 a,b;
        public float width;
        public WalkKind kind;
        public bool royalStair;
    }
    static List<Walk> walks;
    static readonly Dictionary<float,float> crestCache = new();
    static float WestCrestX(float z)
    {
        if (!crestCache.TryGetValue(z,out var x)) crestCache[z] = x = Crest(-1,z).x;
        return x;
    }
    static void PlanSanctuaryWalks()
    {
        var entrance=GrandStairTop(-1);
        entrance.x=Crest(-1,entrance.z).x-9;
        walks=new(); crestCache.Clear();
        void Line(float width,Vector3 from,params (float x,float y,float z,WalkKind kind)[] stops)
        {
            foreach(var stop in stops)
            {
                var to=new Vector3(stop.x,stop.y,stop.z);
                walks.Add(new Walk {a=from,b=to,width=width,kind=stop.kind});from=to;
            }
        }
        const WalkKind Pth=WalkKind.Path, St=WalkKind.Stairs, Br=WalkKind.Bridge;
        var fork=new Vector3(-128,132,61);
        Line(4,entrance,(-99,103,148,Pth),(-114,113,124,St),(-104,118,104,St),(-116,126,79,St),(-128,132,61,St));
        Line(3.6f,fork,(-135,132,52,Pth),(-144.8f,132.16f,34,Pth),(-144.8f,132.16f,24,Pth));
        int firstRoyalFlight=walks.Count;
        Line(4.8f,fork,(-143,138,58,St),(-165,146,57,St),(-185,152,58,St),(-193,155,64,St),
            (-180,159.5f,70,St),(-158,165,70,St),(-145,168,70,St),(-132,168,72,Pth),(-132,168.16f,78.3f,Pth));
        foreach(var w in walks.Skip(firstRoyalFlight))w.royalStair=true;
        Line(3.8f,entrance,(-99,103,183,Pth),(-107,109.5f,205,St),(-119,110.5f,225,Br),(-131,112.2f,236,Pth));
        var col=new Vector3(-131,112.2f,236);
        Line(4,col,(-145,119,255,St),(-162,127,245,St),(-178,135,253,St),(-185,137,247,St),(-192,137.6f,229,Br),(-197,142,213,St),(-207,146,218,St),(-204,146,232,St),(PadCenter(-1,1).x,146.02f,237.5f,Pth));
        // The upper gallery skirts the temple podium and climbs to the astronomical gardens.
        Line(3.7f,new Vector3(-193,155,64),(-209,161,63,St),(-231,168.16f,69,St));
        Line(3.7f,new Vector3(-204,168.16f,167),(-206,170,172,St),(-210,170.7f,180,Br),(-222,180,197,St),(-226,186,214,St),(-218,194.13f,224,St),(-212,194.13f,219,Pth));
    }
    static float CutWalks(Vector2 point,float natural)
    {
        float best=1e8f, height=natural, weight=0;
        foreach(var w in walks)
        {
            Vector2 a=new(w.a.x,w.a.z), b=new(w.b.x,w.b.z), d=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude);
            float distance=(point-Vector2.Lerp(a,b,t)).magnitude;
            if(distance>best || distance>w.width/2+9)continue;
            best=distance;
            if(w.royalStair){
                float inset=Mathf.Min(w.width*.56f/d.magnitude,.21f);
                t=Mathf.InverseLerp(inset,1-inset,t);
            }
            float grade=Mathf.Lerp(w.a.y,w.b.y,t);
            float hollow=w.kind==WalkKind.Bridge?Mathf.Sin(t*Mathf.PI)*8:0;
            height=grade-(w.royalStair?1.45f:.48f)-hollow;
            weight=1-Edge01(w.width/2+2.4f,w.width/2+9,distance);
        }
        return Mathf.Lerp(natural,height,weight);
    }
    static bool refreshingMountains;
    public static object RefreshSanctuaries() => RefreshTerrain(false);
    public static object RefreshRoyalApproach() => RefreshTerrain(true);
    static object RefreshTerrain(bool royalApproachOnly)
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.name!="CliffQuarter" || scene.isDirty || EditorApplication.isPlaying)throw new Exception("Open the saved CliffQuarter in Edit mode.");
        city=GameObject.Find("Cliff Quarter").transform;
        Material M(string n)=>AssetDatabase.LoadAssetAtPath<Material>(MatPath+"/"+n+".mat");
        plaster=M("Plaster");trim=M("Carved limestone");paving=M("Paving");roof=M("Roof tiles");clay=M("Terracotta");
        recess=M("Window recess");shutter=M("Shutters");brass=M("Brass");leaf=M("Foliage");cypressMat=M("Cypress");
        blossom=M("Blossom");wood=M("Cedar");rock=M("Cliff rock");water=M("Shallow river");cloth=M("Cloth");glow=M("Lantern glow");sunMat=M("Sunstone");
        stock=M("Livestock");bedMat=M("River bed");walks=null;InitPads();PlanSanctuaryWalks();vines=new();spots=new();keepOut=new();rng=new System.Random(731921);deco=new System.Random(5150);
        refreshingMountains=true;
        try
        {
            void Bake(string name,Action draw,Material only=null)
            {
                group=city.Find(name);if(!group){group=new GameObject(name).transform;group.SetParent(city,false);}
                batches=new();frame=Matrix4x4.identity;tint=Color.white;smooth=ghost=false;wind=0;aoBase=float.NaN;
                var unused=group.Cast<Transform>().Where(t=>t.name.Contains(" · ")&&t.GetComponent<MeshFilter>()&&(!only || t.GetComponent<MeshRenderer>().sharedMaterial==only)).ToList();
                if(!only){
                    foreach(var door in group.GetComponentsInChildren<QuarterSwingDoor>(true))Object.DestroyImmediate(door.gameObject);
                    foreach(var school in group.GetComponentsInChildren<RiverLife>(true))Object.DestroyImmediate(school.gameObject);
                }
                draw();
                foreach(var entry in batches.Values)
                {
                    string part=entry.mat.name+(entry.solid?" · solid":" · detail")+(entry.smooth?" · smooth":"");
                    bool sticky=!entry.solid && !entry.ghost && entry.mat!=water;
                    var child=unused.FirstOrDefault(t=>t.name==part && (entry.solid||((t.Find("Sticky surface")!=null)==sticky)));
                    if(child)unused.Remove(child);
                    if(!child)
                    {
                        child=new GameObject(part,typeof(MeshFilter),typeof(MeshRenderer)).transform;child.SetParent(group,false);
                        var extra=new Mesh{indexFormat=IndexFormat.UInt32};AssetDatabase.CreateAsset(extra,AssetDatabase.GenerateUniqueAssetPath(GeoPath+"/"+name+" "+part+".asset"));
                        child.GetComponent<MeshFilter>().sharedMesh=extra;child.GetComponent<MeshRenderer>().sharedMaterial=entry.mat;
                        if(entry.solid)child.gameObject.AddComponent<MeshCollider>();
                        else if(sticky){var surface=new GameObject("Sticky surface"){layer=8};surface.transform.SetParent(child,false);surface.AddComponent<MeshCollider>();surface.isStatic=true;}
                        child.gameObject.isStatic=true;
                    }
                    var mesh=child.GetComponent<MeshFilter>().sharedMesh;
                    List<Vector3> v=entry.v; List<Color> c=entry.c; List<int> ix=entry.t;
                    if(entry.smooth)Weld(entry,out v,out c,out ix);
                    mesh.Clear();mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(ix,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                    foreach(var col in child.GetComponentsInChildren<MeshCollider>(true)){col.sharedMesh=null;col.sharedMesh=mesh;}
                    EditorUtility.SetDirty(mesh);
                }
                foreach(var retired in unused)Object.DestroyImmediate(retired.gameObject);
            }
            Bake("Western uplands",()=>Upland(-1));
            if(!royalApproachOnly)Bake("Eastern uplands",()=>Upland(1));
            Physics.SyncTransforms();
            foreach(var anchor in city.GetComponentsInChildren<TraversalAnchor>())
                if(anchor.transform.parent==city.Find("Mountain sanctuaries") || (anchor.transform.parent==city && anchor.transform.position.x < -105 && anchor.transform.position.y>150))
                    Object.DestroyImmediate(anchor.gameObject);
            Bake("Mountain sanctuaries",Mountains);
            foreach(var stone in city.GetComponentsInChildren<Sunstone>()){
                var target=spots.FirstOrDefault(s=>s.place==stone.place && s.fixedSpot);
                if(target.place!=null)stone.transform.position=target.p+Vector3.up*1.05f;
            }
            var sea=GameObject.Find("River water volume").GetComponent<RiverWaterVolume>();
            Object.FindAnyObjectByType<WandererMotor>().travelBounds=TravelBounds;
            SetSeaLimits(sea);
            if(royalApproachOnly){
                Bake("Southern reach",SouthernSeabed,bedMat);
                AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorSceneManager.SaveScene(scene);
                return walks.Count(w=>w.royalStair);
            }
            OpenMarketSluices();Bake("Canal market",CanalMarket);
            OpenExistingCrossings();
            var farm=city.Find("Farmlands");farm.gameObject.SetActive(false);
            try{
                foreach(var t in farm.Cast<Transform>().Where(t=>t.GetComponent<Livestock>()||t.GetComponent<Spinner>()).ToArray())Object.DestroyImmediate(t.gameObject);
                roads=0;rng=new System.Random(62514);deco=new System.Random(7183);CollectKeepOuts();
                Bake("Farmlands",Farms);
            }finally{farm.gameObject.SetActive(true);}
            Bake("Southern reach",SouthernReach);
            Bake("Crown observatory",()=>{Pavilion(Crown,9,15,true,1);
                for(int i=0;i<8;i++){var c=Crown+new Vector3(Mathf.Cos(i*.78f)*12,0,Mathf.Sin(i*.78f)*12);
                    if(Mathf.Abs(c.x-Center(c.z))<Mathf.Abs(Edge(-1,4,c.z)-Center(c.z))-1 && Mathf.Abs(c.x-Center(c.z))>Mathf.Abs(Edge(-1,3,c.z)-Center(c.z))+1)Cypress(c,R(7,10));}
            });
            Bake("River gates and cross-gorge routes",()=>{foreach(var (z,tier,width) in BridgeList)Bridge(z,tier,width);});
            Bake("Water stairs and moorings",WaterStairs);
            Bake("Ship companionways",ShipCompanionways);
            foreach(var f in city.Find("Harbour").GetComponentsInChildren<MeshFilter>()){
                var mesh=f.sharedMesh; var v=mesh.vertices;
                if(!v.Any(p=>p.z<100))continue;
                var indices=mesh.triangles.Where(i=>v[i].z>=100).ToArray();
                if(indices.Length==0){Object.DestroyImmediate(f.gameObject);continue;}
                if(indices.Length%3!=0)throw new Exception("Unexpected harbour triangle");
                if(indices.Length==mesh.triangles.Length)continue;
                mesh.SetTriangles(indices,0);
                var bounds=new Bounds(v[indices[0]],Vector3.zero);
                foreach(int index in indices)bounds.Encapsulate(v[index]);
                mesh.bounds=bounds;EditorUtility.SetDirty(mesh);
                if(f.TryGetComponent<MeshCollider>(out var col)){col.sharedMesh=null;col.sharedMesh=mesh;}
            }
            var court=city.Find("Saffron ascent parkour court/Paving · solid").GetComponent<MeshFilter>().sharedMesh;
            var pigment=court.colors;
            for(int i=0;i<pigment.Length;i++)pigment[i]=new Color(1,1,1,pigment[i].a);
            court.colors=pigment;EditorUtility.SetDirty(court);
            // Existing harbour batches also carry buildings and ships. Trim only the old west quay end.
            foreach(var filter in city.Find("Harbour").GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;bool changed=false;
                for(int i=0;i<vertices.Length;i++)
                {
                    var v=vertices[i];
                    if(Mathf.Abs(v.x+260)>.015f||v.z<399.99f||v.z>480.01f)continue;
                    v.x=TinyWorld.EditorArt.BuildQuarterLandmarks.WestQuayLimit(v.z);
                    if(normals[i].x<-.99f)v.y=-9;
                    vertices[i]=v;changed=true;
                }
                if(!changed)continue;
                mesh.vertices=vertices;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                if(filter.TryGetComponent<MeshCollider>(out var collider)){collider.sharedMesh=null;collider.sharedMesh=mesh;}
            }
            AssetDatabase.SaveAssets();RestoreFarmCollision();Physics.SyncTransforms();EditorSceneManager.SaveScene(scene);
            return new{segments=walks.Count,bridges=walks.Count(w=>w.kind==WalkKind.Bridge)};
        }
        finally{refreshingMountains=false;}
    }
    public static int RestoreFarmCollision()
    {
        var farm=GameObject.Find("Cliff Quarter/Farmlands");int repaired=0;
        foreach(var collider in farm.GetComponentsInChildren<MeshCollider>(true)){
            var filter=collider.GetComponentInParent<MeshFilter>();
            if(!filter || !filter.sharedMesh || collider.sharedMesh==filter.sharedMesh)continue;
            collider.sharedMesh=filter.sharedMesh;EditorUtility.SetDirty(collider);repaired++;
        }
        if(repaired>0)EditorSceneManager.MarkSceneDirty(farm.scene);
        return repaired;
    }
    public static object SouthernRoutes()
    {
        InitPads();PlanSanctuaryWalks();
        float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};
        var tracks=new List<Vector3[]>();
        foreach(float z in new[]{60f,250f}){var c=W0(1,z);
            tracks.Add(new[]{OnGround(1,c+Vector3.left*17),OnGround(1,c+Vector3.right*17)});}
        tracks.Add(new[]{GrandStairStart(-1),GrandStairTop(-1)-Vector3.forward*4,GrandStairTop(-1),walks[0].a});
        tracks.Add(Enumerable.Range(0,12).Select(i=>{float t=i*.07f;return StrandPoint(1,6+4*Mathf.Sin(t*7+1)+t*5,t)+Vector3.up*.09f;}).ToArray());
        return tracks.Select(points=>points.Select(V).ToArray()).ToArray();
    }
    public static object CrossingLayout()
    {
        return BridgeList.Select(item=>{
            var (a,b,length,rise)=BridgeGeometry(item.z,item.tier);
            return new[]{a.x,a.y,a.z,b.x,b.y,b.z};
        }).ToArray();
    }
    public static object WalkLayout()
    {
        InitPads();PlanSanctuaryWalks();
        return walks.Select(w=>new{start=new[]{w.a.x,w.a.y,w.a.z},end=new[]{w.b.x,w.b.y,w.b.z},kind=w.kind.ToString()}).ToArray();
    }
    static void BuildWalk(Walk w)
    {
        var d=w.b-w.a;float len=new Vector2(d.x,d.z).magnitude;
        var dir=new Vector3(d.x,0,d.z).normalized;var side=Vector3.Cross(Vector3.up,dir);
        // Round, level turnouts connect the flights without railings across a junction.
        float footing=w.royalStair?3.7f:0;
        tint=P("E7D6B9");Cylinder(w.a+Vector3.down*(.36f+footing),w.width*.60f,.37f+footing,paving,true,20);
        float inset=Mathf.Min(w.width*.56f,len*.21f);
        var a=w.a+dir*inset;var b=w.b-dir*inset;
        if(w.kind==WalkKind.Stairs)
        {
            int steps=Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(b.y-a.y)/.18f));float run=(len-2*inset)/steps;
            var old=frame;frame=Matrix4x4.TRS(a,Quaternion.LookRotation(dir),Vector3.one);
            if(w.royalStair){
                foreach(int sign in new[]{-1,1}){
                    float x=sign*(w.width*.5f-.08f);
                    var low=new Vector3(x,-.12f,0);var high=new Vector3(x,b.y-a.y-.12f,len-2*inset);
                    QuadN(low,high,high+Vector3.down*3.7f,low+Vector3.down*3.7f,Vector3.right*sign,trim);
                    Beam(low+Vector3.up*.27f,high+Vector3.up*.27f,.27f,trim,true);
                }
            }
            for(int j=0;j<steps;j++){float h=(b.y-a.y)*(j+.5f)/steps;tint=j%5==0?P("D7C19F"):P("E9D9BD");Box(new Vector3(0,h-.42f,(j+.5f)*run),new Vector3(w.width,.84f,run+.008f),paving);}
            frame=old;
            var l=side*w.width*.49f;
            var offset=Vector3.down*.085f;
            QuadN(a-l+offset,b-l+offset,b+l+offset,a+l+offset,Vector3.up,paving);
        }
        else
        {
            var l=side*w.width*.5f;
            tint=P("E8D7B6");QuadN(a-l,b-l,b+l,a+l,Vector3.up,paving);
            foreach(int sign in new[]{-1,1}){var p=a+l*sign;var q=b+l*sign;QuadN(p,q,q+Vector3.down*.62f,p+Vector3.down*.62f,side*sign,trim);}
            if(w.kind==WalkKind.Bridge)
            {
                var old=frame;frame=Matrix4x4.TRS((a+b)*.5f,Quaternion.LookRotation(side),Vector3.one);
                float r=(len-2*inset)*.48f;tint=P("D8BE97");Arch(new Vector3(0,-4.6f,0),r,3.85f,.72f,w.width-.32f,trim,true,24);frame=old;
                foreach(int sign in new[]{-1,1})
                {
                    var p=a+l*sign;var q=b+l*sign;
                    Beam(p+Vector3.up*.83f,q+Vector3.up*.83f,.13f,wood,true);
                    for(int j=0;j<=8;j++){var at=Vector3.Lerp(p,q,j/8f);Beam(at,at+Vector3.up*.88f,.17f,trim,true);}
                }
            }
        }
        Cylinder(w.b+Vector3.down*(.38f+footing),w.width*.59f,.385f+footing,paving,true,20);
        tint=Color.white;
    }
    static void Mountains()
    {
        const int W = -1;
        var top = GrandStairTop(W); var landing = UP(W, 14, top.z);
        var palace = PadCenter(W, 0); var temple = PadCenter(W, 1); var theatre = PadCenter(W, 2); var peak = PadCenter(W, 3); var colossus = PadCenter(W, 4);
        foreach(var walk in walks) BuildWalk(walk);
        Acropolis(temple);
        Palace(palace);
        Theatre(theatre);
        Armillary(peak);
        Colossus(colossus);
        Aqueduct(W, palace + new Vector3(-8, -1, 44), temple + new Vector3(-27, 0, -31));
        // Groves on the slopes.
        for (int i = 0; i < 140; i++)
        {
            float u = D(24, 160), z = D(-50, 395); var p = UP(W, u, z);
            float rear=palace.x-p.x;
            if ((Mathf.Abs(z-palace.z)<53 && rear> -18 && rear<90) || Near(W, u, z, 12) || walks.Any(w => {
                var d = w.b-w.a; d.y=0; var v=p-w.a; v.y=0;
                return (v-d*Mathf.Clamp01(Vector3.Dot(v,d)/d.sqrMagnitude)).sqrMagnitude<42;
            })) continue;
            if (!TinyWorld.EditorArt.QuarterPlanting.TryGround(p, out p)) continue;
            if (Chance(.65f)) Cypress(p - Vector3.up * .08f, D(5, 11)); else UmbrellaPine(p - Vector3.up * .08f, D(.9f, 1.4f));
        }
    }
    static bool Near(int side, float u, float z, float margin)
    {
        foreach (var pd in pads[side < 0 ? 0 : 1]) if ((u - pd.u) * (u - pd.u) + (z - pd.z) * (z - pd.z) < (pd.r + margin) * (pd.r + margin)) return true;
        return side > 0 && StreamAt(u, z).d < 10 + margin;
    }
    // Paved road draped over the uplands; avenue roads get obelisks and cypress pairs.
    static int roads;
    static void Road(int side, Vector3 a, Vector3 b, float width, bool avenue, string color = null)
    {
        var dir = new Vector3(b.x - a.x, 0, b.z - a.z); float length = dir.magnitude; dir /= length;
        var across = Vector3.Cross(Vector3.up, dir) * width * .5f;
        int n = Mathf.CeilToInt(length / 3); float lift = .2f + roads++ % 6 * .012f;
        Vector3 G(Vector3 p) { var crest = Crest(side, p.z); return new Vector3(p.x, UplandH(side, (p.x - crest.x) * side, p.z, crest.y) + lift, p.z); }
        for (int i = 0; i < n; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)n); var q = Vector3.Lerp(a, b, (i + 1f) / n);
            tint = P(color ?? (avenue ? "EDE0C4" : "E2CFA8"));
            if(side==1 && Mathf.Abs(p.x-W0(1,p.z).x)<10 && StreamAt((p.x-Crest(1,p.z).x),p.z).d<7)continue;
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
    static void DoorWall(float width,float bottom,float top,float z,float opening,float doorHeight,Material wall,float thickness=.5f)
    {
        float jamb=(width-opening)*.5f;
        foreach(int side in new[]{-1,1}) Box(new Vector3(side*(width-jamb)/2,(top+bottom)/2,z),new Vector3(jamb,top-bottom,thickness),wall,true);
        float lintel=top-bottom-doorHeight;
        Box(new Vector3(0,top-lintel/2,z),new Vector3(opening,lintel,thickness),wall,true);
    }
    static void Door(string name,float z,float floor,float width,float height,float yaw,Color paint,bool bronze=false)
    {
        TinyWorld.EditorArt.QuarterDoorArt.Place(group,frame,new Vector3(0,floor,z),yaw,name,width,height,paint,bronze);
    }
    static void Flight(float width,float fromZ,float toZ,float fromY,float toY)
    {
        int count=Mathf.CeilToInt(Mathf.Abs(toY-fromY)/.17f);
        float run=(toZ-fromZ)/count;
        for(int i=0;i<count;i++)
        {
            float y=Mathf.Lerp(fromY,toY,(i+.5f)/count);
            Box(new Vector3(0,y-.24f,fromZ+run*(i+.5f)),new Vector3(width,.48f,Mathf.Abs(run)+.006f),paving);
        }
        QuadN(new(-width/2,fromY-.035f,fromZ),new(-width/2,toY-.035f,toZ),new(width/2,toY-.035f,toZ),new(width/2,fromY-.035f,fromZ),Vector3.up,paving);
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
        for (int i = 0; i < front; i++) foreach (int s in new[] { -1, 1 })
        {
            if (s < 0 && (i == front / 2 - 1 || i == front / 2)) continue;
            Column(new Vector3(-w * .5f + i * w / (front - 1), baseY, s * l * .5f), colH, 1.05f);
        }
        for (int i = 1; i < flank - 1; i++) foreach (int s in new[] { -1, 1 }) Column(new Vector3(s * w * .5f, baseY, -l * .5f + i * l / (flank - 1)), colH, 1.05f);
        Flight(7.5f,-44,-33.4f,.1f,baseY+.035f);
        tint=P("EEE0C5");
        foreach(int side in new[]{-1,1})
            Box(new Vector3(side*7.72f,baseY+colH/2,2),new Vector3(.56f,colH,42),plaster,true);
        Box(new Vector3(0,baseY+colH/2,22.75f),new Vector3(15.45f,colH,.5f),plaster,true);
        DoorWall(15.45f,baseY,baseY+colH,-18.75f,4.8f,8.2f,plaster);
        Door("Temple of the Sun", -18.75f,baseY,4.8f,8.2f,0,P("896334"),true);
        tint=P("3C6871"); Box(new Vector3(0,baseY+.026f,0),new Vector3(3.4f,.045f,35),shutter);
        for(int i=0;i<9;i++) {
            tint=Palette.Brass; Box(new Vector3(0,baseY+.065f,-15+i*3.7f),new Vector3(1.32f,.022f,.075f),brass);
        }
        foreach(int side in new[]{-1,1}) for(int i=0;i<5;i++){
            float z=-13+i*7; tint=P("B4A086"); Box(new Vector3(side*6.36f,baseY+.32f,z),new Vector3(1.1f,.64f,3.2f),trim,true);
            tint=Palette.Brass; Cylinder(new Vector3(side*6.95f,baseY+.65f,z),.22f,2,brass,false,10,.14f);
            tint=P("FFE7A0"); Ellipsoid(new Vector3(side*6.95f,baseY+2.77f,z),new Vector3(.24f,.40f,.24f),glow);
        }
        tint=P("B59769"); Cylinder(new Vector3(0,baseY,15.5f),2.4f,.6f,trim,true,24);
        tint=Palette.Brass; Cylinder(new Vector3(0,baseY+.6f,15.5f),.58f,1.55f,brass,true,12,.32f);
        Torus(new Vector3(0,baseY+3.6f,15.5f),1.6f,.1f,brass,32);
        tint=P("ECA855"); Ellipsoid(new Vector3(0,baseY+3.6f,15.5f),Vector3.one*.71f,brass,18,10);
        foreach(int side in new[]{-1,1}) for(int bay=0;bay<6;bay++){
            float z=-15+bay*7;
            tint=P("DFCAAA");Box(new(side*7.32f,baseY+6.7f,z),new(.42f,13.4f,.64f),trim);
            tint=P("547B80");Box(new(side*7.415f,baseY+5.4f,z+2.9f),new(.08f,1.05f,4.54f),shutter);
            tint=Palette.Brass;Box(new(side*7.35f,baseY+5.94f,z+2.9f),new(.08f,.085f,4.62f),brass);
        }
        tint=P("547B80");Box(new(0,baseY+7.7f,22.44f),new(10,7.5f,.08f),shutter);
        tint=Palette.Brass;var mural=new Vector3(0,baseY+7.7f,22.34f);Torus(mural,1.52f,.085f,brass,32);
        for(int i=0;i<16;i++){var ray=new Vector3(Mathf.Cos(i*Mathf.PI/8),Mathf.Sin(i*Mathf.PI/8),0);Beam(mural+ray*1.76f,mural+ray*(i%2==0?3.05f:2.5f),.085f,brass);}
        // Entablature: architrave, brass-studded frieze, cornice.
        float e = baseY + colH;
        tint = P("F3E8D2"); Box(new Vector3(0, e + .7f, 0), new Vector3(w + 2.4f, 1.4f, l + 2.4f), trim, true);
        tint = P("E6D2AE"); Box(new Vector3(0, e + 2, 0), new Vector3(w + 2.2f, 1.2f, l + 2.2f), trim, true);
        tint = Palette.Brass; for (float z = -l * .5f; z <= l * .5f; z += 3.3f) foreach (int s in new[] { -1, 1 }) Box(new Vector3(s * (w * .5f + 1.15f), e + 2, z), new Vector3(.1f, .7f, .7f), brass);
        tint = Color.white; Box(new Vector3(0, e + 2.9f, 0), new Vector3(w + 3.4f, .6f, l + 3.4f), trim, true);
        // Marble roof with pediments and brass acroteria.
        float rY = e + 3.2f, ridge = 5.2f; var hw = w * .5f + 1.7f; var hl = l * .5f + 1.7f;
        tint = P("F6EEDD");
        QuadN(new Vector3(-hw, rY, -hl), new Vector3(0, rY + ridge, -hl), new Vector3(0, rY + ridge, hl), new Vector3(-hw, rY, hl), Vector3.up, roof);
        QuadN(new Vector3(hw, rY, hl), new Vector3(0, rY + ridge, hl), new Vector3(0, rY + ridge, -hl), new Vector3(hw, rY, -hl), Vector3.up, roof);
        foreach (int s in new[] { -1, 1 })
        {
            tint = P("EFE2C8"); var z = s * hl;
            Tri(new Vector3(-hw, rY, z), new Vector3(0, rY + ridge, z), new Vector3(hw, rY, z), trim, true); Tri(new Vector3(hw, rY, z), new Vector3(0, rY + ridge, z), new Vector3(-hw, rY, z), trim, true);
            tint = Palette.Brass;
            Ellipsoid(new Vector3(0, rY + ridge + 1.2f, z), new Vector3(1.1f, 1.6f, .5f), brass, 10, 6);
            foreach (int t in new[] { -1, 1 }) Ellipsoid(new Vector3(t * hw, rY + .8f, z), new Vector3(.7f, 1, .4f), brass, 8, 5);
            // Pediment sculpture: a brass sun.
            Cylinder(new Vector3(0, rY + ridge * .42f, z + s * .1f), 1.3f, .2f, brass, false, 18);
        }
        // Grand stair down the front of the podium.
        tint = P("F3E8D2");
        Flight(20,-54.5f,-45.9f,-5.98f,.12f);
        for (int s = -1; s <= 1; s += 2) { Box(new Vector3(s * 10.6f, -3.5f, -50.2f), new Vector3(1.2f, 7.2f, 8.4f), trim, true); Obelisk(new Vector3(s * 10.6f, .1f, -47), 8); }
        Spot(c + new Vector3(0, rY + ridge + .2f, 0), "Roof of the Great Temple", true);
        Spot(c + new Vector3(0, baseY + .1f, -26), "Temple colonnade", true);
        for (int i = 0; i < 4; i++) Anchor(c + new Vector3(i % 2 == 0 ? -12 : 12, e - 2.5f, i < 2 ? -38 : 38));
        frame = old; tint = Color.white;
    }
    static void Palace(Vector3 c)
    {
        var previous=frame;
        frame=Matrix4x4.TRS(c,Quaternion.Euler(0,-90,0),Vector3.one);
        var ivory=P("F1E1C5"); var enamel=P("327C80");
        const float court=.16f, hall=8.55f;
        tint=ivory;
        Box(new(0,-4.92f,-2),new(79,10.16f,102),trim,true);
        // Open gate, side galleries and two shallow garden pools frame the approach.
        foreach(int side in new[]{-1,1}){
            Box(new(side*24,1.56f,-52),new(30,2.8f,1.25f),trim,true);
            Box(new(side*9.1f,4.05f,-52),new(2.1f,7.8f,2.4f),trim,true);
            Obelisk(new(side*9.1f,7.95f,-52),3.5f);
            tint=ivory;
            if(side<0){
                Box(new(-37,2,-45.95f),new(1,4,6.1f),plaster,true);
                Box(new(-37,2,-26.95f),new(1,4,19.9f),plaster,true);
            }else Box(new(37,2,-33),new(1,4,32),plaster,true);
            for(int j=0;j<4;j++){
                var p=new Vector3(side*28,court,-44+j*7.5f);
                Column(p,4.8f,.48f);
                if(j<3){
                    var gallery=frame; frame*=Matrix4x4.TRS(p+new Vector3(0,4.75f,3.75f),Quaternion.Euler(0,90,0),Vector3.one);
                    Arch(Vector3.zero,3.4f,2.65f,.48f,1.1f,trim,true);frame=gallery;
                }
            }
            Box(new(side*32.5f,7.87f,-32.75f),new(10,.58f,26.8f),trim,true);
            tint=P("D7BD96");Box(new(side*19,.34f,-31),new(6.7f,.36f,22),trim,true);
            tint=enamel;Box(new(side*19,.55f,-31),new(5.7f,.07f,20.8f),shutter);
            tint=ivory;
            for(int j=0;j<3;j++) Cypress(new(side*24,court,-40+j*9),5+j%2);
        }
        tint=P("D6B091");
        for(int j=0;j<10;j++)Box(new(0,.173f,-50+j*3.3f),new(13,.024f,.14f),shutter);
        tint=ivory;
        Flight(19,-17,-.5f,court,hall);
        foreach(int side in new[]{-1,1}){
            for(int j=0;j<6;j++) {
                float t=(j+.5f)/6;
                Box(new(side*10.4f,4.2f*t+.54f,-17+16.5f*t),new(1.25f,8.4f*t+1.1f,2.76f),trim,true);
            }
        }
        Box(new(0,4.32f,23),new(63,8.46f,47.2f),trim,true);
        // Stepped cornices, carved fanlight and paired standards over the royal stair.
        tint=ivory;
        foreach(float x in new[]{-6.8f,6.8f}){
            Column(new(x,hall,1.08f),13.8f,.78f);
            Box(new(x,22.6f,1.08f),new(2.48f,.6f,2.4f),trim,true);
        }
        Box(new(0,23.15f,1.18f),new(16.1f,.65f,2.65f),trim,true);
        tint=enamel;Box(new(0,23.64f,1.35f),new(16.5f,.35f,2.42f),shutter);
        tint=ivory;
        Tri(new(-8.4f,23.81f,-.04f),new(0,28.85f,-.04f),new(8.4f,23.81f,-.04f),trim);
        Tri(new(8.4f,23.81f,-.04f),new(0,28.85f,-.04f),new(-8.4f,23.81f,-.04f),trim);
        tint=Palette.Brass;
        Beam(new(-8.65f,23.74f,-.15f),new(0,29,-.15f),.24f,brass);
        Beam(new(0,29,-.15f),new(8.65f,23.74f,-.15f),.24f,brass);
        Torus(new(0,25.52f,-.21f),.94f,.12f,brass,24);
        foreach(float x in new[]{-26.1f,26.1f,-13.6f,13.6f}){
            tint=Palette.Brass;Beam(new(x-1.25f,23,2),new(x+1.25f,23,2),.14f,brass);
            tint=x<0?enamel:P("A25447");
            var a=new Vector3(x-1.06f,22.95f,1.94f);var b=a+Vector3.right*2.12f;
            QuadN(a,b,b+Vector3.down*6.4f,a+Vector3.down*6.4f,Vector3.back,cloth,false);
            Tri(b+Vector3.down*6.4f,new(x,14.95f,1.91f),a+Vector3.down*6.4f,cloth);
            tint=Palette.Brass;Box(new(x,18.18f,1.88f),new(1.9f,.16f,.04f),brass);
        }
        tint=ivory;
        // Throne hall: the upper arcade admits daylight along both side aisles.
        const float eave=29;
        DoorWall(62,hall,eave,3,7.2f,11.4f,plaster,1.1f);
        Door("Doors of the royal audience",3,hall,7.2f,11.4f,0,P("77503E"),true);
        tint=enamel;Arch(new(0,hall+8.05f,2.15f),4.12f,4.3f,.61f,1,trim,true);
        tint=ivory;
        foreach(int side in new[]{-1,1}){
            Column(new(side*4.46f,hall,2.22f),8.15f,.48f);
            Box(new(side*30.6f,hall+2.5f,24),new(1.3f,5,42),plaster,true);
            for(int j=0;j<6;j++){
                float z=5+j*7.6f;
                Column(new(side*30.6f,hall+5,z),8.5f,.74f);
                Column(new(side*15.4f,hall,z),16.65f,.83f);
                if(j<5){
                    var basis=frame;frame*=Matrix4x4.TRS(new(side*30.6f,hall+13.35f,z+3.8f),Quaternion.Euler(0,90,0),Vector3.one);
                    Arch(Vector3.zero,3.26f,4.1f,.82f,1.7f,trim,true);frame=basis;
                }
            }
            Box(new(side*30.6f,27.78f,24),new(1.65f,2.44f,43),trim,true);
            for(int j=0;j<3;j++){
                var p=new Vector3(side*(10+j*7.2f),hall+1.4f,2.37f);
                Window(p,2.1f,8.3f,false,3);
            }
        }
        tint=ivory;Box(new(0,(eave+hall)/2,45),new(62,eave-hall,1.4f),plaster,true);
        Box(new(0,eave+.25f,24),new(64,.85f,45),trim,true);
        tint=enamel;Box(new(0,eave+1,24),new(63.5f,.68f,44.5f),shutter,true);
        void Cupola(Vector3 spring,float radius,float height,int sides)
        {
            // The projecting cornice closes the underside all the way to the dome's rim.
            tint=ivory;Cylinder(spring-Vector3.up*.55f,radius+.08f,.62f,trim,true,sides,radius+.25f);
            tint=enamel;Dome(spring,radius,height,shutter,true,sides);
        }
        tint=ivory;Cylinder(new(0,eave+1.34f,24),16,7,trim,true,36);
        Cupola(new(0,eave+8.34f,24),16.8f,15.5f,40);
        tint=Palette.Brass;
        Cylinder(new(0,eave+23.3f,24),.75f,3.8f,brass,true,12,.12f);
        for(int rib=0;rib<20;rib++){
            float a=rib*Mathf.PI/10;
            Vector3 Rim(float t){
                float r=16.86f*Mathf.Cos(t);
                return new Vector3(r*Mathf.Cos(a),eave+8.34f+15.56f*Mathf.Sin(t),24+r*Mathf.Sin(a));
            }
            for(int j=0;j<10;j++)Beam(Rim(j*Mathf.PI/20),Rim((j+1)*Mathf.PI/20),.135f,brass);
            var basis=frame;frame*=Matrix4x4.TRS(new(0,eave+1.35f,24),Quaternion.Euler(0,rib*18,0),Vector3.one);
            Window(new(0,1.22f,-16),1.64f,3.48f,false,3);frame=basis;
        }
        // Two lantern towers with open belfries and fluted roofs.
        for(int side=-1;side<=1;side+=2){
            var foot=new Vector3(side*34,0,28);
            float shaft=side<0?52:58;
            tint=ivory;Cylinder(foot,4.45f,shaft,trim,true,12,3.1f);
            for(int j=1;j<=4;j++){
                float y=j*shaft/5;
                tint=enamel;Cylinder(foot+Vector3.up*y,4.65f-.25f*j,.75f,shutter,true,12);
                tint=ivory;Cylinder(foot+Vector3.up*(y+.74f),4.75f-.25f*j,.42f,trim,true,12);
            }
            Cylinder(foot+Vector3.up*shaft,4.25f,1,trim,true,12);
            for(int j=0;j<8;j++){
                float a=j*Mathf.PI/4;
                Column(foot+new Vector3(2.9f*Mathf.Cos(a),shaft+1,2.9f*Mathf.Sin(a)),7,.44f);
            }
            var spring=foot+Vector3.up*(shaft+8.55f);
            Cupola(spring,4.5f,6.5f,20);
            tint=Palette.Brass;Cylinder(spring+Vector3.up*6.3f,.53f,5,brass,true,10,.025f);
        }
        // Patterned aisle and a raised apse with the king's high-backed chair.
        tint=P("9F4F43");Box(new(0,hall+.027f,18),new(5.7f,.05f,29),shutter);
        tint=Palette.Brass;
        foreach(float x in new[]{-2.66f,2.66f})Box(new(x,hall+.06f,18),new(.14f,.02f,29),brass);
        tint=ivory;Flight(12,31,35,hall,hall+1.65f);
        Box(new(0,hall+.825f,39),new(22,1.65f,8),trim,true);
        var seat=new Vector3(0,hall+1.65f,40.5f);
        tint=Palette.Brass;Box(seat+new Vector3(0,1.52f,.45f),new(1.8f,3.04f,.25f),brass,true);
        tint=enamel;Box(seat+new Vector3(0,1.58f,.26f),new(1.48f,2.61f,.17f),shutter,true);
        Box(seat+Vector3.up*.46f,new(1.76f,.92f,1.23f),shutter,true);
        foreach(float x in new[]{-.9f,.9f}){
            tint=Palette.Brass;Box(seat+new Vector3(x,.72f,0),new(.21f,1.44f,1.26f),brass,true);
        }
        foreach(float x in new[]{-.94f,.94f}){
            tint=ivory;Column(seat+new Vector3(x,0,.43f),3.23f,.12f);
            tint=Palette.Brass;Lathe(seat+new Vector3(x,3.18f,.43f),new[]{new Vector2(.18f,0),new Vector2(.23f,.15f),new Vector2(.035f,.48f)},10,brass);
            tint=P("975D4D");Box(seat+new Vector3(x*.92f,1.44f,-.10f),new(.26f,.17f,1.14f),shutter);
        }
        tint=Palette.Brass;Torus(seat+new Vector3(0,2.44f,.14f),.31f,.065f,brass,20);
        tint=P("9C5347");Box(seat+new Vector3(0,.96f,-.04f),new(1.54f,.13f,1.13f),shutter);
        tint=P("315D73");Box(new(0,19.4f,44.2f),new(12,16,.12f),shutter);
        tint=Palette.Brass;Torus(new(0,21,44.06f),2.7f,.22f,brass,32);
        for(int j=0;j<12;j++){
            var d=new Vector3(Mathf.Cos(j*Mathf.PI/6),Mathf.Sin(j*Mathf.PI/6),0);
            Beam(new Vector3(0,21,44.01f)+d*3.1f,new Vector3(0,21,44.01f)+d*4.5f,.19f,brass);
        }
        Spot(frame.MultiplyPoint3x4(new Vector3(0,eave+1.4f,2.9f)),"Dome terrace of the Palace of Winds",true);
        foreach(int sign in new[]{-1,1})Anchor(frame.MultiplyPoint3x4(new Vector3(sign*34,38,23)));
        RoyalGardens();
        frame=Matrix4x4.Translate(frame.MultiplyPoint3x4(new Vector3(0,0,-53)));
        RoyalOverlook();
        frame=previous;tint=Color.white;
    }

    static void RoyalGardens()
    {
        var previousRng=rng;var previousDeco=deco;
        rng=new System.Random(14768);deco=new System.Random(42957);
        const float floor=.16f;
        // The two side walks meet the cross-aisle behind the palace towers.
        void Terrace(float x,float z,float width,float depth){
            tint=P("EDDABB");Box(new(x,floor-8,z),new(width,16,depth),paving,true);
        }
        void Seat(float x,float z){
            tint=P("DDC9A3");
            foreach(int sign in new[]{-1,1})Box(new(x+sign*1.12f,.42f,z),new(.36f,.52f,.64f),trim,true);
            Box(new(x,.77f,z),new(3.4f,.26f,.86f),trim,true);
        }
        void Citrus(float x,float z){
            var p=new Vector3(x,.68f,z);
            tint=P("846548");Limb(p,p+Vector3.up*2.65f,.25f,.17f,wood,8);
            for(int j=0;j<4;j++){
                float a=j*Mathf.PI*.5f+.43f;
                var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var crown=p+d*1.15f+Vector3.up*(3.15f+.33f*(j%2));
                tint=P("846548");Limb(p+Vector3.up*1.65f,crown,.14f,.08f,wood,7);
                tint=j%2==0?P("5D804F"):P("79935A");
                Ellipsoid(crown,new(1.75f,1.54f,1.65f),leaf,9,6,.1f);
                tint=P("E8A345");
                for(int k=0;k<3;k++){
                    float b=a+(k-1)*.72f;
                    Ellipsoid(crown+new Vector3(1.43f*Mathf.Cos(b),-.56f+.23f*(k%2),1.37f*Mathf.Sin(b)),Vector3.one*.18f,blossom,7,4);
                }
            }
        }
        foreach(int side in new[]{-1,1}){
            // Set the south parapet back from the hillside switchback below.
            float width=side<0?6f:9.5f;
            Terrace(side*(39.5f+width*.5f),17,width,64);
            float border=side*(39.5f+width-1.52f);
            for(int j=0;j<5;j++){
                float z=-8+j*11.4f;
                tint=P("B98E6D");Cylinder(new(border,floor,z),1.31f,.45f,clay,true,12,1.45f);
                Cypress(new(border,.61f,z),6.2f+j%3*.65f);
            }
            for(int j=0;j<2;j++) Seat(border,9+j*24);
        }
        Terrace(0,67,98,36);
        // Carry the hanging terrace down to rock where the summit falls away to the sea.
        float Bed(float x,float z){
            var p=frame.MultiplyPoint3x4(new Vector3(x,0,z));
            return Ground(-1,p)-p.y-.7f;
        }
        const float soffit=floor-16,spring=soffit-5;
        foreach(float z in new[]{52f,67f,83f}){
            for(int i=0;i<=9;i++){
                float x=-45+i*10,low=Bed(x,z);
                if(low<spring){
                    tint=P("CDBA9A");Box(new(x,(low+spring)*.5f,z),new(3.2f,spring-low,3.2f),trim,true);
                    tint=P("C2A781");Box(new(x,spring-.32f,z),new(3.8f,.64f,3.85f),trim,true);
                    for(float y=spring-24;y>low+3;y-=24)
                        Box(new(x,y,z),new(3.65f,.65f,3.65f),trim,true);
                }
                if(i==9 || Mathf.Min(low,Bed(x+5,z),Bed(x+10,z))>=soffit)continue;
                tint=P("DDCBA8");Arch(new(x+5,spring,z),4.74f,4.52f,.68f,3.05f,trim,true,16);
            }
        }
        foreach(int side in new[]{-1,1})foreach(float x in new[]{13f,31.5f}){
            var bed=new Vector3(side*x,0,69);
            tint=P("CCAD83");Box(bed+Vector3.up*.41f,new(13.4f,.5f,13.8f),trim,true);
            tint=P("798056");Box(bed+Vector3.up*.69f,new(12.8f,.12f,13.2f),leaf);
            for(int j=0;j<2;j++) Citrus(bed.x,65.7f+j*6.5f);
            foreach(int edge in new[]{-1,1}){
                for(int j=0;j<10;j++){
                    var root=bed+new Vector3(edge*5.56f,.93f,-5.6f+j*1.24f);
                    tint=P("728557");Ellipsoid(root,new(.68f,.42f,.78f),leaf,7,4,.07f);
                    tint=x<20?P("AF8AC4"):P("D78A94");
                    for(int k=0;k<3;k++)Ellipsoid(root+new Vector3((k-1)*.3f,.47f+.09f*(k%2),.17f*(k%2)),new(.13f,.24f,.13f),blossom,5,4);
                }
            }
        }
        foreach(float z in new[]{65f,73f}){
            tint=P("D5BE95");Cylinder(new(0,floor,z),2.55f,.47f,trim,true,28);
            tint=P("53ADB0");Cylinder(new(0,.63f,z),2.25f,.09f,shutter,false,28);
            tint=P("CFB07E");Cylinder(new(0,.72f,z),.39f,1.19f,trim,false,12,.24f);
            Lathe(new(0,1.88f,z),new[]{new Vector2(.25f,0),new Vector2(1.11f,.38f),new Vector2(1.06f,.48f),new Vector2(.16f,.22f)},20,trim);
            tint=Palette.Brass;Ellipsoid(new(0,2.25f,z),Vector3.one*.21f,brass,10,6);
        }
        foreach(int side in new[]{-1,1}){
            Seat(side*19,56);Seat(side*19,79);
            for(int j=0;j<4;j++){
                float x=side*(7+j*5.1f);
                foreach(float z in new[]{78,83})Column(new(x,floor,z),3.55f,.23f);
                tint=P("87694B");Beam(new(x,3.91f,77.65f),new(x,3.91f,83.35f),.25f,wood);
                if(j<3){
                    foreach(float z in new[]{78,83})Beam(new(x,3.75f,z),new(x+side*5.1f,3.75f,z),.24f,wood);
                    Bougainvillea(new(x+side*2.4f,4.12f,82.85f),Vector3.back,1.7f);
                }
            }
        }
        tint=P("E8D5AD");Box(new(0,.76f,84.76f),new(98,1.2f,.48f),trim,true);
        rng=previousRng;deco=previousDeco;
    }

    static void RoyalOverlook()
    {
        const float z=0, top=.16f;
        tint=P("EBD9B7");
        const float radius=5.2f, halfWidth=3.5f, center=60.5f;
        float neck=center-Mathf.Sqrt(radius*radius-halfWidth*halfWidth);
        float angle=Mathf.Acos((neck-center)/radius);
        Box(new(neck*.5f,top-.68f,z),new(neck,1.36f,7),paving,true);
        var pivot=new Vector3(neck,top,0);var down=Vector3.down*1.36f;
        Vector3 Edge(int i){
            if(i==0 || i==32)return pivot+Vector3.forward*(i==0?-halfWidth:halfWidth);
            float a=Mathf.Lerp(-angle,angle,i/32f);
            return new(center+radius*Mathf.Cos(a),top,radius*Mathf.Sin(a));
        }
        for(int i=0;i<32;i++){
            var a=Edge(i);var b=Edge(i+1);
            Tri(pivot,b,a,paving,true);Tri(pivot+down,a+down,b+down,trim,true);
            QuadN(a,b,b+down,a+down,(a+b)*.5f-new Vector3(center,top,0),trim);
        }
        // Spring lines step down the cliff; the openings grow taller toward the gorge.
        for(int j=0;j<3;j++){
            float x=j*19, spring=-11-4*j;
            if(j<2)Arch(new(x+9.5f,spring,z),9.05f,-spring-1.9f,.95f,6.4f,trim,true,24);
            float foot=-66;
            if(j==2){
                var hillside=city.Find("Western terraces and escarpment");
                var terrain=hillside.GetComponentsInChildren<MeshCollider>().First(c=>c.name.StartsWith("Cliff rock") && c.name.Contains("smooth"));
                for(int dx=-1;dx<=1;dx++)for(int dz=-2;dz<=2;dz++){
                    var point=frame.MultiplyPoint3x4(new(x+dx*1.1f,top,z+dz*1.7f));
                    float ground=terrain.Raycast(new Ray(point,Vector3.down),out var hit,240) ? hit.point.y : Level(-1,3,point.z);
                    foot=Mathf.Min(foot,ground-point.y+top-1);
                }
            }
            Box(new(x,(spring+foot)*.5f,z),new(2.2f,spring-foot,6.8f),trim,true);
            tint=P("CCAA7F");Box(new(x,spring-.36f,z),new(2.85f,.74f,7.18f),trim,true);tint=P("EBD9B7");
        }
        // The last pier carries a rising stone haunch under the projecting lookout.
        Vector3 Haunch(float t) => new(38+21.5f*(1-Mathf.Cos(t)), -19+17.8f*Mathf.Sin(t), -3.2f);
        for(int j=0;j<18;j++){
            var a=Haunch(j*Mathf.PI/36);var b=Haunch((j+1)*Mathf.PI/36);
            var u=new Vector3(a.x,-1.2f,a.z);var v=new Vector3(b.x,-1.2f,b.z);var width=Vector3.forward*6.4f;
            QuadN(a,b,v,u,Vector3.back,trim);QuadN(a+width,u+width,v+width,b+width,Vector3.forward,trim);
            QuadN(a,a+width,b+width,b,Vector3.down,trim);
        }
        foreach(int side in new[]{-1,1}){
            Box(new(29,.56f,z+side*3.32f),new(59,.8f,.42f),trim,true);
            tint=Palette.Brass;Beam(new(0,1.03f,z+side*3.32f),new(58,1.03f,z+side*3.32f),.12f,brass,true);
            tint=P("EBD9B7");
        }
        for(int j=0;j<18;j++){
            float a=(-131+j*262f/18)*Mathf.Deg2Rad,b=(-131+(j+1)*262f/18)*Mathf.Deg2Rad;
            Vector3 Rim(float t)=>new(60.5f+4.94f*Mathf.Cos(t),top,z+4.94f*Mathf.Sin(t));
            var p=Rim(a);var q=Rim(b);Beam(p+Vector3.up*.84f,q+Vector3.up*.84f,.18f,trim,true);
            Beam(p,p+Vector3.up*.87f,.17f,trim,true);
        }
    }
    // Semicircular theatre facing the gorge, with a two-storey stage front.
    static void Theatre(Vector3 c)
    {
        var old = frame; frame = Matrix4x4.TRS(c, Quaternion.Euler(0, -90, 0), Vector3.one);
        // Solid seating banks with three stair aisles; every tread faces the sky.
        tint=P("EBDABD");Cylinder(new Vector3(0,-.40f,0),11.35f,.56f,paving,true,64);
        const int sectors=64,rows=14;const float inner=11.1f,depth=1.22f,rise=.44f;
        Vector3 V(float r,float a,float y)=>new(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
        void Band(float r0,float r1,float a,float b,float y,float low)
        {
            // QuadN's normal is in local space; the theatre frame only rotates about Y.
            QuadN(V(r0,a,y),V(r1,a,y),V(r1,b,y),V(r0,b,y),Vector3.up,paving);
            QuadN(V(r0,a,low),V(r0,a,y),V(r0,b,y),V(r0,b,low),-V(1,(a+b)*.5f,0),trim);
        }
        for(int i=0;i<rows;i++)for(int j=0;j<sectors;j++)
        {
            float a=Mathf.PI*j/sectors,b=Mathf.PI*(j+1)/sectors,r0=inner+i*depth,r1=r0+depth;
            float low=.16f+rise*i,y=low+rise;
            bool aisle=j<2||j>=62||(j>=14&&j<18)||(j>=30&&j<34)||(j>=46&&j<50);
            tint=aisle?P("CAAB82"):i%4==0?P("E7CFAE"):P("F1E1C6");
            if(aisle){Band(r0,r0+depth*.5f,a,b,low+rise*.5f,low);Band(r0+depth*.5f,r1,a,b,y,low+rise*.5f);}
            else Band(r0,r1,a,b,y,low);
            if(j==0||j==sectors-1)
            {
                float angle=j==0?a:b;
                float end=i==rows-1?r1+2.2f:r1;
                QuadN(V(r0,angle,-2),V(end,angle,-2),V(end,angle,y),V(r0,angle,y),Vector3.back,trim);
            }
            if(i==rows-1)
            {
                Band(r1,r1+2.2f,a,b,y,y-.1f);
                QuadN(V(r1+2.2f,a,-2),V(r1+2.2f,b,-2),V(r1+2.2f,b,y),V(r1+2.2f,a,y),V(1,(a+b)*.5f,0),trim);
                if(j%8==4)Column(V(r1+1.5f,(a+b)*.5f,y),2.35f,.16f);
            }
        }
        // Two side entries flank the stage and open onto the circular orchestra.
        foreach(int sign in new[]{-1,1})
        {
            var before=frame;frame*=Matrix4x4.TRS(new Vector3(sign*11.7f,0,.9f),Quaternion.Euler(0,90,0),Vector3.one);
            tint=P("DBC4A2");foreach(int side in new[]{-1,1})Column(new Vector3(side*2.15f,0,0),2.4f,.26f);
            Arch(new Vector3(0,2.4f,0),2.14f,1.43f,.48f,.86f,trim,true,18);frame=before;
        }
        tint=P("D7BD96");Box(new Vector3(0,.31f,-1),new Vector3(17,.38f,2.6f),paving,true);
        tint=Palette.Brass;
        var ringFrame=frame;frame*=Matrix4x4.Rotate(Quaternion.Euler(90,0,0));Torus(new Vector3(0,0,-.178f),5.3f,.032f,brass,60,5);frame=ringFrame;
        // Open stage loggia. The orchestra and the mountain path can both see through its arches.
        tint=P("EBDAB9");
        Box(new Vector3(0,.15f,-4.2f),new Vector3(25,.64f,6),trim,true);
        for(int i=0;i<7;i++)
        {
            float x=(i-3)*3.75f;
            Column(new Vector3(x,.47f,-5.9f),3.76f,.36f);
            if(i<6)Arch(new Vector3(x+1.875f,4.05f,-5.9f),1.68f,1.57f,.37f,.92f,trim,true,16);
            if(i%2==0)
            {
                var mark=new Vector3(x,5.45f,-5.92f);tint=Palette.Brass;Cylinder(mark,.16f,.54f,brass,false,10);
                tint=i%4==0?Palette.Terracotta:Palette.Water;wind=.82f;
                var a1=mark+new Vector3(-.58f,0,.5f);var b1=mark+new Vector3(.58f,0,.5f);
                var c1=mark+new Vector3(.49f,-2.08f,.56f);var d1=mark+new Vector3(-.54f,-1.94f,.72f);
                QuadN(a1,b1,c1,d1,Vector3.forward,cloth,false);QuadN(a1,b1,c1,d1,Vector3.back,cloth,false);
                wind=0;tint=P("EBDAB9");
            }
        }
        Box(new Vector3(0,5.97f,-5.9f),new Vector3(24,.45f,1.12f),trim,true);
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
                w=Mathf.Min(w, UMax-33-u); d=Mathf.Min(d,TerrainEnd-7-z);
                if(w<12 || d<12 || !FarmFootprint(u,z,w,d))continue;
                if (Near(E, u + w * .5f, z + d * .5f, 6 + Mathf.Max(w, d) * .5f)) continue;
                if (StreamAt(u + w * .5f, z + d * .5f).d < 14 + w * .5f) continue;
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
        // Hedgerow trees and haystacks.
        for (int i = 0; i < 90; i++)
        {
            float u = D(22, 150), z = D(-55, 395);
            if (Near(E, u, z, 6) || u>UMax-33 || !TryFarmFloor(UP(E,u,z),out _)) continue;
            var p = UP(E, u, z);
            if (Chance(.5f)) OliveTree(p, D(.8f, 1.15f)); else if (Chance(.6f)) Cypress(p - Vector3.up * .3f, D(5, 9)); else Haystack(p);
        }
        // Long working plots beyond the former shoreline, divided by two farm lanes.
        for(int row=0; row<8; row++){
            float z=-46+row*53;
            for(int strip=0;strip<2;strip++){
                float u=148+strip*45;
                int[] crops={0,2,1,3,5,0,4,1};
                if(FarmFootprint(u,z,39,44)) Field(E,u,z,39,44,crops[(row+strip*3)%crops.Length]);
            }
        }
        for(int i=0;i<16;i++){
            float z=-45+i*26.5f;
            Road(E,UP(E,189,z),UP(E,189,z+26.5f),2.8f,false,dirt);
            if(i%2==1) Haystack(UP(E,185,z+21));
        }
        Road(E,mill2,UP(E,189,250),3,false,dirt);
        Spot(OnGround(E, hamlet) + new Vector3(0, 16.5f, 0), "Chapel belfry", false);
    }
    static bool FarmFootprint(float u,float z,float w,float d)
    {
        for(int i=0;i<=4;i++)for(int j=0;j<=4;j++){
            var p=UP(1,u+w*i/4,z+d*j/4);
            if(!TryFarmFloor(p,out _))return false;
        }
        return true;
    }
    static bool TryFarmFloor(Vector3 p,out Vector3 foot)
    {
        foreach(var hit in Physics.RaycastAll(p+Vector3.up*2.5f,Vector3.down,5,1,QueryTriggerInteraction.Ignore)){
            if(hit.collider.transform.parent?.name!="Eastern uplands" || hit.normal.y<.77f)continue;
            foot=hit.point;return true;
        }
        foot=p;return false;
    }
    // One parcel: 0 wheat, 1 lavender, 2 vineyard, 3 orchard, 4 pasture, 5 kitchen garden, 6 sunflowers.
    static void Field(int side, float u0, float z0, float w, float d, int kind)
    {
        Vector3 At(float u, float z) { TryFarmFloor(UP(side,u,z),out var p);return p; }
        void Row(float u, Material mat, Color color, float width, float height, bool pointed)
        {
            for (float z = z0; z < z0 + d - .1f; z += 3)
            {
                float z1 = Mathf.Min(z + 3, z0 + d);
                var a = At(u, z); var b = At(u, z1); var across = Vector3.right * width * .5f;
                tint = Color.Lerp(color, Color.white, D(0, .12f)); wind = .6f;
                var at = a + Vector3.up * height; var bt = b + Vector3.up * height;
                if (pointed) { QuadN(a - across, b - across, bt, at, new Vector3(-1, 1, 0), mat,false); QuadN(a + across, at, bt, b + across, new Vector3(1, 1, 0), mat,false); }
                else { QuadN(at - across, bt - across, bt + across, at + across, Vector3.up, mat,false); QuadN(a - across, b - across, bt - across, at - across, Vector3.left, mat,false); QuadN(a + across, at + across, bt + across, b + across, Vector3.right, mat,false); }
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
                TryFarmFloor(p,out p);TryFarmFloor(q,out q);
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
        if(refreshingMountains && AssetDatabase.LoadAssetAtPath<Mesh>(GeoPath+"/"+name+".asset") is Mesh existing)return baked[name]=existing;
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
        tint = P("B66547");
        foreach(int side in new[]{-1,1}) {
            Box(new Vector3(side*6.78f,2.6f,0),new Vector3(.44f,6.2f,22),plaster,true);
            DoorWall(13.56f,.5f,5.7f,side*10.8f,5.2f,4.3f,plaster,.4f);
        }
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
        foreach(int s in new[]{-1,1})
        {
            // Continuous cedar sheathing follows both slopes of the gambrel.
            for (int plank = 0; plank < 36; plank++) {
                float a = Mathf.Lerp(-11.12f,11.12f,plank/36f), b = Mathf.Lerp(-11.12f,11.12f,(plank+1)/36f);
                tint = Color.Lerp(P("9E7152"),P("CAA177"),((plank*13)%17)/16f);
                var inward = new Vector3(-s,-1,0);
                QuadN(V(s*7.58f,-.10f,a),V(s*4.6f,3.10f,a),V(s*4.6f,3.10f,b),V(s*7.58f,-.10f,b),inward,wood,true);
                QuadN(V(s*4.6f,3.10f,a),V(0,4.70f,a),V(0,4.70f,b),V(s*4.6f,3.10f,b),inward,wood,true);
            }
            float insideZ=s*10.97f;
            tint=P("B28560");
            QuadN(V(-7.6f,-.02f,insideZ),V(-4.6f,3.2f,insideZ),V(4.6f,3.2f,insideZ),V(7.6f,-.02f,insideZ),Vector3.back*s,wood);
            Tri(V(s*4.6f,3.2f,insideZ),V(-s*4.6f,3.2f,insideZ),V(0,4.8f,insideZ),wood);
            float z=s*11;
            tint=P("B66547");
            QuadN(V(-7,0,z),V(-4.6f,3.2f,z),V(4.6f,3.2f,z),V(7,0,z),Vector3.forward*s,plaster);
            Tri(V(-s*4.6f,3.2f,z),V(s*4.6f,3.2f,z),V(0,4.8f,z),plaster,true);
            tint=P("725744"); Box(new Vector3(0,7.4f,z+s*.08f),new Vector3(1.6f,1.35f,.14f),wood);
            Door(s<0?"Barn orchard doors":"Barn field doors",s*10.82f,.5f,5.2f,4.3f,s<0?0:180,P("924A38"));
            Vector3 Apron(float x,float dist) {
                var v=new Vector3(x,.51f,s*(11.12f+dist));
                if(dist>0){var world=frame.MultiplyPoint3x4(v);v.y=Ground(1,world)-c.y+.075f;}
                return v;
            }
            tint=P("D3B48B"); QuadN(Apron(-2.9f,0),Apron(2.9f,0),Apron(2.9f,3.8f),Apron(-2.9f,3.8f),Vector3.up,paving);
        }
        foreach(int s in new[]{-1,1}) for(int bay=0;bay<3;bay++)
        {
            float z=-5.6f+bay*5.2f; tint=P("835738");
            Beam(new(s*6.45f,.55f,z),new(s*6.45f,5.8f,z),.26f,wood,true);
            Beam(new(s*6.45f,5.5f,z),new(-s*.02f,9.7f,z),.24f,wood);
            Box(new(s*4.9f,1.04f,z),new(2.8f,1.08f,.15f),wood,true);
            tint=P("CBAA62"); Box(new(s*5f,1.12f,z+1.25f),new(1.8f,1.24f,1.57f),clay,true);
            tint=P("71523D"); foreach(float band in new[]{-.5f,.5f})Box(new(s*5+band,1.75f,z+1.25f),new(.055f,.03f,1.55f),wood);
        }
        var barnHeading=frame.rotation;
        tint = Color.white; frame = old;
        for (int i=0;i<5;i++) { var p=OnGround(1,c+new Vector3(D(-14,14),0,D(-16,16))+Vector3.right*12);
            var local=Quaternion.Inverse(barnHeading)*(p-c); if(Mathf.Abs(local.x)>10 || Mathf.Abs(local.z)>21) Haystack(p);
        }
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
        // Water is never solid: the bed below carries the player and the swim volumes make it swimmable.
        var path = StreamPath();
        // The ribbon starts inside the spring pond, below its surface so the two never z-fight.
        for (int i = 0; i + 1 < path.Length; i++)
            if (path[i].z >= -43) QuadN(StreamWorld(i, -7.5f), StreamWorld(i + 1, -7.5f), StreamWorld(i + 1, 7.5f), StreamWorld(i, 7.5f), Vector3.up, water, false);
        var pond = W0(side, -48); Cylinder(new Vector3(pond.x, WaterY(-48), -48), 12, .05f, water, false, 24);
        for (int i = 0; i < 14; i++) { float a = i * Mathf.PI / 7; Tree(OnGround(side, new Vector3(pond.x + Mathf.Cos(a) * 15, 0, -48 + Mathf.Sin(a) * 15)), D(.7f, 1), i % 3 == 0 ? 2 : 0); }
        for (float z = -40; z < BendZ - 6; z += D(5, 9))
            foreach (int s in new[] { -1, 1 })
                if (Chance(.35f)) { tint = Color.Lerp(P("6E8B4A"), P("93A862"), D(0, 1)); var p = W0(side, z) + Vector3.right * side * s * D(8, 10); p.y = Ground(side, p); wind = .8f; Ellipsoid(p + Vector3.up * .4f, new Vector3(.5f, 1.1f, .5f), leaf, 6, 5, .2f); wind = 0; }
        tint = Color.white;
    }
    static Vector3 W0(int side, float z) { var crest = Crest(side, z); return new Vector3(crest.x + side * RiverU(z), WaterY(z), z); }
    // The old quay, four terrace levels and uplands fan into coves at the river mouth.
    static List<Vector2> CoastalSection(int side)
    {
        var section=new List<Vector2>(); float edge=Edge(side,0,TerrainStart);
        void At(float x,float y)=>section.Add(new Vector2((x-edge)*side,y));
        At(edge-side*7,-4); At(edge-side*.05f,-2);
        for(int tier=0;tier<4;tier++){
            At(Edge(side,tier,TerrainStart),Level(side,tier,TerrainStart)-.03f);
            At(Edge(side,tier+1,TerrainStart)-side*.08f,Level(side,tier,TerrainStart)-.03f);
        }
        foreach(var p in CliffProfile(side,TerrainStart,30).Take(31))At(p.x,p.y);
        for(float u=3;u<=UplandWidth(side);u+=3){var p=UP(side,u,TerrainStart);At(p.x,p.y);}
        return section;
    }
    static Vector3 Shore(int side,float d,float h,float t)
    {
        float blend=Edge01(0,.77f,t);
        float reach=(side<0?105:135)+18*Mathf.Sin(d*.036f+side)+8*Mathf.Sin(d*.085f)-d*.09f;
        float waves=Mathf.Sin(d*.12f+t*10)*.76f+Mathf.Sin(t*13+d*.056f)*.72f;
        float low=d<0?Mathf.Lerp(-4.2f,1f,Mathf.InverseLerp(-7,0,d)):2.6f+Mathf.Min(d*.042f,4)+waves;
        float slope = 3 + 63*Edge01(15,82,d) + 48*Edge01(61,122,d);
        slope += (3.2f*Mathf.Sin(d*.11f + 8*t)+2.8f*Mathf.Sin(12*t+d*.07f))*Edge01(18,47,d);
        // Ease the urban terrace risers into a continuous hillside as they leave the city.
        float ridge=d<0?h:Mathf.Lerp(h,slope,Edge01(0,.23f,t));
        float top=Mathf.Lerp(ridge,low,blend);
        top=Mathf.Lerp(top,-5.8f,Edge01(.80f,1,t));
        float outer=Crest(side,TerrainStart).x*side-Edge(side,0,TerrainStart)*side+UplandWidth(side);
        top=Mathf.Lerp(top,-9,blend*Edge01(outer-17,outer,d));
        float shoulder=Edge01(0,.09f,t)*(1-Edge01(.12f,.26f,t))*Mathf.Clamp(h-4,0,85)*.19f;
        float x=Edge(side,0,TerrainStart)+side*(d+shoulder+blend*(9*Mathf.Sin(t*6+side)+6*t)+Mathf.Sin(d*.07f)*t*2.5f);
        return new Vector3(x,top,TerrainStart-t*reach);
    }
    static Vector3 StrandPoint(int side,float d,float t)=>Shore(side,d,Level(side,0,TerrainStart)-.03f,t);
    static readonly Vector3 KeyCenter=new(-88,0,726);
    static float KeyRim(float angle)=>1+.09f*Mathf.Cos(3*angle+.45f)+.064f*Mathf.Sin(5*angle-.7f)+.09f*Mathf.Max(0,Mathf.Sin(angle));
    static Vector3 IslandGround(float x,float z)
    {
        var uv=new Vector2(x/44,z/31);
        float radius=uv.magnitude/KeyRim(Mathf.Atan2(uv.y,uv.x));
        float beach=Mathf.Max(0,(radius-.35f)/.65f);
        float y=Mathf.Max(-9.24f,3.15f-6.35f*beach*beach);
        var hill=new Vector2((x+21)/12,(z+6)/11);
        y+=7.4f*Mathf.Exp(-hill.sqrMagnitude)*(1-Edge01(.58f,1,radius));
        float channel=Mathf.Abs(z+5.7f-1.7f*Mathf.Sin(x*.15f));
        float cove=Edge01(9,23,x)*(1-Edge01(2.2f,8.8f,channel));
        y=Mathf.Lerp(y,Mathf.Min(y,-1.65f),cove);
        var pool=new Vector2((x-13.3f)/4,(z+10.1f)/3);
        y-=3.95f*Mathf.Exp(-pool.sqrMagnitude);
        return KeyCenter+new Vector3(x,y,z);
    }
    static Vector3 KeyPoint(float a,float r)=>IslandGround(Mathf.Cos(a)*44*r*KeyRim(a),Mathf.Sin(a)*31*r*KeyRim(a));
    static void CoastHouse(Vector3 basePoint,float yaw,bool island)
    {
        var old=frame;frame=Matrix4x4.TRS(basePoint,Quaternion.Euler(0,yaw,0),Vector3.one);
        const float w=8,d=9,h=4,opening=2.6f;
        tint=P("E0CBA8");Box(new(0,-.62f,0),new(w+1,1.25f,d+1),trim,true);
        tint=island?P("EDE3C6"):P("D7AA84");
        DoorWall(w,0,h,-d/2,opening,2.95f,plaster);
        Box(new(0,h/2,d/2),new(w,h,.4f),plaster,true);
        foreach(int s in new[]{-1,1}){
            Box(new(s*w/2,.63f,0),new(.4f,1.26f,d),plaster,true);
            Box(new(s*w/2,3.55f,0),new(.4f,.9f,d),plaster,true);
            foreach(int e in new[]{-1,1})Box(new(s*w/2,2.18f,e*3.28f),new(.4f,1.86f,2.45f),plaster,true);
            tint=island?P("357477"):P("A15742");
            Box(new(s*w/2,1.27f,0),new(.58f,.1f,4.15f),shutter);
            tint=island?P("EDE3C6"):P("D7AA84");
        }
        Door(island?"Tidekeeper's chart room":"Salt store", -d/2,0,opening,2.95f,0,island?P("286C72"):P("805239"));
        foreach(int s in new[]{-1,1}){
            tint=island?P("C8DEDC"):P("DC9679"); var edge=new Vector3(s*4.65f,4,-5.15f);var peak=new Vector3(0,5.6f,-5.15f);
            QuadN(edge,peak,peak+Vector3.forward*10.3f,edge+Vector3.forward*10.3f,new Vector3(s,2,0),roof);
            QuadN(edge-Vector3.up*.15f,peak-Vector3.up*.15f,peak+new Vector3(0,-.15f,10.3f),edge+new Vector3(0,-.15f,10.3f),Vector3.down,wood,false);
            tint=P("E0CEAC");float z=s*4.5f;Tri(new(-s*4,4,z),new(s*4,4,z),new(0,5.6f,z),plaster,true);
        }
        tint=P("977653");Box(new(0,.85f,3.13f),new(4,.16f,1.22f),wood,true);
        foreach(int s in new[]{-1,1}) Box(new(s*1.72f,.4f,3.13f),new(.16f,.8f,.85f),wood,true);
        if(island) {
            tint=P("E8D8AF");Box(new(-.4f,.955f,3.08f),new(1.8f,.04f,.82f),trim);
            tint=P("487B83");Beam(new(-1.16f,.993f,2.86f),new(.26f,.993f,3.32f),.045f,shutter);
            tint=Palette.Brass;Torus(new(1.14f,1.48f,3.14f),.4f,.045f,brass);
            Cylinder(new(1.14f,.94f,3.14f),.25f,.18f,brass);
        } else {
            for(int i=0;i<4;i++){ tint=P("B78059"); Cylinder(new(-3,.02f,-1+i*1.1f),.44f,.84f,clay,true,12,.48f);
                tint=P("F1E5CC");Ellipsoid(new(-3,.83f,-1+i*1.1f),new(.42f,.17f,.42f),trim);}
            for(int i=0;i<3;i++){var a=new Vector3(3.2f,.08f,-2+i);var b=a+new Vector3(.38f,2.7f,.1f);Beam(a,b,.09f,wood);Beam(a+Vector3.back*.31f,a+Vector3.forward*.31f,.1f,wood);}
        }
        tint=P("DEC8A3");QuadN(new(-2,.025f,-4.98f),new(2,.025f,-4.98f),new(2,-.72f,-8.1f),new(-2,-.72f,-8.1f),Vector3.up,paving);
        tint=Color.white;frame=old;
    }
    const int CoastRows = 56;
    static List<Vector2> CoastSamples(int side)
    {
        var section=CoastalSection(side); var samples=new List<Vector2>();
        for(int k=0;k<section.Count-1;k++) {
            int cuts=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(section[k+1].x-section[k].x)/3));
            for(int j=0;j<cuts;j++)samples.Add(Vector2.Lerp(section[k],section[k+1],(float)j/cuts));
        }
        samples.Add(section[section.Count-1]);return samples;
    }
    static void SouthernSeabed()
    {
        tint=P("CDB591");
        for(int x=-700;x<700;x+=100)for(int z=-220;z>-1220-WildlandsShift;z-=50){
            float south=Mathf.Max(z-50,-1220-WildlandsShift);
            QuadN(new(x,-9.1f,south),new(x+100,-9.1f,south),new(x+100,-9.1f,z),new(x,-9.1f,z),Vector3.up,bedMat);
        }
    }
    static void SouthernReach()
    {
        foreach(int side in new[]{-1,1}){
            var samples=CoastSamples(side);
            const int rows=CoastRows;
            var grid=new Vector3[samples.Count,rows+1];
            for(int j=0;j<samples.Count;j++) for(int i=0;i<=rows;i++)
                grid[j,i]=Shore(side,samples[j].x,samples[j].y,i/(float)rows);
            for(int j=1;j<samples.Count;j++) for(int i=0;i<rows;i++){
                var p=grid[j-1,i];var q=grid[j,i];var r=grid[j,i+1];var v=grid[j-1,i+1];
                bool steep=Mathf.Abs(q.y-p.y)>Mathf.Max(1.2f,Mathf.Abs(q.x-p.x)*.73f);
                tint=Color.Lerp(P(steep?"B49B7B":p.y<1.6f?"D9BC8B":"B6AD78"),P(steep?"DCCBA9":p.y<1.6f?"F2DEB0":"DBCE9A"),Mathf.PerlinNoise(p.x*.08f,p.z*.08f));
                if(side>0)Quad(p,q,r,v,rock,true);else Quad(p,v,r,q,rock,true);
            }
            Vector3 Track(float t,float offset){float d=6+4*Mathf.Sin(t*7+side)+t*5;return GroundAt(d+offset,t)+Vector3.up*.085f;}
            for(int i=0;i<38;i++){
                float t=i*.82f/38,u=(i+1)*.82f/38;
                tint=P("D6BB90");QuadN(Track(t,-1.5f),Track(t,1.5f),Track(u,1.5f),Track(u,-1.5f),Vector3.up,paving);
            }
            Vector3 GroundAt(float d,float t){
                // Place paths, masonry and roots on the same two triangles the player stands on.
                int j=samples.FindIndex(sample=>sample.x>=d)-1;
                int i=Mathf.Min(Mathf.FloorToInt(t*rows),rows-1);
                float across=Mathf.InverseLerp(samples[j].x,samples[j+1].x,d),along=t*rows-i;
                var p=grid[j,i];var diagonal=grid[j+1,i+1]-p;
                if(across>=along)return p+diagonal*along+(grid[j+1,i]-p)*(across-along);
                return p+diagonal*across+(grid[j,i+1]-p)*(along-across);
            }
            for(int i=0;i<64;i++){
                float t=.11f+.72f*Mathf.Repeat(i*.618034f,1), d=21+Mathf.Repeat(i*.3847f+.16f,1)*70;
                var p=GroundAt(d,t);var along=GroundAt(d,t+.006f);var across=GroundAt(d+1,t);
                if(Mathf.Abs(across.y-p.y)>.7f || Mathf.Abs(along.y-p.y)>.72f)continue;
                if(i%7==0)UmbrellaPine(p-Vector3.up*.36f,.68f+.12f*(i%3));
                else if(i%5==0)Cypress(p-Vector3.up*.24f,2.8f+i%4);
                else if(i%3==0)Rock(p-Vector3.up*.49f,new(1.9f,.91f,1.22f),9);
                else {tint=i%6==1?P("798568"):P("A8986B");Ellipsoid(p+Vector3.up*.12f,new(.85f,.48f,1.13f),leaf,8,5,.12f);}
            }
            for(int i=0;i<11;i++){
                float t=.25f+i*.026f; var a=GroundAt(18,t);
                tint=P(i%3==0?"C4AE8D":"D4BC98");
                var left=GroundAt(17.75f,t);var right=GroundAt(18.25f,t);
                var endLeft=GroundAt(17.75f,t+.026f);var endRight=GroundAt(18.25f,t+.026f);
                var lip=Vector3.up*.48f;var foot=Vector3.down*.18f;
                QuadN(left+lip,right+lip,endRight+lip,endLeft+lip,Vector3.up,trim);
                QuadN(left+foot,left+lip,endLeft+lip,endLeft+foot,Vector3.left*side,trim);
                QuadN(right+foot,right+lip,endRight+lip,endRight+foot,Vector3.right*side,trim);
                if(i==0)QuadN(left+foot,right+foot,right+lip,left+lip,Vector3.forward,trim);
                if(i==10)QuadN(endLeft+foot,endRight+foot,endRight+lip,endLeft+lip,Vector3.back,trim);
                if(i%3==0)Box(a+Vector3.up*.34f,new(.44f,.66f,.44f),trim,true);
            }
        }
        // Meet the original sea at z = -450.
        tint=Color.white;
        QuadN(new(-700,-.01f,-1250),new(-700,-.01f,-450),new(700,-.01f,-450),new(700,-.01f,-1250),Vector3.up,water,false);
        void Ocean(float x0,float x1,float z0,float z1){
            QuadN(new(x0,-.01f,z0),new(x0,-.01f,z1),new(x1,-.01f,z1),new(x1,-.01f,z0),Vector3.up,water,false);
        }
        Ocean(-2700,-700,-2700,2400);Ocean(700,2700,-2700,2400);
        Ocean(-700,700,-2700,-1250);Ocean(-700,700,950,2400);
        SouthernSeabed();
        for(int i=0;i<96;i++)for(int j=0;j<48;j++){
            float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48,r=j*.025f,u=(j+1)*.025f;
            var p=KeyPoint(a,r);var next=KeyPoint(b,u);
            bool sand=p.y<1.8f; bool bluff=p.y>5;
            tint=Color.Lerp(P(bluff?"B9A582":sand?"D7BE92":"A7A675"),P(bluff?"DBC9A6":sand?"F0DEB0":"D6CB93"),Mathf.PerlinNoise(p.x*.2f,p.z*.16f));
            QuadN(p,KeyPoint(b,r),next,KeyPoint(a,u),Vector3.up,rock);
        }
        var salt=StrandPoint(1,13,.62f); CoastHouse(salt+Vector3.up*.33f,170,false);
        CoastHouse(IslandGround(.9f,-1.6f)+Vector3.up*.10f,180,true);
        foreach(var tree in new[]{new Vector2(-8,3),new Vector2(-12,6),new Vector2(-25,3),new Vector2(-15,-14),new Vector2(5,-14)})
            UmbrellaPine(IslandGround(tree.x,tree.y)-Vector3.up*.28f,.82f);
        for(int i=0;i<17;i++){float a=2.25f+i*.19f;var p=KeyPoint(a,.79f+.04f*Mathf.Sin(i*2));Rock(p-Vector3.up*.64f,new(1.35f,.96f+i%3*.22f,1.05f),9);}
        // Broken limestone arch straddling the tidal cut. The deeper channel remains swimmable.
        var sill=IslandGround(24,-5.9f);
        var previous=frame;frame=Matrix4x4.TRS(new Vector3(sill.x,-.8f,sill.z),Quaternion.Euler(0,79,0),Vector3.one);
        tint=P("CFB693");Arch(Vector3.zero,4.7f,3.85f,.94f,2.1f,rock,true,17);
        for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
            Rock(new Vector3(side*(4.7f+.49f*i),-.38f,-.4f+.55f*i),new(1.25f,1.85f-i*.2f,1.34f),8);
        frame=previous;
        for(int i=0;i<18;i++){
            float a=i*2.4f;var p=IslandGround(13.3f+3.9f*Mathf.Cos(a),-10.1f+2.8f*Mathf.Sin(a));
            if(i%3==0){Rock(p-Vector3.up*.22f,new(.53f,.42f,.38f),8);continue;}
            tint=i%2==0?P("C27D66"):P("CCAA72");
            for(int j=0;j<5;j++){float f=a+j*Mathf.PI*.4f;Limb(p+Vector3.up*.045f,p+new Vector3(Mathf.Cos(f)*.23f,.015f,Mathf.Sin(f)*.23f),.072f,.015f,clay,5);}
        }
        var trail=new[]{new Vector2(-2,6),new Vector2(-8,8),new Vector2(-16,7),new Vector2(-22,3),new Vector2(-22,-3)};
        for(int i=1;i<trail.Length;i++){
            var from=trail[i-1];var to=trail[i];var direction=(to-from).normalized;var side=new Vector2(-direction.y,direction.x)*.65f;
            int stones=Mathf.CeilToInt(Vector2.Distance(from,to)/1.05f);
            for(int j=0;j<stones;j++){
                var center=Vector2.Lerp(from,to,(j+.5f)/stones);var a=center-direction*.35f;var b=center+direction*.35f;
                Vector3 Stone(Vector2 v)=>IslandGround(v.x,v.y)+Vector3.up*.10f;
                tint=P(j%3==0?"D9C8A1":"C3B28E");QuadN(Stone(a-side),Stone(a+side),Stone(b+side),Stone(b-side),Vector3.up,trim,false);
            }
        }
        var lookout=IslandGround(-22,-6);
        tint=P("DAC8A6");Cylinder(lookout-Vector3.up*1.42f,3.2f,1.65f,trim,true,24);
        for(int i=0;i<12;i++){float a=.25f+i*.22f;var p=lookout+new Vector3(Mathf.Cos(a)*2.88f,.23f,Mathf.Sin(a)*2.88f);
            Cylinder(p,.38f,.47f+i%4*.1f,trim,true,7);}
        var oldMarker=group.Find("Signal lookout");if(oldMarker)Object.DestroyImmediate(oldMarker.gameObject);
        var marker=new GameObject("Signal lookout").transform;marker.SetParent(group,false);marker.position=lookout+new Vector3(-.76f,.23f,0);
        var beacon=lookout+new Vector3(1.75f,.23f,-.5f);
        beacon+=new Vector3(1.7f,-.65f,.1f);
        tint=P("E9DABD");Cylinder(beacon,1.85f,9.5f,trim,true,16,1.24f);
        for(int j=0;j<3;j++){tint=P("438586");Cylinder(beacon+Vector3.up*(2+j*2.5f),1.75f-j*.16f,.61f,shutter,true,16,1.71f-j*.16f);}
        tint=Palette.Limestone;Cylinder(beacon+Vector3.up*9.45f,1.86f,.41f,trim,true,20);
        for(int j=0;j<8;j++){float a=j*Mathf.PI/4;var post=beacon+new Vector3(Mathf.Cos(a)*1.22f,9.8f,Mathf.Sin(a)*1.22f);
            tint=Palette.Brass;Beam(post,post+Vector3.up*2.46f,.12f,brass);}
        tint=P("FFE9A3");Ellipsoid(beacon+Vector3.up*10.96f,new(.7f,.96f,.70f),glow);
        tint=Palette.Terracotta;Dome(beacon+Vector3.up*12.2f,1.67f,1.67f,roof,true,16);
        tint=Palette.Brass;Cylinder(beacon+Vector3.up*13.76f,.13f,1.1f,brass,false,8,.018f);
        for(int i=0;i<3;i++) {
            var pan=salt+new Vector3(3.5f+i*3.2f,-.12f,-8.9f);
            tint=P("D5B58A"); Box(pan-Vector3.up*.75f,new(2.8f,2f,4.6f),trim,true);
            tint=i==2?P("F6E8D1"):P("92BCB2");Box(pan+Vector3.up*.27f,new(2.38f,.06f,4.15f),shutter);
        }
        void School(string name,Vector3 at,int count,float radius,float depth){
            var school=new GameObject(name).AddComponent<RiverLife>();school.transform.SetParent(group,false);
            school.transform.position=at;school.material=shutter;school.count=count;school.radius=radius;school.depth=depth;
        }
        School("Anchovies at the beacon",KeyCenter+new Vector3(25,0,-5.4f),12,3,.60f);
        School("Mullet in the inlet",new(-23,0,-115),10,3.4f,.53f);
        School("Pier sardines",new(QuayEdge(-1,422)+12,0,428),13,3,.52f);
        foreach(float z in new[]{58f,247f})School("Stream trout "+z,W0(1,z),7,1.8f,.52f);
        tint=Color.white;
    }
    static void StreamBridge(int side, float z)
    {
        var c=W0(side,z); var old=frame; frame=Matrix4x4.Translate(c);
        const float half=12, railZ=1.72f, spring=-2.2f;
        float bankL=Ground(side,c+Vector3.left*half)-c.y+.25f, bankR=Ground(side,c+Vector3.right*half)-c.y+.25f;
        float crown=Mathf.Max(1.9f,(bankL+bankR)*.5f+.45f);
        float Deck(float x)=>Mathf.Lerp(bankL,bankR,(x+half)/(2*half))+ (crown-(bankL+bankR)*.5f)*(1-x*x/(half*half));
        float ArchY(float x)=>spring+Mathf.Sqrt(Mathf.Max(0,1-x*x/100))*(crown-spring-.68f);
        for(int i=0;i<32;i++)
        {
            float x=-half+2*half*i/32, u=x+2*half/32;
            float h=Deck(x), k=Deck(u);
            tint=P("E2D1AB"); QuadN(new(x,h,-1.9f),new(x,h,1.9f),new(u,k,1.9f),new(u,k,-1.9f),Vector3.up,paving);
            foreach(int sign in new[]{-1,1}) {
                float d=sign*1.9f;
                tint=P(i%4==0?"C8B08B":"D3BC97");
                QuadN(new(x,h,d),new(u,k,d),new(u,ArchY(u),d),new(x,ArchY(x),d),Vector3.forward*sign,trim);
            }
            tint=P("BFA884"); QuadN(new(x,ArchY(x),-1.9f),new(u,ArchY(u),-1.9f),new(u,ArchY(u),1.9f),new(x,ArchY(x),1.9f),Vector3.down,trim);
        }
        foreach(int sign in new[]{-1,1}) {
            for(int i=0;i<4;i++){float x=-half+i*6; Rail(new(x,Deck(x),sign*railZ),new(x+6,Deck(x+6),sign*railZ));}
            float x0=sign*half, x1=sign*(half+4);
            Vector3 Bank(float x,float d)=>new(x,Ground(side,c+new Vector3(x,0,d))-c.y+.10f,d);
            tint=P("D8BC93");
            QuadN(new(x0,Deck(x0),-1.9f),new(x0,Deck(x0),1.9f),Bank(x1,1.9f),Bank(x1,-1.9f),Vector3.up,paving);
        }
        frame=old;tint=Color.white;
    }
    // ---------- stream cascade: over the crest, then a fall, pool and runnel on each terrace down to the river ----------
    static bool NearCascade(int side, float z, float margin) => side > 0 && Mathf.Abs(z - CascadeZ) < margin;
    static void Cascade()
    {
        const int side = 1; const float z = CascadeZ, poolR = 3.4f;
        var fx = new System.Random(3360); var outward = Vector3.left * side;
        float F(float a, float b) => Mathf.Lerp(a, b, (float)fx.NextDouble());
        void Mist(Vector3 foot, float spread)
        {
            wind = .9f;
            for (int i = 0; i < 9; i++)
            {
                tint = Color.Lerp(P("FBFAF3"), P("CFE8EA"), F(0, .5f));
                Ellipsoid(foot + new Vector3(F(-spread, spread), F(0, 1.2f), F(-spread, spread)), new Vector3(F(.9f, 1.8f), F(.6f, 1.3f), F(.9f, 1.8f)), cloth, 9, 6);
            }
            wind = 0; tint = Color.white;
        }
        // Down the escarpment, hugging the rock.
        FallSheet("Crest falls", (f, s) =>
        {
            var prof = CliffProfile(side, z + s * 10, 30); float j = (1 - f) * 30; int lo = Mathf.Min(Mathf.FloorToInt(j), 29);
            return Vector3.Lerp(prof[lo], prof[lo + 1], j - lo) + outward * .35f + Vector3.up * .15f;
        }, 10, 30);
        for (int tier = 3; tier >= 0; tier--)
        {
            float ground = Level(side, tier, z), back = Edge(side, tier + 1, z) + (tier == 3 ? side * .5f : 0), front = Edge(side, tier, z);
            var pool = new Vector3(back - side * (poolR + .2f), ground, z);
            tint = P("E9D9BB"); Cylinder(pool, poolR, .3f, trim, true, 20);
            tint = Color.white; Cylinder(pool + Vector3.up * .3f, poolR - .4f, .04f, water, false, 20);
            Mist(pool + Vector3.up * .3f, 1.8f);
            // Runnel from the pool across the terrace, kerbed so the water reads as channelled.
            float x0 = pool.x - side * (poolR - .5f), y = ground + .14f;
            tint = P("E9D9BB");
            foreach (int s in new[] { -1, 1 }) Box(new Vector3((x0 + front) * .5f, ground + .12f, z + s * 1.85f), new Vector3(Mathf.Abs(x0 - front), .24f, .4f), trim, true);
            tint = Color.white;
            QuadN(new Vector3(x0, y, z - 1.65f), new Vector3(x0, y, z + 1.65f), new Vector3(front, y, z + 1.65f), new Vector3(front, y, z - 1.65f), Vector3.up, water, false);
            // Over the street edge to the pool below, or into the river.
            float bottom = tier > 0 ? Level(side, tier - 1, z) + .3f : .05f, h = y - bottom; var lip = new Vector3(front, y, z);
            FallSheet($"Cascade fall {tier}", (f, s) => lip + outward * (.2f + .7f * Mathf.Sqrt(h * f)) + Vector3.down * h * f + Vector3.forward * s * 3.4f * (1 + f * .25f), 3.4f, 16);
            if (tier == 0) Mist(new Vector3(front - side * (.2f + .7f * Mathf.Sqrt(h)), 0, z), 3);
        }
        Spot(UP(side, 7, z + 12) + Vector3.up * .1f, "Top of the falls", true);
    }
    // A falling sheet: point(f, s) is the world position a fraction f down and s (-.5..5) across.
    static void FallSheet(string name, Func<float, float, Vector3> point, float width, int rows)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float top = point(0, 0).y, height = top - point(1, 0).y;
        for (int i = 0; i <= rows; i++)
            for (int k = 0; k <= 6; k++)
            {
                var p = point(i / (float)rows, k / 6f - .5f); v.Add(p); uv.Add(new Vector2(k / 6f * width / 4, top - p.y));
                if (i < rows && k < 6) { int n = i * 7 + k; t.AddRange(new[] { n, n + 7, n + 8, n, n + 8, n + 1 }); }
            }
        var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, GeoPath + "/" + name + ".asset");
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(group, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off;
        // The shader foams the last metres of each fall, so every height gets its own material.
        var mat = Asset($"Waterfall {Mathf.RoundToInt(height)} m", "TinyWorld/Waterfall"); mat.CopyPropertiesFromMaterial(fall); mat.SetFloat("_Height", height);
        r.sharedMaterial = mat;
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
                float outer0=side<0?TinyWorld.EditorArt.BuildQuarterLandmarks.WestQuayLimit(z):outer;
                float outer1=side<0?TinyWorld.EditorArt.BuildQuarterLandmarks.WestQuayLimit(z1):outer;
                QuadN(I(z, y), I(z1, y), new Vector3(outer1, y, z1), new Vector3(outer0, y, z), Vector3.up, paving);
                tint = P("E2CFAA"); aoBase = -1; aoSpan = 3;
                QuadN(I(z, -9), I(z1, -9), I(z1, y), I(z, y), Vector3.left * side, trim); aoBase = float.NaN;
                if (Mathf.RoundToInt(z) % 8 == 0) { tint = P("3E4A57"); Cylinder(I(z, y) + Vector3.right * side * .8f, .25f, .7f, wood, true, 8); }
            }
            tint = P("E2CFAA");
            if(side<0)outer=TinyWorld.EditorArt.BuildQuarterLandmarks.WestQuayLimit(PortZ1);
            QuadN(new Vector3(QuayEdge(side, PortZ1), -9, PortZ1), new Vector3(QuayEdge(side, PortZ1), y, PortZ1), new Vector3(outer, y, PortZ1), new Vector3(outer, -9, PortZ1), Vector3.forward, trim);
            if(side>0)QuadN(new Vector3(outer, -9, PortZ0), new Vector3(outer, y, PortZ0), new Vector3(outer, y, PortZ1), new Vector3(outer, -9, PortZ1), Vector3.right * side, trim);
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
    static void CanalMarket()
    {
        float y=QuayY(1);
        // The east wharf opens into six blocks. Both east-west cuts empty into the ship basin.
        var columns=new[]{(102f,150f),(161f,219f),(229f,277f)};
        var rows=new[]{(480f,504f),(515f,542f),(552f,560f)};
        foreach(var (x0,x1) in columns)foreach(var (z0,z1) in rows){
            var size=new Vector3(x1-x0,y+8,z1-z0);
            tint=P("D4B58C");Box(new((x0+x1)*.5f,(y-8)*.5f,(z0+z1)*.5f),size,trim,true);
            tint=P("E9D7B8");Box(new((x0+x1)*.5f,y+.045f,(z0+z1)*.5f),new(size.x,.09f,size.z),paving,true);
            for(float x=x0+3;x<x1-2;x+=9){
                tint=P("856B51");Cylinder(new(x,y+.09f,z1-.83f),.23f,.53f,wood,true,8,.29f);
            }
        }
        void Footbridge(float x,float z,float heading,float length,float width){
            var basis=frame;frame=Matrix4x4.TRS(new(x,y+.09f,z),Quaternion.Euler(0,heading,0),Vector3.one);
            float r=length*.5f, rise=1.05f;
            Vector3 Deck(float t,float across)=>new((2*t-1)*r,4*rise*t*(1-t),across);
            for(int i=0;i<16;i++){
                float a=i/16f,b=(i+1)/16f;
                var p=Deck(a,-width/2);var q=Deck(b,-width/2);var v=Deck(a,width/2);var w=Deck(b,width/2);
                tint=P("ECD9B7");QuadN(p,v,w,q,Vector3.up,paving);
                foreach(float edge in new[]{-width/2,width/2}){
                    var l=Deck(a,edge);var h=Deck(b,edge);
                    QuadN(l,h,h+Vector3.down*.57f,l+Vector3.down*.57f,Vector3.forward*edge,trim);
                    tint=Palette.Brass;Beam(l+Vector3.up*.88f,h+Vector3.up*.88f,.12f,brass,true);
                    if(i%3==0) Beam(l,l+Vector3.up*.9f,.15f,wood,true);
                }
            }
            tint=P("D1AF87");Arch(new(0,-2.25f,0),r-.6f,2.7f,.55f,width-.3f,trim,true);
            frame=basis;tint=Color.white;
        }
        foreach(float x in new[]{123f,194f,252f}){
            Footbridge(x,509.5f,90,11.2f,4);Footbridge(x,547,90,10.2f,3.7f);
        }
        foreach(float z in new[]{490f,528f,556f}){
            Footbridge(155.5f,z,0,11.2f,3.8f);Footbridge(224,z,0,10.2f,3.8f);
        }
        var frontages=new (float x,float z,float yaw,float w,float d,float h,Look look)[]{
            (112,493,0,13,8,10,Look.Merchant),(139,493,0,14,8,15,Look.Venetian),
            (113,520,0,12,12,17,Look.Venetian),(139,520,0,15,12,10,Look.Tuscan),
            (167,494,0,9,8,9,Look.Island),(183,494,0,13,8,13,Look.Merchant),
            (207,494,0,16,8,18,Look.Venetian),(235,519,0,9,16,14,Look.Island),
            (266,520,0,13,14,12,Look.Tuscan)};
        foreach(var f in frontages)Building(new(f.x,y-.12f,f.z),f.yaw,f.w,f.d,f.h,50,0,true,true,false,f.look,true);
        Warehouse(new(242,y+.09f,492),0,20,9,8.6f);
        Warehouse(new(266,y+.09f,492),0,16,10,10.5f);
        foreach(float x in new[]{238f,261f})CrateStack(new(x,y+.09f,485));
        // The paving compass marks the auction floor. Stalls leave both crossing axes clear.
        var plaza=new Vector3(191,y+.09f,528);
        tint=P("C2A884");Cylinder(plaza,7.5f,.045f,trim,false,32);
        tint=P("386E77");Cylinder(plaza+Vector3.up*.046f,6.85f,.024f,shutter,false,32);
        for(int i=0;i<8;i++){
            float a=i*Mathf.PI/4;
            var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var perp=Vector3.Cross(Vector3.up,dir);
            var p=plaza+Vector3.up*.079f;
            tint=i%2==0?Palette.Brass:Palette.Limestone;
            Tri(p-perp*.65f,p+dir*(i%2==0?6.35f:4.25f),p+perp*.65f,shutter);
            Tri(p+perp*.65f,p+dir*(i%2==0?6.35f:4.25f),p-perp*.65f,shutter);
        }
        foreach(float x in new[]{169f,178f,202f,212f})foreach(float z in new[]{518.8f,538.8f})
            Market(new(x,y+.09f,z),z<530?180:0,4.7f);
        foreach(float x in new[]{108f,143f,165f,214f,234f,271f}){
            Lamp(new(x,y+.09f,554.2f)); if(x>160)Parasol(new(x+1.5f,y+.09f,557.8f));
        }
        foreach(var (x0,x1) in new[]{(-247f,-184f),(-175f,-118f)})foreach(var (z0,z1) in new[]{(480f,515f),(526f,560f)}){
            float westY=QuayY(-1);
            tint=P("CFB08C");Box(new((x0+x1)/2,(westY-8)/2,(z0+z1)/2),new(x1-x0,westY+8,z1-z0),trim,true);
            tint=P("E1CCAA");Box(new((x0+x1)/2,westY+.045f,(z0+z1)/2),new(x1-x0,.09f,z1-z0),paving,true);
        }
        float eastY=y;y=QuayY(-1);
        foreach(float x in new[]{-225f,-147f})Footbridge(x,520.5f,90,11.2f,4.4f);
        foreach(float z in new[]{492f,544f})Footbridge(-179.5f,z,0,9.2f,4.2f);
        var fishHouses=new (float x,float z,float yaw,float w,float d,float h,Look style)[]{
            (-129,490,0,12,11,15,Look.Venetian),(-145,503,180,12,10,10,Look.Merchant),
            (-165,490,0,12,12,13,Look.Island),(-231,539,0,15,12,15,Look.Tuscan),
            (-207,539,0,14,10,11,Look.Merchant),(-193,538,0,9,13,17,Look.Venetian),
            (-126,537,0,11,14,13,Look.Island)};
        foreach(var f in fishHouses)Building(new(f.x,y-.12f,f.z),f.yaw,f.w,f.d,f.h,50,0,true,true,false,f.style,true);
        Warehouse(new(-229,y+.09f,491),0,24,16,8);Warehouse(new(-200,y+.09f,491),0,19,15,11);
        foreach(float x in new[]{-167f,-156f,-145f})Market(new(x,y+.09f,537),180,5);
        tint=Color.white;
        foreach(float x in new[]{-165f,-141f}){
            Lamp(new(x,y+.09f,551));CrateStack(new(x,y+.09f,556));Parasol(new(x+5,y+.09f,553));
        }
        y=eastY;
        MarketWaterGates();
        // Swimmers can climb the flights at the basin and at the inner market canal.
        Stairs(new(97,-1.05f,532),new(102.4f,y+.11f,532),3.2f);
        Stairs(new(180,-1.05f,513.85f),new(189,y+.11f,513.85f),2.6f);
        tint=Color.white;
    }

    static void HarbourTown(int side, float y)
    {
        float yaw = side < 0 ? -90 : 90;
        // Front row on the promenade, gaps left for the pier heads.
        for (float z = 403; z < 476;)
        {
            float w = D(7, 11);
            if (side < 0 && z < 474 && z + w > 466) { z += w; continue; }
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
    static void WarehouseGables(float width, float depth, float eave)
    {
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * width * .5f;
            var front = new Vector3(x, eave, -.4f);
            var back = new Vector3(x, eave, depth + .4f);
            var peak = new Vector3(x, eave + depth * .3f, depth * .5f);
            if (side < 0) Tri(front, back, peak, plaster, true);
            else Tri(back, front, peak, plaster, true);
        }
    }
    static void Warehouse(Vector3 foot, float yaw, float w, float d, float h)
    {
        var old = frame; frame = Matrix4x4.TRS(foot, Quaternion.Euler(0, yaw, 0), Vector3.one);
        tint = Pick("E3B26C", "D9905F", "E8C48A", "E2C8A0");
        var wallTint = tint;
        Box(new Vector3(0, h * .5f - .5f, d * .5f), new Vector3(w, h + 1, d), plaster, true, false);
        tint = Color.white; Box(new Vector3(0, h - .1f, d * .5f), new Vector3(w + .4f, .3f, d + .4f), trim, true);
        // Gable across the front, with a hoist beam at the apex.
        float ridge = d * .3f; tint = Color.Lerp(Color.white, P("E7A07E"), D(0, .4f));
        var a = new Vector3(-w * .5f - .4f, h, -.4f); var b = new Vector3(w * .5f + .4f, h, -.4f);
        QuadN(a, b, new Vector3(w * .5f + .4f, h + ridge, d * .5f), new Vector3(-w * .5f - .4f, h + ridge, d * .5f), new Vector3(0, 1, -1), roof);
        QuadN(new Vector3(-w * .5f - .4f, h, d + .4f), new Vector3(-w * .5f - .4f, h + ridge, d * .5f), new Vector3(w * .5f + .4f, h + ridge, d * .5f), new Vector3(w * .5f + .4f, h, d + .4f), new Vector3(0, 1, 1), roof);
        tint = wallTint; WarehouseGables(w, d, h);
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
    static bool SluiceBay(int i)=>i>=5 && i<10 || i>=16 && i<21;
    static Vector3[] BreakwaterPath(int side)
    {
        var p0 = new Vector3(QuayEdge(side, PortZ1) + side * 10, 0, PortZ1 - 2); var p1 = new Vector3(side * 100, 0, 572); var p2 = new Vector3(side < 0 ? -22 : 32, 0, 584);
        Vector3 B(float t) => (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
        var path = new Vector3[41]; for (int i = 0; i <= 40; i++) path[i] = B(i / 40f);
        return path;
    }
    static void MarketWaterGates()
    {
        var path=BreakwaterPath(1);
        foreach(int i in new[]{5,16}){
            var a=path[i];var b=path[i+5];var direction=(b-a).normalized;
            var previous=frame;frame=Matrix4x4.TRS((a+b)/2,Quaternion.LookRotation(Vector3.Cross(direction,Vector3.up)),Vector3.one);
            float r=Vector3.Distance(a,b)*.5f;
            tint=P("D6BD9A");Arch(new(0,-1.32f,0),r-.32f,4.0f,.55f,9.1f,trim,true,24);
            frame=previous;
        }
    }
    static void OpenMarketSluices()
    {
        var path=BreakwaterPath(1);
        foreach(var f in city.Find("Harbour").GetComponentsInChildren<MeshFilter>()){
            var mesh=f.sharedMesh;var v=mesh.vertices;var indices=mesh.triangles;var keep=new List<int>(indices.Length);
            for(int j=0;j<indices.Length;j+=3){
                var a=v[indices[j]];var b=v[indices[j+1]];var c=v[indices[j+2]];var mid=(a+b+c)/3;
                bool cut=false;
                if(mid.x>52 && mid.z>495 && mid.z<562 && (f.sharedMesh.name.Contains("Cliff rock") || Mathf.Min(a.y,b.y,c.y)<3.19f)){
                    foreach(int i in new[]{5,16}){
                        var axis=path[i+5]-path[i];var d=mid-path[i];d.y=0;
                        float along=Vector3.Dot(d,axis)/axis.sqrMagnitude;
                        if(along>.001f && along<.999f && (d-axis*along).sqrMagnitude<324)cut=true;
                    }
                }
                if(!cut){keep.Add(indices[j]);keep.Add(indices[j+1]);keep.Add(indices[j+2]);}
            }
            if(keep.Count==indices.Length)continue;
            mesh.SetTriangles(keep,0);EditorUtility.SetDirty(mesh);
            foreach(var col in f.GetComponentsInChildren<MeshCollider>()){col.sharedMesh=null;col.sharedMesh=mesh;}
        }
    }
    static void Breakwater(int side)
    {
        var path=BreakwaterPath(side);var p2=path[40];
        for (int i = 0; i < 40; i++)
        {
            var a = path[i];
            tint = Color.Lerp(P("E2CFAA"), P("D2BC94"), Mathf.PerlinNoise(i * .3f, side));
            if(side<0 || !SluiceBay(i))Sweep(path, i, -4.5f, 4.5f, -9.2f, 3.2f, trim);
            tint = P("E9D7B5"); Sweep(path, i, -3.8f, 3.8f, 3.2f, 3.5f, paving);
            tint = Color.white; Sweep(path, i, -side * 4.2f - .4f, -side * 4.2f + .4f, 3.35f, 4.65f, trim);
            if (i % 2 == 0 && (side<0 || !SluiceBay(i))) Rock(a + new Vector3(side * D(5, 7), -6, 0), new Vector3(D(1.6f, 2.8f), D(6, 8), D(1.6f, 2.8f)), 7);
            if (i % 8 == 4) Lamp(a + Vector3.up * 3.5f + Vector3.right * side * 2.5f);
        }
        if (side > 0) { tint = P("F3E8D2"); Cylinder(p2, 3.4f, 9, trim, true, 16, 2.8f); tint = P("B66547"); Lathe(p2 + Vector3.up * 9, new[] { new Vector2(3.1f, 0), new Vector2(.1f, 3) }, 16, roof, true); tint = Color.white; }
    }
    // Segment i of a prism swept along a path; joints are shared, so segments never overlap.
    static void Sweep(Vector3[] path, int i, float x0, float x1, float y0, float y1, Material mat)
    {
        Vector3 S(int k) { var t = path[Mathf.Min(k + 1, path.Length - 1)] - path[Mathf.Max(k - 1, 0)]; t.y = 0; return Vector3.Cross(Vector3.up, t).normalized; }
        Vector3 P(int k, float x, float y) => path[k] + S(k) * x + Vector3.up * y;
        int j = i + 1; var fwd = path[j] - path[i]; fwd.y = 0;
        QuadN(P(i, x0, y1), P(j, x0, y1), P(j, x1, y1), P(i, x1, y1), Vector3.up, mat);
        QuadN(P(i, x0, y0), P(j, x0, y0), P(j, x0, y1), P(i, x0, y1), -S(i), mat);
        QuadN(P(i, x1, y0), P(j, x1, y0), P(j, x1, y1), P(i, x1, y1), S(i), mat);
        if (i == 0) QuadN(P(i, x0, y0), P(i, x1, y0), P(i, x1, y1), P(i, x0, y1), -fwd, mat);
        if (j == path.Length - 1) QuadN(P(j, x0, y0), P(j, x1, y0), P(j, x1, y1), P(j, x0, y1), fwd, mat);
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
    static void ShipCompanionways()
    {
        void ShipSteps(float x,float z,float yaw,float length)
        {
            frame=Matrix4x4.TRS(new Vector3(x,0,z),Quaternion.Euler(0,yaw,0),Vector3.one);
            Companionway(length);
        }
        ShipSteps(QuayEdge(-1,458)+17,465.5f,90,30);
        ShipSteps(QuayEdge(-1,458)+17,450.5f,90,28);
        ShipSteps(QuayEdge(1,458)-17,465.5f,-90,30);
        ShipSteps(4,505,0,38);
        frame=Matrix4x4.identity;
    }
    static void Companionway(float length)
    {
        float Deck(float t) => 3 + 1.4f*Mathf.Pow(1-t,3) + .9f*Mathf.Pow(t,3);
        float x = length*.05f;
        Stairs(new Vector3(x, Deck(.38f), -.12f*length), new Vector3(x, Deck(.11f)+3.45f, -.291f*length), 1.4f);
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
