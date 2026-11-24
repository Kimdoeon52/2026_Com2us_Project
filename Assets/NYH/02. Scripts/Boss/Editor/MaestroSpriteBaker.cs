using System.Collections.Generic;
using System.IO;
using RealSteel.NanoBoss;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 나노봇 그림 굽기 (2026-11-24) — 픽셀아트 PNG → RSNanoSpriteShape(그림 부위가 쓰는 봇 자리 + 픽셀 색).
/// KKH 엔진의 굽기 편집 도구가 삭제돼서(11-17) NYH 쪽에 같은 데이터 형식으로 만든다. 엔진 코드는 안 고친다.
///   · 원본 PNG를 파일에서 직접 읽는다 (가져오기 설정·압축과 무관한 원본 픽셀)
///   · 시트: shape의 columns × rows 칸, 위 줄부터 왼쪽 → 오른쪽. 픽셀 하나 = 봇 하나, 봇 수 = 가장 많이 칠한 프레임
///   · 프레임이 바뀔 때 같은 픽셀의 봇은 그대로, 나머지는 가까운 새 픽셀로 (봇이 흘러가며 다음 그림이 된다)
/// 메뉴: Project 창에서 PNG 선택 → Assets/NYH/나노봇 그림 굽기 (→ 04. SO/Boss/Maestro/Look/이름_나노봇그림.asset)
/// </summary>
public static class MaestroSpriteBaker
{
    public const string OutFolder = "Assets/NYH/04. SO/Boss/Maestro/Look";

    [MenuItem("Assets/NYH/나노봇 그림 굽기", false, 2000)]
    private static void BakeSelected()
    {
        foreach (var obj in Selection.objects)
        {
            if (!(obj is Texture2D tex)) continue;
            string png = AssetDatabase.GetAssetPath(tex);
            var shape = BakeToAsset(png, $"{OutFolder}/{Path.GetFileNameWithoutExtension(png)}_나노봇그림.asset");
            if (shape != null) Debug.Log($"[나노봇 그림] {png} → 프레임 {shape.frameCount}, 봇 {shape.botCount}", shape);
        }
    }

    [MenuItem("Assets/NYH/나노봇 그림 굽기", true)]
    private static bool BakeSelectedValid()
    {
        foreach (var obj in Selection.objects) if (obj is Texture2D) return true;
        return false;
    }

    /// <summary>shapePath에 그림 모양이 있으면 그 설정(칸 수·피벗·알파 기준)으로 다시 굽고, 없으면 새로 만든다(피벗 = 발밑)</summary>
    /// <param name="pivotPx">그림 원점(텍스처 픽셀, 왼쪽 아래 = 0,0). 부위를 관절에서 돌리려면 관절 픽셀을 준다. null이면 shape의 피벗 설정</param>
    public static RSNanoSpriteShape BakeToAsset(string pngPath, string shapePath, Vector2Int? pivotPx = null)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        if (tex == null) { Debug.LogError($"[나노봇 그림] PNG가 없음: {pngPath}"); return null; }
        var shape = AssetDatabase.LoadAssetAtPath<RSNanoSpriteShape>(shapePath);
        bool created = shape == null;
        if (created)
        {
            EnsureFolder(Path.GetDirectoryName(shapePath).Replace('\\', '/'));
            shape = ScriptableObject.CreateInstance<RSNanoSpriteShape>();
            shape.pivot = NanoSpritePivot.BottomCenter;
        }
        shape.sheet = tex;
        if (!Bake(shape, pngPath, pivotPx)) return null;
        if (created) AssetDatabase.CreateAsset(shape, shapePath);
        EditorUtility.SetDirty(shape);
        AssetDatabase.SaveAssets();
        return shape;
    }

    public static bool Bake(RSNanoSpriteShape shape, string pngPath, Vector2Int? pivotPx = null)
    {
        var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!raw.LoadImage(File.ReadAllBytes(pngPath))) { Debug.LogError($"[나노봇 그림] 읽기 실패: {pngPath}"); return false; }
        Color32[] px = raw.GetPixels32();
        int tw = raw.width, th = raw.height;
        Object.DestroyImmediate(raw);

        int cols = Mathf.Max(1, shape.columns), rows = Mathf.Max(1, shape.rows);
        int fw = tw / cols, fh = th / rows;
        int frames = cols * rows;
        if (shape.frameLimit > 0) frames = Mathf.Min(frames, shape.frameLimit);
        int ox = fw / 2, oy = shape.pivot == NanoSpritePivot.BottomCenter ? 0 : fh / 2;
        if (pivotPx.HasValue) { ox = pivotPx.Value.x; oy = pivotPx.Value.y; }
        byte cut = (byte)Mathf.RoundToInt(shape.alphaCut * 255f);

        var coords = new List<Vector2Int>[frames];
        var colorOf = new Dictionary<Vector2Int, int>[frames];
        int maxFilled = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int f = 0; f < frames; f++)
        {
            int fx = (f % cols) * fw, fy = th - (f / cols + 1) * fh; // 위 줄부터 (텍스처 y는 아래가 0)
            var list = new List<Vector2Int>();
            var map = new Dictionary<Vector2Int, int>();
            for (int y = 0; y < fh; y++)
                for (int x = 0; x < fw; x++)
                {
                    Color32 c = px[(fy + y) * tw + fx + x];
                    if (c.a < cut || !Picked(shape, c)) continue;
                    var p = new Vector2Int(x - ox, y - oy);
                    list.Add(p);
                    map[p] = c.r | (c.g << 8) | (c.b << 16);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
            // 아래 줄부터 → 프레임 0에서 봇 순서가 '아래에서 위로' (모래성 쌓기와 같은 방향)
            list.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            coords[f] = list;
            colorOf[f] = map;
            maxFilled = Mathf.Max(maxFilled, list.Count);
        }
        if (maxFilled == 0) { Debug.LogError($"[나노봇 그림] 칠한 픽셀이 없음: {pngPath}"); return false; }

        shape.frameCount = frames;
        shape.botCount = maxFilled;
        shape.minX = minX; shape.minY = minY; shape.maxX = maxX; shape.maxY = maxY;
        shape.filled = new int[frames];
        shape.pixels = new int[frames * maxFilled];
        shape.colors = new int[frames * maxFilled];

        var prev = new Vector2Int[maxFilled];
        for (int f = 0; f < frames; f++)
        {
            var P = coords[f];
            shape.filled[f] = P.Count;
            if (P.Count == 0)
            {
                for (int k = 0; k < maxFilled; k++) shape.pixels[f * maxFilled + k] = RSNanoSpriteShape.Pack(prev[k].x, prev[k].y, 0);
                continue;
            }
            // 픽셀마다 받을 봇 수 (봇이 더 많으면 고르게 겹친다)
            var cap = new int[P.Count];
            for (int i = 0; i < P.Count; i++) cap[i] = maxFilled / P.Count;
            for (int j = 0; j < maxFilled % P.Count; j++) cap[(int)((long)j * P.Count / (maxFilled % P.Count))]++;
            var assign = new int[maxFilled];
            for (int k = 0; k < maxFilled; k++) assign[k] = -1;

            if (f == 0)
            {
                int k = 0;
                for (int i = 0; i < P.Count; i++) for (int c = 0; c < cap[i] && k < maxFilled; c++) assign[k++] = i;
            }
            else
            {
                var index = new Dictionary<Vector2Int, int>(P.Count);
                for (int i = 0; i < P.Count; i++) index[P[i]] = i;
                for (int k = 0; k < maxFilled; k++)
                    if (index.TryGetValue(prev[k], out int i) && cap[i] > 0) { assign[k] = i; cap[i]--; }
                for (int k = 0; k < maxFilled; k++)
                {
                    if (assign[k] >= 0) continue;
                    int best = -1;
                    long bd = long.MaxValue;
                    for (int i = 0; i < P.Count; i++)
                    {
                        if (cap[i] <= 0) continue;
                        long dx = P[i].x - prev[k].x, dy = P[i].y - prev[k].y, d = dx * dx + dy * dy;
                        if (d < bd) { bd = d; best = i; }
                    }
                    if (best < 0) best = k % P.Count;
                    else cap[best]--;
                    assign[k] = best;
                }
            }

            var rank = new int[P.Count];
            for (int k = 0; k < maxFilled; k++)
            {
                int i = Mathf.Max(0, assign[k]);
                Vector2Int p = P[i];
                shape.pixels[f * maxFilled + k] = RSNanoSpriteShape.Pack(p.x, p.y, rank[i]++);
                shape.colors[f * maxFilled + k] = colorOf[f][p];
                prev[k] = p;
            }
        }
        shape.bakedKey = $"nyh_{Path.GetFileName(pngPath)}_{frames}_{maxFilled}_{File.GetLastWriteTimeUtc(pngPath).Ticks}";
        return true;
    }

    private static bool Picked(RSNanoSpriteShape shape, Color32 c)
    {
        if (shape.pick == NanoSpritePick.Opaque) return true;
        float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
        bool dark = lum < shape.darkness;
        return shape.invert ? !dark : dark;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
