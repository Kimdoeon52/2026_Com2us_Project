// RE:AL STEEL - 화면 효과(Volume) 진단 (에디터)
//
// "화면이 왜 보라색이지?" 를 찾을 때. 시간대 · 색감 · 자동 초점이 만드는 숨은 Volume 까지 포함해서
//  · 씬의 모든 Volume (주인 · 우선순위 · 세기 · 전역/범위)
//  · 효과 칸마다 누가 덮고 있는지(우선순위 순) + 카메라가 실제로 받는 최종 값
// 을 보여 준다. 메뉴: Tools → RE_AL STEEL → Stage → 화면 효과 진단 (Volume)
//
// 각 주인 컴포넌트 인스펙터에도 '이 컴포넌트가 화면에 덮는 값' 칸을 그린다 (RSVolumeReport.DrawOwned).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common.EditorTools;
using Object = UnityEngine.Object;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSVolumeReport
    {
        // ─────────────────────────────────────────────────────────────
        // 값 읽기
        // ─────────────────────────────────────────────────────────────

        /// <summary>효과 하나의 (칸 이름, 파라미터) 목록 — 선언 순서</summary>
        public static List<(string name, VolumeParameter p)> Params(VolumeComponent c)
        {
            var list = new List<(string, VolumeParameter)>();
            if (c == null) return list;
            foreach (var f in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (typeof(VolumeParameter).IsAssignableFrom(f.FieldType) && f.GetValue(c) is VolumeParameter vp)
                    list.Add((ObjectNames.NicifyVariableName(f.Name), vp));
            return list;
        }

        public static object Value(VolumeParameter p)
        {
            var prop = p.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public);
            return prop != null ? prop.GetValue(p) : null;
        }

        public static string Format(object v)
        {
            switch (v)
            {
                case null: return "없음";
                case float f: return f.ToString("0.###");
                case bool b: return b ? "켬" : "끔";
                case Color c: return $"({c.r:0.##}, {c.g:0.##}, {c.b:0.##})";
                case Vector4 v4: return $"({v4.x:0.##}, {v4.y:0.##}, {v4.z:0.##}, {v4.w:0.##})";
                case Vector2 v2: return $"({v2.x:0.##}, {v2.y:0.##})";
                case Texture t: return t.name;
                case Object o: return o != null ? o.name : "없음";
                default: return v.ToString();
            }
        }

        public static string TypeLabel(Type t)
        {
            return ObjectNames.NicifyVariableName(t.Name);
        }

        /// <summary>이 Volume 을 만든 RS 컴포넌트 (없으면 null)</summary>
        public static IRSVolumeOwner OwnerOf(Volume v)
        {
            if (v == null) return null;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb is IRSVolumeOwner o && o.OwnedVolume == v) return o;
            return null;
        }

        // ─────────────────────────────────────────────────────────────
        // 주인 컴포넌트 인스펙터: 이 컴포넌트가 화면에 덮는 값
        // ─────────────────────────────────────────────────────────────

        public static void DrawOwned(IRSVolumeOwner owner)
        {
            const string Key = "RSVolumeReport.owned";
            bool open = SessionState.GetBool(Key, false);
            bool now = EditorGUILayout.Foldout(open, "이 컴포넌트가 화면에 덮는 값 (숨은 Volume)", true, EditorStyles.foldoutHeader);
            if (now != open) SessionState.SetBool(Key, now);
            if (!now) return;

            var v = owner.OwnedVolume;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(owner.VolumeRole, EditorStyles.wordWrappedMiniLabel);
                if (v == null || v.sharedProfile == null)
                {
                    GUILayout.Label("지금은 Volume 이 없습니다 (꺼져 있거나 세기 0).", EditorStyles.miniLabel);
                }
                else
                {
                    GUILayout.Label($"'{v.name}' · {(v.enabled ? "켜짐" : "꺼짐")} · 우선순위 {v.priority:0.#} · 세기 {v.weight:0.##} · {(v.isGlobal ? "전역" : "범위")} · 씬에 저장 안 됨",
                        EditorStyles.miniBoldLabel);
                    int shown = 0;
                    foreach (var c in v.sharedProfile.components)
                    {
                        if (c == null || !c.active) continue;
                        var ps = Params(c).Where(x => x.p.overrideState).ToList();
                        if (ps.Count == 0) continue;
                        shown++;
                        GUILayout.Label("■ " + TypeLabel(c.GetType()), EditorStyles.miniBoldLabel);
                        foreach (var (name, p) in ps)
                            GUILayout.Label($"     {name}: {Format(Value(p))}", EditorStyles.miniLabel);
                    }
                    if (shown == 0) GUILayout.Label("덮는 칸이 없습니다.", EditorStyles.miniLabel);
                }
                if (GUILayout.Button("화면 효과 진단 창 열기 (모든 Volume · 누가 이기는지)", EditorStyles.miniButton))
                    RSVolumeDiagnosisWindow.Open();
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 화면 효과 진단 창
    // ═════════════════════════════════════════════════════════════════

    public class RSVolumeDiagnosisWindow : EditorWindow
    {
        [MenuItem("Tools/RE_AL STEEL/Stage/화면 효과 진단 (Volume)", false, 41)]
        public static void Open()
        {
            var w = GetWindow<RSVolumeDiagnosisWindow>("화면 효과 진단");
            w.minSize = new Vector2(420f, 300f);
            w.Show();
        }

        Camera cam;
        VolumeStack stack;
        Vector2 scroll;
        bool onlyOverridden = true;
        string filter = "";

        void OnDisable() { if (stack != null) { stack.Dispose(); stack = null; } }
        void OnInspectorUpdate() { Repaint(); }

        void OnGUI()
        {
            if (cam == null) cam = RSAutoFocus.FindViewCamera();
            using (new EditorGUILayout.HorizontalScope())
            {
                cam = (Camera)EditorGUILayout.ObjectField("기준 카메라", cam, typeof(Camera), true);
                if (GUILayout.Button("보이는 카메라", EditorStyles.miniButton, GUILayout.Width(80f))) cam = RSAutoFocus.FindViewCamera();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                filter = EditorGUILayout.TextField("효과 이름 찾기", filter);
                onlyOverridden = GUILayout.Toggle(onlyOverridden, "덮는 칸만", EditorStyles.miniButton, GUILayout.Width(70f));
            }
            RSHelpGUI.Help("우선순위가 높은 Volume 의 값이 이긴다 (세기 1 미만이면 아래 것과 섞임). 'RS 자동' 은 컴포넌트가 만든 숨은 Volume — 그 값은 주인 컴포넌트에서 바꾼다.");

            var vols = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .Where(v => v != null && v.gameObject.scene.IsValid())
                             .OrderByDescending(v => v.priority).ThenByDescending(v => v.weight).ToList();
            var owners = new Dictionary<Volume, IRSVolumeOwner>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb is IRSVolumeOwner o && o.OwnedVolume != null) owners[o.OwnedVolume] = o;

            // 카메라가 받는 최종 값
            if (cam != null && VolumeManager.instance.isInitialized)
            {
                if (stack == null) stack = VolumeManager.instance.CreateStack();
                var ad = cam.GetComponent<UniversalAdditionalCameraData>();
                LayerMask mask = ad != null ? ad.volumeLayerMask : (LayerMask)1;
                Transform trigger = ad != null && ad.volumeTrigger != null ? ad.volumeTrigger : cam.transform;
                VolumeManager.instance.Update(stack, trigger, mask);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            // 1) Volume 목록
            GUILayout.Label($"Volume {vols.Count}개 (우선순위 높은 순)", EditorStyles.boldLabel);
            foreach (var v in vols)
            {
                owners.TryGetValue(v, out var owner);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string who = owner != null ? $"RS 자동 · {((Component)owner).GetType().Name} ('{((Component)owner).name}')" : "씬";
                    string state = v.isActiveAndEnabled ? "" : " · 꺼짐";
                    GUILayout.Label($"{v.name}\n   {who} · {(v.isGlobal ? "전역" : "범위")} · 우선순위 {v.priority:0.#} · 세기 {v.weight:0.##}{state} · 프로필 {(v.sharedProfile != null ? v.sharedProfile.name : "없음")}",
                        EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button(owner != null ? "주인 선택" : "선택", EditorStyles.miniButton, GUILayout.Width(64f)))
                    {
                        var go = owner != null ? ((Component)owner).gameObject : v.gameObject;
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                }
            }

            // 2) 효과별
            EditorGUILayout.Space(6f);
            GUILayout.Label("효과별 — 누가 덮고 있나 (★ = 지금 이기는 쪽, 전역 기준)", EditorStyles.boldLabel);
            var types = new SortedDictionary<string, Type>();
            foreach (var v in vols)
                if (v.sharedProfile != null)
                    foreach (var c in v.sharedProfile.components)
                        if (c != null) types[TypeLabelOf(c.GetType())] = c.GetType();

            foreach (var kv in types)
            {
                if (!string.IsNullOrEmpty(filter) && kv.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                DrawType(kv.Key, kv.Value, vols, owners);
            }
            EditorGUILayout.EndScrollView();
        }

        static string TypeLabelOf(Type t) { return RSVolumeReport.TypeLabel(t); }

        void DrawType(string label, Type type, List<Volume> vols, Dictionary<Volume, IRSVolumeOwner> owners)
        {
            // 이 효과를 가진 (켜진) Volume 들
            var users = new List<(Volume v, VolumeComponent c)>();
            foreach (var v in vols)
            {
                if (!v.isActiveAndEnabled || v.weight <= 0f || v.sharedProfile == null) continue;
                foreach (var c in v.sharedProfile.components)
                    if (c != null && c.GetType() == type && c.active) users.Add((v, c));
            }
            if (onlyOverridden && users.All(u => !RSVolumeReport.Params(u.c).Any(p => p.p.overrideState))) return;

            string k = "RSVolumeDiag." + type.FullName;
            bool open = SessionState.GetBool(k, false);
            bool now = EditorGUILayout.Foldout(open, $"{label}  ({users.Count})", true);
            if (now != open) SessionState.SetBool(k, now);
            if (!now) return;

            VolumeComponent final = stack != null ? stack.GetComponent(type) : null;
            var finalParams = final != null ? RSVolumeReport.Params(final) : null;
            var names = RSVolumeReport.Params(users.Count > 0 ? users[0].c : final);

            EditorGUI.indentLevel++;
            for (int i = 0; i < names.Count; i++)
            {
                string pname = names[i].name;
                var rows = new List<string>();
                bool winnerMarked = false;
                foreach (var (v, c) in users)
                {
                    var ps = RSVolumeReport.Params(c);
                    if (i >= ps.Count || !ps[i].p.overrideState) continue;
                    owners.TryGetValue(v, out var owner);
                    bool win = !winnerMarked && v.isGlobal;
                    if (win) winnerMarked = true;
                    rows.Add($"{(win ? "★" : "  ")} {v.name}{(owner != null ? " (RS 자동)" : "")} · 우선순위 {v.priority:0.#} · 세기 {v.weight:0.##}{(v.isGlobal ? "" : " · 범위 안일 때")}: {RSVolumeReport.Format(RSVolumeReport.Value(ps[i].p))}");
                }
                if (onlyOverridden && rows.Count == 0) continue;
                string fin = finalParams != null && i < finalParams.Count ? RSVolumeReport.Format(RSVolumeReport.Value(finalParams[i].p)) : "-";
                EditorGUILayout.LabelField(pname, $"최종 {fin}", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                if (rows.Count == 0) EditorGUILayout.LabelField("(아무도 안 덮음 — 기본값)", EditorStyles.miniLabel);
                foreach (var r in rows) EditorGUILayout.LabelField(r, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2f);
        }
    }
}
