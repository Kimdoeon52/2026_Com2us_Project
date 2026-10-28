// RE:AL STEEL - 나노봇 군집 인스펙터
//
// '봇 종류' 목록: 한 줄 = [그림] [쓰는 곳] [비율] [실제 %]. 아래에 요약 · 경고 · 미리보기 조각
// (기계 = 몸 + 테두리 / 유체 = 방울 / 낱개) 을 실제 격자 간격으로 그려 비율이 덩어리에서 어떻게 보이는지 바로 본다.
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    [CustomPropertyDrawer(typeof(NanoBotKind))]
    class NanoBotKindDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 4f;

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            var pic = p.FindPropertyRelative("picture");
            var use = p.FindPropertyRelative("use");
            var ratio = p.FindPropertyRelative("ratio");
            r.y += 2f; r.height = EditorGUIUtility.singleLineHeight;

            var thumb = new Rect(r.x, r.y, r.height, r.height);
            EditorGUI.DrawRect(thumb, new Color(0.47f, 0.49f, 0.53f));
            if (pic.objectReferenceValue is Texture2D t) GUI.DrawTexture(thumb, t, ScaleMode.ScaleToFit);

            float x = thumb.xMax + 4f, w = r.xMax - x;
            float wPct = 44f, wRatio = 52f, wUse = Mathf.Min(140f, w * 0.38f);
            float wPic = w - wPct - wRatio - wUse - 12f;
            EditorGUI.PropertyField(new Rect(x, r.y, wPic, r.height), pic, GUIContent.none);
            x += wPic + 4f;
            EditorGUI.PropertyField(new Rect(x, r.y, wUse, r.height), use, GUIContent.none);
            x += wUse + 4f;
            EditorGUI.PropertyField(new Rect(x, r.y, wRatio, r.height), ratio, GUIContent.none);
            x += wRatio + 4f;

            // 같은 '쓰는 곳' 끼리 합에 대한 %
            string pct = "—";
            var list = p.serializedObject.FindProperty("botKinds");
            if (list != null && pic.objectReferenceValue && ratio.floatValue > 0f)
            {
                float sum = 0f;
                for (int i = 0; i < list.arraySize; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("use").enumValueIndex != use.enumValueIndex) continue;
                    if (!e.FindPropertyRelative("picture").objectReferenceValue) continue;
                    sum += Mathf.Max(0f, e.FindPropertyRelative("ratio").floatValue);
                }
                if (sum > 0f) pct = (ratio.floatValue / sum * 100f).ToString("0.#") + "%";
            }
            EditorGUI.LabelField(new Rect(x, r.y, wPct, r.height), pct, EditorStyles.boldLabel);
        }
    }

    [CustomEditor(typeof(RSNanoSwarm))]
    class RSNanoSwarmEditor : Editor
    {
        Texture2D preview;
        int previewKey;
        static readonly string[] UseName = { "몸", "테두리", "유체", "낱개" };

        void OnDisable() { if (preview) DestroyImmediate(preview); }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var sw = (RSNanoSwarm)target;
            var it = serializedObject.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script") { using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it); continue; }
                if (it.name == "spacing")
                {
                    using (new EditorGUI.DisabledScope(sw.spacingFromPicture)) EditorGUILayout.PropertyField(it, true);
                    continue;
                }
                EditorGUILayout.PropertyField(it, true);
                if (it.name == "spacingFromPicture") { serializedObject.ApplyModifiedProperties(); KindsSection(sw); serializedObject.Update(); }
            }
            serializedObject.ApplyModifiedProperties();
        }

        void KindsSection(RSNanoSwarm sw)
        {
            var A = sw.Atlas;
            if (A == null) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 요약
                var sb = new System.Text.StringBuilder();
                bool has1 = false, has2 = false, has3 = false;
                if (sw.botKinds != null)
                    foreach (var k in sw.botKinds)
                    {
                        if (k == null || !k.picture || k.ratio <= 0f) continue;
                        if (k.use == NanoBotUse.Edge) has1 = true;
                        if (k.use == NanoBotUse.Fluid) has2 = true;
                        if (k.use == NanoBotUse.Loose) has3 = true;
                    }
                if (!A.custom) sb.Append("몸: 시험 그림 (목록에 '몸' 그림이 없음)");
                else
                {
                    sb.Append("몸: ");
                    for (int k = 0; k < A.body.Length; k++)
                        sb.Append(k > 0 ? " · " : "").Append($"{A.bodySize[k].x}×{A.bodySize[k].y} {A.BodyShare(k) * 100f:0.#}%");
                }
                sb.Append("\n테두리: ").Append(has1 ? "목록 그림" : A.custom ? "몸 그림에서 자동 (윗줄 밝게)" : "시험 그림");
                sb.Append("   유체: ").Append(has2 ? "목록 그림" : A.custom ? "몸 그림에서 자동 (한 칸 크게 · 모서리 깎기)" : "시험 그림");
                sb.Append("\n낱개: ").Append(has3 ? "목록 그림" : A.custom ? "몸 그림 그대로" : "시험 그림");
                float texel = sw.botWorldSize / RSNanoBotSprites.Tile;
                sb.Append($"\n격자 간격 {sw.spacing * 100f:0.0}cm = {sw.spacing / texel:0.#}px")
                  .Append(sw.spacingFromPicture ? $" (몸 그림 {A.MainSize}px 에 맞춤)" : " (직접 정함)");
                EditorGUILayout.LabelField(sb.ToString(), EditorStyles.wordWrappedMiniLabel);

                if (!sw.spacingFromPicture && A.custom)
                {
                    float px = sw.spacing / texel;
                    if (Mathf.Abs(px - A.MainSize) > 0.5f)
                        EditorGUILayout.HelpBox($"격자 간격 {px:0.#}px 과 몸 그림 {A.MainSize}px 이 다르다 — {(px < A.MainSize ? "판이 겹친다" : "판 사이에 틈이 생긴다")}", MessageType.Info);
                }
                foreach (var n in A.notes) EditorGUILayout.HelpBox(n, MessageType.Warning);

                // 미리보기 조각
                int spx = Mathf.Max(1, Mathf.RoundToInt(sw.spacing / texel));
                int key = RSNanoBotSprites.Signature(sw.botKinds) * 31 + spx;
                if (!preview || key != previewKey) { BuildPreview(A, spx); previewKey = key; }
                if (preview)
                {
                    float scale = Mathf.Max(1f, Mathf.Floor((EditorGUIUtility.currentViewWidth - 40f) / preview.width));
                    scale = Mathf.Min(scale, 4f);
                    var rr = GUILayoutUtility.GetRect(preview.width * scale, preview.height * scale, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(rr, preview, ScaleMode.StretchToFill);
                    var lab = EditorStyles.centeredGreyMiniLabel;
                    var lr = GUILayoutUtility.GetRect(rr.width, 14f, GUILayout.ExpandWidth(false));
                    float third = lr.width / 3f;
                    GUI.Label(new Rect(lr.x, lr.y, third, lr.height), "기계 (몸 + 테두리)", lab);
                    GUI.Label(new Rect(lr.x + third, lr.y, third, lr.height), "유체 (방울)", lab);
                    GUI.Label(new Rect(lr.x + third * 2f, lr.y, third, lr.height), "낱개", lab);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("지금 그림을 PNG 로 뽑기", "Sprites/NanoBoss/ 에 봇_몸 · 봇_낱개 PNG 를 만들고 목록에 넣는다")))
                    {
                        // 대화 상자 · 선택 바꾸기는 인스펙터 그리기 밖에서 (안에서 하면 레이아웃이 깨진다)
                        EditorApplication.delayCall += RSNanoBotSheetTools.Export;
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button(new GUIContent("다시 읽기", "PNG 를 고쳤는데 안 바뀌면")))
                    {
                        sw.RebuildAtlas();
                        previewKey = 0;
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        /// <summary>실제 격자 간격으로 봇을 찍은 조각 3개 (기계 · 유체 · 낱개), 봇 1px = 1px</summary>
        void BuildPreview(NanoBotAtlas A, int s)
        {
            int T = A.tile;
            int pw = Mathf.Clamp(s * 9 + T, 40, 120), ph = Mathf.Clamp(Mathf.RoundToInt(s * 6 + T), 30, 80);
            int W = pw * 3 + 4, H = ph;
            var px = new Color32[W * H];
            var bg = new Color32(120, 124, 134, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            for (int y = 0; y < H; y++) { px[y * W + pw + 1] = new Color32(80, 82, 90, 255); px[y * W + pw * 2 + 2] = new Color32(80, 82, 90, 255); }

            float rowH = s * 0.866f;
            int cols = Mathf.CeilToInt(pw / (float)s) + 2, rows = Mathf.CeilToInt(ph / rowH) + 2;
            float cx = pw * 0.5f, cy = ph * 0.5f, rx = pw * 0.5f - T * 0.5f - 1f, ry = ph * 0.5f - T * 0.5f - 1f;
            bool Inside(int c, int r)
            {
                float x = c * s + ((r & 1) != 0 ? s * 0.5f : 0f) - s, y = r * rowH - rowH;
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                return dx * dx + dy * dy <= 1f;
            }
            var b = new NanoBot();
            for (int panel = 0; panel < 3; panel++)
            {
                int ox = panel * (pw + 2);
                if (panel < 2)
                {
                    for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (!Inside(c, r)) continue;
                        int odd = r & 1;
                        bool edge = !Inside(c - 1, r) || !Inside(c + 1, r)
                                    || !Inside(c - 1 + odd, r - 1) || !Inside(c + odd, r - 1)
                                    || !Inside(c - 1 + odd, r + 1) || !Inside(c + odd, r + 1);
                        int id = r * 131 + c;
                        A.Assign(id, ref b);
                        float x = c * s + (odd != 0 ? s * 0.5f : 0f) - s, y = r * rowH - rowH;
                        int tile;
                        if (panel == 0) tile = edge ? b.tileEdge : b.tileBody;
                        else
                        {
                            tile = b.tileDrop;
                            x += (Hash(id, 7) - 0.5f) * s * 0.6f;
                            y += (Hash(id, 8) - 0.5f) * s * 0.6f;
                        }
                        Stamp(px, W, H, ox, A.tiles[tile], T, x, y, pw);
                    }
                }
                else
                {
                    int n = Mathf.Max(6, pw * ph / (s * s * 6));
                    for (int k = 0; k < n; k++)
                    {
                        A.Assign(k * 17 + 5, ref b);
                        Stamp(px, W, H, ox, A.tiles[b.tileLoose], T, T * 0.5f + Hash(k, 3) * (pw - T), T * 0.5f + Hash(k, 4) * (ph - T), pw);
                    }
                }
            }
            if (preview) DestroyImmediate(preview);
            preview = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            preview.SetPixels32(px);
            preview.Apply();
        }

        static float Hash(int i, int salt)
        {
            uint h = (uint)i * 747796405u + (uint)salt * 2891336453u;
            h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
            return ((h >> 22) ^ h) % 10007u / 10007f;
        }

        /// <summary>칸(위쪽 0)을 중심 (x, y) 에 찍는다. 패널 폭 pw 밖은 자른다</summary>
        static void Stamp(Color32[] px, int W, int H, int ox, Color32[] tile, int T, float x, float y, int pw)
        {
            int x0 = Mathf.FloorToInt(x - T * 0.5f), y0 = Mathf.FloorToInt(y - T * 0.5f);
            for (int ty = 0; ty < T; ty++)
            for (int tx = 0; tx < T; tx++)
            {
                var c = tile[ty * T + tx];
                if (c.a < 128) continue;
                int X = x0 + tx, Y = y0 + ty;
                if (X < 0 || X >= pw || Y < 0 || Y >= H) continue;
                px[(H - 1 - Y) * W + ox + X] = c;
            }
        }
    }
}
