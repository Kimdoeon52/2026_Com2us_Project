// RE:AL STEEL - RS Terrain 요소: 고물 더미 (Heap)
//
// 파일 이름 = 클래스 이름 이어야 Unity 가 씬에 저장된 컴포넌트를 다시 찾는다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    // ═════════════════════════════════════════════════════════════════
    // 고물 더미 (Heap)
    // ═════════════════════════════════════════════════════════════════

    [RSSummary("고물 더미", "봉우리 여러 개가 겹친 불규칙한 무더기. 표면은 층진 고철 느낌으로 나온다.\n· 반경: 씬의 원 핸들")]
    [AddComponentMenu("RE_AL STEEL/Terrain/더미")]
    public class RSHeap : RSTerrainFeature
    {
        [Tooltip("밑면 반경 (m). 원 핸들로도 조절")]
        public float radius = 2.2f;
        [Tooltip("높이 (m)")]
        public float height = 1.5f;
        [Range(0, 6), Tooltip("곁봉우리 수 — 많을수록 불규칙한 실루엣")]
        public int lumps = 4;
        [Range(0f, 1f), Tooltip("능선 거칠기 (0 = 매끈)")]
        public float roughness = 0.42f;
        [Range(0f, 0.6f), Tooltip("계단처럼 층진 표면 (쌓인 고철). 0 = 매끈")]
        public float steps = 0.26f;
        [Tooltip("칠할 레이어 (기본 B 고물)")]
        public RSLayer paint = RSLayer.B;
        [Range(0f, 1f), Tooltip("둘레 흙에 고물 조각을 흩뿌리듯 칠하는 정도")]
        public float scatter = 0.3f;

        public override Color GizmoColor { get { return new Color(0.95f, 0.5f, 0.2f); } }
        public override int DefaultOrder { get { return 10; } }

        Vector2[] lc; float[] lr, lh; int[] ls;

        protected override void OnPrepare(RSTerrain t)
        {
            int n = 1 + Mathf.Max(0, lumps);
            lc = new Vector2[n]; lr = new float[n]; lh = new float[n]; ls = new int[n];
            lc[0] = new Vector2(c.x, c.z); lr[0] = radius * 0.72f; lh[0] = height; ls[0] = salt;
            for (int q = 1; q < n; q++)
            {
                int sub = n - 1;
                float ang = ((q - 1) + RSMath.Hash01(salt, q, 42, 0) * 0.8f) * Mathf.PI * 2f / sub;
                float dist = radius * Mathf.Lerp(0.3f, 0.55f, RSMath.Hash01(salt, q, 43, 0));
                lc[q] = ToTerrain(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist);
                lr[q] = radius * Mathf.Lerp(0.4f, 0.62f, RSMath.Hash01(salt, q, 44, 0));
                lh[q] = height * Mathf.Lerp(0.45f, 0.9f, RSMath.Hash01(salt, q, 45, 0));
                ls[q] = salt + q;
            }
        }

        protected override Rect ComputeBounds() { return RectAround(c.x, c.z, radius * 2.1f); }

        public override void Apply(ref RSSample s, float x, float z)
        {
            var p = new Vector2(x, z);
            float hMax = 0f, mMax = 0f, nMax = 0f;
            for (int i = 0; i < lc.Length; i++)
            {
                float rr = lr[i] * (1f + 0.3f * SN(x, z, 0.6f, 5));
                float dd = Vector2.Distance(p, lc[i]) / Mathf.Max(0.01f, rr);
                if (dd < 1.7f) nMax = Mathf.Max(nMax, Mathf.Clamp01((1.7f - dd) / 0.8f));
                if (dd >= 1f) continue;
                float s1 = 1f - dd;
                float dome = Mathf.Lerp(s1, s1 * (2f - s1), 0.5f);
                float ridge = 1f - Mathf.Abs(2f * N(x, z, 1.35f, 6 + (ls[i] & 1)) - 1f);
                float chunk = Mathf.Floor(N(x, z, 1.7f, 8 + (ls[i] % 3)) * 4f) / 4f;
                float v = lh[i] * dome * ((1f - roughness * 0.76f) + roughness * ridge)
                        + lh[i] * steps * chunk * SS(Mathf.Clamp01(s1 * 2.2f));
                hMax = Mathf.Max(hMax, v);
                mMax = Mathf.Max(mMax, SS(Mathf.Clamp01(s1 * 3.5f)));
            }
            s.h += hMax * strength;
            s.Paint(paint, Mathf.Max(mMax, nMax * scatter) * strength);
        }

        void OnDrawGizmos() { DrawOutline(CirclePts(radius), true, false); }
        void OnDrawGizmosSelected() { DrawOutline(CirclePts(radius), true, true); }
    }
}
