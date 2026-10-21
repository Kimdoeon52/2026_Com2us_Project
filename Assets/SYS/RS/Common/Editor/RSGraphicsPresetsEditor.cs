// RE:AL STEEL - 그래픽 프리셋 에셋 인스펙터 (항목 × 낮음 · 중간 · 높음 표) + 자동 생성
//
// 메뉴: Tools → RE_AL STEEL → 그래픽 프리셋 (낮음 · 중간 · 높음 값)
// 에셋 위치: SYS/Presets/Graphics/Resources/RS_GraphicsPresets.asset — 에디터를 열 때 없으면 만든다
using UnityEditor;
using UnityEngine;

namespace RealSteel.Common.EditorTools
{
    [CustomEditor(typeof(RSGraphicsPresets))]
    public class RSGraphicsPresetsEditor : Editor
    {
        public static string Folder { get { return RSPaths.Presets + "/Graphics/Resources"; } }
        public static string AssetPath { get { return Folder + "/" + RSGraphicsPresets.ResourceName + ".asset"; } }

        [MenuItem("Tools/RE_AL STEEL/그래픽 프리셋 (낮음 · 중간 · 높음 값)", false, 61)]
        public static void OpenAsset()
        {
            var a = EnsureAsset();
            if (a == null) return;
            Selection.activeObject = a;
            EditorGUIUtility.PingObject(a);
        }

        /// <summary>프리셋 에셋을 찾고, 없으면 만든다</summary>
        public static RSGraphicsPresets EnsureAsset()
        {
            var a = AssetDatabase.LoadAssetAtPath<RSGraphicsPresets>(AssetPath);
            if (a == null)
            {
                foreach (var g in AssetDatabase.FindAssets("t:RSGraphicsPresets"))
                {
                    a = AssetDatabase.LoadAssetAtPath<RSGraphicsPresets>(AssetDatabase.GUIDToAssetPath(g));
                    if (a != null) break;
                }
            }
            if (a == null)
            {
                RSPaths.Ensure(Folder);
                a = CreateInstance<RSGraphicsPresets>();
                AssetDatabase.CreateAsset(a, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[그래픽 프리셋] 프리셋 에셋을 만들었습니다: " + AssetPath + "\n낮음 · 중간 · 높음 값을 이 에셋 인스펙터에서 고칩니다.", a);
            }
            RSGraphics.ReloadPresets();
            return a;
        }

        // 에디터를 열 때(스크립트 다시 불러올 때) 에셋이 없으면 만든다 — 빌드에 값이 들어가게
        [InitializeOnLoadMethod]
        static void AutoCreate()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.FindAssets("t:RSGraphicsPresets").Length > 0) return;
                EnsureAsset();
            };
        }

        // ─────────────────────────────────────────────────────────────
        // 인스펙터
        // ─────────────────────────────────────────────────────────────

        static readonly string[] Q3 = { "낮음", "중간", "높음" };
        static readonly string[] WetOpts = { "어둡게만", "화면 반사", "+ 라이트 반사" };
        static readonly string[] WaterOpts = { "하늘색만", "화면 반사", "설정대로" };
        static readonly string[] IndirectOpts = { "끔", "거칠게", "원래대로" };
        static readonly string[] ShadowOpts = { "짧게", "중간", "원래대로" };
        static readonly string[] Levels = { "low", "mid", "high" };
        static readonly string[] Tri = { "low", "mid", "high" };

        const float LabelW = 150f;

        public override void OnInspectorGUI()
        {
            var t = (RSGraphicsPresets)target;
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "플레이어 그래픽 설정의 낮음 · 중간 · 높음 단계 값입니다.\n" +
                "· 고치면 바로 반영 (플레이 중이면 지금 단계에도)\n" +
                "· '높음' 은 보통 아트가 정한 그대로 (전부 켬 · 품질 높음)\n" +
                "· 룩(색 · 밀도 · 세기)은 여기가 아니라 스테이지 룩 에셋에서", MessageType.Info);

            string path = AssetDatabase.GetAssetPath(t);
            if (!string.IsNullOrEmpty(path) && !path.Contains("/Resources/"))
                EditorGUILayout.HelpBox("이 에셋이 Resources 폴더 밖에 있어 게임에서 읽히지 않습니다.\n위치: " + AssetPath, MessageType.Warning);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("note"));

            // ── 단계별 항목 표 ──
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("단계별 항목", EditorStyles.boldLabel);
            Header(Q3);
            Row("볼류메트릭 안개", "끄면 안개 속 빛줄기도 같이 꺼진다", "fog", null);
            Row("  안개 품질", "아래 '품질 숫자' 의 안개 값 중 어느 것을 쓸지", "fogQuality", Q3);
            Row("화면 갓레이", "화면 위 빛살", "screenGodRays", null);
            Row("  갓레이 품질", "아래 '품질 숫자' 의 갓레이 샘플 중 어느 것을 쓸지", "godRayQuality", Q3);
            Row("구름 그림자", "끄면 안개 빛줄기의 얼룩 무늬도 빠진다", "cloudShadows", null);
            Row("햇살 기둥 양", "개수 배율 0 ~ 1", "lightShafts", null);
            Row("캐릭터 밤 빛", "밤에 캐릭터가 읽히게 하는 등불", "characterGlow", null);
            Row("젖은 바닥", "어둡게만 = 반사 없음 · 화면 반사 = 걸음 · 거리 상한 · + 라이트 반사 = 그대로", "wetFloor", WetOpts);
            Row("물 반사", "설정대로 = 물 반사 컴포넌트 설정 (평면 반사 포함)", "water", WaterOpts);
            Row("간접광", "바꾸면 다시 계산한다 (잠깐 걸림)", "indirect", IndirectOpts);
            Row("피사계 심도", "후처리 중 가장 무겁다", "depthOfField", null);
            Row("블룸", "끄면 네온 · 발광이 안 빛난다", "bloom", null);
            Row("  블룸 고품질 필터", "", "bloomHighQuality", null);
            Row("그림자 거리", "아래 '품질 숫자' 의 그림자 거리 배율 중 어느 것을 쓸지", "shadows", ShadowOpts);

            // ── 품질 숫자 표 ──
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("품질 숫자 (위 표에서 고른 '낮음 · 중간 · 높음' 품질이 실제로 몇인지)", EditorStyles.boldLabel);
            Header(new[] { "낮음 품질", "중간 품질", "높음 품질" });
            TriRow("안개 걸음 간격 배율", "클수록 가볍고 빛줄기가 거칠다. 1 = 안개 상자 설정 그대로", "fogStepLengthScale");
            TriRow("안개 걸음 수 상한", "0 = 안개 상자 설정 그대로", "fogMaxSteps");
            TriRow("안개 라이트 최대 수", "0 ~ 8. 0 = 라이트가 안개를 안 밝힘", "fogMaxLights");
            TriRow("갓레이 샘플 상한", "0 = 갓레이 설정 그대로", "godRaySamples");
            TriRow("그림자 거리 배율", "URP 에셋 그림자 거리에 곱한다", "shadowDistanceScale");

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wetMidSteps"), new GUIContent("젖은 바닥 '화면 반사' 걸음 상한"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wetMidDistance"), new GUIContent("젖은 바닥 '화면 반사' 거리 상한 (m)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("indirectCoarseScale"), new GUIContent("간접광 '거칠게' 칸 배율"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("처음 실행할 때 고를 단계 (그래픽 메모리 기준)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("recommendLowBelowMB"), new GUIContent("이보다 작으면 '낮음' (MB)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("recommendMidBelowMB"), new GUIContent("이보다 작으면 '중간' (MB)"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("처음 값으로 되돌리기"))
                {
                    Undo.RecordObject(t, "그래픽 프리셋 처음 값");
                    t.ResetToDefaults();
                    EditorUtility.SetDirty(t);
                }
                if (Application.isPlaying)
                    EditorGUILayout.LabelField("지금 단계: " + RSGraphics.Options.level, GUILayout.Width(130));
            }
        }

        static void Header(string[] cols)
        {
            Rect r = EditorGUILayout.GetControlRect();
            float cw = (r.width - LabelW) / 3f;
            for (int i = 0; i < 3; i++)
                EditorGUI.LabelField(new Rect(r.x + LabelW + i * cw + 2f, r.y, cw - 4f, r.height), cols[i], EditorStyles.centeredGreyMiniLabel);
        }

        void Row(string label, string tip, string field, string[] opts)
        {
            Rect r = EditorGUILayout.GetControlRect();
            float cw = (r.width - LabelW) / 3f;
            EditorGUI.LabelField(new Rect(r.x, r.y, LabelW, r.height), new GUIContent(label, tip));
            for (int i = 0; i < 3; i++)
            {
                var p = serializedObject.FindProperty(Levels[i] + "." + field);
                if (p == null) continue;
                Cell(new Rect(r.x + LabelW + i * cw + 2f, r.y, cw - 4f, r.height), p, opts);
            }
        }

        void TriRow(string label, string tip, string field)
        {
            Rect r = EditorGUILayout.GetControlRect();
            float cw = (r.width - LabelW) / 3f;
            EditorGUI.LabelField(new Rect(r.x, r.y, LabelW, r.height), new GUIContent(label, tip));
            for (int i = 0; i < 3; i++)
            {
                var p = serializedObject.FindProperty(field + "." + Tri[i]);
                if (p == null) continue;
                Cell(new Rect(r.x + LabelW + i * cw + 2f, r.y, cw - 4f, r.height), p, null);
            }
        }

        static void Cell(Rect r, SerializedProperty p, string[] opts)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    // 칸 가운데에 체크박스
                    var tr = new Rect(r.x + r.width * 0.5f - 8f, r.y, 16f, r.height);
                    p.boolValue = EditorGUI.Toggle(tr, p.boolValue);
                    break;
                case SerializedPropertyType.Integer:
                    if (opts != null) p.intValue = EditorGUI.Popup(r, Mathf.Clamp(p.intValue, 0, opts.Length - 1), opts);
                    else p.intValue = EditorGUI.IntField(r, p.intValue);
                    break;
                case SerializedPropertyType.Float:
                    p.floatValue = EditorGUI.FloatField(r, p.floatValue);
                    break;
                default:
                    EditorGUI.PropertyField(r, p, GUIContent.none);
                    break;
            }
        }
    }
}
