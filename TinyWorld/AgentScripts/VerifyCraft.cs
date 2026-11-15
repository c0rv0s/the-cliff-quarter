using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;
using TinyWorld;
using Object = UnityEngine.Object;

public static class VerifyCraft
{
    public static async Task<object> Stage(int mode)
    {
        EditorApplication.isPaused = false;
        var shell = Object.FindAnyObjectByType<WandererShell>(); shell.Begin();
        var p = shell.player; p.TestInput = true; p.TestMove = Vector2.zero;
        p.Teleport(new Vector3(-7.2f, 1.65f, -2.5f)); p.visual.rotation = Quaternion.identity;
        shell.orbit.SetOrbit(mode == 1 ? 172 : 0, mode == 1 ? 5 : 13);
        var body = shell.orbit.rig.GetComponent<CinemachineThirdPersonFollow>(); body.CameraDistance = mode == 1 ? 2.5f : 4.7f;
        await Task.Delay(450);
        p.feedback.dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (mode == 2)
        {
            p.feedback.Land(1); await Task.Delay(70);
            p.feedback.dust.Pause();
            p.feedback.pose.enabled = false;
        }
        else p.feedback.pose.enabled = true;
        return new { mode, particles = p.feedback.dust.particleCount, cameraClear = !Physics.CheckSphere(Camera.main.transform.position, .075f, 1), action = p.Action };
    }
    public static object PaletteCheck()
    {
        var defaults = Object.FindObjectsByType<Renderer>().Where(r => r.sharedMaterials.Any(m => !m || m.name == "Default-Material")).Select(r => r.name).ToArray();
        var whiteParticles = Object.FindObjectsByType<ParticleSystem>().Where(p => p.main.startColor.color == Color.white).Select(p => p.name).ToArray();
        var whiteImages = Object.FindObjectsByType<Image>().Where(i => i.color == Color.white || i.color == Color.gray).Select(i => i.name).ToArray();
        var plainText = Object.FindObjectsByType<TextMeshProUGUI>().Where(t => t.color == Color.white || t.characterSpacing < 1).Select(t => t.name).ToArray();
        return new { defaults, whiteParticles, whiteImages, plainText, templateSky = RenderSettings.skybox != null,
            renderers = Object.FindObjectsByType<Renderer>().Length, triangles = Object.FindObjectsByType<MeshFilter>().Where(f=>f.sharedMesh).Sum(f=>f.sharedMesh.triangles.Length/3) };
    }
}
