using System.Linq;
using System.Threading.Tasks;
using TinyWorld;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class VerifyBaseline
{
    public static async Task<object> Check()
    {
        var frame = Time.frameCount;
        await Task.Delay(500);
        var camera = Camera.main;
        var data = camera.GetUniversalAdditionalCameraData();
        var volume = Object.FindAnyObjectByType<Volume>();
        var effects = volume.sharedProfile.components.Select(c => new { name = c.GetType().Name, active = c.active }).ToArray();
        var renderers = Object.FindObjectsByType<Renderer>();
        var defaults = renderers.Where(r => r.sharedMaterials.Any(m => !m || m.name == "Default-Material")).Select(r => r.name).ToArray();
        var missingMeshes = Object.FindObjectsByType<MeshFilter>().Where(f => !f.sharedMesh).Select(f => f.name).ToArray();
        var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        return new
        {
            playing = Application.isPlaying,
            playerLoopAdvanced = Time.frameCount > frame,
            frameCount = Time.frameCount,
            identityReady = ServicesBootstrap.IsOnline,
            identityTask = ServicesBootstrap.Initialization?.Status.ToString(),
            identityError = ServicesBootstrap.LastError,
            cloudLinked = !string.IsNullOrEmpty(Application.cloudProjectId),
            postProcessing = data.renderPostProcessing,
            volumeLayerIncluded = (data.volumeLayerMask.value & (1 << volume.gameObject.layer)) != 0,
            volumeGlobal = volume.isGlobal,
            effects,
            hdr = pipeline.supportsHDR && camera.allowHDR,
            msaa = pipeline.msaaSampleCount,
            renderScale = pipeline.renderScale,
            shadowDistance = pipeline.shadowDistance,
            defaultMaterials = defaults,
            missingMeshes,
            templateSkybox = RenderSettings.skybox != null,
            renderers = renderers.Length,
            triangles = Object.FindObjectsByType<MeshFilter>().Where(f => f.sharedMesh).Sum(f => f.sharedMesh.triangles.Length / 3),
            macRetinaSupport = UnityEditor.PlayerSettings.macRetinaSupport
        };
    }
}
