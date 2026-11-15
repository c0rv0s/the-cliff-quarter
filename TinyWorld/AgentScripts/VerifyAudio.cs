using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using TinyWorld;
using Object = UnityEngine.Object;

// Audio import settings and a Play-mode check of every feedback event and the ambience.
public static class VerifyAudio
{
    // Long loops stay Vorbis-compressed in memory; one-shots decompress on load for instant playback.
    public static object Import()
    {
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            bool loop = path.EndsWith(".ogg");
            var s = importer.defaultSampleSettings;
            var want = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            if (s.loadType == want && s.compressionFormat == AudioCompressionFormat.Vorbis && importer.loadInBackground == loop) continue;
            s.loadType = want; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = loop ? .6f : .8f;
            importer.defaultSampleSettings = s; importer.loadInBackground = loop; importer.SaveAndReimport(); changed++;
        }
        var clips = Resources.LoadAll<AudioClip>("Audio");
        return new { clips = clips.Length, empty = clips.Where(c => c.length <= 0).Select(c => c.name).ToArray(), changed };
    }

    public static async Task<object> Play()
    {
        EditorApplication.isPaused = false;
        var shell = Object.FindAnyObjectByType<WandererShell>(); shell.Begin();
        var f = shell.player.feedback;
        await Task.Delay(600);
        var played = new System.Collections.Generic.Dictionary<string, bool>();
        foreach (var name in new[] { "Jump", "Grab", "Mantle", "WallRun", "Dash", "Whoosh", "Ring", "Vine", "Respawn", "Roll", "GlideOpen", "GlideFold", "StickyShot", "StickyAttach", "StickyRelease", "Click", "Hover" })
        {
            typeof(WandererFeedback).GetMethod(name).Invoke(f, null);
            await Task.Delay(15);
            played[name] = f.GetComponents<AudioSource>().Any(s => s.isPlaying && s.clip && s.time < .06f);
        }
        f.Land(.3f); f.Land(.9f); f.Splash(); f.Splash(.4f);
        var collected = typeof(WandererFeedback).GetMethod("Collected", BindingFlags.NonPublic | BindingFlags.Instance);
        bool sunstoneTested = Sunstone.CollectedCount > 0;
        if (sunstoneTested) collected.Invoke(f, new object[] { Sunstone.All[0] });
        await Task.Delay(1500);
        var amb = f.GetComponent<CityAmbience>();
        var start = GameObject.Find("City ambience").transform.GetChild(0).position;
        shell.player.Teleport(shell.player.transform.position + new Vector3(0, 0, 6)); await Task.Delay(200);
        Debug.Log($"[VerifyAudio] emitters fixed in world: {GameObject.Find("City ambience").transform.GetChild(0).position == start}");
        return new
        {
            played,
            nullClips = Object.FindObjectsByType<AudioSource>().Where(s => !s.clip && s.isPlaying).Select(s => s.name).ToArray(),
            listenerY = Camera.main.transform.position.y,
            beds = amb.GetComponents<AudioSource>().Select(s => new { clip = s.clip ? s.clip.name : null, s.isPlaying, volume = System.Math.Round(s.volume, 3) }).ToArray(),
            emitters = GameObject.Find("City ambience").GetComponentsInChildren<AudioSource>().Select(s => new { s.name, s.isPlaying, s.volume, pos = s.transform.position.ToString("F0") }).ToArray(),
            sunstoneTested,
            glideWind = shell.player.GetComponentInChildren<Glider>() ? "present" : "none"
        };
    }
}
