using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 마에스트로 키 포즈 굽기 (2026-11-25 사용자 결정 B-2, 에디터 전용).
/// `03. Image/Boss/Maestro/Poses/*.png` = 모션 시트(사용자 11-25 제공)의 키 포즈를 몸 픽셀만 남겨 뽑은 그림.
///   그림 규칙: 240×170, 가슴(하트) 가운데 x = 120, 맨 아래 줄 = 퍼펫 그림 맨 아래 줄. 투명 = 빈 칸.
///   직접 그린 깨끗한 픽셀 그림으로 바꿔 넣어도 된다 (같은 규칙이면 다시 굽기만 하면 됨).
/// 굽기:
///   1) 색 — 시트 색은 버리고(사용자: 색은 말고 모션만), 포즈 픽셀을 밝기 순으로 퍼펫 그림 색(밝기 순)에 1:1로 대응해 다시 칠한다
///   2) 가상 봇 수 V = max(퍼펫 픽셀, 가장 큰 포즈 픽셀) — 모자란 만큼 추가 봇(남는 봇을 잠깐 빌림)
///   3) 짝 — 퍼펫 가상 봇 ↔ 포즈 픽셀을 반씩 나누기(넓은 축 기준, 양쪽 같은 수)로 재귀 대응 → 왼쪽 위는 왼쪽 위로,
///      가까운 봇끼리 가까운 자리로 가서 흘러갈 때 엉키지 않는다. 마지막 몇 개는 밝기 순으로 짝
/// 메뉴 NYH/보스3/키 포즈 굽기. 외형 빌드(MaestroLookBuilder.BuildLook)도 이걸 부른다.
/// </summary>
public static class MaestroPoseBaker
{
    public const string PoseFolder = "Assets/NYH/03. Image/Boss/Maestro/Poses";
    private const string SetPath = MaestroSpriteBaker.OutFolder + "/Maestro_PoseSet.asset";
    private const string DataPath = MaestroSpriteBaker.OutFolder + "/Maestro_PuppetData.asset";
    private const float CanvasChestX = 120f;
    private const float ArtChestX = 53f;      // 퍼펫 그림 가슴 가운데 (상체 뼈 52~54)
    private const int MaxPosePixels = 6000;   // 이보다 많으면 고르게 솎는다 (빌릴 봇 수 상한)

    // 시트 순서·방향 (-1 = 오른쪽→왼쪽). 준비/전환 포즈는 다음 베기 방향
    private static readonly string[] OrderNames = { "베기_준비", "베기_1타", "베기_전환1", "베기_2타", "베기_전환2", "베기_3타" };
    private static readonly int[] OrderDirs = { -1, -1, 1, 1, -1, -1 };

    [MenuItem("NYH/보스3/키 포즈 굽기 (모션 시트 포즈 그림)", false, 212)]
    private static void BakeMenu()
    {
        var data = AssetDatabase.LoadAssetAtPath<MaestroPuppetData>(DataPath);
        if (data == null) { Debug.LogError("[Maestro] 퍼펫 데이터가 없음 — 빌더(마에스트로 만들기·갱신)를 먼저 실행"); return; }
        var set = Bake(data);
        if (set == null) return;
        foreach (var look in Object.FindObjectsByType<MaestroLook>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(look, "키 포즈 연결");
            look.poseSet = set;
            EditorUtility.SetDirty(look);
        }
        Debug.Log("[Maestro] 씬의 MaestroLook에 키 포즈 연결 — 씬 저장할 것");
    }

    private struct Src { public string name; public int dir; public List<Vector2> pos; public List<Color32> col; }

    public static MaestroPoseSet Bake(MaestroPuppetData data)
    {
        if (data == null || data.PixelCount == 0) return null;
        if (!Directory.Exists(PoseFolder)) { Debug.LogWarning($"[Maestro] 키 포즈 그림 폴더가 없음: {PoseFolder} — 베기는 퍼펫 몸짓으로"); return null; }

        // 그림 읽기 (시트 순서 먼저, 나머지는 이름순 · 방향 없음)
        var files = new List<string>(Directory.GetFiles(PoseFolder, "*.png"));
        files.Sort((a, b) =>
        {
            int ia = System.Array.IndexOf(OrderNames, Path.GetFileNameWithoutExtension(a)), ib = System.Array.IndexOf(OrderNames, Path.GetFileNameWithoutExtension(b));
            if (ia < 0) ia = 999; if (ib < 0) ib = 999;
            return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a, b);
        });
        var rng = new System.Random(20261125);
        var srcs = new List<Src>();
        foreach (var f in files)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(f));
            int W = tex.width, H = tex.height;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            string n = Path.GetFileNameWithoutExtension(f);
            int oi = System.Array.IndexOf(OrderNames, n);
            var s = new Src { name = n, dir = oi >= 0 ? OrderDirs[oi] : 0, pos = new List<Vector2>(), col = new List<Color32>() };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color32 c = px[(H - 1 - y) * W + x]; // 텍스처는 아래가 0 → 위가 0
                    if (c.a < 128) continue;
                    s.pos.Add(new Vector2(x - CanvasChestX + ArtChestX, y - H + data.artHeightPx)); // 맨 아래 줄 = 퍼펫 맨 아래 줄
                    s.col.Add(c);
                }
            if (AddBaton && s.dir != 0) AddBatonPixels(s, data.artHeightPx);
            // 너무 많으면 고르게 솎기
            while (s.pos.Count > MaxPosePixels)
            {
                int k = rng.Next(s.pos.Count), last = s.pos.Count - 1;
                s.pos[k] = s.pos[last]; s.col[k] = s.col[last];
                s.pos.RemoveAt(last); s.col.RemoveAt(last);
            }
            if (s.pos.Count > 0) srcs.Add(s);
        }
        if (srcs.Count == 0) { Debug.LogWarning("[Maestro] 키 포즈 그림이 비어 있음"); return null; }

        int N = data.PixelCount, V = N;
        foreach (var s in srcs) V = Mathf.Max(V, s.pos.Count);

        var set = AssetDatabase.LoadAssetAtPath<MaestroPoseSet>(SetPath);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<MaestroPoseSet>();
        set.baseCount = N;
        set.virtualCount = V;
        set.mirrorXPx = ArtChestX;

        // 추가 봇이 나오는 퍼펫 픽셀 — 고르게 섞어서
        var extra = new int[V - N];
        var perm = new int[N];
        for (int i = 0; i < N; i++) perm[i] = i;
        for (int e = 0; e < extra.Length; e++)
        {
            int r = e % N;
            if (r == 0) Shuffle(perm, rng);
            extra[e] = perm[r];
        }
        set.extraSource = extra;

        // 퍼펫 가상 봇 자리 · 밝기
        var basePos = new Vector2[V];
        var baseLum = new float[V];
        for (int i = 0; i < V; i++)
        {
            int k = i < N ? i : extra[i - N];
            basePos[i] = data.pixPos[k];
            baseLum[i] = Lum(data.pixColor[k]);
        }
        // 퍼펫 팔레트 (밝기 순)
        var palette = (Color32[])data.pixColor.Clone();
        System.Array.Sort(palette, (a, b) => Lum(a).CompareTo(Lum(b)));

        var poses = new List<MaestroPoseSet.Pose>();
        var sb = new System.Text.StringBuilder($"[Maestro] 키 포즈 굽기 — 그림 봇 {N}, 가상 봇 {V} (추가 {V - N})");
        foreach (var s in srcs)
        {
            int M = s.pos.Count;
            // 1) 다시 칠하기: 밝기 순위 → 퍼펫 팔레트 같은 순위
            var order = new int[M];
            for (int j = 0; j < M; j++) order[j] = j;
            var lum = new float[M];
            for (int j = 0; j < M; j++) lum[j] = Lum(s.col[j]);
            System.Array.Sort(order, (a, b) => lum[a].CompareTo(lum[b]));
            var newCol = new Color32[M];
            for (int r = 0; r < M; r++) newCol[order[r]] = palette[Mathf.Min(N - 1, (int)((r + 0.5f) / M * N))];

            // 2) V개로 늘리기 (겹치는 봇은 고르게)
            var shuffled = new int[M];
            for (int j = 0; j < M; j++) shuffled[j] = j;
            Shuffle(shuffled, rng);
            var tPos = new Vector2[V];
            var tLum = new float[V];
            var tSrc = new int[V];
            for (int v = 0; v < V; v++) { int j = shuffled[v % M]; tSrc[v] = j; tPos[v] = s.pos[j]; tLum[v] = Lum(newCol[j]); }

            // 3) 짝 (반씩 나누기)
            var a = new int[V]; var b = new int[V];
            for (int v = 0; v < V; v++) { a[v] = v; b[v] = v; }
            var match = new int[V];
            Match(a, b, 0, V, basePos, baseLum, tPos, tLum, match);

            var pose = new MaestroPoseSet.Pose { name = s.name, dir = s.dir, sourcePixels = M, pos = new Vector2[V], col = new Color32[V] };
            for (int v = 0; v < V; v++) { int t = match[v]; pose.pos[v] = tPos[t]; pose.col[v] = newCol[tSrc[t]]; }
            poses.Add(pose);
            sb.Append($", {s.name} {M}px({(s.dir < 0 ? "R→L" : s.dir > 0 ? "L→R" : "-")})");
        }
        set.poses = poses.ToArray();

        if (created) AssetDatabase.CreateAsset(set, SetPath);
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log(sb.ToString());
        return set;
    }

    // ── 지휘봉 덧그리기 (11-25 사용자: 마지막 휘두를 때 지휘봉을 안 들고 있음) ──
    // 시트 타격 컷은 지휘봉이 초승달 이펙트와 겹쳐 있어 이펙트를 지울 때 같이 지워졌다 → 베는 쪽으로 가장 뻗은 손끝에서
    // 가슴 → 손 방향(살짝 위로)으로 지휘봉을 그린다. 손잡이 쪽 2칸 굵기, 끝은 1칸 + 끝 반짝임(십자). 흰색 = 밝은 팔레트로 칠해짐
    private const bool AddBaton = true;
    private const float BatonLengthPx = 28f;

    private static void AddBatonPixels(Src s, float artH)
    {
        float dir = Mathf.Sign(s.dir);
        var chest = new Vector2(ArtChestX, artH * 0.48f);
        // 손 = 가슴에서 이어진 덩어리(2칸 틈까지 이음) 안에서, 어깨 높이 띠(가슴 −35 ~ +15) 중 베는 쪽으로 가장 먼 픽셀
        // — 흩어진 작은 조각·늘어진 자락 끝을 손으로 잡지 않게
        var map = new Dictionary<long, int>();
        for (int j = 0; j < s.pos.Count; j++) map[Key(s.pos[j])] = j;
        int start = -1;
        float bd = float.MaxValue;
        for (int j = 0; j < s.pos.Count; j++)
        {
            float d2 = (s.pos[j] - chest).sqrMagnitude;
            if (d2 < bd) { bd = d2; start = j; }
        }
        if (start < 0) return;
        var seen = new HashSet<int> { start };
        var stack = new Stack<int>();
        stack.Push(start);
        Vector2 hand = s.pos[start];
        float best = float.NegativeInfinity;
        while (stack.Count > 0)
        {
            var q = s.pos[stack.Pop()];
            if (q.y >= chest.y - 35f && q.y <= chest.y + 15f && q.x * dir > best) { best = q.x * dir; hand = q; }
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (map.TryGetValue(Key(q + new Vector2(dx, dy)), out int n) && seen.Add(n)) stack.Push(n);
        }
        if (float.IsNegativeInfinity(best)) return;
        Vector2 d = hand - chest;
        if (d.sqrMagnitude < 1f) d = new Vector2(dir, 0f);
        d = (d.normalized + new Vector2(0f, -0.25f)).normalized; // 살짝 위로 (위쪽 0 좌표라 −y = 위)
        var perp = new Vector2(-d.y, d.x);

        var have = new HashSet<long>();
        foreach (var q in s.pos) have.Add(Key(q));
        var white = new Color32(255, 255, 252, 255);
        void Put(Vector2 p)
        {
            var q = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            if (have.Add(Key(q))) { s.pos.Add(q); s.col.Add(white); }
        }
        for (float t = 0f; t <= BatonLengthPx; t += 0.5f)
        {
            var p = hand + d * t;
            Put(p);
            if (t < BatonLengthPx * 0.4f) Put(p + perp * 0.8f); // 손잡이 쪽 굵게
        }
        var tip = hand + d * BatonLengthPx;
        Put(tip + Vector2.right); Put(tip + Vector2.left); Put(tip + Vector2.up); Put(tip + Vector2.down);
    }

    private static long Key(Vector2 q) => ((long)Mathf.RoundToInt(q.x) << 32) ^ (uint)Mathf.RoundToInt(q.y);

    // 양쪽 같은 수를 넓은 축으로 반씩 나눠 재귀 대응. 작아지면 밝기 순으로 짝
    private static void Match(int[] a, int[] b, int lo, int hi, Vector2[] ap, float[] al, Vector2[] bp, float[] bl, int[] result)
    {
        int n = hi - lo;
        if (n <= 0) return;
        if (n <= 6)
        {
            System.Array.Sort(a, lo, n, Comparer<int>.Create((x, y) => al[x].CompareTo(al[y])));
            System.Array.Sort(b, lo, n, Comparer<int>.Create((x, y) => bl[x].CompareTo(bl[y])));
            for (int k = lo; k < hi; k++) result[a[k]] = b[k];
            return;
        }
        Vector2 amin = Vector2.positiveInfinity, amax = Vector2.negativeInfinity, bmin = Vector2.positiveInfinity, bmax = Vector2.negativeInfinity;
        for (int k = lo; k < hi; k++)
        {
            amin = Vector2.Min(amin, ap[a[k]]); amax = Vector2.Max(amax, ap[a[k]]);
            bmin = Vector2.Min(bmin, bp[b[k]]); bmax = Vector2.Max(bmax, bp[b[k]]);
        }
        Vector2 ext = Vector2.Max(amax - amin, bmax - bmin);
        bool byX = ext.x >= ext.y;
        System.Array.Sort(a, lo, n, Comparer<int>.Create((x, y) => Cmp(ap[x], ap[y], byX, x, y)));
        System.Array.Sort(b, lo, n, Comparer<int>.Create((x, y) => Cmp(bp[x], bp[y], byX, x, y)));
        int mid = lo + n / 2;
        Match(a, b, lo, mid, ap, al, bp, bl, result);
        Match(a, b, mid, hi, ap, al, bp, bl, result);
    }

    private static int Cmp(Vector2 p, Vector2 q, bool byX, int i, int j)
    {
        int c = byX ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y);
        if (c != 0) return c;
        c = byX ? p.y.CompareTo(q.y) : p.x.CompareTo(q.x);
        return c != 0 ? c : i.CompareTo(j);
    }

    private static float Lum(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    private static void Shuffle(int[] arr, System.Random rng)
    {
        for (int i = arr.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (arr[i], arr[j]) = (arr[j], arr[i]); }
    }
}
