using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마에스트로 공통 이펙트 (2026-11-24, 사용자 시트 '이펙트 컬러 예시' — 발광 번짐·광역 고리·지휘봉 초승달 잔상·바닥 그림자).
/// 그림 파일 없이 코드로 만든 텍스처(부드러운 원·고리·세로 빛)를 스프라이트로 쓴다. 전부 그림 전용(Time.deltaTime) — 판정과 무관.
///   · Halo: 부드러운 발광 원 하나를 빌려 주고(Acquire), 쓴 쪽이 매 프레임 자리·크기·색을 정한 뒤 돌려준다(Release)
///   · Ring: 퍼지며 사라지는 고리 (충격·광역 예고). flat이면 바닥에 눕힌 타원
///   · Sweep: 지휘봉 휘두르기 잔상 — 손잡이·끝 자리를 쌓아 띠(초승달)를 그리고 시간이 지나면 흐려진다
/// 봇보다 살짝 뒤(z +)에 그려서 봇이 앞을 가리고 번짐은 둘레로 보인다.
/// </summary>
public class MaestroFx : MonoBehaviour
{
    // 시트 팔레트 (흰빛 · 연보라 · 라벤더 · 보라)
    public static readonly Color MoonWhite = new Color(0.94f, 0.93f, 1f);
    public static readonly Color Lavender = new Color(0.76f, 0.74f, 0.98f);
    public static readonly Color Violet = new Color(0.55f, 0.48f, 0.95f);

    [Tooltip("이펙트 그리기 순서")]
    public int sortingOrder = 5;
    [Tooltip("번짐을 봇보다 얼마나 뒤에 그릴지 (m, z)")]
    public float behindZ = 0.08f;

    private static Sprite discSprite, ringSprite, beamSprite, noteSprite;

    /// <summary>8분음표 ♪ 모양 (주변을 떠도는 음표 — 시트 2·3악장)</summary>
    public static Sprite Note => noteSprite != null ? noteSprite : (noteSprite = MakeSprite(48, (u, v) =>
    {
        // 머리(기운 타원) + 기둥 + 꼬리
        float c = Mathf.Cos(-0.35f), s = Mathf.Sin(-0.35f);
        float hx = (u + 0.22f), hy = (v + 0.55f);
        float ex = hx * c + hy * s, ey = -hx * s + hy * c;
        if (ex * ex / 0.07f + ey * ey / 0.035f <= 1f) return 1f;
        if (u >= 0.0f && u <= 0.1f && v >= -0.55f && v <= 0.8f) return 1f;
        float dx = u - 0.1f;
        if (dx >= 0f && dx <= 0.42f)
        {
            float yc = 0.8f - 1.4f * dx + 1.1f * dx * dx;
            if (v <= yc && v >= yc - 0.16f) return 1f;
        }
        return 0f;
    }));
    private static Material material;
    private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

    private class RingFx { public SpriteRenderer r; public Vector3 pos; public float r0, r1, age, life; public Color color; public bool flat; }
    private readonly List<RingFx> rings = new List<RingFx>();

    public static Sprite Disc => discSprite != null ? discSprite : (discSprite = MakeSprite(64, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Mathf.Exp(-d * d * 4.5f) * Mathf.Clamp01(1f - d); }));
    public static Sprite RingTex => ringSprite != null ? ringSprite : (ringSprite = MakeSprite(128, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.07f, 2f)); }));
    public static Sprite Beam => beamSprite != null ? beamSprite : (beamSprite = MakeSprite(64, (u, v) => Mathf.Exp(-u * u * 6f) * Mathf.Clamp01(1.15f - Mathf.Abs(v))));

    public static Material Mat
    {
        get
        {
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "MaestroFx" };
            return material;
        }
    }

    /// <summary>owner 밑의 이펙트 담당 (없으면 만든다)</summary>
    public static MaestroFx Get(Component owner)
    {
        if (owner == null) return null;
        var fx = owner.GetComponentInChildren<MaestroFx>();
        if (fx != null) return fx;
        var go = new GameObject("이펙트") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(owner.transform, false);
        return go.AddComponent<MaestroFx>();
    }

    // ───────────────────────── 발광 원 ─────────────────────────

    /// <summary>발광 원 하나 빌리기 — 쓴 쪽이 Place로 자리·크기·색을 정한다. 다 쓰면 Release</summary>
    public SpriteRenderer Acquire(Sprite sprite = null)
    {
        var r = spare.Count > 0 ? spare.Pop() : null;
        if (r == null)
        {
            var go = new GameObject("발광") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = Mat;
        }
        r.sprite = sprite != null ? sprite : Disc;
        r.sortingOrder = sortingOrder;
        r.enabled = true;
        return r;
    }

    public void Release(SpriteRenderer r)
    {
        if (r == null) return;
        r.enabled = false;
        spare.Push(r);
    }

    /// <summary>발광 원 자리·크기(지름, m)·색. z는 봇보다 살짝 뒤</summary>
    public void Place(SpriteRenderer r, Vector3 pos, Vector2 size, Color color, float angleDeg = 0f)
    {
        if (r == null) return;
        r.transform.position = new Vector3(pos.x, pos.y, pos.z + behindZ);
        r.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        r.transform.localScale = new Vector3(size.x, size.y, 1f);
        r.color = color;
    }

    // ───────────────────────── 고리 ─────────────────────────

    /// <summary>퍼지며 사라지는 고리. r0 → r1 (반지름, m), life초. flat이면 바닥에 눕힌 타원(세로 0.3배)</summary>
    public void Ring(Vector3 pos, float r0, float r1, float life, Color color, bool flat = false)
    {
        var r = Acquire(RingTex);
        rings.Add(new RingFx { r = r, pos = pos, r0 = r0, r1 = r1, life = Mathf.Max(0.05f, life), color = color, flat = flat });
        UpdateRing(rings[rings.Count - 1]);
    }

    private void UpdateRing(RingFx f)
    {
        float u = Mathf.Clamp01(f.age / f.life);
        float e = 1f - (1f - u) * (1f - u);
        float rad = Mathf.Lerp(f.r0, f.r1, e);
        Color c = f.color;
        c.a *= 1f - u;
        Place(f.r, f.pos, new Vector2(rad * 2f, rad * 2f * (f.flat ? 0.3f : 1f)), c);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            var f = rings[i];
            f.age += dt;
            if (f.age >= f.life) { Release(f.r); rings.RemoveAt(i); continue; }
            UpdateRing(f);
        }
        foreach (var s in sweeps) s.Tick(dt);
    }

    // ───────────────────────── 지휘봉 잔상 (초승달) ─────────────────────────

    public class Sweep
    {
        private struct Sample { public Vector3 a, b; public float age; }
        private readonly List<Sample> samples = new List<Sample>(64);
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Color> cols = new List<Color>();
        private readonly List<int> tris = new List<int>();
        public Color color = Lavender;
        public float fadeSeconds = 0.35f;
        public bool emitting;

        public Sweep(Transform parent, int order)
        {
            var go = new GameObject("지휘봉 잔상") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            mesh = new Mesh { name = "지휘봉 잔상" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat;
            renderer.sortingOrder = order;
        }

        /// <summary>지금 손잡이(a)·끝(b) 자리를 쌓는다 (매 프레임)</summary>
        public void Add(Vector3 a, Vector3 b)
        {
            if (!emitting) return;
            samples.Add(new Sample { a = a, b = b, age = 0f });
            if (samples.Count > 60) samples.RemoveAt(0);
        }

        public void Clear() { samples.Clear(); mesh.Clear(); }

        public void Tick(float dt)
        {
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                var s = samples[i];
                s.age += dt;
                if (s.age > fadeSeconds) { samples.RemoveAt(i); continue; }
                samples[i] = s;
            }
            verts.Clear(); cols.Clear(); tris.Clear();
            if (samples.Count >= 2)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    float life = 1f - samples[i].age / fadeSeconds;
                    // 끝 쪽이 밝고 손잡이 쪽은 옅게 — 초승달처럼 바깥 가장자리가 빛난다
                    Color outer = color; outer.a *= life * 0.85f;
                    Color inner = color; inner.a *= life * 0.12f;
                    var tr = renderer.transform; // 보스를 따라 움직이는 오브젝트 — 월드 자리를 로컬로
                    verts.Add(tr.InverseTransformPoint(Vector3.Lerp(samples[i].a, samples[i].b, 0.35f))); cols.Add(inner);
                    verts.Add(tr.InverseTransformPoint(samples[i].b)); cols.Add(outer);
                }
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    int v = i * 2;
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                    tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            renderer.enabled = verts.Count > 0;
        }
    }

    private readonly List<Sweep> sweeps = new List<Sweep>();

    public Sweep CreateSweep()
    {
        var s = new Sweep(transform, sortingOrder);
        sweeps.Add(s);
        return s;
    }

    // ───────────────────────── 텍스처 ─────────────────────────

    // f(u, v): u, v = -1 ~ 1 → 밝기 0~1. 흰색 + 알파
    private static Sprite MakeSprite(int size, System.Func<float, float, float> f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(f(u, v)) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        // 픽셀 단위 = 지름 1m (size px) → Place의 size가 곧 m
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
