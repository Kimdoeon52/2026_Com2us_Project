// RE:AL STEEL - 조립 부품 모양 (면 목록)
//
// 부품 하나를 '면' 목록으로 만든다 (부품 로컬 좌표, 기준점 = 바닥 가운데). 면 = 볼록 다각형 + 면 번호.
// 같은 번호의 다각형이 여러 개일 수 있다 (기둥 옆면 조각, 계단 디딤판들) — 칠 · 도안 칸은 번호 단위.
// own = 면 자신의 2D 좌표 (m). 평면은 면 축으로 투영, 기둥 옆면은 둘레로 펼친다.
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Build
{
    public class RSFace
    {
        public int id;
        public Vector3[] v;      // 볼록 다각형 (바깥에서 봤을 때 순서는 상관없다 — 법선으로 감기 방향을 맞춘다)
        public Vector2[] own;    // 면 좌표 (m). null 이면 면 축으로 투영
        public Vector3 normal;   // 바깥 방향
    }

    public static class RSShapes
    {
        static readonly string[] BoxNames = { "앞", "뒤", "왼쪽", "오른쪽", "위", "아래" };
        static readonly string[] WedgeNames = { "경사면", "뒤", "왼쪽", "오른쪽", "아래" };
        static readonly string[] CylNames = { "옆", "위", "아래" };
        static readonly string[] StairNames = { "디딤판", "챌판", "왼쪽", "오른쪽", "뒤", "아래" };
        static readonly string[] GableNames = { "앞 경사", "뒤 경사", "처마 밑", "박공벽 왼쪽", "박공벽 오른쪽", "지붕 끝" };
        static readonly string[] ShedNames = { "경사", null, "처마 밑", "옆벽 왼쪽", "옆벽 오른쪽", "지붕 끝" };
        static readonly string[] FlatNames = { "위", null, "아래", null, null, "옆 띠", "난간 안쪽", "난간 위" };
        static readonly string[] PlaneNames = { "앞", "뒤" };

        public static string[] FaceNames(RSPart p)
        {
            switch (p.shape)
            {
                case RSPartShape.경사: return WedgeNames;
                case RSPartShape.기둥: return CylNames;
                case RSPartShape.계단: return StairNames;
                case RSPartShape.지붕: return p.roof == RSRoofKind.박공 ? GableNames : p.roof == RSRoofKind.외쪽 ? ShedNames : FlatNames;
                case RSPartShape.판: return PlaneNames;
                default: return BoxNames;
            }
        }

        /// <summary>부품의 면 목록을 out 에 채운다 (부품 로컬)</summary>
        public static void Build(RSPart p, List<RSFace> outFaces)
        {
            outFaces.Clear();
            Vector3 s = p.size;
            switch (p.shape)
            {
                case RSPartShape.경사: Wedge(s, outFaces); break;
                case RSPartShape.기둥: Cylinder(s, p.sides, p.topScale, outFaces); break;
                case RSPartShape.계단: Stairs(s, p.steps, outFaces); break;
                case RSPartShape.지붕:
                    if (p.roof == RSRoofKind.박공) Gable(s, p.overhang, p.thickness, outFaces);
                    else if (p.roof == RSRoofKind.외쪽) Shed(s, p.overhang, p.thickness, outFaces);
                    else Flat(s, p.overhang, p.thickness, outFaces);
                    break;
                case RSPartShape.판: Plane(s, outFaces); break;
                default: Box(new Vector3(-s.x / 2, 0, -s.z / 2), new Vector3(s.x / 2, s.y, s.z / 2), outFaces, 0, 1, 2, 3, 4, 5); break;
            }
        }

        // ── 도우미 ──
        static void Add(List<RSFace> o, int id, Vector3 n, params Vector3[] v)
        {
            o.Add(new RSFace { id = id, v = v, normal = n.normalized });
        }

        static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

        /// <summary>축 정렬 상자. 면 번호: 앞(-Z) 뒤(+Z) 왼(-X) 오른(+X) 위 아래 — 음수면 그 면은 안 만든다</summary>
        static void Box(Vector3 a, Vector3 b, List<RSFace> o, int front, int back, int left, int right, int top, int bottom)
        {
            if (front >= 0) Add(o, front, Vector3.back, V(a.x, a.y, a.z), V(b.x, a.y, a.z), V(b.x, b.y, a.z), V(a.x, b.y, a.z));
            if (back >= 0) Add(o, back, Vector3.forward, V(a.x, a.y, b.z), V(b.x, a.y, b.z), V(b.x, b.y, b.z), V(a.x, b.y, b.z));
            if (left >= 0) Add(o, left, Vector3.left, V(a.x, a.y, a.z), V(a.x, a.y, b.z), V(a.x, b.y, b.z), V(a.x, b.y, a.z));
            if (right >= 0) Add(o, right, Vector3.right, V(b.x, a.y, a.z), V(b.x, a.y, b.z), V(b.x, b.y, b.z), V(b.x, b.y, a.z));
            if (top >= 0) Add(o, top, Vector3.up, V(a.x, b.y, a.z), V(b.x, b.y, a.z), V(b.x, b.y, b.z), V(a.x, b.y, b.z));
            if (bottom >= 0) Add(o, bottom, Vector3.down, V(a.x, a.y, a.z), V(b.x, a.y, a.z), V(b.x, a.y, b.z), V(a.x, a.y, b.z));
        }

        // ── 경사 (뒤가 높다) ──
        static void Wedge(Vector3 s, List<RSFace> o)
        {
            float x0 = -s.x / 2, x1 = s.x / 2, z0 = -s.z / 2, z1 = s.z / 2, h = s.y;
            Vector3 slopeN = Vector3.Cross(V(1, 0, 0), V(0, h, s.z)).normalized;   // 위 · 앞쪽
            if (slopeN.y < 0) slopeN = -slopeN;
            Add(o, 0, slopeN, V(x0, 0, z0), V(x1, 0, z0), V(x1, h, z1), V(x0, h, z1));
            Add(o, 1, Vector3.forward, V(x0, 0, z1), V(x1, 0, z1), V(x1, h, z1), V(x0, h, z1));
            Add(o, 2, Vector3.left, V(x0, 0, z0), V(x0, 0, z1), V(x0, h, z1));
            Add(o, 3, Vector3.right, V(x1, 0, z0), V(x1, 0, z1), V(x1, h, z1));
            Add(o, 4, Vector3.down, V(x0, 0, z0), V(x1, 0, z0), V(x1, 0, z1), V(x0, 0, z1));
        }

        // ── 기둥 (타원, 위쪽 굵기 배율) ──
        static void Cylinder(Vector3 s, int n, float topScale, List<RSFace> o)
        {
            // 꼭짓점 반지름을 1/cos(π/n) 배로 → 평평한 면이 크기 상자에 닿는다 (4각 = 상자와 같은 크기)
            float k = 1f / Mathf.Cos(Mathf.PI / n);
            float rx = s.x / 2 * k, rz = s.z / 2 * k, h = s.y;
            var bot = new Vector3[n];
            var top = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                // 첫 면(0 → 1)이 정면(-Z)을 보게: 꼭짓점 0 은 -90° 에서 반 칸 앞
                float a = (float)i / n * Mathf.PI * 2f - Mathf.PI * 0.5f - Mathf.PI / n;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                bot[i] = V(cx * rx, 0, cz * rz);
                top[i] = V(cx * rx * topScale, h, cz * rz * topScale);
            }
            float u = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float w = Vector3.Distance(bot[i], bot[j]);
                float slant = Vector3.Distance((bot[i] + bot[j]) * 0.5f, (top[i] + top[j]) * 0.5f);
                Vector3 nrm = Vector3.Cross(top[i] - bot[i], bot[j] - bot[i]);
                Vector3 mid = (bot[i] + bot[j]) * 0.5f;
                if (Vector3.Dot(nrm, V(mid.x, 0, mid.z)) < 0) nrm = -nrm;
                if (topScale < 0.01f)
                {
                    o.Add(new RSFace { id = 0, normal = nrm.normalized, v = new[] { bot[i], bot[j], top[i] },
                        own = new[] { new Vector2(u, 0), new Vector2(u + w, 0), new Vector2(u + w * 0.5f, slant) } });
                }
                else
                {
                    o.Add(new RSFace { id = 0, normal = nrm.normalized, v = new[] { bot[i], bot[j], top[j], top[i] },
                        own = new[] { new Vector2(u, 0), new Vector2(u + w, 0), new Vector2(u + w, slant), new Vector2(u, slant) } });
                }
                u += w;
            }
            if (topScale >= 0.01f) Add(o, 1, Vector3.up, (Vector3[])top.Clone());
            Add(o, 2, Vector3.down, (Vector3[])bot.Clone());
        }

        // ── 계단 (+Z 로 올라감) ──
        static void Stairs(Vector3 s, int n, List<RSFace> o)
        {
            float x0 = -s.x / 2, x1 = s.x / 2, z0 = -s.z / 2, sd = s.z / n, sh = s.y / n;
            for (int i = 0; i < n; i++)
            {
                float za = z0 + i * sd, zb = za + sd, y = (i + 1) * sh, yPrev = i * sh;
                Add(o, 1, Vector3.back, V(x0, yPrev, za), V(x1, yPrev, za), V(x1, y, za), V(x0, y, za));       // 챌판
                Add(o, 0, Vector3.up, V(x0, y, za), V(x1, y, za), V(x1, y, zb), V(x0, y, zb));                  // 디딤판
                Add(o, 2, Vector3.left, V(x0, 0, za), V(x0, 0, zb), V(x0, y, zb), V(x0, y, za));
                Add(o, 3, Vector3.right, V(x1, 0, za), V(x1, 0, zb), V(x1, y, zb), V(x1, y, za));
            }
            float z1 = s.z / 2;
            Add(o, 4, Vector3.forward, V(x0, 0, z1), V(x1, 0, z1), V(x1, s.y, z1), V(x0, s.y, z1));
            Add(o, 5, Vector3.down, V(x0, 0, z0), V(x1, 0, z0), V(x1, 0, z1), V(x0, 0, z1));
        }

        // ── 박공 지붕 (용마루가 X 방향, 바닥 y=0 이 벽 위) ──
        static void Gable(Vector3 s, float oh, float t, List<RSFace> o)
        {
            float hw = s.x / 2, hd = s.z / 2, h = s.y;
            float slope = h / Mathf.Max(0.001f, hd);
            float xa = -hw - oh, xb = hw + oh;
            float ze = hd + oh;                       // 처마 끝 (앞 · 뒤)
            float ye = h - slope * ze;                // 처마 끝 높이 (윗면)
            // 윗면 (앞 · 뒤 경사)
            Vector3 nF = Vector3.Cross(V(1, 0, 0), V(0, h - ye, ze)).normalized; if (nF.y < 0) nF = -nF;
            Vector3 nB = new Vector3(nF.x, nF.y, -nF.z);
            Add(o, 0, nF, V(xa, ye, -ze), V(xb, ye, -ze), V(xb, h, 0), V(xa, h, 0));
            Add(o, 1, nB, V(xa, ye, ze), V(xb, ye, ze), V(xb, h, 0), V(xa, h, 0));
            // 처마 밑 (두께만큼 아래)
            Add(o, 2, -nF, V(xa, ye - t, -ze), V(xb, ye - t, -ze), V(xb, h - t, 0), V(xa, h - t, 0));
            Add(o, 2, -nB, V(xa, ye - t, ze), V(xb, ye - t, ze), V(xb, h - t, 0), V(xa, h - t, 0));
            // 박공벽 (벽 위 삼각형, 지붕 아래)
            float yb = Mathf.Max(0f, h - t);
            float zt = Mathf.Max(0f, (h - t) / slope);   // 지붕 밑면이 y=0 과 만나는 곳은 벽 안쪽
            if (h - t > 0.001f)
            {
                Add(o, 3, Vector3.left, V(-hw, 0, -Mathf.Min(hd, zt)), V(-hw, 0, Mathf.Min(hd, zt)), V(-hw, yb, 0));
                Add(o, 4, Vector3.right, V(hw, 0, -Mathf.Min(hd, zt)), V(hw, 0, Mathf.Min(hd, zt)), V(hw, yb, 0));
            }
            // 지붕 끝 (두께 띠): 양옆 경사 띠 4개 + 앞뒤 처마 띠 2개
            Add(o, 5, Vector3.left, V(xa, ye, -ze), V(xa, h, 0), V(xa, h - t, 0), V(xa, ye - t, -ze));
            Add(o, 5, Vector3.left, V(xa, ye, ze), V(xa, h, 0), V(xa, h - t, 0), V(xa, ye - t, ze));
            Add(o, 5, Vector3.right, V(xb, ye, -ze), V(xb, h, 0), V(xb, h - t, 0), V(xb, ye - t, -ze));
            Add(o, 5, Vector3.right, V(xb, ye, ze), V(xb, h, 0), V(xb, h - t, 0), V(xb, ye - t, ze));
            Add(o, 5, Vector3.back, V(xa, ye, -ze), V(xb, ye, -ze), V(xb, ye - t, -ze), V(xa, ye - t, -ze));
            Add(o, 5, Vector3.forward, V(xa, ye, ze), V(xb, ye, ze), V(xb, ye - t, ze), V(xa, ye - t, ze));
        }

        // ── 외쪽 지붕 (앞이 낮고 뒤가 높다) ──
        static void Shed(Vector3 s, float oh, float t, List<RSFace> o)
        {
            float hw = s.x / 2, hd = s.z / 2, h = s.y;
            float slope = h / Mathf.Max(0.001f, s.z);
            float xa = -hw - oh, xb = hw + oh, za = -hd - oh, zb = hd + oh;
            float ya = -slope * oh, yb = h + slope * oh;
            Vector3 n = Vector3.Cross(V(1, 0, 0), V(0, yb - ya, zb - za)).normalized; if (n.y < 0) n = -n;
            Add(o, 0, n, V(xa, ya, za), V(xb, ya, za), V(xb, yb, zb), V(xa, yb, zb));
            Add(o, 2, -n, V(xa, ya - t, za), V(xb, ya - t, za), V(xb, yb - t, zb), V(xa, yb - t, zb));
            // 옆벽 (벽 위 삼각형)
            float hi = h - t;
            if (hi > 0.001f)
            {
                float zStart = hd - hi / slope;   // 지붕 밑면이 y=0 과 만나는 z
                zStart = Mathf.Max(-hd, zStart);
                Add(o, 3, Vector3.left, V(-hw, 0, zStart), V(-hw, 0, hd), V(-hw, hi, hd));
                Add(o, 4, Vector3.right, V(hw, 0, zStart), V(hw, 0, hd), V(hw, hi, hd));
            }
            Add(o, 5, Vector3.left, V(xa, ya, za), V(xa, yb, zb), V(xa, yb - t, zb), V(xa, ya - t, za));
            Add(o, 5, Vector3.right, V(xb, ya, za), V(xb, yb, zb), V(xb, yb - t, zb), V(xb, ya - t, za));
            Add(o, 5, Vector3.back, V(xa, ya, za), V(xb, ya, za), V(xb, ya - t, za), V(xa, ya - t, za));
            Add(o, 5, Vector3.forward, V(xa, yb, zb), V(xb, yb, zb), V(xb, yb - t, zb), V(xa, yb - t, zb));
        }

        // ── 평지붕 (판 + 난간). 높이(Y) = 난간 꼭대기, 두께 = 판 두께 · 난간 두께 ──
        static void Flat(Vector3 s, float oh, float t, List<RSFace> o)
        {
            float hw = s.x / 2 + oh, hd = s.z / 2 + oh;
            float top = t;
            Box(V(-hw, 0, -hd), V(hw, top, hd), o, 5, 5, 5, 5, -1, 2);
            float ph = s.y - t;
            if (ph < 0.001f || t * 2 >= Mathf.Min(hw, hd) * 2)
            {
                Add(o, 0, Vector3.up, V(-hw, top, -hd), V(hw, top, -hd), V(hw, top, hd), V(-hw, top, hd));
                return;
            }
            float iw = hw - t, id = hd - t, py = top + ph;
            // 안쪽 바닥
            Add(o, 0, Vector3.up, V(-iw, top, -id), V(iw, top, -id), V(iw, top, id), V(-iw, top, id));
            // 난간 바깥 (옆 띠와 같은 면)
            Add(o, 5, Vector3.back, V(-hw, top, -hd), V(hw, top, -hd), V(hw, py, -hd), V(-hw, py, -hd));
            Add(o, 5, Vector3.forward, V(-hw, top, hd), V(hw, top, hd), V(hw, py, hd), V(-hw, py, hd));
            Add(o, 5, Vector3.left, V(-hw, top, -hd), V(-hw, top, hd), V(-hw, py, hd), V(-hw, py, -hd));
            Add(o, 5, Vector3.right, V(hw, top, -hd), V(hw, top, hd), V(hw, py, hd), V(hw, py, -hd));
            // 난간 안쪽
            Add(o, 6, Vector3.forward, V(-iw, top, -id), V(iw, top, -id), V(iw, py, -id), V(-iw, py, -id));
            Add(o, 6, Vector3.back, V(-iw, top, id), V(iw, top, id), V(iw, py, id), V(-iw, py, id));
            Add(o, 6, Vector3.right, V(-iw, top, -id), V(-iw, top, id), V(-iw, py, id), V(-iw, py, -id));
            Add(o, 6, Vector3.left, V(iw, top, -id), V(iw, top, id), V(iw, py, id), V(iw, py, -id));
            // 난간 위 (네 조각)
            Add(o, 7, Vector3.up, V(-hw, py, -hd), V(hw, py, -hd), V(hw, py, -id), V(-hw, py, -id));
            Add(o, 7, Vector3.up, V(-hw, py, id), V(hw, py, id), V(hw, py, hd), V(-hw, py, hd));
            Add(o, 7, Vector3.up, V(-hw, py, -id), V(-iw, py, -id), V(-iw, py, id), V(-hw, py, id));
            Add(o, 7, Vector3.up, V(iw, py, -id), V(hw, py, -id), V(hw, py, id), V(iw, py, id));
        }

        // ── 판 (세운 판, 앞(-Z) · 뒤 면) ──
        static void Plane(Vector3 s, List<RSFace> o)
        {
            float x0 = -s.x / 2, x1 = s.x / 2, h = s.y;
            Add(o, 0, Vector3.back, V(x0, 0, 0), V(x1, 0, 0), V(x1, h, 0), V(x0, h, 0));
            Add(o, 1, Vector3.forward, V(x0, 0, 0), V(x1, 0, 0), V(x1, h, 0), V(x0, h, 0));
        }

        // ─────────────────────────────────────────────────────────────
        // 면 좌표 (텍스처 방향)
        // ─────────────────────────────────────────────────────────────

        /// <summary>면 축: u = 오른쪽, v = 위쪽 (바깥에서 봤을 때). 거의 수평이면 u = right, v = forward (위면) / -forward (아랫면)</summary>
        public static void Frame(Vector3 n, Vector3 up, Vector3 right, out Vector3 u, out Vector3 v)
        {
            n = n.normalized;
            float ny = Vector3.Dot(n, up);
            if (Mathf.Abs(ny) > 0.999f)
            {
                u = (right - n * Vector3.Dot(right, n)).normalized;
                v = Vector3.Cross(u, n);
                return;
            }
            u = Vector3.Cross(n, up).normalized;
            v = Vector3.Cross(u, n);
        }
    }
}
