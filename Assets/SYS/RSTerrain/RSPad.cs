// RE:AL STEEL - RS Terrain 요소: 작업장 · 평탄 (Pad)
//
// 파일 이름 = 클래스 이름 이어야 Unity 가 씬에 저장된 컴포넌트를 다시 찾는다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    // ═════════════════════════════════════════════════════════════════
    // 작업장 · 평탄 (Pad)
    // ═════════════════════════════════════════════════════════════════

    [RSSummary("작업장 · 평탄", "땅을 이 오브젝트의 Y 높이로 반듯하게 편다. 깨진 콘크리트 슬래브를 켤 수 있다.\n· 다리 받침처럼 반듯해야 하는 곳은 Lock Vertices 를 켠다\n· 크기: 씬의 사각 핸들")]
    [AddComponentMenu("RE_AL STEEL/Terrain/작업장 · 평탄")]
    public class RSPad : RSTerrainFeature
    {
        [Tooltip("크기 X, Z (m). 윗면 높이 = 이 오브젝트의 Y 위치. 사각 핸들로도 조절")]
        public Vector2 size = new Vector2(6f, 4f);
        [Tooltip("모서리 둥글기 (m)")]
        public float cornerRound = 0.4f;
        [Tooltip("가장자리 경사 폭 (m). 작을수록 턱이 선다")]
        public float edgeFeather = 0.25f;
        [Tooltip("가장자리 들쭉날쭉 (m)")]
        public float raggedEdge = 0.3f;
        [Tooltip("칠할 레이어 (기본 G 콘크리트)")]
        public RSLayer paint = RSLayer.G;
        [Range(0f, 1f), Tooltip("바깥쪽에 부스러기를 흩뿌리듯 칠하는 정도")]
        public float scatter = 0.3f;
        [Tooltip("이 영역 정점은 옆으로 흔들지 않는다 (다리 받침처럼 모서리가 반듯해야 할 곳)")]
        public bool lockVertices = false;

        [Header("깨진 슬래브")]
        [RSHelp("윗면을 조각(보로노이)으로 나눠 조각마다 높이 · 기울기를 달리하고 금을 낸다. 조각은 작업장 기준이라 옮겨도 모양이 따라다닌다.")]
        [Tooltip("깨진 콘크리트 조각으로 만든다")]
        public bool brokenSlabs = true;
        [Tooltip("조각 크기 (m). 바꾸면 조각 모양이 전부 새로 나온다")]
        public float slabCell = 1.1f;
        [Tooltip("조각마다 높이 차 (m)")]
        public float slabHeightVariation = 0.18f;
        [Tooltip("조각 기울기")]
        public float slabTilt = 0.1f;
        [Tooltip("금 폭 (m)")]
        public float crackWidth = 0.16f;
        [Tooltip("금 깊이 (m)")]
        public float crackDepth = 0.07f;
        [Range(0f, 1f), Tooltip("조각이 빠져 흙이 드러난 칸 비율")]
        public float missingChance = 0.14f;

        public override Color GizmoColor { get { return new Color(0.85f, 0.85f, 0.85f); } }
        public override int DefaultOrder { get { return 30; } }

        Vector2 half; float R;

        protected override void OnPrepare(RSTerrain t)
        {
            R = Mathf.Clamp(cornerRound, 0f, Mathf.Min(size.x, size.y) * 0.5f);
            half = new Vector2(Mathf.Max(0f, size.x * 0.5f - R), Mathf.Max(0f, size.y * 0.5f - R));
        }

        protected override Rect ComputeBounds()
        {
            return RectAround(c.x, c.z, new Vector2(size.x, size.y).magnitude * 0.5f + raggedEdge + 1.2f);
        }

        public override void Apply(ref RSSample s, float x, float z)
        {
            var q = Local(x, z);
            float pdist = RSMath.RoundedRectDist(q, half, R) + SN(x, z, 0.9f, 11) * raggedEdge;
            if (lockVertices) s.lockXZ = Mathf.Max(s.lockXZ, Mathf.Clamp01(1f - pdist / 0.75f));

            float fe = Mathf.Max(0.01f, edgeFeather);
            float pm = 1f - SS(Mathf.Clamp01((pdist + fe * 0.5f) / fe));
            float near = Mathf.Clamp01(1f - pdist / 0.9f) * (1f - pm);
            if (near > 0f && scatter > 0f) s.Paint(paint, near * scatter * strength);
            if (pm <= 0f) return;

            float target = c.y;
            float crack = 0f;
            if (brokenSlabs)
            {
                int cx, cz; Vector2 fp; float d1, d2;
                RSMath.Voronoi(q, slabCell, seed, out cx, out cz, out fp, out d1, out d2);
                if (RSMath.Hash01(cx, cz, 7, seed) < missingChance)
                {
                    s.h -= 0.05f * pm * strength;   // 슬래브가 빠져나간 자리 — 흙이 살짝 꺼짐
                    return;
                }
                target += (RSMath.Hash01(cx, cz, 2, seed) - 0.5f) * slabHeightVariation;
                float ang = RSMath.Hash01(cx, cz, 3, seed) * Mathf.PI * 2f;
                target += Vector2.Dot(q - fp, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang))) * slabTilt * (0.4f + 0.6f * RSMath.Hash01(cx, cz, 4, seed));
                float gap = d2 - d1;
                if (gap < crackWidth) target -= crackDepth * (1f - gap / Mathf.Max(0.001f, crackWidth));
                crack = Mathf.Clamp01(1f - gap / Mathf.Max(0.001f, crackWidth * 1.4f));
            }
            s.h = Mathf.Lerp(s.h, target, pm * strength);
            s.detail *= 1f - pm * strength;
            s.Paint(paint, pm * (1f - crack * 0.9f) * strength);
        }

        void OnDrawGizmos() { DrawOutline(RoundRectPts(size, cornerRound), true, false); }
        void OnDrawGizmosSelected() { DrawOutline(RoundRectPts(size, cornerRound), true, true); }
    }
}
