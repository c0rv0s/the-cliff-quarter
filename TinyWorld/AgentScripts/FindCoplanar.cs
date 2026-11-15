using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Edit-mode audit: finds same-facing surfaces that overlap (z-fighting) or cross at a shallow angle (wedge seams).
public static class FindCoplanar
{
    const float Cell = 2f;
    struct T { public Vector3 a, b, c, n; public float d; public int r; }

    public static object Run(string outPath, float maxAngle = 6, float maxGap = .02f)
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
            .Where(r => r.enabled && r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh && f.sharedMesh.isReadable).ToArray();
        var tris = new List<T>();
        for (int r = 0; r < renderers.Length; r++)
        {
            var m = renderers[r].GetComponent<MeshFilter>().sharedMesh; var w = renderers[r].localToWorldMatrix;
            var v = m.vertices; for (int i = 0; i < v.Length; i++) v[i] = w.MultiplyPoint3x4(v[i]);
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var t = m.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]]; var n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-10f) continue; n.Normalize();
                    tris.Add(new T { a = a, b = b, c = c, n = n, d = Vector3.Dot(n, a), r = r });
                }
            }
        }
        var grid = new Dictionary<Vector3Int, List<int>>();
        for (int i = 0; i < tris.Count; i++)
        {
            var t = tris[i]; var lo = Key(Vector3.Min(t.a, Vector3.Min(t.b, t.c)) - Vector3.one * maxGap); var hi = Key(Vector3.Max(t.a, Vector3.Max(t.b, t.c)) + Vector3.one * maxGap);
            for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++) for (int z = lo.z; z <= hi.z; z++)
                    { var k = new Vector3Int(x, y, z); if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>(); l.Add(i); }
        }
        float cosMax = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
        Physics.SyncTransforms(); bool hb = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
        // Seen if the sample sits on or above its partner and nothing covers it within 0.6 m.
        bool Seen(Vector3 p, Vector3 n) => !Physics.Raycast(p + n * .004f, n, .6f, ~0, QueryTriggerInteraction.Ignore);
        // Cluster hits on a 3 m grid, keyed by the renderer pair.
        var clusters = new Dictionary<(Vector3Int, int, int), (float area, float exact, Vector3 sum, int n, float gap, float angle, Vector3 nrm)>();
        for (int i = 0; i < tris.Count; i++)
        {
            var t = tris[i]; float area = Vector3.Cross(t.b - t.a, t.c - t.a).magnitude * .5f;
            int k = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(area / .04f)), 1, 24);
            float each = area / (k * k);
            for (int u = 0; u < k; u++) for (int v = 0; v < k - u; v++)
                    for (int up = 0; up < 2; up++)
                    {
                        if (up == 1 && u + v + 1 >= k) continue;
                        float bu = (u + (up == 0 ? 1 / 3f : 2 / 3f)) / k, bv = (v + (up == 0 ? 1 / 3f : 2 / 3f)) / k;
                        var p = t.a + (t.b - t.a) * bu + (t.c - t.a) * bv;
                        if (!grid.TryGetValue(Key(p), out var list)) continue;
                        foreach (int j in list)
                        {
                            if (j == i) continue; var o = tris[j];
                            float dot = Vector3.Dot(t.n, o.n); if (dot < cosMax) continue;
                            float gap = Vector3.Dot(o.n, p) - o.d; if (Mathf.Abs(gap) > maxGap) continue;
                            if (!Inside(p - o.n * gap, o)) continue;
                            if (gap < -.0005f || !Seen(p, t.n)) break;
                            int ra = Mathf.Min(t.r, o.r), rb = Mathf.Max(t.r, o.r);
                            var key = (Vector3Int.FloorToInt(p / 3), ra, rb);
                            clusters.TryGetValue(key, out var e);
                            bool exact = Mathf.Abs(gap) < .002f && dot > .99996f;
                            float ang = Mathf.Acos(Mathf.Clamp01(dot)) * Mathf.Rad2Deg;
                            clusters[key] = (e.area + each, e.exact + (exact ? each : 0), e.sum + p, e.n + 1, Mathf.Max(e.gap, Mathf.Abs(gap)), Mathf.Max(e.angle, ang), e.nrm + t.n);
                            break;
                        }
                    }
        }
        Physics.queriesHitBackfaces = hb;
        string Path(int r) { var tr = renderers[r].transform; var s = tr.name; while (tr.parent) { tr = tr.parent; s = tr.name + "/" + s; } return s; }
        var rows = clusters.Select(c => (c.Key.Item2, c.Key.Item3, c.Value.area, c.Value.exact, at: c.Value.sum / c.Value.n, c.Value.gap, c.Value.angle, nrm: c.Value.nrm.normalized))
            .Where(c => c.area > .01f).OrderByDescending(c => c.area).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"tris {tris.Count}, renderers {renderers.Length}, clusters {rows.Count}, area {rows.Sum(r => r.area):F1} m2, exact {rows.Sum(r => r.exact):F1} m2");
        // Summary by renderer-name pair (strip group prefixes) to show which generators collide.
        foreach (var g in rows.GroupBy(r => Short(renderers[r.Item1]) + "  x  " + Short(renderers[r.Item2])).OrderByDescending(g => g.Sum(r => r.area)))
            sb.AppendLine($"PAIR {g.Key}: {g.Count()} sites, {g.Sum(r => r.area):F1} m2 (exact {g.Sum(r => r.exact):F1})");
        foreach (var r in rows)
            sb.AppendLine($"{r.area:F2} m2 exact {r.exact:F2} gap {r.gap:F3} ang {r.angle:F1} at {r.at.x:F1},{r.at.y:F1},{r.at.z:F1} n {r.nrm.x:F2},{r.nrm.y:F2},{r.nrm.z:F2} | {Path(r.Item1)} | {Path(r.Item2)}");
        File.WriteAllText(outPath, sb.ToString());
        return sb.ToString().Split('\n').Take(60).ToArray();
    }
    static string Short(MeshRenderer r) => (r.transform.parent ? r.transform.parent.name.Split(' ')[0] + ":" : "") + r.name;
    static Vector3Int Key(Vector3 p) => Vector3Int.FloorToInt(p / Cell);
    static bool Inside(Vector3 p, T t)
    {
        Vector3 v0 = t.b - t.a, v1 = t.c - t.a, v2 = p - t.a;
        float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
        float den = d00 * d11 - d01 * d01; if (den < 1e-12f) return false;
        float v = (d11 * d20 - d01 * d21) / den, w = (d00 * d21 - d01 * d20) / den;
        const float e = 1e-4f; return v > e && w > e && v + w < 1 - e;
    }
}
