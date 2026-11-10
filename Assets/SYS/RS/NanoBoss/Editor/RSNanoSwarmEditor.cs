// RE:AL STEEL - 나노봇 군집 인스펙터
//
// '봇 색' 목록: 한 줄 = [색] [비율] [실제 %]. 아래에 알갱이 · 칸 · 덩어리 크기 요약과 색 띠 미리보기.
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    [CustomPropertyDrawer(typeof(NanoBotColor))]
    class NanoBotColorDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 2f;

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            var color = p.FindPropertyRelative("color");
            var ratio = p.FindPropertyRelative("ratio");
            r.y += 1f; r.height = EditorGUIUtility.singleLineHeight;
            float wPct = 48f, wRatio = 60f;
            float wCol = r.width - wPct - wRatio - 8f;
            EditorGUI.PropertyField(new Rect(r.x, r.y, wCol, r.height), color, GUIContent.none);
            EditorGUI.PropertyField(new Rect(r.x + wCol + 4f, r.y, wRatio, r.height), ratio, GUIContent.none);

            string pct = "—";
            var list = p.serializedObject.FindProperty("botColors");
            if (list != null && ratio.floatValue > 0f)
            {
                float sum = 0f;
                for (int i = 0; i < list.arraySize; i++) sum += Mathf.Max(0f, list.GetArrayElementAtIndex(i).FindPropertyRelative("ratio").floatValue);
                if (sum > 0f) pct = (ratio.floatValue / sum * 100f).ToString("0.#") + "%";
            }
            EditorGUI.LabelField(new Rect(r.xMax - wPct, r.y, wPct, r.height), pct, EditorStyles.boldLabel);
        }
    }

    [CustomEditor(typeof(RSNanoSwarm))]
    class RSNanoSwarmEditor : Editor
    {
        Texture2D strip;
        int stripKey;

        void OnDisable() { if (strip) DestroyImmediate(strip); }

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
                EditorGUILayout.PropertyField(it, true);
                if (it.name == "freeSize") Summary(sw);
                if (it.name == "grainNoise") Strip(sw);
            }
            serializedObject.ApplyModifiedProperties();
        }

        static void Summary(RSNanoSwarm sw)
        {
            float cell = sw.Cell;
            float area = sw.botCount * sw.spacing * sw.spacing;
            float d = 2f * Mathf.Sqrt(area / Mathf.PI);
            EditorGUILayout.HelpBox($"칸 {cell * 100f:0.##}cm ({sw.grain}px) · 봇 {sw.botCount:N0} 이 다 붙은 덩어리 ≈ 지름 {d:0.0}m\n" +
                                    $"그림 부위는 그림 픽셀 하나 = 봇 하나 (칠한 픽셀 수만큼 봇이 든다)", MessageType.None);
        }

        /// <summary>봇 색 띠 — 봇 번호 0~255 의 색을 그대로 (비율 · 밝기 차이 확인)</summary>
        void Strip(RSNanoSwarm sw)
        {
            int key = 17;
            unchecked
            {
                key = key * 31 + sw.grainNoise.GetHashCode();
                if (sw.botColors != null) foreach (var c in sw.botColors) if (c != null) key = key * 31 + c.color.GetHashCode() + c.ratio.GetHashCode() * 7;
            }
            const int W = 64, H = 8;
            if (!strip || key != stripKey)
            {
                if (!strip) strip = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[W * H];
                for (int i = 0; i < px.Length; i++) px[i] = RSNanoBotColors.Pick(sw.botColors, i, sw.grainNoise);
                strip.SetPixels32(px);
                strip.Apply();
                stripKey = key;
            }
            var r = GUILayoutUtility.GetRect(10f, 32f, GUILayout.ExpandWidth(true));
            r.xMin += 2f; r.xMax -= 2f;
            GUI.DrawTexture(r, strip, ScaleMode.StretchToFill);
            if (GUILayout.Button("지금 봇에 다시 칠하기")) { sw.RecolorAll(); SceneView.RepaintAll(); }
        }
    }
}
