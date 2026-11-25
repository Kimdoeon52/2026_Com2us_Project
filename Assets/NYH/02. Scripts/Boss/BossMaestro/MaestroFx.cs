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

    // ───────────────────────── 번쩍임 (잠깐 보였다 사라지는 그림 — 바닥 경고 등) ─────────────────────────

    private class FlashFx { public SpriteRenderer r; public Vector3 pos; public Vector2 size; public float angle, age, life; public Color color; }
    private readonly List<FlashFx> flashes = new List<FlashFx>();

    /// <summary>sprite를 life초 동안 보였다가 흐려지게 (처음 20%에 밝아지고 나머지에 사라짐). angle = 도</summary>
    public void Flash(Sprite sprite, Vector3 pos, Vector2 size, float angle, Color color, float life)
    {
        var f = new FlashFx { r = Acquire(sprite), pos = pos, size = size, angle = angle, color = color, life = Mathf.Max(0.05f, life) };
        flashes.Add(f);
        UpdateFlash(f);
    }

    private void UpdateFlash(FlashFx f)
    {
        float u = Mathf.Clamp01(f.age / f.life);
        float a = u < 0.2f ? u / 0.2f : 1f - (u - 0.2f) / 0.8f;
        Color c = f.color;
        c.a *= a;
        Place(f.r, f.pos, f.size, c, f.angle);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var f = flashes[i];
            f.age += dt;
            if (f.age >= f.life) { Release(f.r); flashes.RemoveAt(i); continue; }
            UpdateFlash(f);
        }
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            var f = rings[i];
            f.age += dt;
            if (f.age >= f.life) { Release(f.r); rings.RemoveAt(i); continue; }
            UpdateRing(f);
        }
        foreach (var s in sweeps) s.Tick(dt);
        for (int i = crescents.Count - 1; i >= 0; i--)
        {
            crescents[i].Tick(dt);
            if (crescents[i].Done) { crescents[i].Destroy(); crescents.RemoveAt(i); }
        }
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

    // ───────────────────────── 초승달 베기 (사용자 시트 '지휘봉 패턴 모션 — 3연속 횡베기') ─────────────────────────

    /// <summary>
    /// 크고 두꺼운 초승달(C자) 베기 궤적. 타원(반지름 rx, ry) 위를 각도 a0 → a1 (도, 반시계 +)로 그려 나가며(reveal 0~1),
    /// 두께는 가운데가 가장 굵고 양 끝이 가늘다. 바깥 가장자리가 밝고 안쪽으로 옅어진다. Finish 하면 fade초에 흐려진다.
    /// 쓴 쪽이 매 프레임 reveal을 정한다 (판정과 같은 진행률).
    /// </summary>
    public class Crescent
    {
        public Vector3 center;
        public float rx, ry, a0, a1, thickness;   // thickness = 타원 반지름에 대한 비율 (가장 굵은 곳)
        public float reveal;                       // 0~1
        public Color core = MoonWhite, edge = Lavender;
        [Tooltip("안쪽(쓸고 지나간 부채꼴) 채움 진하기 — 알파만 씀")]
        public Color fill = new Color(0f, 0f, 0f, 0f);
        public float fadeSeconds = 0.35f;
        private float fade = 1f;
        private bool finishing;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Color> cols = new List<Color>();
        private readonly List<int> tris = new List<int>();
        public bool Done => finishing && fade <= 0f;

        public Crescent(Transform parent, int order)
        {
            var go = new GameObject("초승달 베기") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "초승달 베기" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat;
            renderer.sortingOrder = order;
        }

        public void Finish() => finishing = true;
        public void Destroy() { if (renderer != null) Object.Destroy(renderer.gameObject); }

        /// <summary>각도 a(도)의 타원 위 점, r = 반지름 배율</summary>
        public Vector3 Point(float aDeg, float r)
        {
            float a = aDeg * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(a) * rx * r, Mathf.Sin(a) * ry * r, 0f);
        }

        /// <summary>
        /// 진행률 s(0~1)의 두께 비율 (0~1) — 시트 베기 초승달처럼 시작은 가늘고, 3분의 2쯤(앞쪽)에서 가장 굵고, 끝은 날카롭게 좁아진다.
        /// 판정(MaestroSlashPattern)도 같은 모양을 쓴다
        /// </summary>
        public static float Profile(float s) => Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(s), 1.5f))), 0.6f); // 끝(s=1)의 sin π가 아주 작은 음수 → 0.6제곱이 NaN 되던 것 막음

        /// <summary>진행률 s(0~1)에서의 두께 비율</summary>
        public float ThicknessAt(float s) => thickness * Profile(s);

        /// <summary>그려지는 머리에서 튀는 불티 (시트 초승달 둘레 파편) — 쓴 쪽이 넣는다. 없으면 안 튐</summary>
        public MaestroFx sparks;
        private int sparked;

        public void Tick(float dt)
        {
            if (finishing) fade = Mathf.MoveTowards(fade, 0f, dt / Mathf.Max(0.05f, fadeSeconds));
            verts.Clear(); cols.Clear(); tris.Clear();
            const int N = 56;
            int shown = Mathf.Clamp(Mathf.CeilToInt(N * Mathf.Clamp01(reveal)), 0, N);
            var tr = renderer.transform;
            // 띠 4겹: 바깥 번짐(투명) → 바깥 가장자리 흰빛(가장 밝음) → 라벤더 → 안쪽 보라(옅게) — 시트 초승달 색 층
            for (int i = 0; i <= shown; i++)
            {
                float s = i / (float)N;
                float a = Mathf.Lerp(a0, a1, s);
                float th = ThicknessAt(s);
                // 막 그려진 앞쪽(머리)이 가장 밝고, 지나간 뒤쪽은 조금 옅게
                float head = shown > 0 ? 0.55f + 0.45f * Mathf.Clamp01(1f - (shown - i) / 20f) : 1f;
                Color co = core; co.a *= fade * head;
                Color ce = edge; ce.a = 0f;
                Color cm = edge; cm.a *= fade * head * 0.75f;
                Color ci = Violet; ci.a = edge.a * fade * head * 0.12f;
                Color cf = Violet; cf.a = fill.a * fade;
                verts.Add(tr.InverseTransformPoint(Point(a, 1f + th * 0.3f))); cols.Add(ce);
                verts.Add(tr.InverseTransformPoint(Point(a, 1f))); cols.Add(co);
                verts.Add(tr.InverseTransformPoint(Point(a, 1f - th * 0.35f))); cols.Add(cm);
                verts.Add(tr.InverseTransformPoint(Point(a, 1f - th))); cols.Add(ci);
                verts.Add(tr.InverseTransformPoint(Point(a, 0.04f))); cols.Add(cf);   // 안쪽 채움 (쓸고 지나간 자리 = 판정)
            }
            for (int i = 0; i < shown; i++)
            {
                int v = i * 5;
                for (int k = 0; k < 4; k++)
                {
                    tris.Add(v + k); tris.Add(v + k + 1); tris.Add(v + k + 5);
                    tris.Add(v + k + 1); tris.Add(v + k + 6); tris.Add(v + k + 5);
                }
            }
            // 새로 그려진 칸마다 바깥 가장자리 근처에 불티 (진하게 보일 때만)
            if (sparks != null && core.a > 0.5f && !finishing)
                for (; sparked < shown; sparked++)
                {
                    if (((sparked * 7919) & 3) == 0) continue; // 4칸 중 3칸
                    float s = sparked / (float)N;
                    float a = Mathf.Lerp(a0, a1, s);
                    float jitter = ((sparked * 2654435761u) % 1000u) / 1000f;
                    var p = Point(a + (jitter - 0.5f) * 6f, 1f + ThicknessAt(s) * (0.1f + 0.5f * jitter));
                    float size = Mathf.Min(rx, ry) * (0.05f + 0.06f * jitter);
                    sparks.Flash(Disc, p, Vector2.one * size, 0f, MoonWhite, 0.18f + 0.2f * jitter);
                }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            renderer.enabled = verts.Count > 0 && fade > 0f;
        }
    }

    private readonly List<Crescent> crescents = new List<Crescent>();

    public Crescent CreateCrescent()
    {
        var c = new Crescent(transform, sortingOrder + 1) { sparks = this };
        crescents.Add(c);
        return c;
    }

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
