// RE:AL STEEL - 나노봇 리그 점검
//
// 리그 · 클립을 훑어 실수하기 쉬운 것을 알려 준다 (리그 인스펙터 요약, 나노봇 동작 창 → 점검 탭).
//   · 리그 원점 · 플레이어 기준점에 키 (덮어써져 무시된다)
//   · 봇이 모자람 (부위들이 한꺼번에 원하는 봇 > 군집)
//   · 자리 수가 모양에 비해 성기거나 빽빽함
//   · 판정이 없는 공격 / 판정 앞 예고가 너무 짧음 (헬 속도 기준)
//   · 클립 끝에도 부위가 남아 있음 (봇이 한꺼번에 돌아간다)
//   · 관절이 하나뿐인 줄기, 서로 봇을 가져오는 부위, 예전 두께 곡선
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    public static class RSNanoRigCheck
    {
        public struct Issue
        {
            public int level;          // 0 알림 · 1 주의 · 2 오류
            public string text;
            public string fixLabel;
            public Action fix;
            public UnityEngine.Object focus;
        }

        public const float MinTelegraph = 0.3f;   // 헬(속도 1.2배)에서 실제로 보이는 예고의 최소 (초)

        static Issue I(int level, string text, UnityEngine.Object focus = null, string fixLabel = null, Action fix = null) =>
            new Issue { level = level, text = text, focus = focus, fixLabel = fixLabel, fix = fix };

        public static List<Issue> Run(RSNanoRig rig, AnimationClip clip)
        {
            var list = new List<Issue>();
            if (!rig) return list;
            var sw = rig.swarm ? rig.swarm : rig.GetComponentInParent<RSNanoSwarm>();
            if (!sw) list.Add(I(2, "군집(보스)이 없다 — 리그를 나노봇 군집 아래에 둔다", rig));
            var limbs = rig.GetComponentsInChildren<RSNanoLimb>(true);
            if (limbs.Length == 0) list.Add(I(1, "부위가 없다 — '+ 줄기' · '+ 부위 프리셋' 으로 만든다", rig));
            if (!clip) list.Add(I(0, "클립이 없다 — 애니메이션 창에서 새 클립을 만든다", rig, "애니메이션 준비", () => RSNanoRigEditor.EnsureAnimator(rig, null)));

            // 원점 · 기준점 키
            if (clip)
            {
                string anchorPath = rig.playerAnchor ? RSNanoKeys.PathOf(rig.transform, rig.playerAnchor) : null;
                var bad = new List<EditorCurveBinding>();
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                    if (b.type == typeof(Transform) && (string.IsNullOrEmpty(b.path) || (anchorPath != null && b.path == anchorPath))) bad.Add(b);
                if (bad.Count > 0 && !rig.allowRootKeys)
                    list.Add(I(1, $"리그 원점 · 플레이어 기준점에 키 {bad.Count}개 — 플레이 중 덮어써져 무시된다", rig, "지우기", () =>
                    {
                        Undo.RegisterCompleteObjectUndo(clip, "원점 키 지우기");
                        foreach (var b in bad) AnimationUtility.SetEditorCurve(clip, b, null);
                        RSNanoKeys.EndEdit(clip);
                    }));
            }

            // 봇 수
            if (sw && limbs.Length > 0)
            {
                float len = clip ? Mathf.Max(0.1f, clip.length) : 0f;
                int peak = 0;
                float peakT = 0f;
                int samples = clip ? 40 : 1;
                for (int s = 0; s < samples; s++)
                {
                    float t = clip ? len * s / (samples - 1f) : 0f;
                    int sum = 0;
                    foreach (var l in limbs)
                        sum += Mathf.RoundToInt(l.bots * Mathf.Clamp01(RSNanoKeys.ValueAt(clip, rig.transform, l, "amount", t, l.amount)));
                    if (sum > peak) { peak = sum; peakT = t; }
                }
                if (peak > sw.botCount)
                    list.Add(I(1, $"봇 모자람 — {peakT:0.00}초에 부위들이 {peak}개를 원하는데 군집은 {sw.botCount}개. 비율대로 줄어 형태가 성기다", rig));
                else if (peak > sw.botCount * 0.85f)
                    list.Add(I(0, $"{peakT:0.00}초에 봇 {peak}/{sw.botCount} — 덩어리에 거의 안 남는다", rig));

                float sp = RSNanoRigEditor.Spacing(sw), floor = RSNanoRigPreview.FloorY(rig);
                foreach (var l in limbs)
                {
                    int est = l.EstimateBots(sp, floor);
                    float r = l.bots / (float)Mathf.Max(1, est);
                    if (r < 0.6f || r > 1.6f)
                    {
                        var limb = l;
                        list.Add(I(0, $"'{l.name}' 자리 {l.bots} — 지금 모양엔 {est} 이 알맞다 ({(r < 1f ? "판 사이가 벌어진다" : "판이 겹친다")})", l, "크기에 맞게", () =>
                        {
                            Undo.RecordObject(limb, "봇 수");
                            RSNanoRigEditor.AutoBots(limb, sw);
                        }));
                    }
                }
            }

            // 판정 · 예고
            if (clip && limbs.Length > 0)
            {
                bool anyDanger = false;
                foreach (var l in limbs)
                {
                    var dc = RSNanoKeys.Curve(clip, rig.transform, l, "danger");
                    var ranges = dc != null ? RSNanoKeys.Ranges(dc, clip.length, 0.5f) : (l.danger ? new List<Vector2> { new Vector2(0f, clip.length) } : new List<Vector2>());
                    if (ranges.Count > 0) anyDanger = true;
                    var am = RSNanoKeys.Curve(clip, rig.transform, l, "amount");
                    var gl = RSNanoKeys.Curve(clip, rig.transform, l, "glow");
                    // 이 부위 자리에서 요동치는 웅덩이도 예고다 (웅덩이 → 가시)
                    var extra = new List<AnimationCurve>();
                    foreach (var m in limbs)
                        if (m != l && m.bumpAt && (m.bumpAt == l.transform || m.bumpAt.IsChildOf(l.transform)))
                        {
                            var bc = RSNanoKeys.Curve(clip, rig.transform, m, "bump");
                            if (bc != null) extra.Add(bc);
                        }
                    foreach (var r in ranges)
                    {
                        float cue = CueStart(am, gl, r.x);
                        foreach (var ec in extra) cue = Mathf.Min(cue, CueStart(null, ec, r.x, false));
                        float tele = (r.x - cue) / 1.2f;
                        if (r.x > 0.02f && tele < MinTelegraph)
                            list.Add(I(1, $"'{l.name}' 판정 {r.x:0.00}초 — 예고가 {r.x - cue:0.00}초 (헬 {tele:0.00}초). 나타나거나 빛나는 예고를 {MinTelegraph * 1.2f:0.0}초 이상 앞에 둔다", l));
                    }
                    if (am != null && am.Evaluate(clip.length) > 0.05f)
                        list.Add(I(0, $"'{l.name}' — 클립 끝에도 양 {am.Evaluate(clip.length):0.00}. 끝나면 봇이 한꺼번에 돌아간다 (마지막에 '사라지기')", l));
                }
                if (!anyDanger) list.Add(I(1, "판정이 없다 — 플레이어를 해치는 구간에 '판정 시작 · 끝' 키를 잡는다", rig));
            }

            // 구조
            foreach (var l in limbs)
            {
                if (l.type == LimbType.Chain)
                {
                    l.RefreshJoints();
                    if (l.Joints.Count < 2) list.Add(I(1, $"'{l.name}' 줄기에 관절이 하나뿐 — 자식 관절을 하나 이상 둔다", l, "+ 끝에 관절", () => RSNanoLimbEditor.AddTipJoint(l)));
                    bool legacy = false;
                    foreach (var j in l.Joints) if (j && !j.GetComponent<RSNanoJoint>()) legacy = true;
                    if (legacy)
                    {
                        var limb = l;
                        list.Add(I(0, $"'{l.name}' 두께가 예전 곡선 — 관절별 두께로 바꾸면 씬 뷰에서 끌어 고치고 키도 잡힌다", l, "바꾸기", () => RSNanoLimbEditor.ConvertThickness(limb)));
                    }
                }
                if (l.feedFrom && l.feedFrom.feedFrom == l)
                    list.Add(I(1, $"'{l.name}' 와 '{l.feedFrom.name}' 가 서로 봇을 가져온다 — 한쪽만 둔다", l));
            }
            return list;
        }

        /// <summary>판정 시각 t 앞에서 예고가 시작된 때 — 양이 0 에서 올라오기 시작한 때 또는 발광이 켜진 때 중 빠른 것</summary>
        static float CueStart(AnimationCurve amount, AnimationCurve glow, float t, bool amountMatters = true)
        {
            const float step = 1f / 60f;
            float cue;
            if (!amountMatters) cue = t;
            else if (amount == null) cue = 0f;   // 양에 키가 없음 — 처음부터 보이던 부위
            else
            {
                float s = t;
                while (s > 0f && amount.Evaluate(s - step) > 0.01f) s -= step;
                cue = s;                    // s == t 면 판정 순간에 나타남 (예고 없음)
            }
            if (glow != null)
            {
                float s = t;
                while (s > 0f && glow.Evaluate(s - step) > 0.05f) s -= step;
                cue = Mathf.Min(cue, s);
            }
            return cue;
        }
    }
}
