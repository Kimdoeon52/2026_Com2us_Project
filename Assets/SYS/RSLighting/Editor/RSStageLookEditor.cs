// RE:AL STEEL - 스테이지 룩 인스펙터 (에디터)
//
//  · RSLookInspector: 조명 컴포넌트 인스펙터가 룩 값을 그릴 때, 스테이지 룩 프로필이 연결돼 있으면 프로필 쪽 값을 그린다
//  · 스테이지 룩(씬) 인스펙터: 프로필 끼우기, '지금 씬 값으로 새 프로필', '프로필 값을 씬으로 복사하고 끊기'
//  · 스테이지 룩 프로필(에셋) 인스펙터: 시스템별로 접히는 칸
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealSteel.Common.EditorTools;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSLookInspector
    {
        static SerializedObject profileSO;

        /// <summary>컴포넌트 종류 → 프로필 안 묶음 이름</summary>
        public static string Section(Object t)
        {
            if (t is RSTimeOfDay) return "timeOfDay";
            if (t is RSColorGrade) return "colorGrade";
            if (t is RSCloudShadow) return "clouds";
            if (t is RSLightShafts) return "shafts";
            if (t is RSFogVolume) return "fog";
            if (t is RSWetness) return "wetness";
            if (t is RSIndirectLight) return "indirect";
            if (t is RSCharacterGlow) return "characterGlow";
            return null;
        }

        public static SerializedProperty Redirect(SerializedProperty lookProp)
        {
            var p = RSStageLook.Current;
            if (p == null) return null;
            var t = lookProp.serializedObject.targetObject;
            if (t is RSFogVolume f && f.ownLook) return null;
            string sec = Section(t);
            if (sec == null) return null;
            if (profileSO == null || profileSO.targetObject != p) profileSO = new SerializedObject(p);
            return profileSO.FindProperty(sec);
        }

        /// <summary>조명 컴포넌트 인스펙터 본문 (요약 상자는 따로). 반환 = 값이 바뀌었는지</summary>
        public static bool Draw(Editor e, params string[] exclude)
        {
            return RSInspector.Draw(e.serializedObject, Redirect, exclude);
        }

        // ─────────────────────────────────────────────────────────────
        // 씬 값 ↔ 프로필
        // ─────────────────────────────────────────────────────────────

        /// <summary>씬의 조명 컴포넌트들의 자기 값(LocalLook)을 프로필에 복사</summary>
        public static int CopySceneToProfile(RSStageLook p)
        {
            Undo.RecordObject(p, "씬 값 → 스테이지 룩");
            int n = 0;
            var t = Object.FindAnyObjectByType<RSTimeOfDay>();        if (t != null) { Copy(t.LocalLook, p.timeOfDay); n++; }
            var g = Object.FindAnyObjectByType<RSColorGrade>();       if (g != null) { Copy(g.LocalLook, p.colorGrade); n++; }
            var c = Object.FindAnyObjectByType<RSCloudShadow>();      if (c != null) { Copy(c.LocalLook, p.clouds); n++; }
            var s = Object.FindAnyObjectByType<RSLightShafts>();      if (s != null) { Copy(s.LocalLook, p.shafts); n++; }
            var f = Object.FindAnyObjectByType<RSFogVolume>();        if (f != null) { Copy(f.LocalLook, p.fog); n++; }
            var w = Object.FindAnyObjectByType<RSWetness>();          if (w != null) { Copy(w.LocalLook, p.wetness); n++; }
            var i = Object.FindAnyObjectByType<RSIndirectLight>();    if (i != null) { Copy(i.LocalLook, p.indirect); n++; }
            var h = Object.FindAnyObjectByType<RSCharacterGlow>();    if (h != null) { Copy(h.LocalLook, p.characterGlow); n++; }
            EditorUtility.SetDirty(p);
            p.NotifyChanged();
            return n;
        }

        /// <summary>프로필 값을 씬의 조명 컴포넌트 자기 값으로 복사 (프로필을 떼어도 같은 룩이 남게)</summary>
        public static void CopyProfileToScene(RSStageLook p)
        {
            var objs = new List<Object>();
            void Rec(Object o) { if (o != null) objs.Add(o); }
            var t = Object.FindAnyObjectByType<RSTimeOfDay>(); Rec(t);
            var g = Object.FindAnyObjectByType<RSColorGrade>(); Rec(g);
            var c = Object.FindAnyObjectByType<RSCloudShadow>(); Rec(c);
            var s = Object.FindAnyObjectByType<RSLightShafts>(); Rec(s);
            var fogs = Object.FindObjectsByType<RSFogVolume>(FindObjectsSortMode.None); foreach (var f in fogs) if (!f.ownLook) Rec(f);
            var w = Object.FindAnyObjectByType<RSWetness>(); Rec(w);
            var i = Object.FindAnyObjectByType<RSIndirectLight>(); Rec(i);
            var h = Object.FindAnyObjectByType<RSCharacterGlow>(); Rec(h);
            if (objs.Count == 0) return;
            Undo.RecordObjects(objs.ToArray(), "스테이지 룩 → 씬 값");
            if (t != null) Copy(p.timeOfDay, t.LocalLook);
            if (g != null) Copy(p.colorGrade, g.LocalLook);
            if (c != null) Copy(p.clouds, c.LocalLook);
            if (s != null) Copy(p.shafts, s.LocalLook);
            foreach (var f in fogs) if (!f.ownLook) Copy(p.fog, f.LocalLook);
            if (w != null) Copy(p.wetness, w.LocalLook);
            if (i != null) Copy(p.indirect, i.LocalLook);
            if (h != null) Copy(p.characterGlow, h.LocalLook);
            foreach (var o in objs) EditorUtility.SetDirty(o);
        }

        static void Copy(object from, object to)
        {
            if (from == null || to == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), to);
        }

        public static RSStageLook CreateProfile(string name)
        {
            string dir = RSPaths.Ensure(RSPaths.Presets + "/StageLook");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}.asset");
            var p = ScriptableObject.CreateInstance<RSStageLook>();
            AssetDatabase.CreateAsset(p, path);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>씬에 스테이지 룩 컴포넌트가 없으면 host 에 붙인다 (프로필은 비워 둠)</summary>
        public static RSStageLookBinding EnsureBinding(GameObject host)
        {
            var b = Object.FindAnyObjectByType<RSStageLookBinding>();
            if (b == null && host != null) b = Undo.AddComponent<RSStageLookBinding>(host);
            return b;
        }
    }

    public static class RSStageLookMenu
    {
        /// <summary>스테이지 룩 컴포넌트를 붙이고, 프로필이 없으면 지금 씬 값으로 만들어 연결 ('스테이지 연출 한 번에 설치' 의 마지막 단계)</summary>
        public static void Setup()
        {
            var b = Object.FindAnyObjectByType<RSStageLookBinding>();
            if (b == null)
            {
                var tod = Object.FindAnyObjectByType<RSTimeOfDay>();
                GameObject host = tod != null ? tod.gameObject : GameObject.Find("LIGHTING_Extras");
                if (host == null)
                {
                    host = new GameObject("LIGHTING_Extras");
                    Undo.RegisterCreatedObjectUndo(host, "스테이지 룩");
                }
                b = RSLookInspector.EnsureBinding(host);
            }
            if (b.profile == null)
            {
                string scene = EditorSceneManager.GetActiveScene().name;
                var p = RSLookInspector.CreateProfile(string.IsNullOrEmpty(scene) ? "RS_StageLook" : scene + "_룩");
                int n = RSLookInspector.CopySceneToProfile(p);
                Undo.RecordObject(b, "스테이지 룩 연결");
                b.profile = p;
                EditorUtility.SetDirty(b);
                Debug.Log($"[스테이지 룩] '{AssetDatabase.GetAssetPath(p)}' 를 만들고 씬 값 {n} 묶음을 옮겼습니다. 씬을 저장하세요.", p);
            }
            Selection.activeGameObject = b.gameObject;
            EditorGUIUtility.PingObject(b.profile);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 스테이지 룩 (씬)
    // ═════════════════════════════════════════════════════════════════

    [CustomEditor(typeof(RSStageLookBinding))]
    public class RSStageLookBindingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var b = (RSStageLookBinding)target;
            RSInspector.Draw(serializedObject);

            EditorGUILayout.Space(4f);
            if (b.profile == null)
            {
                EditorGUILayout.HelpBox("프로필이 없어서 각 컴포넌트가 자기 값(씬에 저장)을 씁니다.\n" +
                    "아래 버튼으로 지금 씬 값을 프로필 에셋으로 옮기면, 다른 스테이지에 가져가거나 여럿이 나눠 고치기 쉬워집니다.", MessageType.Info);
                if (GUILayout.Button("지금 씬 값으로 새 프로필 만들기", GUILayout.Height(26f)))
                {
                    string scene = EditorSceneManager.GetActiveScene().name;
                    var p = RSLookInspector.CreateProfile(string.IsNullOrEmpty(scene) ? "RS_StageLook" : scene + "_룩");
                    int n = RSLookInspector.CopySceneToProfile(p);
                    Undo.RecordObject(b, "스테이지 룩 연결");
                    b.profile = p;
                    EditorUtility.SetDirty(b);
                    EditorGUIUtility.PingObject(p);
                    Debug.Log($"[스테이지 룩] '{AssetDatabase.GetAssetPath(p)}' 를 만들고 씬 값 {n} 묶음을 옮겼습니다. 이제 조명 컴포넌트에서 고친 값은 이 에셋에 저장됩니다.", p);
                }
                return;
            }

            EditorGUILayout.HelpBox($"시간대 · 색감 · 구름 · 햇살 · 안개 · 젖은 바닥 · 간접광 · 캐릭터 빛이 '{b.profile.name}' 값을 씁니다.\n" +
                "각 컴포넌트 인스펙터에서 고쳐도 이 에셋에 저장됩니다.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("프로필 복제 → 이 씬 전용", "지금 프로필을 복사해서 끼운다. 다른 스테이지 룩을 안 건드리고 고칠 때")))
                {
                    string src = AssetDatabase.GetAssetPath(b.profile);
                    string dst = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(src, null) + "_복제.asset");
                    if (AssetDatabase.CopyAsset(src, dst))
                    {
                        Undo.RecordObject(b, "스테이지 룩 복제");
                        b.profile = AssetDatabase.LoadAssetAtPath<RSStageLook>(dst);
                        EditorUtility.SetDirty(b);
                        EditorGUIUtility.PingObject(b.profile);
                    }
                }
                if (GUILayout.Button(new GUIContent("씬 값 → 프로필", "씬 컴포넌트에 남아 있는 자기 값으로 프로필을 덮어쓴다")) &&
                    EditorUtility.DisplayDialog("씬 값 → 프로필", $"'{b.profile.name}' 을 씬 컴포넌트들의 자기 값으로 덮어씁니다. (Ctrl+Z 가능)", "덮어쓰기", "취소"))
                    RSLookInspector.CopySceneToProfile(b.profile);
            }
            if (GUILayout.Button(new GUIContent("프로필 떼기 (값은 씬에 복사)", "프로필 값을 각 컴포넌트에 복사한 뒤 연결을 끊는다. 화면은 그대로")))
            {
                RSLookInspector.CopyProfileToScene(b.profile);
                Undo.RecordObject(b, "스테이지 룩 떼기");
                b.profile = null;
                EditorUtility.SetDirty(b);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 스테이지 룩 프로필 (에셋)
    // ═════════════════════════════════════════════════════════════════

    [CustomEditor(typeof(RSStageLook))]
    public class RSStageLookProfileEditor : Editor
    {
        static readonly (string field, string title, System.Type type)[] Sections =
        {
            ("timeOfDay", "시간대 (해 · 달 · 그늘 색 · 안개색 · 화면 색감)", typeof(RSTimeOfDay.Look)),
            ("colorGrade", "색감 (시간대별 암부 · 명부 · LUT)", typeof(RSColorGrade.Look)),
            ("clouds", "구름 그림자", typeof(RSCloudShadow.Look)),
            ("shafts", "햇살 빛줄기", typeof(RSLightShafts.Look)),
            ("fog", "볼류메트릭 안개", typeof(RSFogVolume.Look)),
            ("wetness", "젖은 바닥 · 반사", typeof(RSWetness.Look)),
            ("indirect", "간접광", typeof(RSIndirectLight.Look)),
            ("characterGlow", "캐릭터 밤 빛", typeof(RSCharacterGlow.Look)),
        };

        public override void OnInspectorGUI()
        {
            var p = (RSStageLook)target;
            serializedObject.Update();
            GUILayout.Label("스테이지 룩 프로필", EditorStyles.boldLabel);
            RSHelpGUI.Help("한 스테이지의 룩 값 모음. 씬의 '스테이지 룩' 컴포넌트에 끼우면 시간대 · 안개 · 젖은 바닥 · 간접광 등이 이 값을 쓴다.\n" +
                           "복제해서 '폐철장_낮' · '폐철장_밤_비' 처럼 여러 벌을 두고 바꿔 끼울 수 있다.");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("note"));
            bool inUse = RSStageLook.Current == p;
            EditorGUILayout.LabelField(inUse ? "● 지금 열린 씬이 이 프로필을 쓰는 중 (고치면 바로 보임)" : "○ 지금 열린 씬은 이 프로필을 안 씀", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            foreach (var s in Sections)
            {
                string k = "RSStageLook.section." + s.field;
                bool open = SessionState.GetBool(k, false);
                bool now = EditorGUILayout.BeginFoldoutHeaderGroup(open, s.title);
                EditorGUILayout.EndFoldoutHeaderGroup();
                if (now != open) SessionState.SetBool(k, now);
                if (!now) continue;
                EditorGUI.indentLevel++;
                RSInspector.DrawChildren(serializedObject.FindProperty(s.field), s.type, "StageLook." + s.field);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(6f);
            }
            if (serializedObject.ApplyModifiedProperties()) p.NotifyChanged();
        }
    }
}
