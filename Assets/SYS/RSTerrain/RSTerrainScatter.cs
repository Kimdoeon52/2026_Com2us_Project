// RE:AL STEEL - RS Terrain 폐자재 조각(RSPiece) · 머티리얼별 메시 합치기(RSDebrisSink)
//
// RSScatter 가 쓴다. MonoBehaviour 가 아니므로 파일 이름과 달라도 된다.
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Terrain
{
    // ═════════════════════════════════════════════════════════════════
    // 폐자재 조각 (볼록 다각형 목록) — 각진 면, 찌그러뜨리기
    // ═════════════════════════════════════════════════════════════════

    public class RSPiece
    {
        public readonly List<Vector3[]> polys = new List<Vector3[]>();
        public readonly List<Vector3> outward = new List<Vector3>();

        void Poly(Vector3[] pts, Vector3 o) { polys.Add(pts); outward.Add(o); }

        /// <summary>같은 위치 → 같은 오프셋이라 면끼리 안 벌어진다</summary>
        public void Dent(float amount, int salt)
        {
            foreach (var pts in polys)
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector3 v = pts[i];
                    float nx = Mathf.PerlinNoise(v.x * 3.1f + salt, v.z * 3.1f + salt * 0.5f) - 0.5f;
                    float ny = Mathf.PerlinNoise(v.z * 2.9f - salt, v.y * 2.9f + salt * 0.8f) - 0.5f;
                    float nz = Mathf.PerlinNoise(v.y * 2.7f + salt * 0.3f, v.x * 2.7f - salt) - 0.5f;
                    pts[i] = v + new Vector3(nx, ny, nz) * amount * 2f;
                }
        }

        public static RSPiece Box(Vector3 size)
        {
            var h = size * 0.5f;
            var p = new RSPiece();
            p.Poly(new[] { V(h, 1, -1, -1), V(h, 1, 1, -1), V(h, 1, 1, 1), V(h, 1, -1, 1) }, Vector3.right);
            p.Poly(new[] { V(h, -1, -1, -1), V(h, -1, -1, 1), V(h, -1, 1, 1), V(h, -1, 1, -1) }, Vector3.left);
            p.Poly(new[] { V(h, -1, 1, -1), V(h, -1, 1, 1), V(h, 1, 1, 1), V(h, 1, 1, -1) }, Vector3.up);
            p.Poly(new[] { V(h, -1, -1, -1), V(h, 1, -1, -1), V(h, 1, -1, 1), V(h, -1, -1, 1) }, Vector3.down);
            p.Poly(new[] { V(h, -1, -1, 1), V(h, 1, -1, 1), V(h, 1, 1, 1), V(h, -1, 1, 1) }, Vector3.forward);
            p.Poly(new[] { V(h, -1, -1, -1), V(h, -1, 1, -1), V(h, 1, 1, -1), V(h, 1, -1, -1) }, Vector3.back);
            return p;
        }

        static Vector3 V(Vector3 h, int sx, int sy, int sz) { return new Vector3(h.x * sx, h.y * sy, h.z * sz); }

        /// <summary>X 축을 따라 누운 n각 기둥 (파이프 · 드럼통)</summary>
        public static RSPiece PrismX(int n, float r, float len, float rotOffset)
        {
            var p = new RSPiece();
            var r0 = new Vector3[n]; var r1 = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float a = rotOffset + k * Mathf.PI * 2f / n;
                r0[k] = new Vector3(-len * 0.5f, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                r1[k] = new Vector3( len * 0.5f, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            for (int k = 0; k < n; k++)
            {
                int k2 = (k + 1) % n;
                float am = rotOffset + (k + 0.5f) * Mathf.PI * 2f / n;
                p.Poly(new[] { r0[k], r0[k2], r1[k2], r1[k] }, new Vector3(0f, Mathf.Cos(am), Mathf.Sin(am)));
            }
            p.Poly((Vector3[])r0.Clone(), Vector3.left);
            p.Poly((Vector3[])r1.Clone(), Vector3.right);
            return p;
        }

        /// <summary>XZ 평면에 누운 토러스 (타이어)</summary>
        public static RSPiece Torus(float R, float rt, int nu, int nv)
        {
            var p = new RSPiece();
            for (int iu = 0; iu < nu; iu++)
                for (int iv = 0; iv < nv; iv++)
                {
                    Vector3 a = TP(R, rt, iu, iv, nu, nv), b = TP(R, rt, iu + 1, iv, nu, nv);
                    Vector3 c = TP(R, rt, iu + 1, iv + 1, nu, nv), d = TP(R, rt, iu, iv + 1, nu, nv);
                    float um = (iu + 0.5f) * Mathf.PI * 2f / nu;
                    Vector3 ring = new Vector3(Mathf.Cos(um) * R, 0f, Mathf.Sin(um) * R);
                    p.Poly(new[] { a, b, c, d }, (a + b + c + d) * 0.25f - ring);
                }
            return p;
        }

        static Vector3 TP(float R, float rt, int iu, int iv, int nu, int nv)
        {
            float u = iu * Mathf.PI * 2f / nu, v = iv * Mathf.PI * 2f / nv;
            float rr = R + rt * Mathf.Cos(v);
            return new Vector3(rr * Mathf.Cos(u), rt * Mathf.Sin(v), rr * Mathf.Sin(u));
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 머티리얼별로 합치는 버퍼
    // ═════════════════════════════════════════════════════════════════

    public class RSDebrisSink
    {
        public class Buffer
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            public bool collider;
        }

        public readonly Dictionary<Material, Buffer> buffers = new Dictionary<Material, Buffer>();
        readonly Buffer nullBuffer = new Buffer();   // 머티리얼이 빈 것끼리

        public Buffer Get(Material m)
        {
            if (m == null) return nullBuffer;
            Buffer b;
            if (!buffers.TryGetValue(m, out b)) { b = new Buffer(); buffers[m] = b; }
            return b;
        }

        public Buffer NullBuffer { get { return nullBuffer; } }

        /// <summary>조각을 위치·회전해서 넣는다 (좌표 = 지형 로컬). UV 는 월드 주축 투영 (64px 타일 / 32PPU).</summary>
        public void Add(Material m, RSPiece piece, Vector3 pos, Quaternion rot, bool collider)
        {
            var b = Get(m);
            b.collider |= collider;
            for (int pi = 0; pi < piece.polys.Count; pi++)
            {
                var src = piece.polys[pi];
                int cnt = src.Length;
                var w = new Vector3[cnt];
                for (int k = 0; k < cnt; k++) w[k] = pos + rot * src[k];
                Vector3 o = rot * piece.outward[pi];

                Vector3 nrm = Vector3.zero;
                for (int k = 1; k < cnt - 1; k++) nrm += Vector3.Cross(w[k] - w[0], w[k + 1] - w[0]);
                bool flip = Vector3.Dot(nrm, o) < 0f;
                if (flip) nrm = -nrm;
                nrm = nrm.normalized;

                Vector3 an = new Vector3(Mathf.Abs(nrm.x), Mathf.Abs(nrm.y), Mathf.Abs(nrm.z));
                int i0 = b.v.Count;
                for (int k = 0; k < cnt; k++)
                {
                    Vector3 q = w[k];
                    b.v.Add(q);
                    b.n.Add(nrm);
                    Vector2 uv = an.y >= an.x && an.y >= an.z ? new Vector2(q.x, q.z)
                               : an.x >= an.z ? new Vector2(q.z, q.y) : new Vector2(q.x, q.y);
                    b.uv.Add(uv * 0.5f);
                }
                for (int k = 1; k < cnt - 1; k++)
                {
                    b.t.Add(i0);
                    b.t.Add(flip ? i0 + k + 1 : i0 + k);
                    b.t.Add(flip ? i0 + k : i0 + k + 1);
                }
            }
        }
    }
}
