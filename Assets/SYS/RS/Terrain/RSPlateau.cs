// RE:AL STEEL - RS Terrain 요소: 둔덕 · 절벽 · 단 (Plateau)
//
// 파일 이름 = 클래스 이름 이어야 Unity 가 씬에 저장된 컴포넌트를 다시 찾는다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    // ═════════════════════════════════════════════════════════════════
    // 둔덕 · 절벽 · 단 (Plateau)
    // ═════════════════════════════════════════════════════════════════

    [RSSummary("둔덕 · 절벽", "사각형 땅을 올리거나(언덕 · 성토) 깎아 내린다(움푹한 터). 절벽은 층층이 진 옥토패스식 단으로 나온다.\n· 크기: 씬의 사각 핸들 · 위치/회전: 오브젝트를 옮기고 Y 로 돌린다")]
    [AddComponentMenu("RE_AL STEEL/Terrain/둔덕 · 절벽")]
    public class RSPlateau : RSTerrainFeature
    {
        public enum Mode { [InspectorName("올리기")] Raise, [InspectorName("깎아 내리기")] Lower }

        [Tooltip("올리기 = 언덕 · 둔덕 / 깎아 내리기 = 움푹한 터")]
        public Mode mode = Mode.Raise;
        [Tooltip("전체 크기 X, Z (m). 씬의 사각 핸들로도 조절")]
        public Vector2 size = new Vector2(10f, 8f);
        [Tooltip("모서리 둥글기 (m). 0 = 각진 사각형")]
        public float cornerRound = 1f;
        [Tooltip("올리거나 내리는 높이 (m)")]
        public float height = 2f;
        [Tooltip("절벽 비탈 폭 (m). 작을수록 가파르다 (0.4 ~ 1)")]
        public float cliffWidth = 0.6f;
        [RSHelp("절벽 모양: 층(단)을 넣고 가장자리를 구불거리게 해서 손으로 쌓은 듯한 실루엣을 만든다.")]
        [Range(0, 6), Tooltip("절벽 층(단) 개수. 0 = 매끈한 비탈")]
        public int terraces = 3;
        [Range(0f, 1f), Tooltip("층이 얼마나 또렷한지 (0 = 매끈, 1 = 계단)")]
        public float terraceStrength = 0.35f;
        [Tooltip("가장자리가 살짝 부푼 턱 높이 (m) — 옥토패스 언덕 실루엣")]
        public float rim = 0.14f;
        [Tooltip("절벽선이 구불거리는 폭 (m)")]
        public float edgeWobble = 0.7f;
        [Tooltip("구불거림 촘촘함. 클수록 잘게")]
        public float wobbleFrequency = 0.35f;
        [Tooltip("윗면 굴곡 높이 (m)")]
        public float topNoise = 0.2f;
        [Tooltip("윗면에 칠할 레이어 (없음 = 바탕 흙)")]
        public RSLayer topPaint = RSLayer.None;

        public override Color GizmoColor { get { return new Color(0.4f, 0.85f, 0.4f); } }
        public override int DefaultOrder { get { return 0; } }

        Vector2 half; float R;

        protected override void OnPrepare(RSTerrain t)
        {
            R = Mathf.Clamp(cornerRound, 0f, Mathf.Min(size.x, size.y) * 0.5f);
            half = new Vector2(Mathf.Max(0f, size.x * 0.5f - R), Mathf.Max(0f, size.y * 0.5f - R));
        }

        protected override Rect ComputeBounds()
        {
            float r = new Vector2(size.x, size.y).magnitude * 0.5f + cliffWidth + edgeWobble + 0.5f;
            return RectAround(c.x, c.z, r);
        }

        public override void Apply(ref RSSample s, float x, float z)
        {
            var q = Local(x, z);
            float e = Mathf.Max(0.01f, cliffWidth);
            float d = RSMath.RoundedRectDist(q, half, R) + SN(x, z, wobbleFrequency, 3) * edgeWobble;
            if (d > e + 0.3f) return;

            float t = Mathf.Clamp01((d + e) / (2f * e));
            float k = 1f - SS(t);
            k = k * k * (3f - 2f * k);
            if (terraces > 0) k = Mathf.Lerp(k, Mathf.Round(k * terraces) / terraces, terraceStrength);
            float rimT = Mathf.Clamp01(1f - Mathf.Abs(d + e * 0.9f) / (e * 1.2f));

            float v = height * k + rim * rimT * rimT + k * SN(x, z, 0.35f, 4) * topNoise;
            s.h += (mode == Mode.Raise ? v : -v) * strength;
            s.Paint(topPaint, k * strength);
        }

        void OnDrawGizmos() { DrawOutline(RoundRectPts(size, cornerRound), true, false); }
        void OnDrawGizmosSelected() { DrawOutline(RoundRectPts(size, cornerRound), true, true); }
    }
}
