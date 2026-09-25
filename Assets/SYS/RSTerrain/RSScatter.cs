// RE:AL STEEL - RS Terrain 폐자재 흩뿌리기 (RSScatter)
//
// 지면에 반쯤 묻힌 폐자재를 흩뿌린다: 콘크리트 파편 · 철판 · 파이프 · 타이어 · 드럼통.
// 지형 모양은 안 바꾼다. 지형을 다시 만들 때마다 최종 지면(요소 + 브러시)에 맞춰 다시 놓인다.
// 메시는 머티리얼별로 하나로 합쳐 만든다 (오브젝트 수가 늘지 않음). 씬에는 저장하지 않는다.
//
// 배치 규칙: 경사가 급한 곳 · 길(R) · 콘크리트(G) · 진흙(A) 위는 피한다 (각각 끌 수 있음).
// 철판 일부는 고물(B) 더미 위에 몰아 놓는다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    [RSSummary("폐자재 흩뿌리기", "콘크리트 파편 · 철판 · 파이프 · 타이어 · 드럼통을 지면에 반쯤 묻어 흩뿌린다. 지형 모양은 안 바꾸고, 지형이 바뀌면 최종 지면에 맞춰 다시 놓인다.\n· 폐자재마다 원래 자리가 정해져 있어서, 값을 바꿔도 근처에서만 조금 움직인다\n· 전체를 새로 섞고 싶으면 아래 [다른 배치로] 버튼")]
    [AddComponentMenu("RE_AL STEEL/Terrain/폐자재 흩뿌리기")]
    public class RSScatter : RSTerrainFeature
    {
        public enum Area { [InspectorName("원 (반경)")] Circle, [InspectorName("지형 전체")] Whole }

        [Tooltip("원 = 이 오브젝트 둘레 반경 안 / 지형 전체")]
        public Area area = Area.Circle;
        [Tooltip("원 모드의 반경 (m). 원 핸들로도 조절")]
        public float radius = 6f;

        [Header("개수")]
        [RSHelp("종류별 개수. 놓을 자리가 모자라면 일부는 빠진다.")]
        [Tooltip("콘크리트 파편 — 비스듬히 박힌 깨진 슬래브")]
        public int slabs = 4;
        [Tooltip("철판 — 한쪽이 묻히고 반대쪽이 들림")]
        public int plates = 4;
        [Tooltip("파이프 — 누워서 반쯤 묻힘")]
        public int pipes = 2;
        [Tooltip("타이어 — 눕거나 세워져 묻히거나 두 개 겹침")]
        public int tires = 3;
        [Tooltip("드럼통 — 넘어지거나 비스듬히 박힘")]
        public int drums = 1;

        [Header("배치 규칙")]
        [RSHelp("어디에 놓을지 정하는 규칙. 경사가 급한 곳 · 길 · 콘크리트 · 진흙 위는 피한다.")]
        [Tooltip("폐자재끼리 최소 간격 (m)")]
        public float minGap = 1.1f;
        [Range(0.3f, 1f), Tooltip("이보다 가파른 곳엔 안 놓는다 (1 = 평지만)")]
        public float minFlatness = 0.75f;
        [Tooltip("길(R) 위에는 안 놓는다")]
        public bool avoidPath = true;
        [Tooltip("콘크리트(G) 위에는 안 놓는다")]
        public bool avoidConcrete = true;
        [Tooltip("진흙(A) · 배수로 바닥에는 안 놓는다")]
        public bool avoidMud = true;
        [Range(0f, 1f), Tooltip("철판 중 고물(B) 더미 위에 몰아 놓을 비율")]
        public float platesOnScrap = 0.5f;
        [Tooltip("원래 자리에 못 놓을 때 옆으로 비켜 설 수 있는 최대 거리 (m). 이 안에 놓을 곳이 없으면 그 폐자재는 빠진다")]
        public float searchReach = 4f;

        [Header("머티리얼")]
        [RSHelp("비어 있으면 아래 [기본 머티리얼] 버튼으로 MAT_Stage_* 를 채울 수 있다.")]
        [Tooltip("콘크리트 파편 머티리얼")]
        public Material concrete;
        [Tooltip("철판 머티리얼 (절반은 녹슨 것)")]
        public Material metal;
        [Tooltip("녹슨 철판 · 파이프 · 드럼통 머티리얼")]
        public Material rust;
        [Tooltip("타이어 머티리얼")]
        public Material tire;
        [Tooltip("폐자재에 MeshCollider 를 붙인다 (밟고 부딪혀야 할 때)")]
        public bool addCollider = false;

        public override Color GizmoColor { get { return new Color(0.9f, 0.4f, 0.9f); } }
        public override int DefaultOrder { get { return 100; } }

        // 지형 모양은 안 건드린다
        protected override Rect ComputeBounds() { return new Rect(0f, 0f, 0f, 0f); }
        public override void Apply(ref RSSample s, float x, float z) { }

        // ─────────────────────────────────────────────────────────────
        // 흩뿌리기
        // ─────────────────────────────────────────────────────────────

        public void Scatter(RSTerrain t, RSDebrisSink sink)
        {
            UpdateFrame(t);
            // 난수를 항목(종류, 번호)마다 따로 뽑는다.
            // 예전엔 난수 하나를 모두가 이어 썼기 때문에, 지형이 조금만 바뀌어 한 자리가
            // 탈락해도 뒤의 모든 폐자재 위치 · 모양이 통째로 뒤섞였다.
            // 이제는 조건이 바뀐 그 자리의 폐자재만 옮겨 간다.
            System.Random rng = null;   // 모양 · 회전용 (항목마다 새로 만든다)
            var used = new List<Vector2>();
            System.Func<float, float, float> R = (lo, hi) => Mathf.Lerp(lo, hi, (float)rng.NextDouble());

            System.Func<Vector4, bool> open = w =>
                (!avoidPath || w.x < 0.25f) && (!avoidConcrete || w.y < 0.4f) && (!avoidMud || w.w < 0.4f);
            System.Func<Vector4, bool> onScrap = w => open(w) && w.z > 0.45f;

            Vector3 p, n;

            for (int i = 0; i < slabs; i++)
            {
                if (!FindSpot(t, ItemRng(0, i, 0), used, open, minFlatness, out p, out n)) continue;
                rng = ItemRng(0, i, 1);
                var size = new Vector3(R(0.8f, 1.6f), R(0.16f, 0.3f), R(0.6f, 1.2f));
                var piece = RSPiece.Box(size); piece.Dent(0.05f, rng.Next(1, 999));
                var rot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(R(-22f, 22f), R(0f, 360f), R(-22f, 22f));
                sink.Add(concrete, piece, p + Vector3.up * (-0.1f * size.y), rot, addCollider);
            }

            int onHeap = Mathf.RoundToInt(plates * platesOnScrap);
            for (int i = 0; i < plates; i++)
            {
                if (!FindSpot(t, ItemRng(1, i, 0), used, i < onHeap ? onScrap : open, Mathf.Min(minFlatness, 0.35f), out p, out n)
                    && !(i < onHeap && FindSpot(t, ItemRng(1, i, 2), used, open, minFlatness, out p, out n))) continue;
                rng = ItemRng(1, i, 1);
                var size = new Vector3(R(1.0f, 1.8f), 0.06f, R(0.6f, 1.2f));
                var piece = RSPiece.Box(size); piece.Dent(0.04f, rng.Next(1, 999));
                float tilt = R(18f, 42f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                var rot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(tilt, 0f, R(-8f, 8f));
                sink.Add(i % 2 == 0 ? rust : metal, piece, p + Vector3.up * 0.05f, rot, addCollider);
            }

            for (int i = 0; i < pipes; i++)
            {
                if (!FindSpot(t, ItemRng(2, i, 0), used, open, Mathf.Max(minFlatness, 0.8f), out p, out n)) continue;
                rng = ItemRng(2, i, 1);
                float r = R(0.15f, 0.25f);
                var piece = RSPiece.PrismX(8, r, R(1.6f, 2.6f), Mathf.PI / 8f);
                var rot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, R(0f, 360f), R(-6f, 6f));
                sink.Add(rust, piece, p + Vector3.up * (r * 0.25f), rot, addCollider);
            }

            for (int i = 0; i < tires; i++)
            {
                if (!FindSpot(t, ItemRng(3, i, 0), used, open, Mathf.Max(minFlatness, 0.8f), out p, out n)) continue;
                rng = ItemRng(3, i, 1);
                var baseRot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, R(0f, 360f), 0f);
                int kind = i % 3;
                if (kind == 1)
                {
                    sink.Add(tire, RSPiece.Torus(0.36f, 0.14f, 12, 6), p + Vector3.up * 0.06f, baseRot * Quaternion.Euler(90f, 0f, R(-12f, 12f)), addCollider);
                }
                else
                {
                    sink.Add(tire, RSPiece.Torus(0.36f, 0.14f, 12, 6), p + Vector3.up * 0.02f,
                             baseRot * Quaternion.Euler(R(-10f, 10f), 0f, R(-10f, 10f)), addCollider);
                    if (kind == 2)
                        sink.Add(tire, RSPiece.Torus(0.36f, 0.14f, 12, 6),
                                 p + baseRot * new Vector3(R(-0.1f, 0.1f), 0.27f, R(-0.1f, 0.1f)),
                                 baseRot * Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)), addCollider);
                }
            }

            for (int i = 0; i < drums; i++)
            {
                if (!FindSpot(t, ItemRng(4, i, 0), used, open, Mathf.Max(minFlatness, 0.8f), out p, out n)) continue;
                rng = ItemRng(4, i, 1);
                var piece = RSPiece.PrismX(10, 0.3f, 0.9f, 0f); piece.Dent(0.03f, rng.Next(1, 999));
                if (i % 2 == 0)
                    sink.Add(rust, piece, p + Vector3.up * 0.12f, Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, R(0f, 360f), 0f), addCollider);
                else
                    sink.Add(rust, piece, p + Vector3.up * 0.2f, Quaternion.Euler(0f, R(0f, 360f), 90f + R(-18f, 18f)), addCollider);
            }
        }

        /// <summary>항목(종류 kind, 번호 i)마다 고정된 난수. stream 0 = 자리 후보, 1 = 모양, 2 = 예비 자리</summary>
        System.Random ItemRng(int kind, int i, int stream)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)(kind + 1) * 0x85EBCA77u; h = (h << 13) | (h >> 19);
                h ^= (uint)(i + 1) * 0xC2B2AE3Du;    h = (h << 11) | (h >> 21);
                h ^= (uint)(stream + 1) * 0x27D4EB2Fu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return new System.Random((int)(h & 0x7FFFFFFF));
            }
        }

        /// <summary>
        /// 항목마다 "원래 자리" 하나를 고정해 두고, 거기서 가장 가까운 놓을 수 있는 곳을 찾는다.
        /// 길 · 배수로 · 경사 때문에 원래 자리에 못 놓으면 바로 옆으로 비켜 설 뿐, 멀리 순간이동하지 않는다.
        /// → 요소 수치를 바꿔도 폐자재는 그 근처에서만 조금씩 움직인다.
        /// </summary>
        bool FindSpot(RSTerrain t, System.Random rng, List<Vector2> used, System.Func<Vector4, bool> ok, float minNy,
                      out Vector3 p, out Vector3 n)
        {
            float hx = t.size.x * 0.5f - 0.8f, hz = t.size.y * 0.5f - 0.8f;
            float x0, z0;
            if (area == Area.Whole)
            {
                x0 = Mathf.Lerp(-hx, hx, (float)rng.NextDouble());
                z0 = Mathf.Lerp(-hz, hz, (float)rng.NextDouble());
            }
            else
            {
                float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r0 = Mathf.Sqrt((float)rng.NextDouble()) * Mathf.Max(0.1f, radius);
                x0 = c.x + Mathf.Cos(a0) * r0;
                z0 = c.z + Mathf.Sin(a0) * r0;
            }
            float spin = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r2 = radius * radius;

            // 원래 자리 → 바깥으로 링을 넓혀 가며 (최대 searchReach)
            const float step = 0.3f;
            int rings = Mathf.CeilToInt(Mathf.Max(0f, searchReach) / step);
            for (int ring = 0; ring <= rings; ring++)
            {
                float rr = ring * step;
                int cnt = ring == 0 ? 1 : Mathf.Max(6, Mathf.CeilToInt(2f * Mathf.PI * rr / step));
                for (int k = 0; k < cnt; k++)
                {
                    float a = spin + k * (Mathf.PI * 2f / cnt);
                    float x = x0 + Mathf.Cos(a) * rr, z = z0 + Mathf.Sin(a) * rr;
                    if (x < -hx || x > hx || z < -hz || z > hz) continue;
                    if (area == Area.Circle && (x - c.x) * (x - c.x) + (z - c.z) * (z - c.z) > r2) continue;
                    if (!ok(t.SampleWeights(x, z))) continue;
                    Vector3 nn = t.SampleNormal(x, z);
                    if (nn.y < minNy) continue;

                    bool clear = true;
                    foreach (var u in used)
                        if ((u - new Vector2(x, z)).sqrMagnitude < minGap * minGap) { clear = false; break; }
                    if (!clear) continue;

                    used.Add(new Vector2(x, z));
                    p = new Vector3(x, t.SampleHeight(x, z), z);
                    n = nn;
                    return true;
                }
            }
            p = Vector3.zero; n = Vector3.up;
            return false;   // 근처에 놓을 곳이 없으면 이 폐자재는 안 놓는다
        }

        void OnDrawGizmos() { if (area == Area.Circle) DrawOutline(CirclePts(radius), true, false); }
        void OnDrawGizmosSelected() { if (area == Area.Circle) DrawOutline(CirclePts(radius), true, true); }
    }
}
