// RE:AL STEEL - 접히는 인스펙터 (공용)
//
// RS 컴포넌트 인스펙터를 이렇게 그린다:
//   ┌ 자주 쓰는 것   ← [RSKey] 붙은 칸 (늘 펼침, 3 ~ 5개)
//   ├ 묶음 없는 칸   ← 맨 앞 [RSGroup] 전의 칸 (연결 · 대상 등)
//   ├ ▶ 묶음 A (6)  ← [RSGroup("묶음 A")] 부터 다음 묶음 전까지. 처음엔 접힘
//   └ ▶ 묶음 B (4)
// [RSLook] 필드(룩 값 묶음)는 안쪽 칸을 같은 방식으로 펼친다. 다른 곳(스테이지 룩 프로필)으로 돌려 그릴 수도 있다.
//
// 접힘 상태는 에디터 세션 동안 기억한다 (컴포넌트 종류 · 묶음 이름별).
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealSteel.Common.EditorTools
{
    public static class RSInspector
    {
        /// <summary>[RSLook] 칸을 다른 곳(예: 프로필 에셋의 같은 묶음)으로 돌려 그릴 때. null 이면 제자리</summary>
        public delegate SerializedProperty LookRedirect(SerializedProperty lookProp);

        struct Entry
        {
            public SerializedProperty prop;
            public string group;
            public bool groupOpen;
            public bool key;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static FieldInfo FieldOf(Type t, string name)
        {
            for (; t != null && t != typeof(object); t = t.BaseType)
            {
                var f = t.GetField(name, Flags);
                if (f != null) return f;
            }
            return null;
        }

        static GUIStyle groupStyle, keyTitle;
        static GUIStyle GroupStyle
        {
            get
            {
                if (groupStyle == null) groupStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
                return groupStyle;
            }
        }
        static GUIStyle KeyTitle
        {
            get
            {
                if (keyTitle == null) keyTitle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.45f, 0.75f, 1f) } };
                return keyTitle;
            }
        }

        /// <summary>
        /// 컴포넌트 전체를 그린다. 반환 = 값이 바뀌었는지.
        /// exclude: 따로 그릴 칸 이름 (m_Script 는 늘 뺀다)
        /// </summary>
        public static bool Draw(SerializedObject so, LookRedirect redirect = null, params string[] exclude)
        {
            bool pending = so.ApplyModifiedProperties();   // 위에서 고친 값이 있으면 먼저 반영
            so.Update();
            var others = new List<SerializedObject>();
            var entries = new List<Entry>();
            var type = so.targetObject.GetType();
            string group = null; bool open = false;

            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script" || Array.IndexOf(exclude, it.name) >= 0) continue;
                var fi = FieldOf(type, it.name);
                if (fi != null && fi.IsDefined(typeof(RSLookAttribute), true))
                {
                    var src = it.Copy();
                    if (redirect != null && so.targetObjects.Length == 1)
                    {
                        var r = redirect(src);
                        if (r != null)
                        {
                            src = r;
                            if (!others.Contains(r.serializedObject)) { r.serializedObject.Update(); others.Add(r.serializedObject); }
                            entries.Add(new Entry { prop = null, group = "\u0001" + r.serializedObject.targetObject.name, key = false });
                        }
                    }
                    Collect(entries, src, fi.FieldType, ref group, ref open);
                    continue;
                }
                Add(entries, it.Copy(), fi, ref group, ref open);
            }

            DrawEntries(entries, type.Name);

            bool changed = so.ApplyModifiedProperties() | pending;
            foreach (var o in others) changed |= o.ApplyModifiedProperties();
            return changed;
        }

        /// <summary>[RSLook] 같은 묶음 칸 하나의 안쪽을 그린다 (프로필 에셋 인스펙터용). scope = 접힘 상태 구분 이름</summary>
        public static void DrawChildren(SerializedProperty parent, Type parentType, string scope)
        {
            var entries = new List<Entry>();
            string group = null; bool open = false;
            Collect(entries, parent, parentType, ref group, ref open);
            DrawEntries(entries, scope);
        }

        static void Collect(List<Entry> entries, SerializedProperty parent, Type parentType, ref string group, ref bool open)
        {
            var c = parent.Copy();
            var end = parent.GetEndProperty();
            bool enter = true;
            while (c.NextVisible(enter) && !SerializedProperty.EqualContents(c, end))
            {
                enter = false;
                Add(entries, c.Copy(), FieldOf(parentType, c.name), ref group, ref open);
            }
        }

        static void Add(List<Entry> entries, SerializedProperty p, FieldInfo fi, ref string group, ref bool open)
        {
            bool key = false;
            if (fi != null)
            {
                var g = fi.GetCustomAttribute<RSGroupAttribute>(true);
                if (g != null) { group = g.title; open = g.open; }
                key = fi.IsDefined(typeof(RSKeyAttribute), true);
            }
            entries.Add(new Entry { prop = p, group = group, groupOpen = open, key = key });
        }

        static void DrawEntries(List<Entry> entries, string scope)
        {
            // 1) 자주 쓰는 것
            bool anyKey = false;
            foreach (var e in entries) if (e.key) { anyKey = true; break; }
            if (anyKey)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("자주 쓰는 것", KeyTitle);
                    foreach (var e in entries) if (e.key && e.prop != null) EditorGUILayout.PropertyField(e.prop, true);
                }
                EditorGUILayout.Space(2f);
            }

            // 2) 묶음 없는 칸 · 묶음 순서
            var order = new List<string>();
            var openDefault = new Dictionary<string, bool>();
            foreach (var e in entries)
            {
                if (e.prop == null)
                {
                    order.Add(e.group);   // 프로필 안내 표시 자리
                    continue;
                }
                if (e.key) continue;
                if (e.group == null) { EditorGUILayout.PropertyField(e.prop, true); continue; }
                if (!openDefault.ContainsKey(e.group)) { openDefault[e.group] = e.groupOpen; order.Add(e.group); }
            }
            if (order.Count == 0) return;

            // 모두 펼치기 · 접기
            int groups = 0;
            foreach (var g in order) if (g[0] != '\u0001') groups++;
            if (groups >= 3)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("모두 펼치기", EditorStyles.miniButtonLeft, GUILayout.Width(70f))) SetAll(order, scope, true);
                    if (GUILayout.Button("모두 접기", EditorStyles.miniButtonRight, GUILayout.Width(60f))) SetAll(order, scope, false);
                }
            }

            foreach (var g in order)
            {
                if (g[0] == '\u0001')
                {
                    // 룩 값이 프로필 에셋에 있다는 안내
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label($"아래 룩 값은 스테이지 룩 프로필 '{g.Substring(1)}' 에 저장됩니다 (이 씬은 안 바뀜)", EditorStyles.wordWrappedMiniLabel);
                    }
                    continue;
                }
                int n = 0;
                foreach (var e in entries) if (e.prop != null && !e.key && e.group == g) n++;
                if (n == 0) continue;

                string k = "RSInspector." + scope + "." + g;
                bool isOpen = SessionState.GetBool(k, openDefault[g]);
                bool now = EditorGUILayout.Foldout(isOpen, $"{g}  ({n})", true, GroupStyle);
                if (now != isOpen) SessionState.SetBool(k, now);
                if (!now) continue;
                EditorGUI.indentLevel++;
                foreach (var e in entries)
                    if (e.prop != null && !e.key && e.group == g) EditorGUILayout.PropertyField(e.prop, true);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
            }
        }

        static void SetAll(List<string> order, string scope, bool open)
        {
            foreach (var g in order)
                if (g[0] != '\u0001') SessionState.SetBool("RSInspector." + scope + "." + g, open);
        }
    }

    /// <summary>
    /// 따로 인스펙터가 없는 RS 컴포넌트용 기본 인스펙터 (요약 상자 + 접히는 묶음).
    /// 쓰는 법: [CustomEditor(typeof(내컴포넌트))] class 내컴포넌트Editor : RSGroupedEditor { }
    /// </summary>
    public class RSGroupedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            RSInspector.Draw(serializedObject);
        }
    }
}
