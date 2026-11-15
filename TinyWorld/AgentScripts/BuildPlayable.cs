using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using TMPro;
using TinyWorld;
using Object = UnityEngine.Object;

public static class BuildPlayable
{
    const string Path = "Assets/Scenes/RiverCity.unity";
    static Material stone, sand, clay, blue, brass;
    static Transform routes;
    public static object Build()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play mode and save first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Path)) throw new Exception("Playable scene already exists; edit it, do not regenerate.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/RiverCityBaseline.unity");
        stone = Mat("Limestone"); sand = Mat("Sandstone"); clay = Mat("Terracotta"); blue = Mat("Deep blue shutters"); brass = Mat("Aged brass");
        routes = new GameObject("Walkable routes — stairs, vaults, rooftop landings").transform;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int tier = 0; tier < 2; tier++)
            {
                float baseY = 1.6f + tier * 4.4f;
                for (int i = 0; i < 22; i++)
                {
                    float height = (i + 1) * .2f;
                    Cube("Terrace stair", routes, new Vector3(side * (7.15f + tier * 10 + i * .38f), baseY + height / 2, -7), new Vector3(.48f, height, 3.4f), stone, true);
                    Cube("Stair edge inlay", routes, new Vector3(side * (7.15f + tier * 10 + i * .38f), baseY + height + .008f, -8.4f), new Vector3(.42f, .012f, .15f), brass);
                }
            }
        }
        // The first residence has a readable, four-stage rooftop approach.
        float[] tops = { 2.65f, 3.8f, 4.95f };
        for (int i = 0; i < 3; i++)
        {
            Cube("Rooftop climbing ledge " + (i + 1), routes, new Vector3(-8.85f, tops[i] - .14f, 3.8f + i * 1.8f), new Vector3(1.6f, .28f, 1.8f), stone, true);
            Cube("Brass climb lip", routes, new Vector3(-8.03f, tops[i] - .04f, 3.8f + i * 1.8f), new Vector3(.05f, .07f, 1.65f), brass);
            Cube("Ledge support", routes, new Vector3(-9.22f, (tops[i] + 1.6f) / 2, 3.8f + i * 1.8f), new Vector3(.4f, tops[i] - 1.6f, 1.5f), sand, true);
        }
        Cube("Roof link to terrace", routes, new Vector3(-16.5f, 6.06f, 8), new Vector3(3.8f, .2f, 1.9f), clay, true);
        for (int i = 0; i < 5; i++)
        {
            float z = 19 + i * 9;
            Cube("Vault bench", routes, new Vector3(-7.25f, 2.04f, z), new Vector3(1.65f, .88f, .6f), sand, true);
            Cube("Vault bench cap", routes, new Vector3(-7.25f, 2.52f, z), new Vector3(1.8f, .08f, .74f), clay, true);
        }
        // Upper roof bridges make a continuous optional high route.
        foreach (int side in new[] { -1, 1 })
            for (int row = 0; row < 5; row++)
            {
                float z = 8 + row * 13;
                float first = 1.6f + 4.2f + ((row + (side + 1)) % 3) * 1.1f + .36f;
                float second = 1.6f + 4.2f + ((row + 1 + (side + 1)) % 3) * 1.1f + .36f;
                float startZ = side == -1 ? z + 3.65f : z + .65f;
                float span = 5.9f;
                var plank = Cube("Rooftop footbridge", routes, new Vector3(side * 12.2f, (first + second) / 2 - .08f, startZ + span / 2), new Vector3(1.8f, .18f, Mathf.Sqrt(span * span + (second - first) * (second - first))), clay, true);
                plank.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(second - first, span) * Mathf.Rad2Deg, 0, 0);
            }
        var player = new GameObject("Wanderer"); player.layer = 2; player.tag = "Player";
        player.transform.position = new Vector3(-7.2f, 1.65f, -2.5f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = .29f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .34f; cc.slopeLimit = 52; cc.skinWidth = .035f; cc.minMoveDistance = 0;
        var motor = player.AddComponent<WandererMotor>();
        var model = new GameObject("Hooded wayfarer").transform; model.SetParent(player.transform, false);
        var pose = model.gameObject.AddComponent<WandererPose>(); pose.player = motor;
        pose.torso = Pivot("Body", model, new Vector3(0, 1.02f, 0));
        Primitive("Travel tunic", PrimitiveType.Capsule, pose.torso, new Vector3(0, .13f, 0), new Vector3(.54f, .32f, .36f), blue);
        Primitive("Woven belt", PrimitiveType.Cylinder, pose.torso, new Vector3(0, -.02f, 0), new Vector3(.56f, .045f, .38f), clay);
        Cube("Brass buckle", pose.torso, new Vector3(0, -.02f, .2f), new Vector3(.13f, .11f, .05f), brass);
        pose.head = Pivot("Hood and head", pose.torso, new Vector3(0, .61f, .015f));
        Primitive("Limestone hood", PrimitiveType.Sphere, pose.head, Vector3.zero, new Vector3(.47f, .53f, .44f), stone);
        Primitive("Shadow within hood", PrimitiveType.Sphere, pose.head, new Vector3(0, -.025f, .18f), new Vector3(.29f, .34f, .12f), blue);
        Primitive("Face scarf", PrimitiveType.Sphere, pose.head, new Vector3(0, -.12f, .24f), new Vector3(.31f, .13f, .08f), clay);
        pose.leftArm = Arm("Left", pose.torso, -.32f); pose.rightArm = Arm("Right", pose.torso, .32f);
        pose.leftLeg = Leg("Left", model, -.16f); pose.rightLeg = Leg("Right", model, .16f);
        pose.cloak = Pivot("Short travelling cloak", pose.torso, new Vector3(0, .43f, -.1f));
        var cloak = new GameObject("Tailored cloak", typeof(MeshFilter), typeof(MeshRenderer)); cloak.transform.SetParent(pose.cloak, false);
        var mesh = new Mesh { name = "Wayfarer cloak" };
        mesh.vertices = new[] { new Vector3(-.32f, 0, 0), new Vector3(.32f, 0, 0), new Vector3(-.49f, -.85f, -.12f), new Vector3(.49f, -.85f, -.12f), new Vector3(0, -.93f, -.23f), new Vector3(0, -.12f, -.22f) };
        mesh.triangles = new[] { 0, 5, 1, 0, 2, 5, 2, 4, 5, 4, 3, 5, 3, 1, 5, 1, 5, 0, 5, 2, 0, 5, 4, 2, 5, 3, 4, 5, 1, 3 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, "Assets/Geometry/WandererCloak.asset"); cloak.GetComponent<MeshFilter>().sharedMesh = mesh; cloak.GetComponent<MeshRenderer>().sharedMaterial = stone;
        pose.scarf = Pivot("Scarf tail", pose.torso, new Vector3(.22f, .48f, -.18f));
        Cube("Terracotta scarf ribbon", pose.scarf, new Vector3(.12f, -.15f, -.12f), new Vector3(.14f, .48f, .045f), clay);
        Primitive("Leather satchel", PrimitiveType.Sphere, pose.torso, new Vector3(-.33f, -.12f, -.14f), new Vector3(.27f, .3f, .18f), clay);
        foreach (var t in player.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
        motor.visual = model;
        var follow = new GameObject("Camera orbit target — independent yaw").AddComponent<WandererCamera>();
        follow.player = motor; follow.transform.position = player.transform.position + Vector3.up * 1.25f;
        motor.heading = follow.transform;
        var cam = Camera.main; cam.gameObject.AddComponent<CinemachineBrain>();
        var rig = new GameObject("Wanderer follow — Cinemachine").AddComponent<CinemachineCamera>();
        rig.Follow = follow.transform; rig.Lens.FieldOfView = 57; rig.Lens.NearClipPlane = .08f; rig.Lens.FarClipPlane = 170;
        var body = rig.gameObject.AddComponent<CinemachineThirdPersonFollow>();
        body.ShoulderOffset = new Vector3(.48f, .12f, 0); body.VerticalArmLength = .26f; body.CameraDistance = 4.7f; body.CameraSide = 1;
        body.Damping = new Vector3(.12f, .25f, .16f);
        body.AvoidObstacles.Enabled = true; body.AvoidObstacles.CollisionFilter = 1; body.AvoidObstacles.CameraRadius = .18f; body.AvoidObstacles.IgnoreTag = "Player"; body.AvoidObstacles.DampingIntoCollision = 0; body.AvoidObstacles.DampingFromCollision = .35f;
        rig.gameObject.AddComponent<CinemachineImpulseListener>();
        follow.rig = rig;
        var feedback = player.AddComponent<WandererFeedback>(); feedback.player = motor; feedback.pose = pose;
        feedback.source = player.AddComponent<AudioSource>(); feedback.source.playOnAwake = false; feedback.source.spatialBlend = 0;
        feedback.impulse = player.AddComponent<CinemachineImpulseSource>(); feedback.impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        feedback.impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump; feedback.impulse.ImpulseDefinition.ImpulseDuration = .15f;
        motor.feedback = feedback;
        var dust = new GameObject("Pooled limestone dust").AddComponent<ParticleSystem>();
        var main = dust.main; main.playOnAwake = false; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .35f); main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .7f); main.startSize = new ParticleSystem.MinMaxCurve(.035f, .09f); main.startColor = Palette.Sandstone; main.gravityModifier = -.12f; main.maxParticles = 64; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = dust.emission; emission.enabled = false;
        var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 45; shape.radius = .2f;
        dust.transform.rotation = Quaternion.Euler(-90, 0, 0);
        var color = dust.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Palette.Limestone, 0), new GradientColorKey(Palette.Sandstone, 1) }, new[] { new GradientAlphaKey(.65f, 0), new GradientAlphaKey(0, 1) }); color.color = gradient;
        var dustMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Limestone dust" };
        dustMaterial.SetColor("_BaseColor", Palette.Limestone); dustMaterial.SetFloat("_Surface", 1); dustMaterial.SetFloat("_Blend", 0); dustMaterial.SetFloat("_SrcBlend", 5); dustMaterial.SetFloat("_DstBlend", 10); dustMaterial.SetFloat("_ZWrite", 0); dustMaterial.renderQueue = 3000;
        AssetDatabase.CreateAsset(dustMaterial, "Assets/Materials/Limestone dust.mat"); dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustMaterial;
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); feedback.dust = dust;
        var shell = new GameObject("River Quarter game").AddComponent<WandererShell>(); shell.player = motor; shell.orbit = follow; shell.feedback = feedback;
        shell.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (!shell.font) throw new Exception("Import TMP Essentials before building.");
        shell.gameObject.AddComponent<FrameTelemetry>().shell = shell;
        var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline; pipeline.useSRPBatcher = true; EditorUtility.SetDirty(pipeline);
        PlayerSettings.productName = "The River Quarter"; PlayerSettings.companyName = "Tiny World"; PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080; PlayerSettings.macRetinaSupport = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        QualitySettings.vSyncCount = 1;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Path, true) };
        EditorSceneManager.SaveScene(scene, Path); AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Playable River Quarter saved");
        return new { scene = Path, player = player.transform.position.ToString(), camera = rig.name, colliders = Object.FindObjectsByType<Collider>().Length };
    }
    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + name + ".mat");
    static Transform Pivot(string name, Transform parent, Vector3 position) { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 size, Material material, bool collision = false)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
        else go.isStatic = true;
        return go;
    }
    static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision = false) => Primitive(name, PrimitiveType.Cube, parent, position, size, material, collision);
    static Transform Arm(string side, Transform torso, float x)
    {
        var pivot = Pivot(side + " shoulder", torso, new Vector3(x, .4f, 0));
        Primitive(side + " sleeve", PrimitiveType.Capsule, pivot, new Vector3(0, -.22f, 0), new Vector3(.18f, .23f, .19f), blue);
        Primitive(side + " glove", PrimitiveType.Sphere, pivot, new Vector3(0, -.45f, .015f), new Vector3(.16f, .18f, .15f), sand); return pivot;
    }
    static Transform Leg(string side, Transform parent, float x)
    {
        var pivot = Pivot(side + " hip", parent, new Vector3(x, .91f, 0));
        Primitive(side + " trousers", PrimitiveType.Capsule, pivot, new Vector3(0, -.29f, 0), new Vector3(.22f, .29f, .23f), blue);
        Primitive(side + " boot", PrimitiveType.Cube, pivot, new Vector3(0, -.73f, .055f), new Vector3(.22f, .3f, .35f), sand);
        Cube(side + " boot cuff", pivot, new Vector3(0, -.57f, 0), new Vector3(.25f, .08f, .26f), clay); return pivot;
    }
}
