// RE:AL STEEL - RS Terrain 요소: 길 · 배수로 · 경사로 (Spline)
//
// 파일 이름 = 클래스 이름 이어야 Unity 가 씬에 저장된 컴포넌트를 다시 찾는다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    // ═════════════════════════════════════════════════════════════════
    // 길 · 배수로 · 경사로 (Spline)
    // ═════════════════════════════════════════════════════════════════

    [RSSummary("길 · 경사로 · 배수로", "점을 이어 지나가는 자리를 편다(길) 또는 판다(배수로).\n· 씬: 점 클릭 = 선택 후 이동 · Shift+클릭 = 끝에 점 추가 · Ctrl+점 클릭 = 삭제\n· 한 요소는 처음부터 끝까지 같은 모양. 모양이 다른 구간(예: 다리 밑 콘크리트 수로)은 요소를 나눈다")]
    [AddComponentMenu("RE_AL STEEL/Terrain/길 · 배수로")]
    public class RSSpline : RSTerrainFeature
    {
        public enum Kind { [InspectorName("길")] Path, [InspectorName("배수로 · 도랑")] Trench }
        public enum PathHeight { [InspectorName("지형 따라 (잔요철만 눌러 폄)")] Follow, [InspectorName("점 높이로 평탄화 (경사로 · 도로)")] Points }

        [Tooltip("길 = 지면을 펴고 칠한다 / 배수로 = 파고 물을 채운다")]
        public Kind kind = Kind.Path;
        [Tooltip("점 목록 (이 오브젝트 기준). 씬에서 Shift+클릭으로 끝에 추가, Ctrl+점 클릭으로 삭제")]
        public List<Vector3> points = new List<Vector3> { new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f) };
        [Tooltip("점 사이를 곡선으로 잇는다 (끄면 직선 — 수로 · 경사로)")]
        public bool smooth = true;

        [RSGroup("길")]
        [RSHelp("종류가 '길' 일 때만 쓰인다. 경사로는 높이 모드를 '점 높이로' 로 두고 점의 Y 를 올린다.")]
        [Tooltip("길 반폭 (m). 전체 폭은 2배")]
        public float halfWidth = 0.9f;
        [Tooltip("가장자리가 주변 땅과 섞이는 폭 (m)")]
        public float feather = 0.8f;
        [Tooltip("지형 따라 = 원래 굴곡은 두고 잔요철만 편다 / 점 높이로 = 점의 Y 로 평탄화 (경사로 · 도로)")]
        public PathHeight heightMode = PathHeight.Follow;
        [Range(0f, 1f), Tooltip("'지형 따라' 에서 잔요철을 눌러 펴는 정도")]
        public float noiseFlatten = 0.85f;
        [Tooltip("바퀴 자국 두 줄을 판다")]
        public bool ruts = true;
        [Tooltip("바퀴 자국이 가운데서 떨어진 거리 (m)")]
        public float rutOffset = 0.52f;
        [Tooltip("바퀴 자국 반폭 (m)")]
        public float rutHalfWidth = 0.24f;
        [Tooltip("바퀴 자국 깊이 (m)")]
        public float rutDepth = 0.09f;
        [Tooltip("길에 칠할 레이어 (기본 R 길, 없음 = 칠 안 함)")]
        public RSLayer pathPaint = RSLayer.R;

        [RSGroup("배수로")]
        [RSHelp("종류가 '배수로' 일 때만 쓰인다.\n흙 도랑: 벽 경사 1 · 둑 깎임 0.3 · 바닥 칠 A\n콘크리트 수로: 벽 경사 3 · 둑 좁게 · 벽 칠 G · 정점 고정 · 곡선 끔")]
        [Tooltip("바닥 반폭 (m)")]
        public float bottomHalfWidth = 0.75f;
        [Tooltip("둑 폭 (m) — 바닥에서 지면까지 비탈 폭")]
        public float bankWidth = 1.1f;
        [Tooltip("점 높이에서 바닥까지 깊이 (m)")]
        public float depth = 1.7f;
        [Range(1f, 4f), Tooltip("벽 경사. 1 = 흙 둑, 3 = 콘크리트 수로처럼 벽이 선다")]
        public float wallSteepness = 1f;
        [Tooltip("둑 가장자리가 무너진 정도 (0 = 반듯)")]
        public float bankErode = 0.35f;
        [Tooltip("바닥 울퉁불퉁 (m)")]
        public float floorNoise = 0.14f;
        [Tooltip("벽에 칠할 레이어 (콘크리트 수로는 G)")]
        public RSLayer wallPaint = RSLayer.None;
        [Tooltip("바닥에 칠할 레이어 (기본 A 진흙)")]
        public RSLayer floorPaint = RSLayer.A;
        [Tooltip("이 배수로 주변 정점은 흔들지 않는다 (수로 벽을 반듯하게)")]
        public bool lockVertices = false;
        [Tooltip("수면을 만든다 (머티리얼은 RS 지형의 Water Material)")]
        public bool water = true;
        [Tooltip("바닥에서 수면까지 (m)")]
        public float waterHeight = 0.38f;

        public override Color GizmoColor { get { return kind == Kind.Trench ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.85f, 0.35f); } }
        public override int DefaultOrder { get { return kind == Kind.Trench ? 50 : 40; } }

        [System.NonSerialized] Vector3[] P;   // 지형 로컬 폴리라인

        public int PointCount { get { return points == null ? 0 : points.Count; } }

        protected override void OnPrepare(RSTerrain t)
        {
            P = BuildPolyline(t);
        }

        Vector3[] BuildPolyline(RSTerrain t)
        {
            if (points == null || points.Count == 0) return new Vector3[0];
            var m = t.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            var raw = new List<Vector3>(points.Count);
            foreach (var p in points) raw.Add(m.MultiplyPoint3x4(p));
            if (!smooth || raw.Count < 3) return raw.ToArray();

            var outp = new List<Vector3>();
            const int sub = 6;
            for (int i = 0; i < raw.Count - 1; i++)
            {
                Vector3 p0 = raw[Mathf.Max(0, i - 1)], p1 = raw[i], p2 = raw[i + 1], p3 = raw[Mathf.Min(raw.Count - 1, i + 2)];
                for (int k = 0; k < sub; k++)
                {
                    float u = k / (float)sub;
                    outp.Add(CatmullRom(p0, p1, p2, p3, u));
                }
            }
            outp.Add(raw[raw.Count - 1]);
            return outp.ToArray();
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        float Reach
        {
            get
            {
                return kind == Kind.Trench
                    ? bottomHalfWidth + bankWidth + Mathf.Abs(bankErode) + 1f
                    : Mathf.Max(halfWidth + feather, rutOffset + rutHalfWidth) + 0.2f;
            }
        }

        protected override Rect ComputeBounds()
        {
            if (P == null || P.Length == 0) return new Rect(0, 0, 0, 0);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in P) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z); }
            float r = Reach;
            return Rect.MinMaxRect(x0 - r, z0 - r, x1 + r, z1 + r);
        }

        /// <summary>폴리라인까지 수평 거리와 그 지점의 점 높이</summary>
        bool Nearest(float x, float z, out float dist, out float y)
        {
            dist = float.MaxValue; y = 0f;
            if (P == null || P.Length == 0) return false;
            if (P.Length == 1) { dist = new Vector2(x - P[0].x, z - P[0].z).magnitude; y = P[0].y; return true; }
            float best = float.MaxValue;
            for (int i = 0; i < P.Length - 1; i++)
            {
                Vector3 a = P[i], b = P[i + 1];
                float abx = b.x - a.x, abz = b.z - a.z;
                float len2 = abx * abx + abz * abz;
                float t = len2 < 1e-8f ? 0f : Mathf.Clamp01(((x - a.x) * abx + (z - a.z) * abz) / len2);
                float qx = a.x + abx * t - x, qz = a.z + abz * t - z;
                float d2 = qx * qx + qz * qz;
                if (d2 < best) { best = d2; y = Mathf.Lerp(a.y, b.y, t); }
            }
            dist = Mathf.Sqrt(best);
            return true;
        }

        public override void Apply(ref RSSample s, float x, float z)
        {
            float d, py;
            if (!Nearest(x, z, out d, out py)) return;
            if (kind == Kind.Path) ApplyPath(ref s, x, z, d, py);
            else ApplyTrench(ref s, x, z, d, py);
        }

        void ApplyPath(ref RSSample s, float x, float z, float d, float py)
        {
            float m = 1f - SS(Mathf.Clamp01((d - halfWidth) / Mathf.Max(0.01f, feather)));
            if (m > 0f)
            {
                if (heightMode == PathHeight.Follow) s.h -= s.detail * noiseFlatten * m * strength;
                else s.h = Mathf.Lerp(s.h, py, m * strength);
                s.detail *= 1f - m * (heightMode == PathHeight.Follow ? noiseFlatten : 1f) * strength;   // 뒤따르는 길이 두 번 누르지 않게
            }
            if (ruts)
            {
                float rd = Mathf.Abs(d - rutOffset);
                if (rd < rutHalfWidth)
                {
                    float q = rd / rutHalfWidth;
                    s.h -= rutDepth * (1f - q * q) * strength;
                }
            }
            s.Paint(pathPaint, SS(Mathf.Clamp01((m - 0.25f) / 0.5f)) * strength);
        }

        void ApplyTrench(ref RSSample s, float x, float z, float d, float py)
        {
            float dz = d + SN(x, z, 0.6f, 13) * bankErode;
            float bank = Mathf.Max(0.01f, bankWidth);
            if (lockVertices) s.lockXZ = Mathf.Max(s.lockXZ, Mathf.Clamp01(1f - (dz - bottomHalfWidth - bank) / 0.75f));

            float u = Mathf.Clamp01((dz - bottomHalfWidth) / bank);
            u = Mathf.Pow(u, wallSteepness);
            float carve = 1f - SS(u);
            if (carve <= 0f) return;

            float floorY = py - depth + SN(x, z, 0.9f, 14) * floorNoise;
            s.h = Mathf.Lerp(s.h, floorY, carve * strength);
            s.Paint(wallPaint, carve * strength);
            s.Paint(floorPaint, SS(Mathf.Clamp01((carve - 0.35f) / 0.5f)) * strength);
        }

        public override void AddWater(RSTerrain t, List<Vector3> v, List<int> tri)
        {
            if (kind != Kind.Trench || !water || P == null || P.Length < 2 || strength <= 0f) return;
            float hw = bottomHalfWidth + bankWidth * 0.8f;
            float hx = t.size.x * 0.5f, hz = t.size.y * 0.5f;

            // 0.5 간격으로 다시 찍는다
            var pts = new List<Vector3>();
            var dirs = new List<Vector2>();
            for (int i = 0; i < P.Length - 1; i++)
            {
                Vector3 a = P[i], b = P[i + 1];
                Vector2 dir = new Vector2(b.x - a.x, b.z - a.z);
                float len = dir.magnitude;
                if (len < 1e-5f) continue;
                dir /= len;
                int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
                for (int k = 0; k < n; k++) { pts.Add(Vector3.Lerp(a, b, k / (float)n)); dirs.Add(dir); }
            }
            pts.Add(P[P.Length - 1]);
            dirs.Add(dirs.Count > 0 ? dirs[dirs.Count - 1] : Vector2.right);

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 l0, r0, l1, r1;
                Edge(pts[i], dirs[i], hw, hx, hz, out l0, out r0);
                Edge(pts[i + 1], dirs[i + 1], hw, hx, hz, out l1, out r1);
                RSTerrain.AddQuadUp(v, tri, l0, l1, r1, r0);
            }
        }

        void Edge(Vector3 p, Vector2 dir, float hw, float hx, float hz, out Vector3 l, out Vector3 r)
        {
            float y = p.y - depth + waterHeight;
            Vector2 perp = new Vector2(-dir.y, dir.x) * hw;
            l = new Vector3(Mathf.Clamp(p.x + perp.x, -hx, hx), y, Mathf.Clamp(p.z + perp.y, -hz, hz));
            r = new Vector3(Mathf.Clamp(p.x - perp.x, -hx, hx), y, Mathf.Clamp(p.z - perp.y, -hz, hz));
        }

        void DrawSpline(bool selected)
        {
            var t = Owner;
            if (t == null || points == null || points.Count == 0) return;
            UpdateFrame(t);
            var poly = BuildPolyline(t);
            var col = GizmoColor; col.a = selected ? 0.95f : 0.5f;
            Gizmos.color = col;
            float r = kind == Kind.Trench ? bottomHalfWidth + bankWidth : halfWidth;
            Vector3 prevC = Vector3.zero, prevL = Vector3.zero, prevR = Vector3.zero;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector3 p = poly[i];
                Vector3 nxt = poly[Mathf.Min(poly.Length - 1, i + 1)], prv = poly[Mathf.Max(0, i - 1)];
                Vector2 dir = new Vector2(nxt.x - prv.x, nxt.z - prv.z).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x) * r;
                Vector3 cW = G(t, p.x, p.z), lW = G(t, p.x + perp.x, p.z + perp.y), rW = G(t, p.x - perp.x, p.z - perp.y);
                if (i > 0)
                {
                    Gizmos.DrawLine(prevC, cW);
                    if (selected) { Gizmos.DrawLine(prevL, lW); Gizmos.DrawLine(prevR, rW); }
                }
                prevC = cW; prevL = lW; prevR = rW;
            }
        }

        static Vector3 G(RSTerrain t, float x, float z)
        {
            float y = (t.HasHeights ? t.SampleHeight(x, z) : 0f) + 0.08f;
            return t.transform.TransformPoint(new Vector3(x, y, z));
        }

        void OnDrawGizmos() { DrawSpline(false); }
        void OnDrawGizmosSelected() { DrawSpline(true); }
    }
}
