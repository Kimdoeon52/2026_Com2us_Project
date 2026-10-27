// RE:AL STEEL - 나노봇 리그: 키 넣기 도우미 (동작 창 · 행동 블록 · 포즈 · 템플릿이 함께 쓴다)
//
// 애니메이션 클립에 직접 커브를 쓴다 (기록 모드를 켜지 않아도 된다). 같은 시각에 키가 있으면 바꾼다.
// 판정 · 고정 · 발사 같은 켜고 끄는 값은 계단(Constant) 키, 나머지는 부드러운(ClampedAuto) 키.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealSteel.NanoBoss.EditorTools
{
    public static class RSNanoKeys
    {
        public const float Fps = 60f;
        public static float F(float frames) => frames / Fps;

        // ─────────────────────────────────────────────────────────────
        // 애니메이션 창
        // ─────────────────────────────────────────────────────────────
        static AnimationWindow cachedWindow;
        static double cachedAt;

        /// <summary>열려 있는 애니메이션 창 (없으면 null). 포커스를 빼앗지 않는다</summary>
        public static AnimationWindow AnimWindow()
        {
            if (cachedWindow) return cachedWindow;
            if (EditorApplication.timeSinceStartup - cachedAt < 0.5) return null;
            cachedAt = EditorApplication.timeSinceStartup;
            var all = Resources.FindObjectsOfTypeAll<AnimationWindow>();
            cachedWindow = all.Length > 0 ? all[0] : null;
            return cachedWindow;
        }

        public static void OpenAnimWindow(RSNanoRig rig)
        {
            if (rig) Selection.activeGameObject = rig.gameObject;
            EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
            cachedWindow = null;
            cachedAt = 0;
        }

        /// <summary>리그 애니메이터 컨트롤러에 든 클립들</summary>
        public static AnimationClip[] ClipsOf(RSNanoRig rig)
        {
            var an = rig ? rig.GetComponent<Animator>() : null;
            var ctrl = an ? an.runtimeAnimatorController : null;
            return ctrl ? ctrl.animationClips : Array.Empty<AnimationClip>();
        }

        public static bool ClipBelongs(RSNanoRig rig, AnimationClip clip)
        {
            if (!rig || !clip) return false;
            foreach (var c in ClipsOf(rig)) if (c == clip) return true;
            return false;
        }

        /// <summary>지금 편집 중인 클립 — 애니메이션 창의 클립이 이 리그 것이면 그것, 아니면 리그의 첫 클립</summary>
        public static AnimationClip CurrentClip(RSNanoRig rig)
        {
            var aw = AnimWindow();
            if (aw && aw.animationClip && ClipBelongs(rig, aw.animationClip)) return aw.animationClip;
            var clips = ClipsOf(rig);
            return clips.Length > 0 ? clips[0] : null;
        }

        /// <summary>애니메이션 창의 지금 시각 (초)</summary>
        public static float Now()
        {
            var aw = AnimWindow();
            return aw ? aw.time : 0f;
        }

        public static void SetNow(float t)
        {
            var aw = AnimWindow();
            if (!aw) return;
            if (!aw.previewing && aw.canPreview) aw.previewing = true;
            aw.time = Mathf.Max(0f, t);
            aw.Repaint();
        }

        // ─────────────────────────────────────────────────────────────
        // 커브
        // ─────────────────────────────────────────────────────────────
        public static string PathOf(Transform root, Transform t) => AnimationUtility.CalculateTransformPath(t, root);

        static Transform TransformOf(Object target) =>
            target is Component c ? c.transform : target is GameObject g ? g.transform : null;

        public static EditorCurveBinding Binding(Transform root, Object target, string prop)
        {
            var t = TransformOf(target);
            Type type = target is Transform ? typeof(Transform) : target is GameObject ? typeof(GameObject) : target.GetType();
            return EditorCurveBinding.FloatCurve(PathOf(root, t), type, prop);
        }

        public static AnimationCurve Curve(AnimationClip clip, Transform root, Object target, string prop) =>
            clip && target ? AnimationUtility.GetEditorCurve(clip, Binding(root, target, prop)) : null;

        /// <summary>시각 t 의 값 — 커브가 있으면 커브, 없으면 지금 값</summary>
        public static float ValueAt(AnimationClip clip, Transform root, Object target, string prop, float t, float fallback)
        {
            var c = Curve(clip, root, target, prop);
            return c != null && c.length > 0 ? c.Evaluate(t) : fallback;
        }

        public static void BeginEdit(AnimationClip clip, string label)
        {
            if (clip) Undo.RegisterCompleteObjectUndo(clip, label);
        }

        public static void EndEdit(AnimationClip clip)
        {
            if (!clip) return;
            EditorUtility.SetDirty(clip);
            var aw = AnimWindow();
            if (aw) aw.Repaint();
            RSNanoRigPreview.MarkDirty();
        }

        static void ApplyModes(AnimationCurve c, int i, bool step)
        {
            var mode = step ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.ClampedAuto;
            AnimationUtility.SetKeyLeftTangentMode(c, i, mode);
            AnimationUtility.SetKeyRightTangentMode(c, i, mode);
        }

        /// <summary>키 하나 (같은 시각 키는 바꾼다)</summary>
        public static void Key(AnimationClip clip, Transform root, Object target, string prop, float t, float v, bool step = false)
        {
            if (!clip || !target) return;
            var b = Binding(root, target, prop);
            var c = AnimationUtility.GetEditorCurve(clip, b) ?? new AnimationCurve();
            for (int k = c.length - 1; k >= 0; k--) if (Mathf.Abs(c[k].time - t) < 0.5f / Fps) c.RemoveKey(k);
            int idx = c.AddKey(new Keyframe(t, v));
            if (idx >= 0) ApplyModes(c, idx, step);
            // 이웃 자동 접선 다시 계산
            for (int k = 0; k < c.length; k++)
                if (k != idx && AnimationUtility.GetKeyLeftTangentMode(c, k) == AnimationUtility.TangentMode.ClampedAuto)
                    ApplyModes(c, k, false);
            AnimationUtility.SetEditorCurve(clip, b, c);
        }

        public static void Bool(AnimationClip clip, Transform root, Object target, string prop, float t, bool v)
        {
            // 계단 키는 앞이 비어 있으면 0 에서 시작하게 둔다
            var c = Curve(clip, root, target, prop);
            if (v && (c == null || c.length == 0) && t > 0.5f / Fps) Key(clip, root, target, prop, 0f, 0f, true);
            Key(clip, root, target, prop, t, v ? 1f : 0f, true);
        }

        /// <summary>커브를 통째로 (템플릿)</summary>
        public static void SetCurve(AnimationClip clip, Transform root, Object target, string prop, bool step, params (float t, float v)[] keys)
        {
            var c = new AnimationCurve();
            foreach (var k in keys) c.AddKey(new Keyframe(k.t, k.v));
            for (int i = 0; i < c.length; i++) ApplyModes(c, i, step);
            AnimationUtility.SetEditorCurve(clip, Binding(root, target, prop), c);
        }

        /// <summary>z 회전 키 (x · y 는 0 키도 함께 — 회전 커브는 세 축이 다 있어야 한다)</summary>
        public static void EulerZ(AnimationClip clip, Transform root, Transform t, float time, float z)
        {
            Vector3 e = t.localEulerAngles;
            if (Curve(clip, root, t, "localEulerAnglesRaw.x") == null) Key(clip, root, t, "localEulerAnglesRaw.x", time, NormDeg(e.x));
            if (Curve(clip, root, t, "localEulerAnglesRaw.y") == null) Key(clip, root, t, "localEulerAnglesRaw.y", time, NormDeg(e.y));
            Key(clip, root, t, "localEulerAnglesRaw.z", time, z);
        }

        public static float NormDeg(float d) { d %= 360f; if (d > 180f) d -= 360f; if (d < -180f) d += 360f; return d; }

        /// <summary>트랜스폼 모양 전체 키 (포즈)</summary>
        public static void TransformPose(AnimationClip clip, Transform root, Transform t, float time, Vector3 pos, Vector3 euler, Vector3 scale, bool onlyChanged)
        {
            KeyIf(clip, root, t, "m_LocalPosition.x", time, pos.x, onlyChanged);
            KeyIf(clip, root, t, "m_LocalPosition.y", time, pos.y, onlyChanged);
            KeyIf(clip, root, t, "m_LocalPosition.z", time, pos.z, onlyChanged);
            bool rot = !onlyChanged || Changed(clip, root, t, "localEulerAnglesRaw.x", time, euler.x) ||
                       Changed(clip, root, t, "localEulerAnglesRaw.y", time, euler.y) || Changed(clip, root, t, "localEulerAnglesRaw.z", time, euler.z);
            if (rot)
            {
                Key(clip, root, t, "localEulerAnglesRaw.x", time, euler.x);
                Key(clip, root, t, "localEulerAnglesRaw.y", time, euler.y);
                Key(clip, root, t, "localEulerAnglesRaw.z", time, euler.z);
            }
            KeyIf(clip, root, t, "m_LocalScale.x", time, scale.x, onlyChanged);
            KeyIf(clip, root, t, "m_LocalScale.y", time, scale.y, onlyChanged);
            KeyIf(clip, root, t, "m_LocalScale.z", time, scale.z, onlyChanged);
        }

        static float CurrentOf(Object target, string prop)
        {
            if (target is Transform tr)
            {
                Vector3 p = tr.localPosition, e = tr.localEulerAngles, s = tr.localScale;
                switch (prop)
                {
                    case "m_LocalPosition.x": return p.x;
                    case "m_LocalPosition.y": return p.y;
                    case "m_LocalPosition.z": return p.z;
                    case "localEulerAnglesRaw.x": return NormDeg(e.x);
                    case "localEulerAnglesRaw.y": return NormDeg(e.y);
                    case "localEulerAnglesRaw.z": return NormDeg(e.z);
                    case "m_LocalScale.x": return s.x;
                    case "m_LocalScale.y": return s.y;
                    case "m_LocalScale.z": return s.z;
                }
            }
            var so = new SerializedObject(target);
            var sp = so.FindProperty(prop.Contains(".") ? prop.Substring(0, prop.IndexOf('.')) : prop);
            if (sp == null) return 0f;
            if (prop.Contains("."))
            {
                string sub = prop.Substring(prop.IndexOf('.') + 1);
                var child = sp.FindPropertyRelative(sub);
                if (child != null && child.propertyType == SerializedPropertyType.Float) return child.floatValue;
                return 0f;
            }
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Float: return sp.floatValue;
                case SerializedPropertyType.Integer: return sp.intValue;
                case SerializedPropertyType.Boolean: return sp.boolValue ? 1f : 0f;
                default: return 0f;
            }
        }

        static bool Changed(AnimationClip clip, Transform root, Object target, string prop, float time, float v)
        {
            float cur = ValueAt(clip, root, target, prop, time, CurrentOf(target, prop));
            return Mathf.Abs(cur - v) > 1e-4f || Curve(clip, root, target, prop) != null;
        }

        public static void KeyIf(AnimationClip clip, Transform root, Object target, string prop, float time, float v, bool onlyChanged)
        {
            if (!onlyChanged || Changed(clip, root, target, prop, time, v)) Key(clip, root, target, prop, time, v);
        }

        /// <summary>리그에 애니메이터 + 컨트롤러를 준비한다 (컨트롤러는 편집용)</summary>
        public static AnimatorController EnsureAnimator(RSNanoRig rig, AnimationClip clip) => RSNanoRigEditor.EnsureAnimator(rig, clip);

        /// <summary>새 클립을 만들어 리그에 붙인다</summary>
        public static AnimationClip NewClip(RSNanoRig rig, string name, float length)
        {
            string dir = RealSteel.Common.EditorTools.RSPaths.Ensure(RealSteel.Common.EditorTools.RSPaths.Data + "/NanoBoss/리그");
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + name + ".anim");
            var clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = Fps };
            AssetDatabase.CreateAsset(clip, path);
            // 길이를 정하는 빈 키 (리그 흔들림 0)
            SetCurve(clip, rig.transform, rig, "shake", false, (0f, 0f), (length, 0f));
            EnsureAnimator(rig, clip);
            AssetDatabase.SaveAssets();
            return clip;
        }

        /// <summary>커브에서 값이 기준을 넘는 구간들 (프레임 단위 표본)</summary>
        public static List<Vector2> Ranges(AnimationCurve c, float length, float threshold, bool above = true)
        {
            var res = new List<Vector2>();
            if (c == null || c.length == 0) return res;
            int n = Mathf.Max(1, Mathf.CeilToInt(length * Fps));
            float start = -1f;
            for (int f = 0; f <= n; f++)
            {
                float t = f / Fps;
                bool on = above ? c.Evaluate(t) > threshold : c.Evaluate(t) < threshold;
                if (on && start < 0f) start = t;
                if (!on && start >= 0f) { res.Add(new Vector2(start, t)); start = -1f; }
            }
            if (start >= 0f) res.Add(new Vector2(start, length));
            return res;
        }
    }
}
