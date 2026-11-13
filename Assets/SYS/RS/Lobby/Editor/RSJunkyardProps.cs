// RE:AL STEEL - 고물상 꾸미기: 소품 목록 (조립 도구 부품으로 만든 3D 소품 + 픽셀 텍스처)
//
// RSJunkyardDressing.cs 의 배치 코드가 부르는 소품 만들기 함수 모음. 치수는 전부 픽셀(1px = 1/32 m).
//  · 소품 하나 = 조립품(RSAssembly) 하나. 고르면 씬 뷰 '조립' 도구로 부품 · 칠을 그대로 고칠 수 있다
//  · 재질은 Textures/Build/고물상 의 PNG → Data/Surfaces/고물상 (같은 이름으로 그림을 덮어쓰면 전부 바뀐다)
//  · 앞면에 그림을 픽셀 그대로 붙이는 소품(자판기 · 간판 · 가전)은 그림 크기 = 면 크기
//  · 머리 위로 지나가는 것(지붕 · 전선 · 들보)은 충돌 없음 — 캐릭터 컨트롤러가 걸리지 않게
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RealSteel.Build;
using RealSteel.Common;
using RealSteel.Common.EditorTools;
using RealSteel.Lighting;
using RealSteel.Terrain;

public static partial class RSJunkyard
{
    const float U = 1f / 32f;
    static Dictionary<string, RSSurface> surf;
    static readonly HashSet<string> missing = new HashSet<string>();
    static readonly List<string> warn = new List<string>();
    static readonly List<Vector3> used = new List<Vector3>();   // x, z, 반지름
    static Material glowWarm, glowCool, glowAmber;
    static RSTerrain terrain;
    static Transform root;
    static System.Random rng;
    static readonly Color Warm = new Color(1f, 0.78f, 0.5f);
    static readonly Color Cool = new Color(0.8f, 0.9f, 1f);

    static RSSurface S(string n)
    {
        RSSurface s;
        if (surf != null && surf.TryGetValue(n, out s)) return s;
        missing.Add(n);
        return null;
    }

    static float Snap(float m) { return Mathf.Round(m * 32f) / 32f; }
    static int Px(float m) { return Mathf.RoundToInt(m * 32f); }
    static float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static string Pick(params string[] a) { return a[rng.Next(a.Length)]; }

    // ─────────────────────────────────────────────────────────────
    // 조립품 만들기 도우미
    // ─────────────────────────────────────────────────────────────

    class Asm
    {
        public GameObject go;
        public RSAssembly a;

        public Asm(Transform parent, string name, bool collide)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            a = go.AddComponent<RSAssembly>();
            a.colliders = collide;
            a.snap = RSSnap.픽셀;
            a.defaultSurface = S("녹철판");
        }

        /// <summary>이 조립품 아래의 딸린 조립품 (따로 돌리거나, 충돌을 따로 줄 때)</summary>
        public Asm Sub(string name, bool collide, float x = 0, float y = 0, float z = 0, float rx = 0, float ry = 0, float rz = 0)
        {
            var s = new Asm(go.transform, name, collide);
            s.go.transform.localPosition = new Vector3(Snap(x * U), Snap(y * U), Snap(z * U));
            s.go.transform.localEulerAngles = new Vector3(rx, ry, rz);
            return s;
        }

        RSPart Make(string n, RSPartShape shape, float x, float y, float z, Vector3 sizePx, string s)
        {
            var g = new GameObject(n);
            g.transform.SetParent(go.transform, false);
            g.transform.localPosition = new Vector3(Snap(x * U), Snap(y * U), Snap(z * U));
            var p = g.AddComponent<RSPart>();
            p.shape = shape;
            p.size = new Vector3(Mathf.Max(U, sizePx.x * U), Mathf.Max(U, sizePx.y * U), Mathf.Max(U, sizePx.z * U));
            p.paint = new RSFacePaint { surface = S(s) };
            return p;
        }

        /// <summary>상자. 위치 = 바닥 가운데 (px), 크기 가로 · 높이 · 깊이 (px)</summary>
        public RSPart Box(string n, float x, float y, float z, float sx, float sy, float sz, string s)
        {
            return Make(n, RSPartShape.상자, x, y, z, new Vector3(sx, sy, sz), s);
        }

        /// <summary>기둥. d = 지름 (px), top = 위쪽 굵기 배율</summary>
        public RSPart Cyl(string n, float x, float y, float z, float d, float h, int sides, string s, float top = 1f)
        {
            var p = Make(n, RSPartShape.기둥, x, y, z, new Vector3(d, h, d), s);
            p.sides = sides;
            p.topScale = top;
            return p;
        }

        /// <summary>판 (앞 · 뒤 양면, 두께 없음)</summary>
        public RSPart Plane(string n, float x, float y, float z, float w, float h, string s)
        {
            return Make(n, RSPartShape.판, x, y, z, new Vector3(w, h, 1), s);
        }
    }

    static RSPart Rot(RSPart p, float rx, float ry, float rz)
    {
        p.transform.localEulerAngles = new Vector3(rx, ry, rz);
        return p;
    }

    /// <summary>면 하나를 다른 재질로 (반복)</summary>
    static RSPart Face(RSPart p, int face, string s)
    {
        p.facePaints.Add(new RSFacePaint { face = face, surface = S(s) });
        return p;
    }

    /// <summary>상자 · 판의 면에 그림을 픽셀 그대로 (그림 왼쪽 아래 = 면 왼쪽 아래). 부품이 조립품 안에서 돌아가 있지 않아야 한다</summary>
    static RSPart Decal(RSPart p, int face, string s)
    {
        Vector3 lp = p.transform.localPosition, sz = p.size;
        int ox, oy;
        switch (face)
        {
            case 0: ox = -Px(lp.x - sz.x / 2); oy = -Px(lp.y); break;          // 앞 (u = +x)
            case 1: ox = Px(lp.x + sz.x / 2); oy = -Px(lp.y); break;           // 뒤 (u = -x)
            case 2: ox = Px(lp.z + sz.z / 2); oy = -Px(lp.y); break;           // 왼쪽 (u = -z)
            case 3: ox = -Px(lp.z - sz.z / 2); oy = -Px(lp.y); break;          // 오른쪽 (u = +z)
            default: ox = -Px(lp.x - sz.x / 2); oy = -Px(lp.z - sz.z / 2); break;   // 위 (u = +x, v = +z)
        }
        p.facePaints.Add(new RSFacePaint { face = face, surface = S(s), offset = new Vector2Int(ox, oy) });
        return p;
    }

    /// <summary>윗면에 그림을 픽셀 그대로 (기둥 윗면 = 면 1, 상자 윗면 = 면 4)</summary>
    static RSPart TopDecal(RSPart p, string s)
    {
        Vector3 lp = p.transform.localPosition, sz = p.size;
        p.facePaints.Add(new RSFacePaint
        {
            face = p.shape == RSPartShape.기둥 ? 1 : 4, surface = S(s),
            offset = new Vector2Int(-Px(lp.x - sz.x / 2), -Px(lp.z - sz.z / 2))
        });
        return p;
    }

    // ─────────────────────────────────────────────────────────────
    // 지형 · 놓기
    // ─────────────────────────────────────────────────────────────

    static float H(float x, float z)
    {
        return terrain != null ? terrain.SampleHeightWorld(new Vector3(x, 0f, z)) : 0f;
    }

    /// <summary>지면에 놓는다. r = 차지하는 반지름 (m) — 큰 소품은 발밑 네 곳 중 낮은 쪽에 맞춰 살짝 묻는다</summary>
    static GameObject Put(Asm a, float x, float z, float rotY = 0f, float r = 0.6f, float sink = 0.03f)
    {
        return PutGo(a.go, x, z, rotY, r, sink);
    }

    static GameObject PutGo(GameObject g, float x, float z, float rotY, float r, float sink)
    {
        float h = H(x, z), lo = h, hi = h;
        if (r > 0.45f)
        {
            float k = r * 0.7f;
            float[] hs = { H(x - k, z - k), H(x + k, z - k), H(x - k, z + k), H(x + k, z + k) };
            foreach (float v in hs) { lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
            if (hi - lo > 0.5f) warn.Add(g.name + " (" + x.ToString("0.#") + ", " + z.ToString("0.#") + ") — 발밑 높이 차 " + (hi - lo).ToString("0.0") + " m");
        }
        float y = r > 0.45f ? Mathf.Lerp(lo, h, 0.3f) : h;
        g.transform.position = new Vector3(x, y - sink, z);
        g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        used.Add(new Vector3(x, z, r));
        return g;
    }

    static void Raise(GameObject g, float m) { g.transform.position += Vector3.up * m; }

    /// <summary>소품 로컬 px 좌표 → 월드</summary>
    static Vector3 At(Asm a, float x, float y, float z) { return a.go.transform.TransformPoint(new Vector3(x * U, y * U, z * U)); }

    // ─────────────────────────────────────────────────────────────
    // 불빛
    // ─────────────────────────────────────────────────────────────

    static Material GlowMat(string name, Color hdr)
    {
        string dir = RSPaths.Ensure(RSPaths.Materials + "/고물상");
        string path = dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) { warn.Add("URP Unlit 셰이더를 못 찾음 — 발광 소품이 분홍색일 수 있음"); sh = Shader.Find("Unlit/Color"); }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
        if (m.HasProperty("_Color")) m.SetColor("_Color", hdr);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>발광 조각 (그림자 없음 · 충돌 없음). 위치 = 가운데 (부모 로컬 px), 크기 px</summary>
    static Renderer Glow(Transform parent, string name, Vector3 centerPx, Vector3 sizePx, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        var c = g.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = centerPx * U;
        g.transform.localScale = sizePx * U;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    static Light PLight(Transform parent, string name, Vector3 posPx, Color c, float range)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = posPx * U;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        return l;
    }

    static void Lamp(GameObject host, Light l, float intensity, params Renderer[] glows)
    {
        var n = host.AddComponent<RSNightLamp>();
        n.lights = l != null ? new[] { l } : new Light[0];
        n.intensity = l != null ? new[] { intensity } : new float[0];
        n.glows = glows;
    }

    static void Capsule(GameObject g, float xPx, float zPx, float radius, float height)
    {
        var c = g.AddComponent<CapsuleCollider>();
        c.radius = radius;
        c.height = height;
        c.center = new Vector3(xPx * U, height * 0.5f, zPx * U);
    }

    // ─────────────────────────────────────────────────────────────
    // 소품: 통 · 타이어 · 큐브 · 컨테이너
    // ─────────────────────────────────────────────────────────────

    static readonly float[] DrumX = { 0, 20, 9, -12, 31 };
    static readonly float[] DrumZ = { 0, 3, 18, 15, 20 };

    static Asm Drums(int n, bool tipped = false)
    {
        var a = new Asm(root, "드럼통", true);
        for (int i = 0; i < n && i < DrumX.Length; i++)
        {
            var p = a.Cyl("통", DrumX[i], 0, DrumZ[i], 18, 28, 10, Pick("드럼_청", "드럼_적", "드럼_황", "드럼_녹슨"));
            Face(p, 1, "짙은철");
            p.brightJitter = 0.1f;
        }
        if (tipped)
        {
            var p = a.Cyl("넘어진 통", -10, 9, -16, 18, 28, 10, Pick("드럼_청", "드럼_녹슨"));
            Face(p, 1, "짙은철"); Face(p, 2, "짙은철");
            Rot(p, 0, rng.Next(-25, 25), 90);
        }
        return a;
    }

    static Asm Tires(int n)
    {
        var a = new Asm(root, "타이어 더미", n >= 3);
        for (int i = 0; i < n; i++)
        {
            var p = a.Cyl("타이어", rng.Next(-2, 3), i * 7, rng.Next(-2, 3), 24, 7, 10, "타이어");
            if (i == n - 1) TopDecal(p, "타이어_윗면");
        }
        return a;
    }

    /// <summary>압축한 폐차 큐브를 층층이. rows = 층마다 개수 (아래부터)</summary>
    static Asm Cubes(params int[] rows)
    {
        var a = new Asm(root, "압축 큐브 더미", true);
        float x0 = -rows[0] * 41 / 2f;
        for (int r = 0; r < rows.Length; r++)
        {
            int shift = rng.Next(0, rows[0] - rows[r] + 1);
            for (int i = 0; i < rows[r]; i++)
            {
                var p = a.Box("큐브", x0 + (shift + i) * 41 + 20 + rng.Next(-1, 2), r * 24, rng.Next(-2, 3), 40, 24, 32, Pick("압축큐브_A", "압축큐브_B"));
                p.brightJitter = 0.16f;
                Rot(p, 0, rng.Next(-3, 4), 0);
            }
        }
        return a;
    }

    static void ContainerBody(Asm a, string color)
    {
        a.Box("몸통", 0, 0, 0, 192, 80, 78, color);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                a.Box("모서리 기둥", sx * 95, 0, sz * 38, 4, 80, 4, "짙은철");
        a.Box("아랫대", 0, 0, -40, 192, 4, 2, "짙은철");
        a.Box("윗대", 0, 76, -40, 192, 4, 2, "짙은철");
        a.Cyl("잠금봉", 97, 4, -10, 2, 72, 6, "아연");
        a.Cyl("잠금봉", 97, 4, 10, 2, 72, 6, "아연");
    }

    /// <summary>컨테이너 (6 × 2.5 × 2.4 m). color2 를 주면 위에 하나 더 얹는다</summary>
    static Asm Containers(string color, string color2 = null, float shift2 = 14, float rot2 = 0)
    {
        var a = new Asm(root, "컨테이너", true);
        ContainerBody(a, color);
        if (color2 != null) ContainerBody(a.Sub("위 컨테이너", true, shift2, 80, 0, 0, rot2, 0), color2);
        return a;
    }

    // ─────────────────────────────────────────────────────────────
    // 울타리 · 전봇대 · 가로등 · 전선
    // ─────────────────────────────────────────────────────────────

    /// <summary>a → b 로 가는 울타리. 칸마다 지면 높이를 따라 계단처럼 놓는다. 앞면은 진행 방향의 오른쪽
    /// (서 → 동으로 그으면 남쪽 = 카메라 쪽). chain = 철망, 아니면 골함석</summary>
    static Asm Fence(string name, Vector2 a, Vector2 b, bool chain, float hPx)
    {
        var f = new Asm(root, name, true);
        float len = Vector2.Distance(a, b);
        float bay = chain ? 2.5f : 2f;
        int n = Mathf.Max(1, Mathf.RoundToInt(len / bay));
        Vector2 d = (b - a) / n;
        float yaw = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float w = d.magnitude * 32f;
        for (int i = 0; i <= n; i++)
        {
            Vector2 p = a + d * i;
            float hp = H(p.x, p.y);
            var post = f.Cyl("기둥", 0, 0, 0, 3, hPx + 6, 6, chain ? "아연" : "짙은철");
            post.transform.position = new Vector3(p.x, hp - 0.08f, p.y) + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 3 * U);
            if (i == n) break;
            Vector2 q = p + d, m = (p + q) * 0.5f;
            float y = Mathf.Min(hp, H(q.x, q.y), H(m.x, m.y)) - 0.1f;
            RSPart panel;
            if (chain)
            {
                panel = f.Plane("철망", 0, 0, 0, w, hPx, "철망");
                var rail = f.Box("윗대", 0, 0, 0, w, 2, 2, "아연");
                rail.transform.position = new Vector3(m.x, y + hPx * U, m.y);
                rail.transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
            else
            {
                string s = rng.NextDouble() < 0.7 ? "골함석_녹" : Pick("골함석_청", "골함석_황");
                panel = f.Box("함석판", 0, 0, 0, w, hPx + rng.Next(-3, 4), 2, s);
                panel.brightJitter = 0.1f;
            }
            panel.transform.position = new Vector3(m.x, y, m.y);
            panel.transform.rotation = Quaternion.Euler(!chain && rng.NextDouble() < 0.12 ? Rand(-5f, 5f) : 0f, yaw, 0);
        }
        return f;
    }

    static Asm Pole(bool transformer)
    {
        var a = new Asm(root, "전봇대", false);
        a.Cyl("기둥", 0, 0, 0, 9, 240, 8, "전주", 0.66f);
        a.Box("가로대", 0, 222, 0, 60, 4, 4, "짙은철");
        a.Box("가로대 받침", 0, 208, 0, 4, 14, 5, "짙은철");
        for (int x = -26; x <= 26; x += 26) a.Cyl("애자", x, 226, 0, 3, 5, 6, "아연");
        if (transformer)
        {
            a.Box("변압기 받침", 7, 156, 0, 18, 3, 10, "짙은철");
            a.Cyl("변압기", 10, 159, 0, 14, 22, 8, "아연");
        }
        Capsule(a.go, 0, 0, 0.16f, 3f);
        return a;
    }

    /// <summary>가로등 (팔이 +X 로 뻗는다). 밤에 켜진다</summary>
    static Asm StreetLamp(float intensity = 2.2f)
    {
        var a = new Asm(root, "가로등", false);
        a.Cyl("기둥", 0, 0, 0, 4, 130, 6, "짙은철", 0.8f);
        a.Box("받침", 0, 0, 0, 8, 6, 8, "짙은철");
        a.Box("팔", 15, 128, 0, 30, 2, 2, "짙은철");
        a.Box("등 갓", 28, 125, 0, 14, 3, 8, "아연");
        var g = Glow(a.go.transform, "등 불빛", new Vector3(28, 124.5f, 0), new Vector3(12, 1, 6), glowWarm);
        var l = PLight(a.go.transform, "등 빛", new Vector3(28, 110, 0), Warm, 8f);
        Lamp(a.go, l, intensity, g);
        Capsule(a.go, 0, 0, 0.12f, 3f);
        return a;
    }

    /// <summary>늘어진 전선 (월드 좌표 a → b). host 는 원점에 있는 충돌 없는 조립품</summary>
    static void Wire(Asm host, Vector3 a, Vector3 b, float sag, int seg = 6)
    {
        Vector3 prev = a;
        for (int i = 1; i <= seg; i++)
        {
            float t = i / (float)seg;
            Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            Vector3 d = p - prev;
            var part = host.Box("전선", 0, 0, 0, 1, 1, d.magnitude * 32f + 1f, "전선");
            part.transform.position = (prev + p) * 0.5f;
            part.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            prev = p;
        }
    }

    static Asm WireHost(string name)
    {
        var w = new Asm(root, name, false);
        w.a.castShadows = false;
        return w;
    }

    /// <summary>줄전구 (월드 좌표). 전구는 발광 조각, light = 가운데에 점광 하나</summary>
    static void StringLights(string name, Vector3 a, Vector3 b, int bulbs, float sag, bool light)
    {
        var w = WireHost(name);
        Wire(w, a, b, sag, 6);
        var gl = new List<Renderer>();
        for (int i = 1; i <= bulbs; i++)
        {
            float t = i / (bulbs + 1f);
            Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t) + 0.06f);
            gl.Add(Glow(w.go.transform, "전구", p * 32f, new Vector3(2, 3, 2), glowWarm));
        }
        Vector3 mid = (a + b) * 0.5f + Vector3.down * (sag + 0.35f);
        Light l = light ? PLight(w.go.transform, "줄전구 빛", mid * 32f, Warm, 6.5f) : null;
        Lamp(w.go, l, 1.6f, gl.ToArray());
    }

    // ─────────────────────────────────────────────────────────────
    // 소품: 정문 · 간판 · 광고판 · 자판기
    // ─────────────────────────────────────────────────────────────

    static Asm Gate(float gapPx)
    {
        var a = new Asm(root, "정문", true);
        for (int s = -1; s <= 1; s += 2)
        {
            a.Box("문기둥", s * gapPx / 2, 0, 0, 8, 104, 8, "녹철판");
            a.Box("문기둥 띠", s * gapPx / 2, 0, 0, 10, 16, 10, "위험띠");
            a.Box("문기둥 머리", s * gapPx / 2, 104, 0, 10, 3, 10, "짙은철");
        }
        float cx = gapPx / 2 + 72;
        a.Box("미닫이문", cx, 4, 6, 130, 60, 2, "골함석_청");
        a.Box("문틀 위", cx, 64, 6, 134, 3, 3, "짙은철");
        a.Box("문틀 아래", cx, 1, 6, 134, 3, 3, "짙은철");
        a.Box("문틀 옆", cx - 66, 1, 6, 3, 66, 3, "짙은철");
        a.Box("문틀 옆", cx + 66, 1, 6, 3, 66, 3, "짙은철");
        a.Box("문 등 갓", gapPx / 2, 96, -7, 8, 2, 6, "짙은철");
        var g = Glow(a.go.transform, "문 등", new Vector3(gapPx / 2, 93, -7), new Vector3(5, 4, 4), glowWarm);
        Lamp(a.go, null, 0f, g);
        return a;
    }

    /// <summary>판 하나짜리 간판 (앞면에 그림 그대로). 자리(슬롯) 안의 임시 모형으로 만든다</summary>
    static GameObject SignSlot(string name, string tex, int w, int h, string note)
    {
        var slot = new GameObject("SLOT_" + name);
        slot.transform.SetParent(root, false);
        var ps = slot.AddComponent<RSPropSlot>();
        ps.note = note;
        ps.size = new Vector3(w * U, h * U, 0.1f);
        var a = new Asm(slot.transform, RSPropSlot.TempPrefix + name, false);
        var p = a.Box("판", 0, 0, 0, w, h, 2, "녹철판");
        Decal(p, 0, tex);
        return slot;
    }

    static Asm Billboard()
    {
        var a = new Asm(root, "리그 광고판", false);
        for (int s = -1; s <= 1; s += 2)
        {
            a.Box("다리", s * 46, 0, 0, 7, 98, 7, "녹철판");
            a.Box("다리 받침", s * 46, 0, 0, 12, 5, 12, "콘크리트");
            var br = a.Box("버팀대", s * 46, 0, 14, 3, 100, 3, "녹철판");
            Rot(br, -16, 0, 0);
        }
        a.Box("발판", 0, 92, -9, 138, 2, 14, "녹철판");
        a.Box("난간", 0, 104, -16, 138, 1, 1, "짙은철");
        a.Box("난간 기둥", -68, 94, -16, 1, 10, 1, "짙은철");
        a.Box("난간 기둥", 68, 94, -16, 1, 10, 1, "짙은철");
        a.Box("틀 위", 0, 162, 0, 136, 3, 7, "짙은철");
        a.Box("틀 아래", 0, 94, 0, 136, 3, 7, "짙은철");
        a.Box("뒷판", 0, 97, 2, 132, 65, 3, "녹철판");

        var slot = new GameObject("SLOT_광고판 그림 (4 × 2 m)");
        slot.transform.SetParent(a.go.transform, false);
        slot.transform.localPosition = new Vector3(0, 97 * U, -1 * U);
        var ps = slot.AddComponent<RSPropSlot>();
        ps.note = "로봇 리그 광고 그림 자리. 진짜 그림(판 · 스프라이트)을 자식으로 넣으면 임시 그림이 꺼진다";
        ps.size = new Vector3(4f, 2f, 0.1f);
        var pic = new Asm(slot.transform, RSPropSlot.TempPrefix + "광고판 그림", false);
        Decal(pic.Box("그림", 0, 0, 0, 128, 64, 2, "짙은철"), 0, "광고판_리그");

        var gl = new List<Renderer>();
        for (int x = -40; x <= 40; x += 40)
        {
            a.Box("등 팔", x, 165, -12, 2, 2, 24, "짙은철");
            a.Box("등 갓", x, 162, -24, 10, 3, 5, "짙은철");
            gl.Add(Glow(a.go.transform, "등 불빛", new Vector3(x, 161.5f, -24), new Vector3(8, 1, 3), glowWarm));
        }
        var l = PLight(a.go.transform, "광고판 빛", new Vector3(0, 140, -44), Warm, 7f);
        Lamp(a.go, l, 2.2f, gl.ToArray());
        var bc = a.go.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.5f, 0); bc.size = new Vector3(3.3f, 3f, 0.4f);
        return a;
    }

    static Asm Vending()
    {
        var a = new Asm(root, "자판기", true);
        Decal(a.Box("몸", 0, 0, 0, 32, 64, 24, "도장_청"), 0, "자판기_앞");
        var g = Glow(a.go.transform, "진열창 불빛", new Vector3(0, 62, -12.7f), new Vector3(28, 2, 1), glowCool);
        var l = PLight(a.go.transform, "자판기 빛", new Vector3(0, 40, -34), Cool, 4.5f);
        Lamp(a.go, l, 1.5f, g);
        var n = a.go.GetComponent<RSNightLamp>();
        n.dayGlow = 0.5f;
        return a;
    }

    // ─────────────────────────────────────────────────────────────
    // 소품: 가전 · 공구 · 차 · 기계
    // ─────────────────────────────────────────────────────────────

    static Asm Appliance(Asm host, string name, string body, string front, int w, int h, int d, float x, float y, float z, float rx, float ry, float rz)
    {
        var s = host.Sub(name, true, x, y, z, rx, ry, rz);
        Decal(s.Box("몸", 0, 0, 0, w, h, d, body), 0, front);
        return s;
    }

    static Asm Appliances()
    {
        var a = new Asm(root, "가전 더미", false);
        Appliance(a, "냉장고", "가전_흰", "냉장고_앞", 24, 56, 22, -22, 0, 2, 0, 8, 0);
        Appliance(a, "넘어진 냉장고", "가전_흰", "냉장고_앞", 24, 56, 22, 38, 12, 8, 0, -12, 90);
        Appliance(a, "세탁기", "가전_흰", "세탁기_앞", 24, 28, 22, 6, 0, -22, 0, -6, 0);
        Appliance(a, "모니터", "가전_회", "모니터_앞", 16, 14, 14, 8, 28, -22, 0, 14, 0);
        Appliance(a, "모니터", "가전_회", "모니터_앞", 16, 14, 14, -46, 0, -18, 0, 26, 0);
        Appliance(a, "모니터", "가전_회", "모니터_앞", 16, 14, 14, 10, 24, 8, 0, -20, 0);
        Appliance(a, "실외기", "가전_흰", "실외기_앞", 28, 20, 10, 44, 0, -20, 0, -18, 0);
        return a;
    }

    static Asm Workbench(bool chest)
    {
        var a = new Asm(root, "작업대", true);
        a.Box("상판", 0, 26, 0, 60, 3, 24, "나무판");
        a.Box("선반", 0, 8, 0, 54, 2, 20, "나무판");
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                a.Box("다리", sx * 27, 0, sz * 9, 3, 26, 3, "짙은철");
        a.Box("바이스", -20, 29, -5, 7, 6, 9, "짙은철");
        a.Box("바이스 손잡이", -20, 31, -11, 1, 1, 8, "아연");
        a.Box("공구통", 14, 29, 2, 16, 8, 8, "도장_적");
        a.Box("공구통 손잡이", 14, 37, 2, 10, 2, 1, "짙은철");
        a.Box("로봇 손 (수리 중)", -2, 29, -3, 8, 5, 8, "로봇장갑");
        a.Box("손가락", -2, 29, -9, 6, 2, 4, "짙은철");
        a.Cyl("기름통", 24, 29, -6, 5, 7, 8, "드럼_황");
        a.Box("상자", -14, 10, 0, 16, 12, 14, "골판지");
        a.Cyl("페인트통", 10, 10, -2, 8, 9, 8, "아연");
        if (chest) Appliance(a, "공구함", "도장_적", "공구함_앞", 24, 30, 16, 48, 0, 2, 0, -8, 0);
        return a;
    }

    /// <summary>폐차 (앞이 +X). 보닛이 열려 있고 앞바퀴 하나는 블록으로 받쳐 놓았다</summary>
    static Asm Car(string color)
    {
        var a = new Asm(root, "폐차", true);
        a.Box("차체", 0, 8, 0, 124, 18, 54, color);
        var cab = a.Box("지붕칸", -10, 26, 0, 62, 15, 48, color);
        for (int f = 0; f < 4; f++) Face(cab, f, "차창");
        a.Box("범퍼 앞", 63, 9, 0, 3, 6, 56, "짙은철");
        a.Box("범퍼 뒤", -63, 9, 0, 3, 6, 56, "짙은철");
        a.Box("전조등", 62, 17, -18, 2, 5, 9, "아연");
        a.Box("전조등", 62, 17, 18, 2, 5, 9, "아연");
        a.Box("엔진", 42, 26, 0, 22, 5, 30, "짙은철");
        Rot(a.Box("열린 보닛", 40, 34, 0, 36, 1, 50, color), 0, 0, 40);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            Rot(a.Cyl("바퀴", sx * 38, 10, 22, 20, 6, 10, "타이어"), 90, 0, 0);
            if (sx < 0) Rot(a.Cyl("바퀴", sx * 38, 10, -28, 20, 6, 10, "타이어"), 90, 0, 0);
        }
        a.Box("받침 블록", 38, 0, -22, 12, 8, 10, "시멘트블록");
        return a;
    }

    static Asm Compactor()
    {
        var a = new Asm(root, "압축기", true);
        a.Box("몸통", 0, 0, 0, 96, 72, 70, "노랑도장");
        a.Box("아래 띠", 0, 0, 0, 98, 10, 72, "위험띠");
        a.Box("투입구", -4, 24, -35, 50, 34, 2, "짙은철");
        a.Box("투입구 턱", -4, 22, -37, 54, 3, 5, "녹철판");
        a.Cyl("유압 실린더", -22, 72, 0, 12, 30, 8, "아연");
        a.Cyl("유압 실린더", 22, 72, 0, 12, 30, 8, "아연");
        a.Box("머리 들보", 0, 102, 0, 76, 8, 22, "노랑도장");
        a.Box("조작함", 36, 28, -37, 14, 20, 5, "짙은철");
        Glow(a.go.transform, "조작함 등", new Vector3(36, 43, -40), new Vector3(3, 3, 1), glowAmber);
        a.Box("사다리", 46, 0, -37, 1, 72, 1, "짙은철");
        a.Box("사다리", 54, 0, -37, 1, 72, 1, "짙은철");
        for (int y = 8; y < 72; y += 10) a.Box("사다리 단", 50, y, -37, 8, 1, 1, "짙은철");
        Decal(a.Plane("경고 표지", -39, 46, -36, 16, 16, "표지_경고"), 0, "표지_경고");
        a.Box("배출대", -78, 0, 0, 60, 10, 44, "녹철판");
        a.Box("나온 큐브", -80, 10, 0, 40, 24, 32, "압축큐브_A");
        return a;
    }

    /// <summary>기둥 넷에 비스듬한 지붕. 지붕은 충돌 없음. 밑에 작업등 하나</summary>
    static Asm Shed(float w, float d, float h, string roof, float lightIntensity)
    {
        var a = new Asm(root, "작업 천막", false);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            a.Box("기둥", sx * w / 2, 0, -d / 2, 4, h, 4, "녹철판");
            a.Box("기둥", sx * w / 2, 0, d / 2, 4, h + 10, 4, "녹철판");
            Capsule(a.go, sx * w / 2, -d / 2, 0.09f, 2.6f);
            Capsule(a.go, sx * w / 2, d / 2, 0.09f, 2.6f);
        }
        var r = a.Sub("지붕", false, 0, h + 5, 0, -6, 0, 0);
        r.Box("지붕판", 0, 0, 0, w + 18, 2, d + 22, roof);
        r.Box("도리", 0, -3, -d / 2, w + 8, 3, 3, "짙은철");
        r.Box("도리", 0, -3, d / 2, w + 8, 3, 3, "짙은철");
        if (lightIntensity > 0f)
        {
            a.Box("등 줄", 0, h - 14, 0, 1, 18, 1, "전선");
            a.Cyl("등 갓", 0, h - 18, 0, 12, 4, 8, "짙은철", 0.3f);
            var g = Glow(a.go.transform, "작업등", new Vector3(0, h - 20, 0), new Vector3(5, 4, 5), glowWarm);
            var l = PLight(a.go.transform, "작업등 빛", new Vector3(0, h - 28, 0), Warm, 7f);
            Lamp(a.go, l, lightIntensity, g);
        }
        return a;
    }

    static Asm Crane(float span)
    {
        var a = new Asm(root, "마그넷 크레인", false);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * span / 2;
            a.Box("다리", x, 0, -16, 6, 170, 6, "노랑도장");
            a.Box("다리", x, 0, 16, 6, 170, 6, "노랑도장");
            a.Box("발", x, 0, 0, 10, 5, 46, "짙은철");
            a.Box("다리 띠", x, 5, -16, 7, 14, 7, "위험띠");
            for (int k = 0; k < 5; k++) Rot(a.Plane("다리 트러스", x, 8 + k * 32, 0, 32, 32, "트러스_노랑"), 0, 90, 0);
            var bc = a.go.AddComponent<BoxCollider>();
            bc.center = new Vector3(x * U, 1.5f, 0); bc.size = new Vector3(0.4f, 3f, 1.4f);
        }
        a.Box("들보", 0, 170, 0, span + 24, 12, 10, "노랑도장");
        a.Box("들보 레일", 0, 167, 0, span + 12, 3, 4, "짙은철");
        a.Box("트롤리", 18, 158, 0, 24, 10, 16, "짙은철");
        a.Box("줄", 18, 96, 0, 1, 62, 1, "전선");
        a.Cyl("자석 테", 18, 82, 0, 38, 3, 12, "노랑도장");
        a.Cyl("자석", 18, 85, 0, 34, 11, 12, "짙은철");
        Rot(a.Box("붙은 고철", 10, 73, -4, 16, 9, 12, "고철더미"), 8, 20, 12);
        Rot(a.Box("붙은 고철", 28, 75, 6, 10, 7, 9, "녹철판"), -10, -15, -8);
        Rot(a.Box("붙은 고철", 20, 70, 0, 8, 6, 14, "압축큐브_B"), 0, 40, 0);
        var cab = a.Box("조종실", -span / 2 + 16, 136, -10, 22, 26, 18, "노랑도장");
        Face(cab, 0, "차창");
        Glow(a.go.transform, "경고등", new Vector3(span / 2, 184, 0), new Vector3(3, 3, 3), glowAmber);
        return a;
    }

    // ─────────────────────────────────────────────────────────────
    // 소품: 로봇
    // ─────────────────────────────────────────────────────────────

    /// <summary>정비대에 걸린 로봇 상체 — 자리(슬롯) 안의 임시 모형. 진짜 로봇 모델을 넣으면 꺼진다</summary>
    static GameObject RobotStand()
    {
        var slot = new GameObject("SLOT_정비 중인 로봇 (1.6 × 2.6 × 1.2 m)");
        slot.transform.SetParent(root, false);
        var ps = slot.AddComponent<RSPropSlot>();
        ps.note = "주인공이 고치는 로봇. 진짜 로봇 모델 · 스프라이트를 자식으로 넣으면 임시 모형이 꺼진다 (앞 = -Z)";
        ps.size = new Vector3(1.6f, 2.6f, 1.2f);
        var a = new Asm(slot.transform, RSPropSlot.TempPrefix + "정비 중인 로봇", true);
        a.Box("받침", 0, 0, 0, 46, 3, 30, "짙은철");
        a.Box("세움대", -24, 0, 8, 3, 76, 3, "짙은철");
        a.Box("세움대", 24, 0, 8, 3, 76, 3, "짙은철");
        a.Box("가로대", 0, 74, 8, 52, 3, 3, "짙은철");
        a.Box("걸쇠 줄", 0, 62, 6, 1, 13, 1, "전선");
        a.Box("골반", 0, 26, 0, 20, 8, 14, "짙은철");
        a.Box("허벅지 (잘림)", -7, 12, 0, 9, 14, 10, "로봇장갑");
        a.Box("허벅지 (잘림)", 7, 16, 0, 9, 10, 10, "로봇장갑");
        a.Box("몸통", 0, 34, 0, 30, 26, 18, "로봇장갑");
        a.Box("가슴판", 0, 44, -10, 22, 13, 2, "로봇_주황");
        Glow(a.go.transform, "코어", new Vector3(0, 50, -11.6f), new Vector3(4, 4, 1), glowAmber);
        a.Box("어깨", -21, 50, 0, 12, 12, 14, "로봇_주황");
        a.Box("어깨 (팔 없음)", 20, 51, 0, 9, 9, 11, "짙은철");
        a.Box("늘어진 전선", 22, 40, -2, 1, 11, 1, "전선");
        a.Box("늘어진 전선", 25, 43, 2, 1, 8, 1, "전선");
        a.Box("위팔", -21, 34, 0, 8, 16, 8, "로봇장갑");
        a.Cyl("팔꿈치", -21, 29, 0, 9, 5, 8, "짙은철");
        a.Box("아래팔", -21, 13, 0, 9, 16, 9, "로봇장갑");
        a.Box("주먹", -21, 4, 0, 11, 9, 11, "짙은철");
        a.Box("목", 0, 60, 0, 8, 4, 8, "짙은철");
        a.Box("머리", 0, 64, 0, 16, 13, 16, "로봇장갑");
        a.Box("눈가리개", 0, 69, -8.5f, 14, 5, 1, "차창");
        a.Box("안테나", 6, 77, 2, 1, 8, 1, "짙은철");
        return slot;
    }

    static Asm RobotScrap(int kind)
    {
        var a = new Asm(root, "버려진 로봇 부품", false);
        if (kind % 3 == 0)
        {
            Rot(a.Box("아래팔", 0, 4, 0, 9, 18, 9, "로봇장갑"), 0, 20, 78);
            Rot(a.Box("주먹", -20, 2, 4, 11, 9, 11, "짙은철"), 0, 35, 10);
            Rot(a.Box("어깨 갑옷", 14, 0, -8, 14, 3, 16, "로봇_주황"), 14, -20, 0);
        }
        else if (kind % 3 == 1)
        {
            Rot(a.Box("머리", 0, 2, 0, 16, 13, 16, "로봇장갑"), 0, 30, 68);
            Rot(a.Box("가슴판", 16, 0, 6, 22, 2, 14, "로봇_주황"), -10, 15, 6);
            a.Cyl("관절", -14, 0, -8, 9, 6, 8, "짙은철");
        }
        else
        {
            Rot(a.Box("정강이", 0, 5, 0, 10, 24, 11, "로봇장갑"), 0, -15, 82);
            a.Box("발", -30, 0, 2, 12, 7, 16, "짙은철");
            Rot(a.Box("갑옷 조각", 10, 0, 12, 12, 2, 10, "로봇_주황"), 20, 40, 0);
        }
        return a;
    }

    // ─────────────────────────────────────────────────────────────
    // 소품: 더미 · 잡동사니
    // ─────────────────────────────────────────────────────────────

    static Asm Dumpster()
    {
        var a = new Asm(root, "고철 수거함", true);
        a.Box("통", 0, 0, 0, 96, 38, 50, "컨테이너_청");
        a.Box("테", 0, 38, 0, 100, 3, 54, "짙은철");
        a.Box("고철", 0, 36, 0, 92, 7, 46, "고철더미");
        Rot(a.Box("삐져나온 철판", -24, 38, 4, 30, 2, 22, "골함석_녹"), 24, 18, 0);
        Rot(a.Cyl("삐져나온 배관", 20, 40, -6, 5, 34, 8, "배관"), 0, 0, -52);
        Rot(a.Box("삐져나온 철골", 6, 40, 10, 4, 30, 4, "녹철판"), 30, 0, 24);
        a.Box("걸이", -49, 22, 0, 3, 5, 12, "짙은철");
        a.Box("걸이", 49, 22, 0, 3, 5, 12, "짙은철");
        return a;
    }

    static Asm TarpPile(string tarp)
    {
        var a = new Asm(root, "방수포 덮은 더미", true);
        Rot(a.Cyl("큰 덩이", 0, 0, 0, 80, 40, 6, tarp, 0.55f), 0, 18, 0);
        Rot(a.Cyl("작은 덩이", 36, 0, -10, 52, 28, 6, tarp, 0.6f), 0, -10, 0);
        a.Box("드러난 상자", -34, 0, -22, 32, 32, 32, "나무상자");
        Rot(a.Box("밧줄", 0, 40, 0, 1, 1, 50, "전선"), 0, 60, 0);
        var t = a.Cyl("누름 타이어", -2, 40, 2, 24, 7, 10, "타이어");
        TopDecal(t, "타이어_윗면");
        return a;
    }

    static Asm Pallets(int n, int crates)
    {
        var a = new Asm(root, "팔레트 더미", n >= 2);
        for (int i = 0; i < n; i++)
            Rot(a.Box("팔레트", rng.Next(-1, 2), i * 5, rng.Next(-1, 2), 38, 4, 32, "팔레트"), 0, rng.Next(-4, 5), 0);
        var c = a.Sub("상자", true, 0, n * 5 - 1, 0, 0, 0, 0);
        if (crates >= 1) c.Box("나무상자", -16, 0, 0, 32, 32, 32, "나무상자");   // 가장자리가 32px 격자에 맞아야 무늬가 맞는다
        if (crates >= 2) c.Box("골판지 상자", 8, 0, -8, 16, 16, 16, "골판지");
        if (crates >= 3) c.Box("골판지 상자", 8, 16, -8, 16, 16, 16, "골판지");
        return a;
    }

    static Asm Cartons()
    {
        var a = new Asm(root, "상자 더미", false);
        a.Box("상자", -8, 0, 8, 16, 16, 16, "골판지");
        a.Box("상자", 8, 0, 8, 16, 16, 16, "골판지");
        a.Box("상자", 8, 16, 8, 16, 16, 16, "골판지");
        a.Box("나무상자", -16, 0, -32, 32, 32, 32, "나무상자");
        return a;
    }

    static Asm Pipes()
    {
        var a = new Asm(root, "배관 더미", true);
        for (int i = 0; i < 3; i++) Rot(a.Cyl("배관", 48, 4, (i - 1) * 8, 8, 96, 8, "배관"), 0, 0, 90);
        for (int i = 0; i < 2; i++) Rot(a.Cyl("배관", 44 + i * 6, 11, i * 8 - 4, 8, 96, 8, "배관"), 0, 0, 90);
        a.Box("말뚝", -30, 0, -13, 2, 14, 2, "짙은철");
        a.Box("말뚝", 30, 0, -13, 2, 14, 2, "짙은철");
        a.Box("말뚝", -30, 0, 13, 2, 14, 2, "짙은철");
        a.Box("말뚝", 30, 0, 13, 2, 14, 2, "짙은철");
        return a;
    }

    static Asm Spool()
    {
        var a = new Asm(root, "케이블 릴", true);
        Rot(a.Cyl("옆판", 0, 19, -11, 38, 3, 12, "나무판"), 90, 0, 0);
        Rot(a.Cyl("옆판", 0, 19, 8, 38, 3, 12, "나무판"), 90, 0, 0);
        Rot(a.Cyl("감긴 케이블", 0, 19, -8, 24, 16, 10, "케이블"), 90, 0, 0);
        return a;
    }

    static Asm Cones(int n)
    {
        var a = new Asm(root, "러버콘", false);
        for (int i = 0; i < n; i++)
        {
            a.Box("받침", i * 22, 0, (i % 2) * 6, 12, 1, 12, "러버콘");
            a.Cyl("콘", i * 22, 1, (i % 2) * 6, 10, 17, 8, "러버콘", 0.2f);
        }
        return a;
    }

    static Asm Barricade()
    {
        var a = new Asm(root, "바리케이드", true);
        a.Box("가로대", 0, 20, 0, 48, 8, 1, "위험띠");
        a.Box("가로대", 0, 8, 0, 48, 5, 1, "위험띠");
        a.Box("다리", -20, 0, 0, 2, 30, 7, "짙은철");
        a.Box("다리", 20, 0, 0, 2, 30, 7, "짙은철");
        return a;
    }

    static Asm GasCylinders()
    {
        var a = new Asm(root, "가스통", true);
        for (int i = 0; i < 3; i++)
        {
            a.Cyl("가스통", i * 9 - 9, 0, 0, 8, 40, 8, "가스통");
            a.Cyl("마개", i * 9 - 9, 40, 0, 4, 4, 6, "짙은철");
        }
        a.Box("사슬", 0, 26, -4.5f, 30, 1, 1, "짙은철");
        return a;
    }

    static Asm WaterTank()
    {
        var a = new Asm(root, "물탱크", true);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                a.Box("다리", sx * 18, 0, sz * 18, 3, 36, 3, "짙은철");
        a.Box("받침", 0, 36, 0, 44, 2, 44, "녹철판");
        a.Cyl("탱크", 0, 38, 0, 40, 44, 12, "물탱크");
        a.Cyl("뚜껑", 0, 82, 0, 12, 3, 8, "짙은철");
        a.Cyl("배관", 21, 0, -14, 3, 44, 6, "아연");
        return a;
    }

    static Asm Solar(int n)
    {
        var a = new Asm(root, "태양광 패널", false);
        for (int i = 0; i < n; i++)
        {
            float x = i * 58 - (n - 1) * 29;
            a.Cyl("기둥", x, 0, 8, 3, 34, 6, "아연");
            var p = a.Box("패널", x, 24, 0, 54, 2, 34, "아연");
            Face(p, 4, "태양광패널");
            Rot(p, -32, 0, 0);
        }
        a.Box("인버터", (n - 1) * 29 + 36, 0, 8, 10, 16, 6, "가전_회");
        return a;
    }

    static Asm SignPost(string tex)
    {
        var a = new Asm(root, "표지판", false);
        a.Cyl("기둥", 0, 0, 0, 2, 58, 6, "아연");
        Decal(a.Plane("표지", 0, 42, -1.5f, 16, 16, tex), 0, tex);
        return a;
    }

    /// <summary>화살표 안내판 (화살표는 +X 쪽을 가리킨다)</summary>
    static Asm ArrowBoard()
    {
        var a = new Asm(root, "안내판", false);
        a.Box("다리", -12, 0, 0, 2, 26, 2, "짙은철");
        a.Box("다리", 12, 0, 0, 2, 26, 2, "짙은철");
        Decal(a.Box("판", 0, 24, -1, 32, 16, 1, "짙은철"), 0, "표지_화살");
        return a;
    }

    static Asm Bench()
    {
        var a = new Asm(root, "긴 의자", true);
        a.Box("앉는 판", 0, 13, 0, 46, 2, 13, "나무판");
        a.Box("등받이", 0, 20, 6, 46, 9, 2, "나무판");
        for (int sx = -1; sx <= 1; sx += 2)
        {
            a.Box("다리", sx * 20, 0, -4, 2, 13, 2, "짙은철");
            a.Box("다리", sx * 20, 0, 6, 2, 29, 2, "짙은철");
        }
        return a;
    }

    static Asm Mailbox()
    {
        var a = new Asm(root, "우편함", false);
        a.Box("기둥", 0, 0, 0, 2, 30, 2, "나무판");
        a.Box("함", 0, 30, 0, 9, 8, 13, "도장_적");
        a.Box("깃발", 5, 34, -3, 1, 6, 1, "아연");
        return a;
    }

    static Asm Laundry()
    {
        var a = new Asm(root, "빨랫줄", false);
        a.Cyl("장대", -52, 0, 0, 2, 64, 6, "나무판");
        a.Cyl("장대", 52, 0, 0, 2, 64, 6, "나무판");
        a.Box("줄", 0, 60, 0, 104, 1, 1, "전선");
        a.Plane("빨래", -30, 40, 0, 16, 20, "천_흰");
        a.Plane("빨래", -8, 44, 0, 14, 16, "천_청");
        a.Plane("빨래", 14, 38, 0, 18, 22, "천_주황");
        a.Plane("빨래", 36, 46, 0, 10, 14, "천_흰");
        return a;
    }

    /// <summary>마당 평상 (팔레트 바닥 + 케이블 릴 탁자 + 걸상)</summary>
    static Asm Deck()
    {
        var a = new Asm(root, "마당 평상", true);
        a.Box("바닥", 0, 0, 0, 76, 5, 64, "팔레트");
        Rot(a.Cyl("릴 탁자", 6, 5, 4, 30, 20, 12, "나무판"), 0, 15, 0);
        a.Cyl("걸상", -20, 5, -8, 11, 12, 8, "드럼_녹슨");
        a.Cyl("걸상", 28, 5, -12, 11, 12, 8, "나무판");
        a.Cyl("주전자", 4, 25, 2, 5, 5, 8, "아연");
        a.Box("공구 상자", -4, 25, 8, 9, 4, 5, "도장_적");
        return a;
    }

    static Asm CinderBlocks()
    {
        var a = new Asm(root, "시멘트 블록", false);
        a.Box("블록", 0, 0, 0, 40, 16, 20, "시멘트블록");
        a.Box("블록", -6, 16, 0, 24, 8, 20, "시멘트블록");
        return a;
    }

    static Asm Beams()
    {
        var a = new Asm(root, "철골 더미", true);
        for (int i = 0; i < 3; i++) a.Box("철골", 0, 0, (i - 1) * 7, 96, 5, 5, "녹철판");
        Rot(a.Box("철골", 4, 5, -3, 96, 5, 5, "녹철판"), 0, 9, 0);
        Rot(a.Box("철골", -6, 5, 5, 80, 5, 5, "녹철판"), 0, -6, 0);
        return a;
    }

    static Asm LeanSheet()
    {
        var a = new Asm(root, "기대 놓은 함석", false);
        Rot(a.Box("함석", 0, 0, 0, 40, 54, 1, Pick("골함석_녹", "골함석_황")), -16, 0, 0);
        Rot(a.Box("함석", 14, 0, 4, 36, 44, 1, "골함석_녹"), -22, 6, 0);
        a.Box("받침대", 6, 0, 14, 4, 40, 4, "녹철판");
        return a;
    }

    static Asm WoodPole(float hPx)
    {
        var a = new Asm(root, "장대", false);
        a.Cyl("장대", 0, 0, 0, 4, hPx, 6, "나무판", 0.8f);
        Capsule(a.go, 0, 0, 0.08f, 2.4f);
        return a;
    }
}
