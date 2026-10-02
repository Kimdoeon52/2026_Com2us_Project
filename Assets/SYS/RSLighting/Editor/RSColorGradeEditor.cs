// RE:AL STEEL - 색감 인스펙터: 시간대 타임라인 · 키 편집 · LUT 작업 도구
//
// 기획 · 그래픽이 코드 없이 쓰는 화면:
//   타임라인  위 줄 = 밝은 곳 색, 아래 줄 = 어두운 곳 색. ▼ 표시가 키(시각). 빨간 선 = 지금 시각.
//             빈 곳 클릭 = 그 시각으로 미리보기 / ▼ 클릭 = 그 키 편집 / ▼ 드래그 = 시각 옮기기
//   버튼      지금 시각에 키 추가 · 복제 · 삭제 · 옥토패스 기본값 · 프리셋 복제
//   LUT       작업 이미지 내보내기 → 포토샵에서 보정 → 보정 이미지에서 LUT 만들기
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common.EditorTools;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSColorGradeGUI
    {
        public static string PresetFolder => RSPaths.ColorGradePresets;
        public static string LutFolder => RSPaths.Luts;
        /// <summary>URP 에셋의 Grading LUT Size 를 따른다 (다르면 URP 가 LUT 를 조용히 무시한다)</summary>
        static int LutSize => RSColorGrade.RequiredLutSize;

        static readonly RSColorGradeProfile.Key tmp = new RSColorGradeProfile.Key();
        static int dragIndex = -1;

        /// <summary>회색 기준 색 차이를 키워서 보여준다 (0.5 근처 미묘한 색도 눈에 띄게)</summary>
        static Color Boost(Color c, float k)
        {
            return new Color(Mathf.Clamp01(0.5f + (c.r - 0.5f) * k), Mathf.Clamp01(0.5f + (c.g - 0.5f) * k), Mathf.Clamp01(0.5f + (c.b - 0.5f) * k), 1f);
        }

        /// <summary>타임라인 + 선택 키 편집. selected 는 호출한 쪽이 들고 있는 선택 번호</summary>
        public static void DrawProfile(RSColorGradeProfile profile, RSColorGrade grade, ref int selected)
        {
            if (profile == null) return;
            RSTimeOfDay tod = grade != null ? grade.timeOfDay : Object.FindAnyObjectByType<RSTimeOfDay>();
            float nowHour = grade != null ? grade.CurrentHour : (tod != null ? tod.Hour : -1f);

            EditorGUILayout.LabelField("시간대 타임라인", EditorStyles.boldLabel);
            Rect r = GUILayoutUtility.GetRect(10f, 58f, GUILayout.ExpandWidth(true));
            Rect bar = new Rect(r.x + 4f, r.y + 14f, r.width - 8f, 30f);

            // 색 띠: 위 = 밝은 곳 색, 아래 = 어두운 곳 색
            int cols = Mathf.Clamp((int)(bar.width / 3f), 24, 240);
            float cw = bar.width / cols;
            for (int i = 0; i < cols; i++)
            {
                float h = (i + 0.5f) / cols * 24f;
                if (!profile.Evaluate(h, tmp, out _, out _, out _)) break;
                EditorGUI.DrawRect(new Rect(bar.x + i * cw, bar.y, cw + 1f, bar.height * 0.5f), Boost(tmp.highlightTint, 2.5f));
                EditorGUI.DrawRect(new Rect(bar.x + i * cw, bar.y + bar.height * 0.5f, cw + 1f, bar.height * 0.5f), Boost(tmp.shadowTint, 2.5f));
            }
            EditorGUI.DrawRect(new Rect(bar.x, bar.y + bar.height * 0.5f, bar.width, 1f), new Color(0f, 0f, 0f, 0.3f));

            // 시각 눈금
            var small = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
            for (int h = 0; h <= 24; h += 3)
            {
                float x = bar.x + h / 24f * bar.width;
                EditorGUI.DrawRect(new Rect(x, bar.yMax, 1f, 4f), new Color(0.5f, 0.5f, 0.5f));
                GUI.Label(new Rect(x - 12f, bar.yMax + 2f, 24f, 12f), h + "시", small);
            }

            // 지금 시각
            if (nowHour >= 0f)
            {
                float x = bar.x + nowHour / 24f * bar.width;
                EditorGUI.DrawRect(new Rect(x - 1f, bar.y - 2f, 2f, bar.height + 4f), new Color(1f, 0.25f, 0.2f));
            }

            // 키 표시
            Event e = Event.current;
            var keys = profile.keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == null) continue;
                float x = bar.x + Mathf.Repeat(keys[i].hour, 24f) / 24f * bar.width;
                Rect mk = new Rect(x - 6f, r.y, 12f, 13f);
                bool sel = i == selected;
                GUI.Label(mk, sel ? "▼" : "▽", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = sel ? 13 : 11 });
                EditorGUIUtility.AddCursorRect(mk, MouseCursor.SlideArrow);
                if (e.type == EventType.MouseDown && e.button == 0 && mk.Contains(e.mousePosition))
                {
                    selected = i; dragIndex = i;
                    GUI.FocusControl(null);
                    e.Use();
                }
            }
            if (dragIndex >= 0 && dragIndex < keys.Count && e.type == EventType.MouseDrag)
            {
                Undo.RecordObject(profile, "색감 키 시각 옮기기");
                keys[dragIndex].hour = Mathf.Round(Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width) * 24f * 4f) / 4f;
                profile.version++;
                EditorUtility.SetDirty(profile);
                e.Use();
            }
            if (e.type == EventType.MouseUp) dragIndex = -1;

            // 빈 곳 클릭 = 그 시각 미리보기
            if (e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition) && tod != null)
            {
                float h = Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width) * 24f;
                Undo.RecordObject(tod, "시각 미리보기");
                tod.SetTime(h);
                EditorUtility.SetDirty(tod);
                e.Use();
            }
            EditorGUILayout.LabelField("빈 곳 클릭 = 그 시각 보기 · ▽ 클릭 = 편집 · ▽ 드래그 = 시각 옮기기 (15분 단위)", EditorStyles.miniLabel);

            // 버튼
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = nowHour >= 0f;
                if (GUILayout.Button(new GUIContent("지금 시각에 키 추가", "지금 화면에 보이는 색감을 그대로 복사해 새 키로")))
                {
                    Undo.RecordObject(profile, "색감 키 추가");
                    var k = new RSColorGradeProfile.Key();
                    if (profile.Evaluate(nowHour, k, out _, out var b, out _) && b != null) k.lut = b.lut;
                    k.hour = Mathf.Round(nowHour * 4f) / 4f;
                    k.name = $"{Mathf.FloorToInt(k.hour)}시 {Mathf.RoundToInt(Mathf.Repeat(k.hour, 1f) * 60f):00}분";
                    keys.Add(k);
                    selected = keys.Count - 1;
                    profile.version++;
                    EditorUtility.SetDirty(profile);
                }
                GUI.enabled = selected >= 0 && selected < keys.Count;
                if (GUILayout.Button("선택 키 복제"))
                {
                    Undo.RecordObject(profile, "색감 키 복제");
                    var k = keys[selected].Clone();
                    k.hour = Mathf.Repeat(k.hour + 1f, 24f);
                    k.name += " 복사";
                    keys.Add(k);
                    selected = keys.Count - 1;
                    profile.version++;
                    EditorUtility.SetDirty(profile);
                }
                if (GUILayout.Button("선택 키 삭제"))
                {
                    Undo.RecordObject(profile, "색감 키 삭제");
                    keys.RemoveAt(selected);
                    selected = Mathf.Min(selected, keys.Count - 1);
                    profile.version++;
                    EditorUtility.SetDirty(profile);
                }
                GUI.enabled = selected >= 0 && selected < keys.Count && tod != null;
                if (GUILayout.Button("이 키 시각 보기"))
                {
                    Undo.RecordObject(tod, "시각 미리보기");
                    tod.SetTime(keys[selected].hour);
                    EditorUtility.SetDirty(tod);
                }
                GUI.enabled = true;
            }

            // 선택 키 편집
            if (selected >= 0 && selected < keys.Count)
            {
                var so = new SerializedObject(profile);
                so.Update();
                var arr = so.FindProperty("keys");
                if (selected < arr.arraySize)
                {
                    var el = arr.GetArrayElementAtIndex(selected);
                    EditorGUILayout.Space(4f);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField($"키 편집 — {keys[selected].name} ({keys[selected].hour:0.##}시)", EditorStyles.boldLabel);
                        var it = el.Copy();
                        var end = el.GetEndProperty();
                        bool enter = true;
                        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
                        {
                            enter = false;
                            EditorGUILayout.PropertyField(it, true);
                        }
                        if (GUILayout.Button("어두운 곳 · 밝은 곳 색을 회색으로 (영향 없음)"))
                        {
                            el.FindPropertyRelative("shadowTint").colorValue = RSColorGradeProfile.Neutral;
                            el.FindPropertyRelative("highlightTint").colorValue = RSColorGradeProfile.Neutral;
                        }
                    }
                }
                if (so.ApplyModifiedProperties()) profile.version++;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 에셋
        // ─────────────────────────────────────────────────────────────

        public static RSColorGradeProfile CreateDefaultProfile(string name = "RS_ColorGrade_옥토패스")
        {
            EnsureFolder(PresetFolder);
            var p = ScriptableObject.CreateInstance<RSColorGradeProfile>();
            p.FillOctopathDefaults();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{PresetFolder}/{name}.asset");
            AssetDatabase.CreateAsset(p, path);
            AssetDatabase.SaveAssets();
            return p;
        }

        public static RSColorGradeProfile Duplicate(RSColorGradeProfile src)
        {
            string from = AssetDatabase.GetAssetPath(src);
            string dir = string.IsNullOrEmpty(from) ? PresetFolder : Path.GetDirectoryName(from).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{src.name}_복사.asset");
            if (!string.IsNullOrEmpty(from) && AssetDatabase.CopyAsset(from, path))
                return AssetDatabase.LoadAssetAtPath<RSColorGradeProfile>(path);
            var p = Object.Instantiate(src);
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ─────────────────────────────────────────────────────────────
        // LUT 작업
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 지금 화면(LUT 뺀 상태) + 왼쪽 위에 '보정 없음' LUT 띠를 넣은 PNG 를 만든다.
        /// 포토샵에서 이 그림 전체에 조정 레이어(곡선 · 색상 균형 · 선택 색상 등)만 써서 보정하고 PNG 로 저장 →
        /// '보정 이미지에서 LUT 만들기' 로 띠를 잘라 LUT 로 쓴다. (띠가 그림과 똑같이 보정되므로 그 보정이 그대로 LUT 가 된다)
        /// </summary>
        public static void ExportLutWorkImage(RSColorGrade grade)
        {
            var cam = RSAutoFocus.FindViewCamera();
            if (cam == null) { EditorUtility.DisplayDialog("LUT 작업 이미지", "화면을 그리는 카메라가 없습니다.", "확인"); return; }

            int w = Mathf.Max(1280, LutSize * LutSize), h = Mathf.Max(LutSize * 2, Mathf.RoundToInt(w / Mathf.Max(0.3f, cam.aspect)));
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var img = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            bool captured = false;
            if (grade != null) grade.SetLutSuspended(true);
            try
            {
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req))
                {
                    RenderPipeline.SubmitRenderRequest(cam, req);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    img.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    img.Apply();
                    RenderTexture.active = prev;
                    captured = true;
                }
            }
            finally
            {
                if (grade != null) grade.SetLutSuspended(false);
                RenderTexture.ReleaseTemporary(rt);
            }
            if (!captured)
            {
                var fill = new Color32[w * h];
                for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(128, 128, 128, 255);
                img.SetPixels32(fill);
            }

            // 왼쪽 위에 '보정 없음' 띠 (가로 = 칸 안 빨강 + 칸 번호 파랑, 세로 = 초록)
            for (int y = 0; y < LutSize; y++)
                for (int x = 0; x < LutSize * LutSize; x++)
                {
                    int slice = x / LutSize, red = x % LutSize;
                    img.SetPixel(x, h - LutSize + y, new Color32(
                        (byte)Mathf.RoundToInt(red * 255f / (LutSize - 1)),
                        (byte)Mathf.RoundToInt(y * 255f / (LutSize - 1)),
                        (byte)Mathf.RoundToInt(slice * 255f / (LutSize - 1)), 255));
                }
            img.Apply();

            EnsureFolder(LutFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{LutFolder}/LUT작업_{System.DateTime.Now:MMdd_HHmm}.png");
            File.WriteAllBytes(path, img.EncodeToPNG());
            Object.DestroyImmediate(img);
            AssetDatabase.ImportAsset(path);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null) { ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("LUT 작업 이미지",
                (captured ? "" : "※ 화면을 찍지 못해 회색 바탕에 띠만 넣었습니다. 게임 뷰 스크린샷을 이 그림에 붙여 써도 됩니다 (왼쪽 위 띠는 그대로).\n\n") +
                $"저장: {path}\n\n" +
                "1. 포토샵에서 열고 '조정 레이어'(곡선 · 색상 균형 · 선택 색상 · 그라디언트 맵 등)로만 보정\n" +
                "   (흐림 · 붓 · 필터 · 크기 변경은 쓰면 안 됨 — 왼쪽 위 띠도 같이 보정돼야 한다)\n" +
                "2. 같은 크기 PNG 로 저장\n" +
                "3. 색감 인스펙터의 '보정 이미지에서 LUT 만들기' → 선택한 키에 들어간다", "확인");
        }

        /// <summary>보정한 작업 이미지의 왼쪽 위 띠를 잘라 LUT 텍스처로 저장</summary>
        public static Texture2D ImportLutFromImage(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!src.LoadImage(File.ReadAllBytes(file)))
            {
                EditorUtility.DisplayDialog("LUT 만들기", "그림을 읽지 못했습니다 (PNG · JPG 만).", "확인");
                return null;
            }
            if (src.width < LutSize * LutSize || src.height < LutSize)
            {
                EditorUtility.DisplayDialog("LUT 만들기", $"그림이 너무 작습니다. 'LUT 작업 이미지 내보내기' 로 만든 그림을 크기 그대로 저장해야 합니다 (최소 {LutSize * LutSize} x {LutSize}).", "확인");
                Object.DestroyImmediate(src);
                return null;
            }
            var lut = new Texture2D(LutSize * LutSize, LutSize, TextureFormat.RGBA32, false, true);
            lut.SetPixels(src.GetPixels(0, src.height - LutSize, LutSize * LutSize, LutSize));
            lut.Apply();
            Object.DestroyImmediate(src);

            EnsureFolder(LutFolder);
            string baseName = Path.GetFileNameWithoutExtension(file);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{LutFolder}/{baseName}_LUT.png");
            File.WriteAllBytes(path, lut.EncodeToPNG());
            Object.DestroyImmediate(lut);
            AssetDatabase.ImportAsset(path);
            FixLutImport(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>LUT 텍스처 가져오기 설정 (sRGB 끔 · 밉맵 끔 · 무압축 · Bilinear · Clamp)</summary>
        public static void FixLutImport(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.SaveAndReimport();
        }

        public static void DrawLutTools(RSColorGradeProfile profile, RSColorGrade grade, int selected)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("LUT (포토샵 색 보정표)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("1. LUT 작업 이미지 내보내기", "지금 화면 + 보정 없음 띠를 PNG 로. 포토샵에서 보정한다")))
                    ExportLutWorkImage(grade);
                GUI.enabled = profile != null && selected >= 0 && selected < profile.keys.Count;
                if (GUILayout.Button(new GUIContent("2. 보정 이미지에서 LUT 만들기", "포토샵에서 보정해 저장한 PNG 를 골라 선택한 키의 LUT 로")))
                {
                    string file = EditorUtility.OpenFilePanel("보정한 LUT 작업 이미지", Path.GetFullPath(LutFolder), "png,jpg");
                    var lut = ImportLutFromImage(file);
                    if (lut != null)
                    {
                        Undo.RecordObject(profile, "LUT 넣기");
                        profile.keys[selected].lut = lut;
                        profile.keys[selected].lutAmount = 1f;
                        profile.version++;
                        EditorUtility.SetDirty(profile);
                        EditorGUIUtility.PingObject(lut);
                    }
                }
                GUI.enabled = true;
            }
            EditorGUILayout.LabelField($"키마다 LUT 를 따로 넣을 수 있다 (시각 사이는 자동으로 섞임). 선택: 위에서 ▽ 클릭 · LUT 크기 {LutSize} (URP 에셋 Grading LUT Size)", EditorStyles.miniLabel);
            if (profile != null)
                foreach (var k in profile.keys)
                    if (k != null && k.lut != null && (k.lut.height != LutSize || k.lut.width != LutSize * LutSize))
                        EditorGUILayout.HelpBox($"'{k.name}' 키의 LUT '{k.lut.name}' ({k.lut.width} x {k.lut.height}) 가 URP 에셋 LUT 크기({LutSize * LutSize} x {LutSize}) 와 달라 적용되지 않습니다. 'LUT 작업 이미지' 부터 다시 만드세요.", MessageType.Warning);
            if (grade != null && !string.IsNullOrEmpty(grade.LutProblem))
                EditorGUILayout.HelpBox(grade.LutProblem, MessageType.Warning);
        }
    }

    [CustomEditor(typeof(RSColorGrade))]
    public class RSColorGradeEditor : Editor
    {
        int selected = -1;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var g = (RSColorGrade)target;

            RSLookInspector.Draw(this);

            EditorGUILayout.Space(6f);
            if (g.L.profile == null)
            {
                EditorGUILayout.HelpBox("색감 프리셋이 없습니다. 옥토패스풍 기본값(새벽 · 아침 · 한낮 · 오후 · 노을 · 밤)으로 하나 만들 수 있습니다.", MessageType.Info);
                if (GUILayout.Button("기본 프리셋 만들기 (옥토패스)", GUILayout.Height(26f)))
                {
                    RSLookEdit.Record(g, g.UsesStageLook, "색감 프리셋");
                    g.L.profile = RSColorGradeGUI.CreateDefaultProfile();
                    RSLookEdit.Dirty(g, g.UsesStageLook);
                    g.Apply();
                }
                return;
            }

            RSColorGradeGUI.DrawProfile(g.L.profile, g, ref selected);
            RSColorGradeGUI.DrawLutTools(g.L.profile, g, selected);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("프리셋 복제 → 이 씬 전용으로", "지금 프리셋을 복사해서 끼운다. 다른 스테이지 색감을 안 건드리고 고칠 때")))
                {
                    RSLookEdit.Record(g, g.UsesStageLook, "색감 프리셋 복제");
                    g.L.profile = RSColorGradeGUI.Duplicate(g.L.profile);
                    RSLookEdit.Dirty(g, g.UsesStageLook);
                    EditorGUIUtility.PingObject(g.L.profile);
                }
                if (GUILayout.Button("옥토패스 기본값으로 되돌리기") &&
                    EditorUtility.DisplayDialog("색감 되돌리기", $"'{g.L.profile.name}' 의 키를 전부 기본값 6개로 바꿉니다. (Ctrl+Z 가능)", "바꾸기", "취소"))
                {
                    Undo.RecordObject(g.L.profile, "색감 기본값");
                    g.L.profile.FillOctopathDefaults();
                    EditorUtility.SetDirty(g.L.profile);
                    selected = -1;
                }
            }
            if (GUI.changed) g.Apply();
            EditorGUILayout.Space(4f);
            RSVolumeReport.DrawOwned(g);
        }
    }

    [CustomEditor(typeof(RSColorGradeProfile))]
    public class RSColorGradeProfileEditor : Editor
    {
        int selected = -1;

        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var p = (RSColorGradeProfile)target;
            var grade = Object.FindAnyObjectByType<RSColorGrade>();
            RSColorGradeGUI.DrawProfile(p, grade != null && grade.L.profile == p ? grade : null, ref selected);
            RSColorGradeGUI.DrawLutTools(p, grade != null && grade.L.profile == p ? grade : null, selected);
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("옥토패스 기본값으로 채우기") &&
                EditorUtility.DisplayDialog("색감 기본값", "키를 전부 기본값 6개로 바꿉니다. (Ctrl+Z 가능)", "바꾸기", "취소"))
            {
                Undo.RecordObject(p, "색감 기본값");
                p.FillOctopathDefaults();
                EditorUtility.SetDirty(p);
                selected = -1;
            }
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("전체 목록", EditorStyles.boldLabel);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("keys"), true);
            if (serializedObject.ApplyModifiedProperties()) p.version++;
        }
    }
}
