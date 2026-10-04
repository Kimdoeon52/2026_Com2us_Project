// RE:AL STEEL - 인스펙터 설명 표기 (에디터)
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RealSteel.Common.EditorTools
{
    public static class RSHelpGUI
    {
        const string Key = "RS_ShowInspectorHelp";

        public static bool Show
        {
            get { return EditorPrefs.GetBool(Key, true); }
            set { EditorPrefs.SetBool(Key, value); }
        }


        static GUIStyle box, title;

        public static GUIStyle Box
        {
            get
            {
                if (box == null)
                    box = new GUIStyle(EditorStyles.helpBox)
                    {
                        richText = true, wordWrap = true, fontSize = 11,
                        padding = new RectOffset(8, 8, 5, 5),
                    };
                return box;
            }
        }

        static GUIStyle Title
        {
            get
            {
                if (title == null) title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, wordWrap = true };
                return title;
            }
        }

        /// <summary>컴포넌트 맨 위 요약 상자. 커스텀 인스펙터 OnInspectorGUI 첫 줄에서 부른다</summary>
        public static void DrawSummary(Object target)
        {
            if (target == null) return;
            var s = target.GetType().GetCustomAttribute<RSSummaryAttribute>(false);
            if (s == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(s.title, Title);
                if (GUILayout.Button(Show ? "설명 숨기기" : "설명 보기", EditorStyles.miniButton, GUILayout.Width(70f)))
                {
                    Show = !Show;
                    InternalEditorUtility.RepaintAllViews();
                }
            }
            if (Show) GUILayout.Label(s.text, Box);
            EditorGUILayout.Space(2f);
        }

        /// <summary>커스텀 인스펙터 중간에 직접 넣는 도움말 상자</summary>
        public static void Help(string text)
        {
            if (Show) GUILayout.Label(text, Box);
        }
    }

    [CustomPropertyDrawer(typeof(RSHelpAttribute))]
    public class RSHelpDrawer : DecoratorDrawer
    {
        public override float GetHeight()
        {
            if (!RSHelpGUI.Show) return 0f;
            var a = (RSHelpAttribute)attribute;
            float w = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 28f);
            // 인스펙터가 GUI 밖에서(UI Toolkit 레이아웃 계산 등) 높이를 물을 때는 에디터 스킨이 없어서
            // 이름 있는 스타일(EditorStyles.helpBox)을 만들 수 없다 → 글자 수로 어림한다
            if (Event.current == null) return Estimate(a.text, w);
            return RSHelpGUI.Box.CalcHeight(new GUIContent(a.text), w) + 6f;
        }

        static float Estimate(string text, float width)
        {
            float inner = Mathf.Max(40f, width - 16f);
            int lines = 0;
            foreach (var line in text.Split('\n'))
            {
                float px = 0f;
                foreach (char c in line) px += c < 128 ? 6.5f : 11f;
                lines += Mathf.Max(1, Mathf.CeilToInt(px / inner));
            }
            return lines * 14f + 10f + 6f;
        }

        public override void OnGUI(Rect position)
        {
            if (!RSHelpGUI.Show) return;
            var a = (RSHelpAttribute)attribute;
            position.y += 2f;
            position.height -= 4f;
            GUI.Label(position, a.text, RSHelpGUI.Box);
        }
    }
}
