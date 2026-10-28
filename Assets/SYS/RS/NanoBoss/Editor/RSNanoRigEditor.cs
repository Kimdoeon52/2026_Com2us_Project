// RE:AL STEEL - 나노봇 리그 인스펙터
//
// 부위 표 (자리 · 붙은 봇 · 판정 · 발광 · 유체), 봇 합계, 부위 · 프리셋 · 발사기 · 플레이어 기준점 추가,
// 나노봇 동작 창 · 애니메이션 창 열기, 점검 요약.
using System.Collections.Generic;
using RealSteel.Common.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    [CustomEditor(typeof(RSNanoRig))]
    public class RSNanoRigEditor : Editor
    {
        public override bool RequiresConstantRepaint() => RSNanoRigPreview.Current == target;

        static GUIStyle mini;
        static GUIStyle Mini => mini ??= new GUIStyle(EditorStyles.miniLabel) { richText = true };

        public override void OnInspectorGUI()
        {
            var rig = (RSNanoRig)target;
            EditorGUILayout.HelpBox(
                "자식에 부위(줄기 · 덩어리 · 웅덩이) · 발사기를 두고, 애니메이션 창에서 부위의 위치 · 회전 · 크기와 값에 키를 잡는다.\n" +
                "키 포즈만 잡으면 사이는 봇이 메운다 — 메우는 성격은 부위의 반응 · 출렁임 · 끝 늦음으로 정한다.\n" +
                "창 → RE_AL STEEL → 나노봇 동작: 버튼 한 번에 키 · 행동 블록 · 포즈 · 점검 · 느린 재생 · 어니언 스킨.\n" +
                "+x = 플레이어 쪽 (플레이 중 반대면 자동 반전). 리그 원점 · 플레이어 기준점에는 키를 잡지 않는다.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("나노봇 동작 창", GUILayout.Height(24))) RSNanoActionWindow.Open(rig);
                if (GUILayout.Button("애니메이션 창", GUILayout.Height(24))) { EnsureAnimator(rig, null); RSNanoKeys.OpenAnimWindow(rig); }
            }
            EditorGUILayout.Space(4);

            DrawDefaultInspector();

            // ── 부위 표 ──
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("부위", EditorStyles.boldLabel);
            var limbs = rig.GetComponentsInChildren<RSNanoLimb>(true);
            int sum = 0, li = 0;
            foreach (var l in limbs)
            {
                sum += l.Desired;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(l.name, EditorStyles.linkLabel, GUILayout.Width(110))) Selection.activeGameObject = l.gameObject;
                    GUILayout.Label(TypeName(l.type), Mini, GUILayout.Width(40));
                    Rect r = GUILayoutUtility.GetRect(60, 14, GUILayout.ExpandWidth(true));
                    int slots = rig.Active ? rig.SlotCountOf(li) : l.bots;
                    FillBar(r, rig.Active ? l.assigned : 0, l.Desired, Mathf.Max(1, slots), l.danger ? new Color(1f, 0.4f, 0.35f) : l.bind < 0.5f ? new Color(0.45f, 0.7f, 1f) : new Color(0.5f, 0.85f, 0.95f));
                    string flags = (l.danger ? "<color=#ff7766>판정</color> " : "") + (l.glow > 0.05f ? "<color=#ffdd55>발광</color> " : "") + (l.bind < 0.5f ? "<color=#77aaff>유체</color>" : "");
                    GUILayout.Label(flags, Mini, GUILayout.Width(70));
                }
                li++;
            }
            var sw = rig.swarm ? rig.swarm : rig.GetComponentInParent<RSNanoSwarm>();
            EditorGUILayout.LabelField($"지금 원하는 봇 {sum}" + (sw ? $" / 군집 {sw.botCount}" : "  (군집 없음)") +
                                       (sw && sum > sw.botCount * 0.85f ? "  <color=#ffaa55>→ 덩어리에 남는 봇이 거의 없다</color>" : ""), Mini);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 줄기")) Select(AddChain(rig.transform, "줄기", 3, new Vector3(1f, 0f, 0f), Vector3.zero, sw));
                if (GUILayout.Button("+ 덩어리")) Select(AddLimb(rig.transform, "덩어리", LimbType.Blob, Vector3.zero, sw));
                if (GUILayout.Button("+ 웅덩이")) Select(AddLimb(rig.transform, "웅덩이", LimbType.Puddle, new Vector3(3f, -2.1f, 0f), sw));
                if (GUILayout.Button("+ 발사기")) Select(AddEmitter(rig.transform, "발사기", Vector3.right));
            }
            if (GUILayout.Button(new GUIContent("+ 그림 (스프라이트 시트 따라가기)", "픽셀아트 시트를 넣으면 어두운 픽셀을 봇이 따라간다")))
                Select(AddSprite(rig.transform, "그림", new Vector3(1.5f, 0f, 0f), sw));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 부위 프리셋 ▾")) RSNanoLimbPresets.Menu(rig, Selection.activeTransform);
                using (new EditorGUI.DisabledScope(rig.playerAnchor))
                    if (GUILayout.Button("+ 플레이어 기준점")) EnsurePlayerAnchor(rig);
                if (GUILayout.Button("애니메이션 준비")) EnsureAnimator(rig, null);
            }

            // ── 점검 요약 ──
            var clip = RSNanoKeys.CurrentClip(rig);
            var issues = RSNanoRigCheck.Run(rig, clip);
            int warn = 0;
            foreach (var i in issues) if (i.level >= 1) warn++;
            if (warn > 0)
            {
                EditorGUILayout.Space(4);
                int shown = 0;
                foreach (var i in issues)
                {
                    if (i.level < 1) continue;
                    EditorGUILayout.HelpBox(i.text, i.level >= 2 ? MessageType.Error : MessageType.Warning);
                    if (++shown >= 3) break;
                }
                if (warn > shown) EditorGUILayout.LabelField($"… 외 {warn - shown}개 — 나노봇 동작 창 → 점검", Mini);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(RSNanoRigPreview.Current != rig))
                if (GUILayout.Button("미리보기 다시 시작 (봇 새로)")) RSNanoRigPreview.Restart();
            if (RSNanoRigPreview.Current == rig && rig.swarm)
                EditorGUILayout.LabelField(RSNanoRigPreview.Scrubbing
                        ? $"미리보기: 애니메이션 창 프레임 {RSNanoRigPreview.SimFrame} (다시 계산 {RSNanoRigPreview.LastResimMs:0}ms)"
                        : "미리보기: 실시간 (애니메이션 창에서 미리보기를 켜면 프레임 고정)", Mini);
        }

        public static void FillBar(Rect r, int have, int want, int max, Color c)
        {
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.25f));
            float k = r.width / Mathf.Max(1, max);
            EditorGUI.DrawRect(new Rect(r.x, r.y, want * k, r.height), new Color(c.r, c.g, c.b, 0.35f));
            EditorGUI.DrawRect(new Rect(r.x, r.y + 3, have * k, r.height - 6), c);
            GUI.Label(r, $" {have}/{want}", EditorStyles.miniLabel);
        }

        static string TypeName(LimbType t) => t == LimbType.Chain ? "줄기" : t == LimbType.Blob ? "덩어리" : t == LimbType.Sprite ? "그림" : "웅덩이";

        static void Select(Component c) { if (c) Selection.activeGameObject = c.gameObject; }

        // ─────────────────────────────────────────────────────────────
        // 만들기 도우미 (프리셋 · 템플릿도 쓴다)
        // ─────────────────────────────────────────────────────────────
        public static float Spacing(RSNanoSwarm sw) => sw ? sw.spacing : 0.1f;

        public static RSNanoLimb AddLimb(Transform parent, string name, LimbType type, Vector3 local, RSNanoSwarm sw = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "나노봇 부위");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = Undo.AddComponent<RSNanoLimb>(go);
            l.type = type;
            if (type == LimbType.Blob) { l.ApplyPreset(RSNanoLimb.Presets[1]); }
            if (type == LimbType.Puddle) { l.ApplyPreset(RSNanoLimb.Presets[0]); l.bind = 0f; l.flow = 0.2f; }
            AutoBots(l, sw);
            return l;
        }

        /// <summary>관절 체인 줄기. steps = 관절마다 앞 관절 기준 위치 (하나면 모두 같은 간격)</summary>
        public static RSNanoLimb AddChain(Transform parent, string name, int joints, Vector3 step, Vector3 local, RSNanoSwarm sw = null, float[] thickness = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "나노봇 부위");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = Undo.AddComponent<RSNanoLimb>(go);
            l.type = LimbType.Chain;
            Transform t = l.transform;
            var jc = Undo.AddComponent<RSNanoJoint>(go);
            jc.thickness = thickness != null && thickness.Length > 0 ? thickness[0] : 1f;
            for (int i = 1; i <= joints; i++)
            {
                var j = new GameObject("관절" + i);
                Undo.RegisterCreatedObjectUndo(j, "나노봇 관절");
                j.transform.SetParent(t, false);
                j.transform.localPosition = step;
                var c = j.AddComponent<RSNanoJoint>();
                c.thickness = thickness != null && i < thickness.Length ? thickness[i] : Mathf.Lerp(1f, 0.15f, i / (float)joints);
                t = j.transform;
            }
            l.RefreshJoints();
            AutoBots(l, sw);
            return l;
        }

        public static void AutoBots(RSNanoLimb l, RSNanoSwarm sw)
        {
            if (!sw) sw = l.GetComponentInParent<RSNanoSwarm>();
            float floor = sw ? (sw.floorFromTransform ? sw.transform.position.y : sw.floorY) : l.transform.position.y - 2.1f;
            l.bots = l.EstimateBots(Spacing(sw), floor);
        }

        public static RSNanoLimb AddSprite(Transform parent, string name, Vector3 local, RSNanoSwarm sw, Texture2D sheet = null)
        {
            var l = AddLimb(parent, name, LimbType.Sprite, local, sw);
            l.ApplyPreset(RSNanoLimb.Presets[2]);
            l.pixelSize = Spacing(sw);
            if (sheet) l.picture = RSNanoSpriteBaker.CreateOrFind(sheet);
            l.bots = Mathf.Max(1, l.SlotTotal);
            return l;
        }

        public static RSNanoEmitter AddEmitter(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "나노봇 발사기");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return Undo.AddComponent<RSNanoEmitter>(go);
        }

        public static Transform EnsurePlayerAnchor(RSNanoRig rig)
        {
            if (rig.playerAnchor) return rig.playerAnchor;
            var go = new GameObject("플레이어");
            Undo.RegisterCreatedObjectUndo(go, "플레이어 기준점");
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = rig.previewOffset;
            Undo.RecordObject(rig, "플레이어 기준점");
            rig.playerAnchor = go.transform;
            return go.transform;
        }

        static string RigDir => RSPaths.Ensure(RSPaths.Data + "/NanoBoss/리그");

        /// <summary>애니메이션 창에서 클립을 만들 수 있게 애니메이터 + 컨트롤러를 준비한다 (컨트롤러는 편집용)</summary>
        public static AnimatorController EnsureAnimator(RSNanoRig rig, AnimationClip clip)
        {
            var an = rig.GetComponent<Animator>();
            if (!an) an = Undo.AddComponent<Animator>(rig.gameObject);
            var ctrl = an.runtimeAnimatorController as AnimatorController;
            if (!ctrl)
            {
                string path = AssetDatabase.GenerateUniqueAssetPath(RigDir + "/" + rig.name + ".controller");
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
                Undo.RecordObject(an, "애니메이터");
                an.runtimeAnimatorController = ctrl;
            }
            if (clip)
            {
                bool has = false;
                foreach (var c in ctrl.animationClips) if (c == clip) has = true;
                if (!has) ctrl.AddMotion(clip);
            }
            return ctrl;
        }
    }
}
