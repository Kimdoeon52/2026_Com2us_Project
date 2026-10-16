// RE:AL STEEL - RS Terrain 요소: 웅덩이 · 크레이터 (Pit)
//
// 파일 이름 = 클래스 이름 이어야 Unity 가 씬에 저장된 컴포넌트를 다시 찾는다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    // ═════════════════════════════════════════════════════════════════
    // 웅덩이 · 크레이터 (Pit)
    // ═════════════════════════════════════════════════════════════════

    [RSSummary("웅덩이 · 크레이터", "둥글게 파이고 가장자리에 턱이 선다. 물이 고이게 할 수 있다.\n· 반경: 씬의 원 핸들 · 수면 머티리얼은 RS 지형의 Water Material")]
    [AddComponentMenu("RE_AL STEEL/Terrain/웅덩이")]
    public class RSPit : RSTerrainFeature
    {
        [Tooltip("반경 (m). 원 핸들로도 조절")]
        public float radius = 1.2f;
        [Tooltip("깊이 (m)")]
        public float depth = 0.4f;
        [Tooltip("가장자리 턱 높이 (m)")]
        public float rim = 0.1f;
        [Tooltip("바닥에 칠할 레이어 (기본 A 진흙)")]
        public RSLayer paint = RSLayer.A;
        [Range(0.1f, 1.5f), Tooltip("칠하는 범위 (반경 대비)")]
        public float paintSize = 0.75f;
        [Tooltip("물이 고인다 (수면 메시). 주변이 수면보다 높은 만큼만 채운다")]
        public bool puddle = true;
        [Range(0f, 1f), Tooltip("바닥에서 수면까지 (깊이 대비)")]
        public float puddleLevel = 0.3f;

        public override Color GizmoColor { get { return new Color(0.3f, 0.5f, 1f); } }
        public override int DefaultOrder { get { return 20; } }

        protected override Rect ComputeBounds() { return RectAround(c.x, c.z, radius * 1.45f); }

        public override void Apply(ref RSSample s, float x, float z)
        {
            float dd = new Vector2(x - c.x, z - c.z).magnitude / Mathf.Max(0.01f, radius);
            if (dd >= 1.4f) return;
            float bowl = -depth * (1f - SS(Mathf.Clamp01(dd)));
            float rimT = Mathf.Clamp01(1f - Mathf.Abs(dd - 1f) / 0.4f);
            s.h += (bowl + rim * SS(rimT)) * strength;
            float ps = Mathf.Max(0.05f, paintSize);
            s.Paint(paint, (1f - SS(Mathf.Clamp01((dd - ps * 0.4f) / (ps * 0.73f)))) * strength);
        }

        public override void AddWater(RSTerrain t, List<Vector3> v, List<int> tri)
        {
            if (!puddle || strength <= 0f) return;
            float bottom = t.SampleHeight(c.x, c.z);
            float level = bottom + depth * strength * puddleLevel;

            // 둘레가 전부 수면보다 높은 반경 → 원판 가장자리가 흙에 묻힌다
            float r = radius * 0.85f;
            while (r > radius * 0.15f)
            {
                bool ok = true;
                for (int k = 0; k < 16 && ok; k++)
                {
                    float a = k * Mathf.PI * 2f / 16f;
                    if (t.SampleHeight(c.x + Mathf.Cos(a) * r, c.z + Mathf.Sin(a) * r) < level + 0.02f) ok = false;
                }
                if (ok) break;
                r -= 0.05f;
            }
            if (r <= radius * 0.15f) return;

            int seg = 14;
            int ic = v.Count;
            v.Add(new Vector3(c.x, level, c.z));
            for (int k = 0; k < seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg;
                float rr = r * (0.9f + 0.2f * RSMath.Hash01(salt, k, 31, 0));
                v.Add(new Vector3(c.x + Mathf.Cos(a) * rr, level, c.z + Mathf.Sin(a) * rr));
            }
            for (int k = 0; k < seg; k++)
            {
                int a = ic + 1 + k, b = ic + 1 + (k + 1) % seg;
                // 반시계(위에서 볼 때) 로 도는 점 → (중심, 다음, 현재) 가 위를 본다
                tri.Add(ic); tri.Add(b); tri.Add(a);
            }
        }

        void OnDrawGizmos() { DrawOutline(CirclePts(radius), true, false); }
        void OnDrawGizmosSelected() { DrawOutline(CirclePts(radius), true, true); }
    }
}
