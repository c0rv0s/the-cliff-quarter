using UnityEngine;

namespace TinyWorld
{
    public sealed class QuarterQuestMark : MonoBehaviour
    {
        public string who;
        public Transform glyph;
        Vector3 rest, baseScale;
        float phase;
        Camera view;
        Transform halo;
        static Sprite goldHaze;

        static Sprite MakeHalo()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "Quest marker falloff", wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                var offset = new Vector2(x + .5f, y + .5f) / size * 2 - Vector2.one;
                float falloff = Mathf.Max(0, 1 - offset.sqrMagnitude);
                pixels[y * size + x] = new Color(1f, .69f, .22f, .63f * falloff * falloff * falloff);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
        }

        void Awake()
        {
            rest = glyph.localPosition;
            baseScale = glyph.localScale;
            phase = Mathf.Repeat(transform.position.z * .17f, Mathf.PI * 2);
            view = Camera.main;

            var surface = glyph.GetComponent<Renderer>();
            var brass = new MaterialPropertyBlock();
            surface.GetPropertyBlock(brass);
            brass.SetColor("_EmissionColor", new Color(1.4f, .48f, .065f));
            surface.SetPropertyBlock(brass);
            if (!goldHaze) goldHaze = MakeHalo();
            var haze = new GameObject("Brass glow").AddComponent<SpriteRenderer>();
            haze.sprite = goldHaze;
            halo = haze.transform;
            halo.SetParent(glyph, false);
            halo.localPosition = Vector3.up * .27f;
            halo.localScale = new Vector3(.69f, 1.02f, .69f);
            surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            var q = QuarterQuests.Current;
            bool show = q && (q.HasOffer(who) || q.Needs(who) || who == "Moss" && q.Stage(1) == 1 && !q.race.Running);
            if (glyph.gameObject.activeSelf != show) glyph.gameObject.SetActive(show);
            if (!show) return;

            if (!view) view = Camera.main;
            if (view) halo.rotation = view.transform.rotation;
            float distance = view ? Vector3.Distance(view.transform.position, transform.position) : 0;
            float size = Mathf.Lerp(1.5f, 3f, Mathf.InverseLerp(9, 38, distance));
            glyph.localScale = Vector3.Scale(baseScale, new Vector3(size * 1.32f, size, size * 1.32f));
            float lift = .23f + .19f * Mathf.Sin(Time.time * 3.65f + phase);
            glyph.localPosition = rest + Vector3.up * lift;
        }
    }
}
