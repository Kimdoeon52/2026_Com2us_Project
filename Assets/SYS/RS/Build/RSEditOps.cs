// RE:AL STEEL - 편집 메시 연산 (모양 바꾸기). 에디터 도구가 Undo 를 걸고 부른다
//  상자 추가 · 메시 가져오기(점 합치기 + 같은 평면 삼각형 합치기) · 자르기 · 밀어내기 · 지우기 · 합치기 · 복제 · 뒤집기
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Build
{
    /// <summary>그려서 만드는 도형</summary>
    public enum RSEShape { 상자, 원기둥, 판, 계단, 쐐기, 지붕 }

    public static class RSEditOps
    {
        // ─────────────────────────────────────────────────────────────
        // 기본
        // ─────────────────────────────────────────────────────────────

        static long EdgeKey(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }
        static long DirKey(int a, int b) { return ((long)a << 32) | (uint)b; }

        public static bool RayTri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0;
            Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (Mathf.Abs(det) < 1e-12f) return false;
            float inv = 1f / det;
            Vector3 tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv;
            if (u < 0 || u > 1) return false;
            Vector3 qv = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, qv) * inv;
            if (v < 0 || u + v > 1) return false;
            t = Vector3.Dot(e2, qv) * inv;
            return t > 1e-6f;
        }

        static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        static float SignedArea(Vector2[] p, List<int> idx)
        {
            float s = 0;
            for (int i = 0; i < idx.Count; i++) { var a = p[idx[i]]; var b = p[idx[(i + 1) % idx.Count]]; s += a.x * b.y - b.x * a.y; }
            return s * 0.5f;
        }

        /// <summary>귀 자르기 삼각분할 (볼록 · 오목 다각형). 감기 방향 유지</summary>
        public static void EarClip(Vector2[] p, List<int> outTris)
        {
            outTris.Clear();
            int n = p.Length;
            if (n < 3) return;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            float sgn = SignedArea(p, idx) >= 0 ? 1f : -1f;
            float scale = 0;
            for (int i = 0; i < n; i++) scale = Mathf.Max(scale, p[i].sqrMagnitude);
            float eps = 1e-9f * Mathf.Max(1f, scale);
            int guard = n * n + 8;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool found = false;
                int cnt = idx.Count;
                for (int k = 0; k < cnt; k++)
                {
                    int ia = idx[(k + cnt - 1) % cnt], ib = idx[k], ic = idx[(k + 1) % cnt];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if (sgn * Cross2(b - a, c - b) <= eps) continue;   // 오목 · 일직선
                    bool inside = false;
                    for (int m = 0; m < cnt && !inside; m++)
                    {
                        int im = idx[m];
                        if (im == ia || im == ib || im == ic) continue;
                        Vector2 q = p[im];
                        if (q == a || q == b || q == c) continue;
                        float d1 = sgn * Cross2(b - a, q - a), d2 = sgn * Cross2(c - b, q - b), d3 = sgn * Cross2(a - c, q - c);
                        if (d1 > -eps && d2 > -eps && d3 > -eps) inside = true;
                    }
                    if (inside) continue;
                    outTris.Add(ia); outTris.Add(ib); outTris.Add(ic);
                    idx.RemoveAt(k);
                    found = true;
                    break;
                }
                if (!found)
                {
                    // 꼬인 다각형: 남은 것은 부채꼴로
                    for (int k = 1; k + 1 < idx.Count; k++) { outTris.Add(idx[0]); outTris.Add(idx[k]); outTris.Add(idx[k + 1]); }
                    return;
                }
            }
            if (idx.Count == 3) { outTris.Add(idx[0]); outTris.Add(idx[1]); outTris.Add(idx[2]); }
        }

        /// <summary>면마다 부품 번호 (점을 공유하면 같은 부품)</summary>
        public static int[] Components(RSEditMesh m, out int count)
        {
            int nv = m.verts.Count;
            var par = new int[nv];
            for (int i = 0; i < nv; i++) par[i] = i;
            int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
            for (int f = 0; f < m.faces.Count; f++)
            {
                if (!m.ValidFace(f)) continue;
                var vv = m.faces[f].v;
                int r0 = Find(vv[0]);
                for (int i = 1; i < vv.Length; i++) { int r = Find(vv[i]); if (r != r0) par[r] = r0; }
            }
            var ids = new Dictionary<int, int>();
            var res = new int[m.faces.Count];
            for (int f = 0; f < m.faces.Count; f++)
            {
                if (!m.ValidFace(f)) { res[f] = -1; continue; }
                int r = Find(m.faces[f].v[0]);
                if (!ids.TryGetValue(r, out int id)) { id = ids.Count; ids[r] = id; }
                res[f] = id;
            }
            count = ids.Count;
            return res;
        }

        /// <summary>그 면과 이어진 면 전체 (부품)</summary>
        public static HashSet<int> Linked(RSEditMesh m, int face)
        {
            var set = new HashSet<int>();
            if (!m.ValidFace(face)) return set;
            var parts = m.Parts(out _);
            int p = parts[face];
            for (int f = 0; f < parts.Length; f++) if (parts[f] == p) set.Add(f);
            return set;
        }

        /// <summary>중복 없는 선 목록 (점 번호 두 개)</summary>
        public static void Edges(RSEditMesh m, List<Vector2Int> into)
        {
            into.Clear();
            var seen = new HashSet<long>();
            for (int f = 0; f < m.faces.Count; f++)
            {
                if (!m.ValidFace(f)) continue;
                var vv = m.faces[f].v;
                for (int i = 0; i < vv.Length; i++)
                {
                    int a = vv[i], b = vv[(i + 1) % vv.Length];
                    if (a == b) continue;
                    if (seen.Add(EdgeKey(a, b))) into.Add(new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b)));
                }
            }
        }

        /// <summary>면이 그 선(두 점이 이웃)을 쓰나</summary>
        public static bool FaceHasEdge(int[] vv, int a, int b)
        {
            for (int i = 0; i < vv.Length; i++)
            {
                int x = vv[i], y = vv[(i + 1) % vv.Length];
                if ((x == a && y == b) || (x == b && y == a)) return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────
        // 만들기
        // ─────────────────────────────────────────────────────────────

        static Vector3 Newell(RSEditMesh m, int[] loop)
        {
            Vector3 n = Vector3.zero;
            for (int i = 0; i < loop.Length; i++) n += Vector3.Cross(m.verts[loop[i]], m.verts[loop[(i + 1) % loop.Length]]);
            return n;
        }

        /// <summary>면 추가. 법선이 outward 와 반대면 순서를 뒤집는다</summary>
        public static int AddFaceOutward(RSEditMesh m, int[] loop, Vector3 outward, RSEFace paint)
        {
            if (Vector3.Dot(Newell(m, loop), outward) < 0) System.Array.Reverse(loop);
            var f = new RSEFace { v = loop };
            if (paint != null) { f.CopyPaint(paint); f.fitGroup = 0; f.mode = RSEditPaintMode.반복; }
            m.faces.Add(f);
            return m.faces.Count - 1;
        }

        /// <summary>상자 추가 (bottomCenter = 바닥 가운데, 로컬). 새 면 번호들</summary>
        public static List<int> AddBox(RSEditMesh m, Vector3 bottomCenter, Vector3 size, RSEFace paint)
        {
            var res = new List<int>();
            int b0 = m.verts.Count;
            Vector3 h = new Vector3(size.x * 0.5f, 0, size.z * 0.5f);
            for (int y = 0; y < 2; y++)
            {
                float yy = bottomCenter.y + y * size.y;
                m.verts.Add(new Vector3(bottomCenter.x - h.x, yy, bottomCenter.z - h.z));
                m.verts.Add(new Vector3(bottomCenter.x + h.x, yy, bottomCenter.z - h.z));
                m.verts.Add(new Vector3(bottomCenter.x + h.x, yy, bottomCenter.z + h.z));
                m.verts.Add(new Vector3(bottomCenter.x - h.x, yy, bottomCenter.z + h.z));
            }
            int[][] quads =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 },
                new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            };
            Vector3 center = bottomCenter + Vector3.up * size.y * 0.5f;
            foreach (var q in quads)
            {
                var loop = new[] { b0 + q[0], b0 + q[1], b0 + q[2], b0 + q[3] };
                Vector3 fc = Vector3.zero; foreach (int i in loop) fc += m.verts[i]; fc /= 4f;
                res.Add(AddFaceOutward(m, loop, fc - center, paint));
            }
            return res;
        }

        // ─────────────────────────────────────────────────────────────
        // 그려서 만들기: 도형 → 다각형 목록 (미리보기와 만들기가 같이 쓴다)
        // ─────────────────────────────────────────────────────────────

        /// <summary>밑면 다각형 loop 를 E 만큼 밀어 올린 기둥 (모든 면이 바깥을 본다)</summary>
        public static void PrismPolys(Vector3[] loop, Vector3 E, List<Vector3[]> into)
        {
            int n = loop.Length;
            if (n < 3 || E.sqrMagnitude < 1e-12f) return;
            Vector3 nw = Vector3.zero;
            for (int i = 0; i < n; i++) nw += Vector3.Cross(loop[i], loop[(i + 1) % n]);
            if (nw.sqrMagnitude < 1e-14f) return;
            var b = (Vector3[])loop.Clone();
            if (Vector3.Dot(nw, E) > 0) System.Array.Reverse(b);      // 밑면은 -E 쪽을 본다
            into.Add(b);
            var t = new Vector3[n];
            for (int i = 0; i < n; i++) t[i] = b[n - 1 - i] + E;
            into.Add(t);
            for (int i = 0; i < n; i++)
            {
                Vector3 a = b[i], c = b[(i + 1) % n];
                into.Add(new[] { c, a, a + E, c + E });                 // 밑면 a→c 의 이웃은 c→a
            }
        }

        /// <summary>양면 판: 선분 s→e 를 E 만큼 세운 사각형 앞 · 뒤</summary>
        public static void PlatePolys(Vector3 s, Vector3 e, Vector3 E, List<Vector3[]> into)
        {
            if ((e - s).sqrMagnitude < 1e-12f || E.sqrMagnitude < 1e-12f) return;
            var q = new[] { s, e, e + E, s + E };
            into.Add(q);
            into.Add(new[] { q[3], q[2], q[1], q[0] });
        }

        /// <summary>그리기 도형. O = 평면 원점, A · B = 평면 축, N = 위(법선), start · end = 평면 좌표, h = 높이 (모두 로컬)</summary>
        public static void ShapePolys(RSEShape shape, Vector3 O, Vector3 A, Vector3 B, Vector3 N, Vector2 start, Vector2 end, float h,
                                      int sides, int steps, float minThick, List<Vector3[]> into)
        {
            into.Clear();
            if (Mathf.Abs(h) < 1e-6f) return;
            Vector3 P(float a, float b, float y) { return O + A * a + B * b + N * y; }
            if (shape == RSEShape.판) { PlatePolys(P(start.x, start.y, 0), P(end.x, end.y, 0), N * h, into); return; }

            Vector2 lo = Vector2.Min(start, end), hi = Vector2.Max(start, end);
            // 한쪽이 거의 0 이면 얇은 벽 (1 픽셀)
            if (hi.x - lo.x < minThick) { float c = (lo.x + hi.x) * 0.5f; lo.x = c - minThick * 0.5f; hi.x = c + minThick * 0.5f; }
            if (hi.y - lo.y < minThick) { float c = (lo.y + hi.y) * 0.5f; lo.y = c - minThick * 0.5f; hi.y = c + minThick * 0.5f; }
            float W = hi.x - lo.x, D = hi.y - lo.y;
            switch (shape)
            {
                case RSEShape.상자:
                    PrismPolys(new[] { P(lo.x, lo.y, 0), P(hi.x, lo.y, 0), P(hi.x, hi.y, 0), P(lo.x, hi.y, 0) }, N * h, into);
                    break;
                case RSEShape.원기둥:
                    {
                        int n = Mathf.Clamp(sides, 3, 64);
                        Vector2 c = (lo + hi) * 0.5f;
                        float k = 1f / Mathf.Cos(Mathf.PI / n);            // 평평한 변이 네모에 닿게
                        var loop = new Vector3[n];
                        for (int i = 0; i < n; i++)
                        {
                            float t = Mathf.PI * 2f * i / n + Mathf.PI / n;
                            loop[i] = P(c.x + Mathf.Cos(t) * W * 0.5f * k, c.y + Mathf.Sin(t) * D * 0.5f * k, 0);
                        }
                        PrismPolys(loop, N * h, into);
                    }
                    break;
                case RSEShape.계단:
                case RSEShape.쐐기:
                    {
                        // 끈 방향(B)으로 올라간다. 옆 단면을 A 방향으로 민다
                        float sg = end.y >= start.y ? 1f : -1f;
                        float b0 = sg > 0 ? lo.y : hi.y;
                        Vector3 Q(float t, float y) { return P(lo.x, b0 + sg * t, y); }
                        var prof = new List<Vector3> { Q(0, 0), Q(D, 0), Q(D, h) };
                        if (shape == RSEShape.계단)
                        {
                            int n = Mathf.Clamp(steps, 1, 64);
                            float st = D / n, r = h / n;
                            for (int i = n - 1; i >= 1; i--) { prof.Add(Q(i * st, (i + 1) * r)); prof.Add(Q(i * st, i * r)); }
                            prof.Add(Q(0, r));
                        }
                        PrismPolys(prof.ToArray(), A * W, into);
                    }
                    break;
                case RSEShape.지붕:
                    {
                        // 용마루는 긴 쪽 방향
                        if (W >= D)
                            PrismPolys(new[] { P(lo.x, lo.y, 0), P(lo.x, hi.y, 0), P(lo.x, (lo.y + hi.y) * 0.5f, h) }, A * W, into);
                        else
                            PrismPolys(new[] { P(lo.x, lo.y, 0), P(hi.x, lo.y, 0), P((lo.x + hi.x) * 0.5f, lo.y, h) }, B * D, into);
                    }
                    break;
            }
        }

        /// <summary>다각형들을 면으로 더한다 (같은 자리 점은 하나로). 새 면 번호들</summary>
        public static List<int> AddPolys(RSEditMesh m, List<Vector3[]> polys, RSEFace paint)
        {
            var res = new List<int>();
            var map = new Dictionary<Vector3Int, int>();
            foreach (var poly in polys)
            {
                var loop = new int[poly.Length];
                for (int i = 0; i < poly.Length; i++)
                {
                    var k = Q(poly[i], 1e-5f);
                    if (!map.TryGetValue(k, out int idx)) { idx = m.verts.Count; m.verts.Add(poly[i]); map[k] = idx; }
                    loop[i] = idx;
                }
                if (!Distinct(loop)) continue;
                var f = new RSEFace { v = loop };
                if (paint != null) { f.CopyPaint(paint); f.fitGroup = 0; f.mode = RSEditPaintMode.반복; }
                m.faces.Add(f);
                res.Add(m.faces.Count - 1);
            }
            m.version++;
            return res;
        }

        /// <summary>유니티 메시를 덧붙인다 (toLocal = 메시 → 편집 메시 로컬). 같은 자리 점은 합친다. 새 면 수</summary>
        public static int AppendMesh(RSEditMesh m, Mesh src, Matrix4x4 toLocal, float weld = 1e-4f, IList<RealSteel.Common.RSSurface> surfaces = null)
        {
            if (src == null) return 0;
            var pos = src.vertices;
            bool flipWinding = toLocal.determinant < 0;
            var grid = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < m.verts.Count; i++) grid[Q(m.verts[i], weld)] = i;
            var map = new int[pos.Length];
            for (int i = 0; i < pos.Length; i++)
            {
                Vector3 p = toLocal.MultiplyPoint3x4(pos[i]);
                var k = Q(p, weld);
                if (!grid.TryGetValue(k, out int idx)) { idx = m.verts.Count; m.verts.Add(p); grid[k] = idx; }
                map[i] = idx;
            }
            int added = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var topo = src.GetTopology(s);
                int step = topo == MeshTopology.Triangles ? 3 : topo == MeshTopology.Quads ? 4 : 0;
                if (step == 0) continue;
                var ind = src.GetIndices(s);
                for (int t = 0; t + step <= ind.Length; t += step)
                {
                    var loop = new int[step];
                    for (int k = 0; k < step; k++) loop[k] = map[ind[t + k]];
                    if (flipWinding) System.Array.Reverse(loop);
                    if (!Distinct(loop)) continue;
                    m.faces.Add(new RSEFace { v = loop, surface = surfaces != null && s < surfaces.Count ? surfaces[s] : null });
                    added++;
                }
            }
            return added;
        }

        static Vector3Int Q(Vector3 p, float e) { return new Vector3Int(Mathf.RoundToInt(p.x / e), Mathf.RoundToInt(p.y / e), Mathf.RoundToInt(p.z / e)); }

        static bool Distinct(int[] loop)
        {
            for (int i = 0; i < loop.Length; i++)
                for (int j = i + 1; j < loop.Length; j++)
                    if (loop[i] == loop[j]) return false;
            return true;
        }

        static bool SamePaint(RSEFace a, RSEFace b)
        {
            return a.surface == b.surface && a.mode == b.mode && a.rotate == b.rotate && a.flip == b.flip && a.offset == b.offset && a.fitGroup == b.fitGroup;
        }

        /// <summary>같은 평면에 붙은 이웃 면을 볼록 다각형으로 합친다 (가져온 삼각형 → 사각형 · 다각형). 합친 횟수</summary>
        public static int MergeCoplanar(RSEditMesh m, float maxAngleDeg = 0.5f)
        {
            float cosTol = Mathf.Cos(maxAngleDeg * Mathf.Deg2Rad);
            int total = 0;
            for (int pass = 0; pass < 64; pass++)
            {
                int nf = m.faces.Count;
                var normals = new Vector3[nf];
                var dir = new Dictionary<long, int>();
                for (int f = 0; f < nf; f++)
                {
                    if (!m.ValidFace(f)) continue;
                    normals[f] = m.FaceNormal(f);
                    var vv = m.faces[f].v;
                    for (int i = 0; i < vv.Length; i++) dir[DirKey(vv[i], vv[(i + 1) % vv.Length])] = f;
                }
                var removed = new bool[nf];
                var touched = new bool[nf];
                int merged = 0;
                for (int f = 0; f < nf; f++)
                {
                    if (removed[f] || touched[f] || !m.ValidFace(f)) continue;
                    var vv = m.faces[f].v;
                    for (int i = 0; i < vv.Length; i++)
                    {
                        int a = vv[i], b = vv[(i + 1) % vv.Length];
                        if (!dir.TryGetValue(DirKey(b, a), out int g) || g == f || removed[g] || touched[g]) continue;
                        if (Vector3.Dot(normals[f], normals[g]) < cosTol || !SamePaint(m.faces[f], m.faces[g])) continue;
                        var gv = m.faces[g].v;
                        int j = System.Array.IndexOf(gv, b);
                        if (j < 0 || gv[(j + 1) % gv.Length] != a) continue;
                        // f: [b ... a], g: [a ... b] → [b ... a] + g 안쪽
                        var loop = new List<int>(vv.Length + gv.Length - 2);
                        int s0 = (i + 1) % vv.Length;
                        for (int k = 0; k < vv.Length; k++) loop.Add(vv[(s0 + k) % vv.Length]);
                        int t0 = (j + 1) % gv.Length;   // a 자리
                        for (int k = 1; k < gv.Length - 1; k++) loop.Add(gv[(t0 + k) % gv.Length]);
                        var arr = loop.ToArray();
                        if (!Distinct(arr) || !Convex(m, arr, normals[f])) continue;
                        m.faces[f].v = arr;
                        removed[g] = true; touched[f] = true; touched[g] = true;
                        merged++;
                        break;
                    }
                }
                if (merged == 0) break;
                total += merged;
                var keep = new List<RSEFace>(nf - merged);
                for (int f = 0; f < nf; f++) if (!removed[f]) keep.Add(m.faces[f]);
                m.faces.Clear(); m.faces.AddRange(keep);
                m.version++;
            }
            return total;
        }

        static bool Convex(RSEditMesh m, int[] loop, Vector3 n)
        {
            int c = loop.Length;
            float scale = 0;
            for (int i = 0; i < c; i++) scale = Mathf.Max(scale, (m.verts[loop[i]] - m.verts[loop[0]]).magnitude);
            float eps = 1e-5f * scale * scale;
            for (int i = 0; i < c; i++)
            {
                Vector3 a = m.verts[loop[i]], b = m.verts[loop[(i + 1) % c]], d = m.verts[loop[(i + 2) % c]];
                if (Vector3.Dot(Vector3.Cross(b - a, d - b), n) < -eps) return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // 자르기
        // ─────────────────────────────────────────────────────────────

        /// <summary>면들을 평면(점 · 법선, 로컬)으로 자른다. 잘린 선의 이웃 면에도 점을 끼워 틈이 안 생기게. 새로 생긴 면 번호</summary>
        public static List<int> Cut(RSEditMesh m, ICollection<int> faceSet, Vector3 planePoint, Vector3 planeN)
        {
            var created = new List<int>();
            planeN = planeN.normalized;
            float d0 = Vector3.Dot(planeN, planePoint);
            const float eps = 1e-5f;
            var edgeVert = new Dictionary<long, int>();
            var edgeEnds = new Dictionary<long, Vector2Int>();
            float Dist(int vi) { return Vector3.Dot(planeN, m.verts[vi]) - d0; }
            int Side(int vi) { float d = Dist(vi); return d > eps ? 1 : d < -eps ? -1 : 0; }
            int EdgeVert(int a, int b)
            {
                long k = EdgeKey(a, b);
                if (edgeVert.TryGetValue(k, out int idx)) return idx;
                float da = Dist(a), db = Dist(b);
                float t = da / (da - db);
                idx = m.verts.Count;
                m.verts.Add(Vector3.Lerp(m.verts[a], m.verts[b], t));
                edgeVert[k] = idx; edgeEnds[k] = new Vector2Int(a, b);
                return idx;
            }

            var list = new List<int>(faceSet);
            foreach (int f in list)
            {
                if (!m.ValidFace(f)) continue;
                var vv = m.faces[f].v;
                bool hasPos = false, hasNeg = false;
                foreach (int vi in vv) { int s = Side(vi); if (s > 0) hasPos = true; if (s < 0) hasNeg = true; }
                if (!hasPos || !hasNeg) continue;
                var pos = new List<int>(); var neg = new List<int>();
                for (int i = 0; i < vv.Length; i++)
                {
                    int a = vv[i], b = vv[(i + 1) % vv.Length];
                    int sa = Side(a), sb = Side(b);
                    if (sa >= 0) pos.Add(a);
                    if (sa <= 0) neg.Add(a);
                    if (sa * sb < 0) { int c = EdgeVert(a, b); pos.Add(c); neg.Add(c); }
                }
                if (pos.Count < 3 || neg.Count < 3) continue;
                var old = m.faces[f];
                if (old.mode == RSEditPaintMode.딱맞게 && old.fitGroup == 0) old.fitGroup = m.NewGroup();
                old.v = pos.ToArray();
                var nf = new RSEFace { v = neg.ToArray() };
                nf.CopyPaint(old);
                m.faces.Add(nf);
                created.Add(m.faces.Count - 1);
            }

            // 이웃 면의 같은 선에 새 점 끼우기 (T 자 틈 방지)
            if (edgeVert.Count > 0)
            {
                for (int f = 0; f < m.faces.Count; f++)
                {
                    if (!m.ValidFace(f)) continue;
                    var vv = m.faces[f].v;
                    var nl = new List<int>(vv.Length + 2);
                    bool changed = false;
                    for (int i = 0; i < vv.Length; i++)
                    {
                        int a = vv[i], b = vv[(i + 1) % vv.Length];
                        nl.Add(a);
                        if (edgeVert.TryGetValue(EdgeKey(a, b), out int c) && System.Array.IndexOf(vv, c) < 0) { nl.Add(c); changed = true; }
                    }
                    if (changed) m.faces[f].v = nl.ToArray();
                }
            }
            m.version++;
            return created;
        }

        // ─────────────────────────────────────────────────────────────
        // 밀어내기
        // ─────────────────────────────────────────────────────────────

        /// <summary>면들을 밀어낸다: 고른 면이 쓰는 점을 복제해 옮기고, 가장자리마다 옆면을 만든다.
        /// 반환 = 움직일 새 점들. 평균 법선은 out</summary>
        public static HashSet<int> Extrude(RSEditMesh m, ICollection<int> faceSet, out Vector3 avgNormal, List<int> sideFaces = null)
        {
            avgNormal = Vector3.zero;
            var sel = new HashSet<int>();
            foreach (int f in faceSet) if (m.ValidFace(f)) sel.Add(f);
            var moved = new HashSet<int>();
            if (sel.Count == 0) return moved;

            // 고른 면들의 방향 있는 선 → 반대 방향 선이 고른 면 안에 없으면 가장자리
            var inSel = new HashSet<long>();
            foreach (int f in sel)
            {
                var vv = m.faces[f].v;
                for (int i = 0; i < vv.Length; i++) inSel.Add(DirKey(vv[i], vv[(i + 1) % vv.Length]));
                avgNormal += m.FaceNormal(f);
            }
            avgNormal = avgNormal.sqrMagnitude > 1e-12f ? avgNormal.normalized : Vector3.up;

            var dup = new Dictionary<int, int>();
            int Dup(int vi)
            {
                if (dup.TryGetValue(vi, out int d)) return d;
                d = m.verts.Count; m.verts.Add(m.verts[vi]); dup[vi] = d; moved.Add(d);
                return d;
            }

            var sides = new List<RSEFace>();
            foreach (int f in sel)
            {
                var face = m.faces[f];
                var vv = face.v;
                for (int i = 0; i < vv.Length; i++)
                {
                    int a = vv[i], b = vv[(i + 1) % vv.Length];
                    if (inSel.Contains(DirKey(b, a))) continue;      // 안쪽 선
                    var side = new RSEFace { v = new[] { a, b, Dup(b), Dup(a) } };
                    side.CopyPaint(face);
                    side.mode = RSEditPaintMode.반복; side.fitGroup = 0;
                    sides.Add(side);
                }
            }
            foreach (int f in sel)
            {
                var vv = m.faces[f].v;
                for (int i = 0; i < vv.Length; i++) vv[i] = Dup(vv[i]);
            }
            foreach (var s in sides) { m.faces.Add(s); if (sideFaces != null) sideFaces.Add(m.faces.Count - 1); }
            m.version++;
            return moved;
        }

        // ─────────────────────────────────────────────────────────────
        // 지우기 · 합치기 · 복제 · 뒤집기
        // ─────────────────────────────────────────────────────────────

        public static void DeleteFaces(RSEditMesh m, ICollection<int> set)
        {
            var del = new HashSet<int>(set);
            var keep = new List<RSEFace>(m.faces.Count);
            for (int f = 0; f < m.faces.Count; f++) if (!del.Contains(f)) keep.Add(m.faces[f]);
            m.faces.Clear(); m.faces.AddRange(keep);
            Compact(m);
        }

        public static void DeleteVerts(RSEditMesh m, ICollection<int> set)
        {
            var del = new HashSet<int>(set);
            var faces = new List<int>();
            for (int f = 0; f < m.faces.Count; f++)
                foreach (int vi in m.faces[f].v) if (del.Contains(vi)) { faces.Add(f); break; }
            DeleteFaces(m, faces);
        }

        /// <summary>점들을 한 점(pos)으로 합친다. 찌그러진 면은 지운다</summary>
        public static void MergeVerts(RSEditMesh m, ICollection<int> set, Vector3 pos)
        {
            if (set.Count < 2) return;
            int target = int.MaxValue;
            foreach (int v in set) target = Mathf.Min(target, v);
            m.verts[target] = pos;
            var mapTo = new HashSet<int>(set);
            var keep = new List<RSEFace>();
            foreach (var f in m.faces)
            {
                var nl = new List<int>(f.v.Length);
                foreach (int vi in f.v)
                {
                    int x = mapTo.Contains(vi) ? target : vi;
                    if (nl.Count == 0 || nl[nl.Count - 1] != x) nl.Add(x);
                }
                while (nl.Count > 1 && nl[0] == nl[nl.Count - 1]) nl.RemoveAt(nl.Count - 1);
                var arr = nl.ToArray();
                if (arr.Length < 3 || !Distinct(arr)) continue;
                f.v = arr;
                keep.Add(f);
            }
            m.faces.Clear(); m.faces.AddRange(keep);
            Compact(m);
        }

        /// <summary>면 복제 (새 점). 새 면 번호들</summary>
        public static List<int> Duplicate(RSEditMesh m, ICollection<int> set)
        {
            var res = new List<int>();
            var map = new Dictionary<int, int>();
            var src = new List<int>(set);
            src.Sort();
            foreach (int f in src)
            {
                if (!m.ValidFace(f)) continue;
                var nf = m.faces[f].Clone();
                if (nf.fitGroup != 0) nf.fitGroup = 0;
                for (int i = 0; i < nf.v.Length; i++)
                {
                    int vi = nf.v[i];
                    if (!map.TryGetValue(vi, out int d)) { d = m.verts.Count; m.verts.Add(m.verts[vi]); map[vi] = d; }
                    nf.v[i] = d;
                }
                m.faces.Add(nf);
                res.Add(m.faces.Count - 1);
            }
            m.version++;
            return res;
        }

        public static void Flip(RSEditMesh m, ICollection<int> set)
        {
            foreach (int f in set) if (m.ValidFace(f)) System.Array.Reverse(m.faces[f].v);
            m.version++;
        }

        /// <summary>쓰이지 않는 점을 지우고 번호를 당긴다. old → new (지운 점 = -1)</summary>
        public static int[] Compact(RSEditMesh m)
        {
            var used = new bool[m.verts.Count];
            foreach (var f in m.faces) foreach (int vi in f.v) if (vi >= 0 && vi < used.Length) used[vi] = true;
            var map = new int[m.verts.Count];
            var nv = new List<Vector3>(m.verts.Count);
            for (int i = 0; i < m.verts.Count; i++)
            {
                if (used[i]) { map[i] = nv.Count; nv.Add(m.verts[i]); }
                else map[i] = -1;
            }
            m.verts.Clear(); m.verts.AddRange(nv);
            foreach (var f in m.faces) for (int i = 0; i < f.v.Length; i++) f.v[i] = f.v[i] >= 0 && f.v[i] < map.Length ? map[f.v[i]] : -1;
            m.version++;
            return map;
        }
    }
}
