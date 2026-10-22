// RE:AL STEEL - 그래픽 설정 시험 창 (ESC 설정창이 생기기 전까지)
//
// 메뉴: Tools → RE_AL STEEL → 그래픽 설정 (시험)
// 플레이 중에 품질 단계 · 항목을 바꿔 보며 화면 · 프레임을 비교한다. 바꾼 값은 플레이어 설정 파일(persistentDataPath/graphics_settings.json)에 저장된다 — 빌드와 같은 파일.
// 편집 중(플레이 아님)에는 씬 화면이 항상 '높음'으로 보인다 — 아트 작업 화면이 흔들리지 않게.
using UnityEditor;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting.EditorTools
{
    public class RSGraphicsWindow : EditorWindow
    {
        [MenuItem("Tools/RE_AL STEEL/그래픽 설정 (시험)", false, 60)]
        static void Open()
        {
            var w = GetWindow<RSGraphicsWindow>("그래픽 설정");
            w.minSize = new Vector2(320, 420);
        }

        Vector2 scroll;

        void OnEnable() { RSGraphics.Changed += Repaint; EditorApplication.playModeStateChanged += OnPlayMode; }
        void OnDisable() { RSGraphics.Changed -= Repaint; EditorApplication.playModeStateChanged -= OnPlayMode; }
        void OnPlayMode(PlayModeStateChange s) { Repaint(); }

        void OnGUI()
        {
            var o = RSGraphics.Options.Clone();

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("편집 중에는 화면이 항상 '높음'으로 보입니다.\n플레이를 누르면 아래 값이 적용됩니다.", MessageType.Info);

            EditorGUILayout.LabelField("지금 단계", o.level.ToString(), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("낮음")) { RSGraphics.SetLevel(RSGraphicsLevel.낮음); return; }
                if (GUILayout.Button("중간")) { RSGraphics.SetLevel(RSGraphicsLevel.중간); return; }
                if (GUILayout.Button("높음")) { RSGraphics.SetLevel(RSGraphicsLevel.높음); return; }
            }
            EditorGUILayout.Space(6);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();

            Header("대기");
            o.fog = EditorGUILayout.Toggle(new GUIContent("볼류메트릭 안개", "끄면 안개 속 빛줄기도 같이 꺼진다"), o.fog);
            using (new EditorGUI.DisabledScope(!o.fog))
                o.fogQuality = Level("  안개 품질", o.fogQuality);
            o.screenGodRays = EditorGUILayout.Toggle("화면 갓레이", o.screenGodRays);
            using (new EditorGUI.DisabledScope(!o.screenGodRays))
                o.godRayQuality = Level("  갓레이 품질", o.godRayQuality);
            o.cloudShadows = EditorGUILayout.Toggle(new GUIContent("구름 그림자", "끄면 안개 빛줄기의 얼룩 무늬도 빠진다"), o.cloudShadows);
            o.lightShafts = EditorGUILayout.Slider("햇살 기둥 양", o.lightShafts, 0f, 1f);
            o.characterGlow = EditorGUILayout.Toggle("캐릭터 밤 빛", o.characterGlow);

            Header("반사 · 간접광");
            o.wetFloor = EditorGUILayout.Popup("젖은 바닥", o.wetFloor, new[] { "어둡게만 (반사 없음)", "화면 반사", "화면 반사 + 라이트 반사" });
            o.water = EditorGUILayout.Popup("물 반사", o.water, new[] { "하늘색만", "화면 반사", "물 반사 설정대로" });
            o.indirect = EditorGUILayout.Popup(new GUIContent("간접광", "바꾸면 다시 계산한다 (잠깐 걸림)"), o.indirect, new[] { "끔", "거칠게 (칸 2배)", "원래대로" });

            Header("후처리");
            o.depthOfField = EditorGUILayout.Toggle("피사계 심도", o.depthOfField);
            o.bloom = EditorGUILayout.Toggle("블룸", o.bloom);
            using (new EditorGUI.DisabledScope(!o.bloom))
                o.bloomHighQuality = EditorGUILayout.Toggle("  블룸 고품질 필터", o.bloomHighQuality);

            Header("그림자");
            o.shadows = EditorGUILayout.Popup(new GUIContent("그림자 거리", "URP 에셋 값 × 배율. 플레이를 멈추면 원래 값으로 돌아간다"), o.shadows, new[] { "절반", "3/4", "원래대로" });

            Header("단계와 상관없는 값");
            o.brightness = EditorGUILayout.Slider("밝기 (EV)", o.brightness, -1f, 1f);
            o.vSync = EditorGUILayout.Toggle("수직 동기화", o.vSync);
            using (new EditorGUI.DisabledScope(o.vSync))
                o.targetFps = EditorGUILayout.IntField(new GUIContent("최대 프레임", "-1 = 제한 없음"), o.targetFps);
            EditorGUILayout.LabelField("해상도 · 전체 화면", "빌드에서만 적용");

            bool changed = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndScrollView();
            if (changed) RSGraphics.Set(o);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("처음 값으로 (권장 단계)")) RSGraphics.ResetToDefault();
                if (GUILayout.Button("저장된 값 다시 읽기")) RSGraphics.Load();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("저장 파일", System.IO.Path.GetFileName(RSGraphics.SettingsFilePath));
                if (GUILayout.Button("폴더 열기", GUILayout.Width(80)))
                {
                    string f = RSGraphics.SettingsFilePath;
                    EditorUtility.RevealInFinder(System.IO.File.Exists(f) ? f : System.IO.Path.GetDirectoryName(f));
                }
            }
            if (GUILayout.Button("낮음 · 중간 · 높음 값 고치기 (프리셋 에셋 열기)"))
                RealSteel.Common.EditorTools.RSGraphicsPresetsEditor.OpenAsset();
            if (RSGraphics.UsingFallbackPresets)
                EditorGUILayout.HelpBox("프리셋 에셋이 없어 코드 기본값을 쓰는 중입니다. 위 버튼을 누르면 만듭니다.", MessageType.Warning);
            if (Application.isPlaying)
                EditorGUILayout.LabelField("FPS", (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0"));
        }

        static void Header(string t)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
        }

        static int Level(string label, int v)
        {
            return EditorGUILayout.Popup(label, v, new[] { "낮음", "중간", "높음" });
        }

        void Update()
        {
            if (Application.isPlaying) Repaint();   // FPS 표시
        }
    }
}
