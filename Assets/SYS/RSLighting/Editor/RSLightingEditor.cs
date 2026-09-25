// RE:AL STEEL - RS Lighting 에디터: 설치 메뉴 + 시간대 인스펙터
using System.Linq;
using UnityEditor;
using UnityEngine;
using RealSteel.Common.EditorTools;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSLightingMenu
    {
        const string ShaftShader = "RE_AL STEEL/Light Shaft";
        const string ShaftMatName = "MAT_RS_LightShaft";

        [MenuItem("Tools/RE_AL STEEL/Stage/옥토패스 조명 (시간대 · 구름 그림자 · 햇살)", false, 33)]
        public static void Setup()
        {
            // 1) 태양 찾기: RenderSettings.sun → LIGHT_Key_Sun → 아무 Directional → 새로
            Light sun = RenderSettings.sun;
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (sun == null || sun.type != LightType.Directional)
                sun = lights.FirstOrDefault(l => l.type == LightType.Directional && l.name == "LIGHT_Key_Sun")
                   ?? lights.FirstOrDefault(l => l.type == LightType.Directional && l.name != "LIGHT_Fill");
            if (sun == null)
            {
                var go = new GameObject("LIGHT_Sun");
                Undo.RegisterCreatedObjectUndo(go, "RS 조명");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }
            Light fill = lights.FirstOrDefault(l => l != sun && l.type == LightType.Directional && l.name == "LIGHT_Fill");

            // 2) 시간대 루트
            var tod = Object.FindFirstObjectByType<RSTimeOfDay>();
            if (tod == null)
            {
                var root = new GameObject("LIGHTING_TimeOfDay");
                Undo.RegisterCreatedObjectUndo(root, "RS 조명");
                tod = root.AddComponent<RSTimeOfDay>();
                tod.time = 10f;
            }
            Undo.RecordObject(tod, "RS 조명");
            tod.sun = sun;
            if (tod.fillLight == null) tod.fillLight = fill;

            // 3) 구름 그림자 (태양에)
            var clouds = sun.GetComponent<RSCloudShadow>();
            if (clouds == null) clouds = Undo.AddComponent<RSCloudShadow>(sun.gameObject);
            tod.clouds = clouds;

            // 4) 햇살
            var shafts = Object.FindFirstObjectByType<RSLightShafts>();
            if (shafts == null)
            {
                var go = new GameObject("LightShafts");
                Undo.RegisterCreatedObjectUndo(go, "RS 조명");
                go.transform.SetParent(tod.transform, false);
                shafts = go.AddComponent<RSLightShafts>();
            }
            Undo.RecordObject(shafts, "RS 조명");
            shafts.sun = sun;
            if (shafts.material == null) shafts.material = FindOrCreateShaftMaterial();
            tod.shafts = shafts;

            tod.Apply();
            EditorUtility.SetDirty(tod);
            Selection.activeGameObject = tod.gameObject;

            Debug.Log("[RS 조명] 설치 완료 — 태양: " + sun.name + (fill != null ? ", 보조광: " + fill.name : "") +
                      "\n· 'LIGHTING_TimeOfDay' 에서 시각 · 프리셋을 바꾸세요.\n" +
                      "· 'LightShafts' 오브젝트를 전투 구역 가운데 · 지면 높이에 두세요 (영역 36x36).\n" +
                      "· 구름 그림자가 안 보이면 URP Asset → Lighting → Light Cookies 가 켜져 있는지 확인하세요.");
        }

        public static Material FindOrCreateShaftMaterial()
        {
            foreach (var g in AssetDatabase.FindAssets(ShaftMatName + " t:Material"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == ShaftMatName) return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            var sh = Shader.Find(ShaftShader);
            if (sh == null) { Debug.LogWarning("[RS 조명] 셰이더 '" + ShaftShader + "' 를 찾지 못했습니다."); return null; }

            // 셰이더 옆에 저장
            string dir = "Assets";
            var shPath = AssetDatabase.GetAssetPath(sh);
            if (!string.IsNullOrEmpty(shPath)) dir = System.IO.Path.GetDirectoryName(shPath).Replace('\\', '/');
            var m = new Material(sh) { name = ShaftMatName };
            AssetDatabase.CreateAsset(m, dir + "/" + ShaftMatName + ".mat");
            AssetDatabase.SaveAssets();
            return m;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 시간대 인스펙터: 시계 + 프리셋 버튼
    // ═════════════════════════════════════════════════════════════════

    [CustomEditor(typeof(RSTimeOfDay))]
    public class RSTimeOfDayEditor : Editor
    {
        static readonly string[] PresetLabels = { "새벽", "아침", "한낮", "오후", "노을", "밤" };

        public override void OnInspectorGUI()
        {
            var t = (RSTimeOfDay)target;
            RSHelpGUI.DrawSummary(target);
            serializedObject.Update();

            float h = t.Hour;
            int hh = Mathf.FloorToInt(h), mm = Mathf.FloorToInt((h - hh) * 60f);
            var big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField(string.Format("{0:00}:{1:00}   {2}", hh, mm, Describe(t)), big, GUILayout.Height(28f));

            var timeProp = serializedObject.FindProperty("time");
            EditorGUILayout.Slider(timeProp, 0f, 24f, new GUIContent("시각"));

            int picked = -1;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < PresetLabels.Length; i++)
                if (GUILayout.Button(PresetLabels[i])) picked = i;
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button(t.IsNight ? "밤 → 아침 전환 (2초)" : "낮 → 밤 전환 (2초)")) picked = 100;
            if (picked == 100)
            {
                GUI.FocusControl(null);
                t.ToggleDayNight(2f);
                picked = -1;
            }
            if (picked >= 0)
            {
                // 시각 칸에 입력 중인 값이 남아 있으면 프리셋을 덮어쓰므로 입력을 끝내고 직접 넣는다
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
                Undo.RecordObject(t, "시각 프리셋");
                t.SetTime(RSTimeOfDay.PresetHours[picked]);
                EditorUtility.SetDirty(t);
                serializedObject.Update();   // 방금 바꾼 값을 다시 읽어서, 아래에서 옛 값이 덮어쓰지 않게
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4f);
            DrawPropertiesExcluding(serializedObject, "m_Script", "time");

            if (serializedObject.ApplyModifiedProperties()) t.Apply();

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("지금 적용")) { t.Apply(); SceneView.RepaintAll(); }
            if (GUILayout.Button("색 · 커브 기본값으로"))
            {
                Undo.RecordObject(t, "시간대 기본값");
                t.ResetLook();
                EditorUtility.SetDirty(t);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("전부 옥토패스 권장값으로 (시간대 + 구름 + 햇살)"))
            {
                var objs = new System.Collections.Generic.List<Object> { t };
                if (t.clouds != null) objs.Add(t.clouds);
                if (t.shafts != null) objs.Add(t.shafts);
                Undo.RecordObjects(objs.ToArray(), "옥토패스 권장값");
                t.ResetLook();
                if (t.clouds != null) { t.clouds.ResetLook(); EditorUtility.SetDirty(t.clouds); }
                if (t.shafts != null) { t.shafts.ResetLook(); EditorUtility.SetDirty(t.shafts); }
                t.Apply();
                EditorUtility.SetDirty(t);
                SceneView.RepaintAll();
            }
            if (t.clouds == null || t.shafts == null)
                EditorGUILayout.HelpBox("구름 그림자 · 햇살이 연결되지 않았습니다. 메뉴 Tools → RE_AL STEEL → Stage → 옥토패스 조명 을 실행하면 자동으로 연결됩니다.", MessageType.Info);
        }

        static string Describe(RSTimeOfDay t)
        {
            float h = t.Hour;
            if (t.IsMoon) return "밤";
            if (h < 7f) return "새벽";
            if (h < 10.5f) return "아침";
            if (h < 14.5f) return "한낮";
            if (h < 17.3f) return "오후";
            return "노을";
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 구름 · 햇살 인스펙터: 권장값 버튼
    // ═════════════════════════════════════════════════════════════════

    [CustomEditor(typeof(RSCloudShadow))]
    public class RSCloudShadowEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            DrawDefaultInspector();
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("옥토패스 권장값으로"))
            {
                var c = (RSCloudShadow)target;
                Undo.RecordObject(c, "구름 권장값");
                c.ResetLook();
                EditorUtility.SetDirty(c);
                SceneView.RepaintAll();
            }
        }
    }

    [CustomEditor(typeof(RSLightShafts))]
    public class RSLightShaftsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            DrawDefaultInspector();
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("옥토패스 권장값으로"))
            {
                var c = (RSLightShafts)target;
                Undo.RecordObject(c, "햇살 권장값");
                c.ResetLook();
                EditorUtility.SetDirty(c);
                SceneView.RepaintAll();
            }
            EditorGUILayout.HelpBox("세기 · 색은 시간대(LIGHTING_TimeOfDay)의 '햇살 빛줄기 (시각별)' 커브 · 그라데이션이 정합니다.", MessageType.None);
        }
    }
}
