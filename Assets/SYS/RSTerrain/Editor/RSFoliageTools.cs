// RE:AL STEEL - 풀 · 꽃 심기 에디터 도구 (시트 자르기 · 머티리얼 · 가져오기 설정 · 풀 레이어 텍스처)
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Terrain.EditorTools
{
    public static class RSFoliageTools
    {
        public const string FoliageShader = "RE_AL STEEL/Foliage Sprite";
        const string GapKey = "RS_Foliage_SliceGap";
        const string LastSheetKey = "RS_Foliage_LastSheet";
        static readonly Dictionary<int, Texture2D> seenSheet = new Dictionary<int, Texture2D>();

        public static int SliceGap
        {
            get { return EditorPrefs.GetInt(GapKey, 3); }
            set { EditorPrefs.SetInt(GapKey, Mathf.Clamp(value, 1, 16)); }
        }

        // ─────────────────────────────────────────────────────────────
        // 시트 자르기
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 투명한 틈으로 시트를 자른다. 먼저 빈 가로줄로 줄(띠)을 나누고, 띠 안에서 빈 세로줄이 gap 칸 이상이면 다른 그림으로 본다.
        /// 결과 사각형은 유니티 텍스처 좌표 (왼쪽 아래 기준, 픽셀). 위 줄부터 · 왼쪽부터 순서.
        /// </summary>
        public static List<RectInt> Slice(Texture2D tex, int gap)
        {
            return Slice(tex, gap, null);
        }

        /// <summary>flowers 를 주면 그림마다 꽃인지(분홍 · 보라 · 노랑 픽셀이 많은지)도 채운다</summary>
        public static List<RectInt> Slice(Texture2D tex, int gap, List<bool> flowers)
        {
            var res = new List<RectInt>();
            var px = ReadPixels(tex);
            if (px == null) return res;
            int W = tex.width, H = tex.height;
            gap = Mathf.Max(1, gap);
            System.Func<int, int, bool> on = (x, y) => px[y * W + x].a > 25;

            // 가로줄 띠 (위에서 아래로)
            var rowOn = new bool[H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (on(x, y)) { rowOn[y] = true; break; }
            var bands = Runs(rowOn, Mathf.Max(2, gap));
            bands.Reverse();   // 텍스처 좌표는 아래가 0 → 위 줄부터

            foreach (var band in bands)
            {
                var colOn = new bool[W];
                for (int x = 0; x < W; x++)
                    for (int y = band.x; y <= band.y; y++) if (on(x, y)) { colOn[x] = true; break; }
                foreach (var run in Runs(colOn, gap))
                {
                    // 그림마다 위아래를 딱 맞게 줄인다
                    int y0 = int.MaxValue, y1 = int.MinValue;
                    for (int y = band.x; y <= band.y; y++)
                        for (int x = run.x; x <= run.y; x++)
                            if (on(x, y)) { if (y < y0) y0 = y; if (y > y1) y1 = y; }
                    if (y0 > y1) continue;
                    int w = run.y - run.x + 1, h = y1 - y0 + 1;
                    if (w * h < 4) continue;   // 먼지 한두 점은 버린다
                    res.Add(new RectInt(run.x, y0, w, h));
                    if (flowers != null)
                    {
                        int all = 0, colored = 0;
                        for (int y = y0; y <= y1; y++)
                            for (int x = run.x; x <= run.y; x++)
                            {
                                var c = px[y * W + x];
                                if (c.a <= 25) continue;
                                all++;
                                bool pinkPurple = c.r > c.g + 25 || c.b > c.g + 25;
                                bool yellowWhite = c.r > 190 && c.g > 170 && c.r + 10 >= c.g;
                                if (pinkPurple || yellowWhite) colored++;
                            }
                        flowers.Add(all > 0 && colored > all * 0.12f);
                    }
                }
            }
            return res;
        }

        /// <summary>켜진 칸 구간들. 사이의 빈 칸이 gap 보다 적으면 한 구간으로 잇는다.</summary>
        static List<Vector2Int> Runs(bool[] on, int gap)
        {
            var l = new List<Vector2Int>();
            int s = -1, e = -1, empty = 0;
            for (int i = 0; i < on.Length; i++)
            {
                if (on[i]) { if (s < 0) s = i; e = i; empty = 0; }
                else if (s >= 0 && ++empty >= gap) { l.Add(new Vector2Int(s, e)); s = -1; empty = 0; }
            }
            if (s >= 0) l.Add(new Vector2Int(s, e));
            return l;
        }

        /// <summary>
        /// 시트 픽셀 (왼쪽 아래 기준, 텍스처 크기에 맞춤).
        /// 가져오기 설정(압축 · 최대 크기 · 알파 처리)에 휘둘리지 않게 원본 PNG 파일을 직접 읽는다.
        /// 파일이 없거나 PNG/JPG 가 아니면 GPU 복사로 읽는다.
        /// </summary>
        public static Color32[] ReadPixels(Texture2D tex)
        {
            if (tex == null) return null;
            int W = tex.width, H = tex.height;
            string path = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (ImageConversion.LoadImage(tmp, System.IO.File.ReadAllBytes(path), false))
                        {
                            var src = tmp.GetPixels32();
                            int sw = tmp.width, sh = tmp.height;
                            if (sw == W && sh == H) return src;
                            // 가져올 때 크기가 줄었다 → 텍스처 크기로 맞춘다 (가장 가까운 픽셀)
                            var dst = new Color32[W * H];
                            for (int y = 0; y < H; y++)
                                for (int x = 0; x < W; x++)
                                    dst[y * W + x] = src[Mathf.Min(sh - 1, y * sh / H) * sw + Mathf.Min(sw - 1, x * sw / W)];
                            return dst;
                        }
                    }
                    finally { Object.DestroyImmediate(tmp); }
                }
            }

            if (tex.isReadable)
            {
                try { return tex.GetPixels32(); } catch { }
            }
            var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var t2 = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t2.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            t2.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = t2.GetPixels32();
            Object.DestroyImmediate(t2);
            return px;
        }

        public static void SliceInto(RSFoliage f)
        {
            if (f.sheet == null) return;
            var isFlower = new List<bool>();
            var rects = Slice(f.sheet, SliceGap, isFlower);
            Undo.RecordObject(f, "시트 자르기");
            // 같은 자리 그림은 비율을 이어받는다
            var old = new Dictionary<RectInt, float>();
            foreach (var it in f.items) if (it != null) old[it.rect] = it.weight;
            f.items.Clear();
            int nf = 0;
            for (int i = 0; i < rects.Count; i++)
            {
                // 꽃은 기본 비율을 낮게 — 풀밭 사이사이에 드문드문 (목록에서 바꿀 수 있다)
                float def = isFlower[i] ? 0.3f : 1f;
                if (isFlower[i]) nf++;
                f.items.Add(new RSFoliage.Item { rect = rects[i], weight = old.TryGetValue(rects[i], out var w) ? w : def });
            }
            EditorUtility.SetDirty(f);
            if (rects.Count <= 1 && f.sheet.width * f.sheet.height > 64 * 64)
                Debug.LogWarning($"[풀 · 꽃] '{f.sheet.name}' 이 {rects.Count}개로만 잘렸습니다. 배경이 투명한 PNG 인지, 그림 사이에 투명한 틈이 있는지 확인하세요.", f);
            Debug.Log($"[풀 · 꽃] '{f.sheet.name}' 에서 {rects.Count}개를 잘랐습니다 (꽃 {nf} · 풀 {rects.Count - nf}, 틈 {SliceGap}픽셀 기준). 꽃은 비율 0.3 으로 시작합니다.", f);
        }

        /// <summary>새로 만든 요소: 마지막에 쓴 시트(없으면 이름에 꽃 · flower · foliage 가 든 텍스처)를 넣고 바로 쓸 수 있게</summary>
        public static void SetupNew(RSFoliage f)
        {
            Texture2D tex = null;
            string last = EditorPrefs.GetString(LastSheetKey, "");
            if (!string.IsNullOrEmpty(last)) tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(last));
            if (tex == null) tex = FindTexture("꽃", "flower", "foliage");
            if (tex == null) return;
            f.sheet = tex;
            AutoPrepare(f);
        }

        /// <summary>시트가 바뀌면 자동으로 자르고 머티리얼을 붙인다</summary>
        static void AutoPrepare(RSFoliage f)
        {
            if (f.sheet == null) return;
            seenSheet[f.GetInstanceID()] = f.sheet;
            EditorPrefs.SetString(LastSheetKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f.sheet)));
            if (!ImportOk(f.sheet, true)) FixImport(f.sheet, true);
            bool sheetChanged = f.material == null || f.material.GetTexture("_MainTex") != f.sheet;
            if (f.items.Count == 0 || sheetChanged) SliceInto(f);   // 같은 자리 그림의 비율은 이어받는다
            if (sheetChanged) EnsureMaterial(f);
            f.Owner?.RebuildFoliage();
        }

        /// <summary>Assets 안에서 이름에 키워드가 든 텍스처 (SYS 폴더 우선)</summary>
        public static Texture2D FindTexture(params string[] keys)
        {
            Texture2D best = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                foreach (var k in keys)
                {
                    if (!n.Contains(k.ToLowerInvariant())) continue;
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (t == null) continue;
                    if (p.StartsWith("Assets/SYS/")) return t;
                    if (best == null) best = t;
                }
            }
            return best;
        }

        // ─────────────────────────────────────────────────────────────
        // 머티리얼 · 가져오기 설정
        // ─────────────────────────────────────────────────────────────

        /// <summary>시트 옆의 MAT_RS_Foliage_시트이름 (없으면 만든다)</summary>
        public static Material MaterialFor(Texture2D sheet)
        {
            if (sheet == null) return null;
            var sh = Shader.Find(FoliageShader);
            if (sh == null) { Debug.LogWarning("[풀 · 꽃] 셰이더 '" + FoliageShader + "' 를 못 찾았습니다."); return null; }

            string texPath = AssetDatabase.GetAssetPath(sheet);
            string dir = string.IsNullOrEmpty(texPath) ? "Assets/SYS/Materials" : System.IO.Path.GetDirectoryName(texPath).Replace('\\', '/');
            string path = dir + "/MAT_RS_Foliage_" + sheet.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh) { name = "MAT_RS_Foliage_" + sheet.name };
                m.SetTexture("_MainTex", sheet);
                AssetDatabase.CreateAsset(m, path);
                AssetDatabase.SaveAssets();
            }
            else if (m.GetTexture("_MainTex") != sheet)
            {
                Undo.RecordObject(m, "풀 머티리얼");
                m.SetTexture("_MainTex", sheet);
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        public static Material EnsureMaterial(RSFoliage f)
        {
            if (f.sheet == null) return f.material;
            var m = MaterialFor(f.sheet);
            if (m == null) return f.material;
            Undo.RecordObject(f, "풀 머티리얼");
            f.material = m;
            EditorUtility.SetDirty(f);
            return m;
        }

        /// <summary>픽셀아트용 가져오기 설정인지 (Point · 압축 없음 · 밉맵 끔)</summary>
        public static bool ImportOk(Texture2D tex, bool sprite)
        {
            var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (ti == null) return true;
            return ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed &&
                   !ti.mipmapEnabled && (!sprite || ti.alphaIsTransparency);
        }

        /// <summary>sprite = 투명 배경 시트 (가장자리 반복 안 함) / 아니면 바닥 타일 (반복)</summary>
        public static void FixImport(Texture2D tex, bool sprite)
        {
            string p = AssetDatabase.GetAssetPath(tex);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) return;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = sprite ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
        }

        // ─────────────────────────────────────────────────────────────
        // 인스펙터 조각
        // ─────────────────────────────────────────────────────────────

        public static void DrawFoliageInspector(RSFoliage f)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("풀 · 꽃 시트", EditorStyles.boldLabel);

            if (f.sheet == null)
            {
                EditorGUILayout.HelpBox("위 '시트' 칸에 풀 · 꽃 스프라이트 시트를 넣으세요. 넣으면 자동으로 잘리고 머티리얼이 붙습니다.", MessageType.Info);
                return;
            }
            if (!seenSheet.TryGetValue(f.GetInstanceID(), out var seen) || seen != f.sheet)
            {
                // 처음 보거나 시트가 바뀌었다 — 레이아웃 도중 에셋을 만들지 않도록 다음 틱에
                seenSheet[f.GetInstanceID()] = f.sheet;
                if (f.items.Count == 0 || f.material == null || f.material.GetTexture("_MainTex") != f.sheet)
                    EditorApplication.delayCall += () => { if (f != null) AutoPrepare(f); };
            }

            if (!ImportOk(f.sheet, true))
            {
                EditorGUILayout.HelpBox("시트 가져오기 설정이 픽셀아트용이 아닙니다 (흐리거나 멀리서 깨질 수 있음).", MessageType.Warning);
                if (GUILayout.Button("가져오기 설정 맞추기 (Point · 압축 없음 · 밉맵 끔)")) FixImport(f.sheet, true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                SliceGap = EditorGUILayout.IntSlider(new GUIContent("자르기 틈 (픽셀)", "그림 사이가 이 칸 수 이상 비어 있으면 다른 그림으로 본다. 한 그림이 여러 개로 쪼개지면 늘리고, 두 그림이 붙으면 줄인다"), SliceGap, 1, 12);
                if (GUILayout.Button("시트 자르기", GUILayout.Width(90f))) { SliceInto(f); f.Notify(); }
            }
            if (f.items.Count == 0)
            {
                EditorGUILayout.HelpBox("아직 잘린 그림이 없습니다. [시트 자르기] 를 누르세요.", MessageType.Warning);
            }
            else
            {
                DrawThumbs(f);
            }

            if (f.material == null || f.material.shader == null || f.material.shader.name != FoliageShader)
            {
                EditorGUILayout.HelpBox("머티리얼이 없습니다 (RE_AL STEEL/Foliage Sprite).", MessageType.Warning);
                if (GUILayout.Button("머티리얼 만들기 (시트 옆에)")) { EnsureMaterial(f); f.Owner?.RebuildFoliage(); }
            }
        }

        static void DrawThumbs(RSFoliage f)
        {
            const float cell = 34f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 30f) / cell));
            float total = 0f; foreach (var it in f.items) if (it != null) total += Mathf.Max(0f, it.weight);
            EditorGUILayout.LabelField($"잘린 그림 {f.items.Count}개 — 아래 목록 'Items' 에서 항목마다 비율(Weight)을 바꿀 수 있다", EditorStyles.miniLabel);
            int rows = Mathf.CeilToInt(f.items.Count / (float)perRow);
            var area = GUILayoutUtility.GetRect(perRow * cell, rows * cell);
            for (int i = 0; i < f.items.Count; i++)
            {
                var it = f.items[i];
                if (it == null) continue;
                var r = new Rect(area.x + (i % perRow) * cell, area.y + (i / perRow) * cell, cell - 2f, cell - 2f);
                EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, it.weight > 0f ? 0.18f : 0.5f));
                var uv = new Rect(it.rect.x / (float)f.sheet.width, it.rect.y / (float)f.sheet.height,
                                  it.rect.width / (float)f.sheet.width, it.rect.height / (float)f.sheet.height);
                float s = Mathf.Min((cell - 4f) / it.rect.width, (cell - 4f) / it.rect.height);
                var d = new Rect(0, 0, it.rect.width * s, it.rect.height * s);
                d.center = new Vector2(r.center.x, r.yMax - 2f - d.height * 0.5f);
                GUI.DrawTextureWithTexCoords(d, f.sheet, uv);
                GUI.Label(new Rect(r.x + 1f, r.y, r.width, 12f), (i).ToString(), EditorStyles.miniLabel);
            }
        }

        /// <summary>RS 지형 인스펙터: 지형 머티리얼의 풀 레이어 텍스처 칸</summary>
        public static void DrawGrassLayerField(RSTerrain t)
        {
            var m = t.material;
            if (m == null || !m.HasProperty("_LayerGrass")) return;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("풀 레이어", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var cur = m.GetTexture("_LayerGrass") as Texture2D;
                if (cur == null)
                {
                    var found = FindTexture("풀밭", "grass");
                    if (found != null && GUILayout.Button("'" + found.name + "' 넣기", GUILayout.Width(110f)))
                    {
                        Undo.RecordObject(m, "풀 텍스처");
                        m.SetTexture("_LayerGrass", found);
                        EditorUtility.SetDirty(m);
                        if (!ImportOk(found, false)) FixImport(found, false);
                        cur = found;
                    }
                }
                var tex = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("풀 텍스처", "지형 머티리얼의 풀 레이어 (브러시 '칠할 레이어 = 풀')"), cur, typeof(Texture2D), false);
                if (tex != cur)
                {
                    Undo.RecordObject(m, "풀 텍스처");
                    m.SetTexture("_LayerGrass", tex);
                    EditorUtility.SetDirty(m);
                    if (tex != null && !ImportOk(tex, false)) FixImport(tex, false);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("풀꽃 하나씩 놓기", "시트에서 고른 풀 · 꽃을 씬 뷰에 클릭해서 하나씩 세운다 (풀꽃 배치 창)")))
                    RSPlantPainter.Open();
                if (GUILayout.Button(new GUIContent("풀꽃 자동 채우기 추가", "풀을 칠한 곳 전체에 풀 · 꽃이 저절로 자라게 하는 요소 (촘촘히 덮을 때)")))
                    RSTerrainMenu.AddFeature<RSFoliage>(t, "풀꽃_자동");
            }
        }
    }
}
