using System;
using System.Collections.Generic;
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
        var ambience = GameObject.Find("City ambience").GetComponentsInChildren<AudioSource>();
        var fixedEmitter = ambience.First(s => s.spatialBlend > 0 && s.name != "stream").transform;
        var start = fixedEmitter.position;
        shell.player.Teleport(shell.player.transform.position + new Vector3(0, 0, 6)); await Task.Delay(200);
        Debug.Log($"[VerifyAudio] emitters fixed in world: {fixedEmitter.position == start}");
        return new
        {
            played,
            nullClips = Object.FindObjectsByType<AudioSource>().Where(s => !s.clip && s.isPlaying).Select(s => s.name).ToArray(),
            listenerY = Camera.main.transform.position.y,
            beds = ambience.Where(s => s.spatialBlend == 0).Select(s => new { s.name, s.isPlaying, volume = System.Math.Round(s.volume, 3) }).ToArray(),
            emitters = ambience.Where(s => s.spatialBlend > 0).Select(s => new { s.name, s.isPlaying, s.volume, pos = s.transform.position.ToString("F0") }).ToArray(),
            sunstoneTested,
            glideWind = shell.player.GetComponentInChildren<Glider>() ? "present" : "none"
        };
    }

    public static async Task<object> Regions()
    {
        var f = Object.FindAnyObjectByType<WandererFeedback>();
        var player = f.player;
        var world = GameObject.Find("City ambience");
        var sources = world.GetComponentsInChildren<AudioSource>();
        AudioSource Source(string name) => sources.Single(s => s.name == name);
        var results = new List<object>();
        var spawn = player.transform.position;
        float timeScale = Time.timeScale, volume = f.volume;
        var grotto = Object.FindAnyObjectByType<GrottoPool>();
        async Task Region(string name, Vector3 position, string expected, bool quietWater = false)
        {
            player.Teleport(position);
            await Task.Delay(2900);
            var source = Source(expected);
            bool quietMarket = expected == "market_murmur" || Source("market_murmur").volume < .003f;
            bool quietRiver = !quietWater || Source("river_lapping").volume < .003f;
            bool passed = source.isPlaying && source.volume > .045f && quietMarket && quietRiver;
            results.Add(new { name, passed, clip = expected, source.volume });
        }

        try
        {
            Time.timeScale = 0;
            f.volume = .65f;
            await Region("River quay", new(12, 5, 118), "river_lapping");
            await Region("Canal market", new(193, 5, 528), "market_murmur");
            await Region("Rooftops over the river", new(44, 84, 118), "high_wind", true);
            await Region("Farm lane", new(283, 115, 126), "field_grass", true);
            await Region("Outer coast", new(0, 0, 630), "shore_swell", true);
            await Region("Wildlands ruins", GameObject.Find("Court of the broken sun").transform.position, "wild_canopy", true);
            await Region("Grotto chamber", grotto.passage[8], "grotto_air");

            int muteFrame = Time.frameCount;
            f.volume = 0;
            while (Time.frameCount < muteFrame + 2) await Task.Delay(30);
            var audible = sources.Where(s => s.isPlaying && s.volume > .0001f).Select(s => new { s.name, s.volume }).ToArray();
            results.Add(new { name = "Sound toggle", passed = audible.Length == 0, audible });
        }
        finally
        {
            f.volume = volume;
            Time.timeScale = timeScale;
            player.Teleport(spawn);
        }

        Debug.Log("[VerifyAudio] Checked seven ambience locations and mute.");
        return new
        {
            results,
            missingLoops = sources.Where(s => s.loop && !s.clip).Select(s => s.name).ToArray(),
            birdLoops = sources.Where(s => s.loop && s.name.Contains("gull", StringComparison.OrdinalIgnoreCase)).Select(s => s.name).ToArray()
        };
    }
}
