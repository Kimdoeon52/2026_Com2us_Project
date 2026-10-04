// RE:AL STEEL - 조립품 메시 만들기 (부품 면 → 재질별 서브메시)
//
// UV (1m = 32 픽셀):
//  · 반복   : 조립품 축으로 투영 (옆 부품과 무늬가 이어짐). 기둥 옆면은 둘레로 펼침
//  · 도안 칸: 면 좌표(부품 축)를 칸 크기에 꼭 맞게 → 도안 텍스처 픽셀 1:1
//  · 늘리기 : 사각 면을 3×3 으로 쪼개 테두리 픽셀은 그대로, 가운데는 반복
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Build
{
    public class RSMeshBuilder
    {
        const float PPM = RSSurface.PixelsPerMeter;

        readonly RSAssembly asm;
        readonly List<Vector3> pos = new List<Vector3>();
        readonly List<Vector3> nrm = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> cols = new List<Color>();
        readonly Dictionary<Material, List<int>> subs = new Dictionary<Material, List<int>>();
        public readonly List<Material> Materials = new List<Material>();
        readonly List<RSFace> faces = new List<RSFace>();

        public RSMeshBuilder(RSAssembly a) { asm = a; }

        // 면 하나의 조립품 좌표 정보
        struct FaceW
        {
            public RSFace src;
            public Vector3[] p;        // 조립품 로컬
            public Vector3 n;          // 조립품 로컬 법선
            public Vector2[] own;      // 면 좌표 (m)
        }

        readonly List<FaceW> fw = new List<FaceW>();
        readonly Dictionary<int, Rect> ownBounds = new Dictionary<int, Rect>();
        readonly HashSet<int> mixed = new HashSet<int>();   // 방향이 다른 조각이 섞인 면 번호 (도안 칸 불가 → 반복)
        readonly Dictionary<int, Vector3> firstN = new Dictionary<int, Vector3>();

        void Prepare(RSPart part)
        {
            RSShapes.Build(part, faces);
            fw.Clear();
            ownBounds.Clear();
            mixed.Clear();
            firstN.Clear();
            Matrix4x4 m = asm.transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
            Vector3 up = m.MultiplyVector(Vector3.up).normalized;
            Vector3 right = m.MultiplyVector(Vector3.right).normalized;
            foreach (var f in faces)
            {
                if (f.v == null || f.v.Length < 3) continue;
                var w = new FaceW { src = f, p = new Vector3[f.v.Length], own = new Vector2[f.v.Length] };
                for (int i = 0; i < f.v.Length; i++) w.p[i] = m.MultiplyPoint3x4(f.v[i]);
                w.n = m.inverse.transpose.MultiplyVector(f.normal).normalized;
                if (f.own != null)
                {
                    for (int i = 0; i < f.v.Length; i++) w.own[i] = f.own[i];
                }
                else
                {
                    RSShapes.Frame(w.n, up, right, out Vector3 u, out Vector3 v);
                    for (int i = 0; i < w.p.Length; i++) w.own[i] = new Vector2(Vector3.Dot(w.p[i], u), Vector3.Dot(w.p[i], v));
                }
                fw.Add(w);
                if (f.own == null)
                {
                    if (firstN.TryGetValue(f.id, out var n0)) { if (Vector3.Dot(n0, w.n) < 0.999f) mixed.Add(f.id); }
                    else firstN[f.id] = w.n;
                }
                Rect r;
                bool had = ownBounds.TryGetValue(f.id, out r);
                foreach (var o in w.own)
                {
                    if (!had) { r = new Rect(o, Vector2.zero); had = true; }
                    else { r.xMin = Mathf.Min(r.xMin, o.x); r.yMin = Mathf.Min(r.yMin, o.y); r.xMax = Mathf.Max(r.xMax, o.x); r.yMax = Mathf.Max(r.yMax, o.y); }
                }
                ownBounds[f.id] = r;
            }
        }

        static Vector2Int FacePixels(Rect r)
        {
            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(r.width * PPM)), Mathf.Max(1, Mathf.RoundToInt(r.height * PPM)));
        }

        /// <summary>도안 칸 크기 (돌리기 반영)</summary>
        static Vector2Int CellSize(Vector2Int facePx, int rot)
        {
            return (rot & 1) == 1 ? new Vector2Int(facePx.y, facePx.x) : facePx;
        }

        public void CollectNeeds(RSPart part, List<RSAssembly.CellNeed> into)
        {
            Prepare(part);
            var done = new HashSet<int>();
            foreach (var w in fw)
            {
                int id = w.src.id;
                if (done.Contains(id)) continue;
                done.Add(id);
                var paint = part.PaintFor(id);
                if (paint.mode != RSPaintMode.도안칸 || mixed.Contains(id)) continue;
                var px = FacePixels(ownBounds[id]);
                into.Add(new RSAssembly.CellNeed
                {
                    key = RSAssembly.CellKey(part, id),
                    size = CellSize(px, paint.rotate & 3),
                    label = part.name + " · " + part.FaceName(id),
                    part = part, face = id,
                    fallback = paint.surface != null ? paint.surface : asm.defaultSurface,
                });
            }
        }

        public void AddPart(RSPart part, int partIndex, List<RSAssembly.PickTri> picks, List<RSAssembly.FaceOutline> outlines)
        {
            Prepare(part);
            Color vc = Color.white;
            if (part.brightJitter > 0f)
            {
                float h = (part.Id.GetHashCode() & 0xffff) / 65535f;
                float b = 1f + (h - 0.5f) * 2f * part.brightJitter;
                vc = new Color(b, b, b, 1f);
            }
            Texture2D sheet = asm.SheetTexture;
            foreach (var w in fw)
            {
                int id = w.src.id;
                var paint = part.PaintFor(id);
                outlines.Add(new RSAssembly.FaceOutline { part = partIndex, face = id, poly = w.p });

                // 도안 칸
                if (paint.mode == RSPaintMode.도안칸 && sheet != null && !mixed.Contains(id))
                {
                    var cell = asm.FindCell(RSAssembly.CellKey(part, id));
                    var mat = asm.SheetMaterial();
                    if (cell != null && mat != null)
                    {
                        var r = ownBounds[id];
                        var fpx = FacePixels(r);
                        // 면 크기가 바뀌었는데 아직 '도안 갱신' 전이면: 옛 칸 크기에 맞춰 늘려 보인다 (옆 칸이 비치지 않게)
                        var need = CellSize(fpx, paint.rotate & 3);
                        float kx = cell.rect.width / (float)need.x, ky = cell.rect.height / (float)need.y;
                        var uv = new Vector2[w.p.Length];
                        for (int i = 0; i < w.p.Length; i++)
                        {
                            float fx = (w.own[i].x - r.xMin) / Mathf.Max(1e-6f, r.width) * fpx.x;
                            float fy = (w.own[i].y - r.yMin) / Mathf.Max(1e-6f, r.height) * fpx.y;
                            Vector2 c = SheetRotate(fx, fy, fpx, paint.rotate & 3, paint.flip);
                            uv[i] = new Vector2((cell.rect.x + c.x * kx) / sheet.width, (cell.rect.y + c.y * ky) / sheet.height);
                        }
                        AddPoly(mat, w.p, w.n, uv, vc, partIndex, id, picks);
                        continue;
                    }
                }

                var surf = paint.surface != null ? paint.surface : asm.defaultSurface;
                var smat = asm.SurfaceMaterial(surf);
                if (smat == null) continue;
                Vector2Int tpx = surf != null ? surf.TexturePixels : new Vector2Int(32, 32);

                // 늘리기 (면 좌표에서 꼭 맞는 사각형 조각만 — 계단 디딤판 하나 · 상자 면 등)
                if (paint.mode == RSPaintMode.늘리기 && w.src.own == null && surf != null && surf.texture != null && IsOwnRect(w, out Rect pr))
                {
                    AddStretch(smat, w, pr, tpx, paint, vc, partIndex, id, picks);
                    continue;
                }

                // 반복: 조립품 축으로 투영 (기둥 옆면은 펼친 좌표)
                var uvr = new Vector2[w.p.Length];
                Vector3 U = Vector3.right, V = Vector3.up;
                if (w.src.own == null) RSShapes.Frame(w.n, Vector3.up, Vector3.right, out U, out V);
                for (int i = 0; i < w.p.Length; i++)
                {
                    Vector2 m = w.src.own == null ? new Vector2(Vector3.Dot(w.p[i], U), Vector3.Dot(w.p[i], V)) : w.own[i];
                    Vector2 px = m * PPM;
                    if (paint.flip) px.x = -px.x;
                    px = Rot90(px, paint.rotate & 3);
                    px += paint.offset;
                    uvr[i] = new Vector2(px.x / tpx.x, px.y / tpx.y);
                }
                AddPoly(smat, w.p, w.n, uvr, vc, partIndex, id, picks);
            }
        }

        /// <summary>도안 칸 돌리기와 같은 방향 (k=1: (x,y) → (y, -x))</summary>
        static Vector2 Rot90(Vector2 v, int k)
        {
            switch (k)
            {
                case 1: return new Vector2(v.y, -v.x);
                case 2: return new Vector2(-v.x, -v.y);
                case 3: return new Vector2(-v.y, v.x);
                default: return v;
            }
        }

        /// <summary>이 조각이 면 좌표에서 축 정렬 사각형인지 (꼭짓점 4개가 범위의 네 모서리)</summary>
        static bool IsOwnRect(FaceW w, out Rect r)
        {
            r = default;
            if (w.p.Length != 4) return false;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var o in w.own) { x0 = Mathf.Min(x0, o.x); y0 = Mathf.Min(y0, o.y); x1 = Mathf.Max(x1, o.x); y1 = Mathf.Max(y1, o.y); }
            const float eps = 1e-4f;
            foreach (var o in w.own)
                if (!((Mathf.Abs(o.x - x0) < eps || Mathf.Abs(o.x - x1) < eps) && (Mathf.Abs(o.y - y0) < eps || Mathf.Abs(o.y - y1) < eps))) return false;
            if (x1 - x0 < eps || y1 - y0 < eps) return false;
            r = Rect.MinMaxRect(x0, y0, x1, y1);
            return true;
        }

        /// <summary>면 픽셀 좌표 → 칸 픽셀 좌표 (뒤집기 → 돌리기)</summary>
        static Vector2 SheetRotate(float x, float y, Vector2Int f, int k, bool flip)
        {
            if (flip) x = f.x - x;
            switch (k)
            {
                case 1: return new Vector2(y, f.x - x);
                case 2: return new Vector2(f.x - x, f.y - y);
                case 3: return new Vector2(f.y - y, x);
                default: return new Vector2(x, y);
            }
        }

        // 늘리기: 3×3 (가운데 줄은 원본 가운데 크기만큼 반복)
        void AddStretch(Material mat, FaceW w, Rect r, Vector2Int tpx, RSFacePaint paint, Color vc, int partIndex, int id, List<RSAssembly.PickTri> picks)
        {
            var fpx = FacePixels(r);
            int W = fpx.x, H = fpx.y;
            int l = Mathf.Clamp((int)paint.border.x, 0, tpx.x), rr = Mathf.Clamp((int)paint.border.y, 0, tpx.x - l);
            int bb = Mathf.Clamp((int)paint.border.z, 0, tpx.y), tt = Mathf.Clamp((int)paint.border.w, 0, tpx.y - bb);
            ScaleBorders(ref l, ref rr, W);
            ScaleBorders(ref bb, ref tt, H);
            var colsSeg = Segments(W, l, rr, tpx.x);
            var rowsSeg = Segments(H, bb, tt, tpx.y);

            // 면 좌표 → 위치: 꼭짓점 0 기준 평면 위에서
            RSShapes.Frame(w.n, Vector3.up, Vector3.right, out Vector3 U, out Vector3 V);
            // 면 좌표(own)는 부품 축 기준이라 같은 축을 다시 구해야 한다: own 과 p 의 관계로 축을 복원
            Vector3 ou = Vector3.zero, ov = Vector3.zero;
            if (!SolveAxes(w, out ou, out ov)) { ou = U; ov = V; }
            Vector3 origin = w.p[0] - ou * (w.own[0].x - r.xMin) - ov * (w.own[0].y - r.yMin);
            float sx = r.width / W, sy = r.height / H;   // 면 픽셀 → m

            foreach (var c in colsSeg)
                foreach (var row in rowsSeg)
                {
                    float x0 = c.x, x1 = c.y, y0 = row.x, y1 = row.y;
                    float u0 = c.z, u1 = c.w, v0 = row.z, v1 = row.w;
                    if (paint.flip) { x0 = W - c.x; x1 = W - c.y; }
                    var p = new[]
                    {
                        origin + ou * (x0 * sx) + ov * (y0 * sy),
                        origin + ou * (x1 * sx) + ov * (y0 * sy),
                        origin + ou * (x1 * sx) + ov * (y1 * sy),
                        origin + ou * (x0 * sx) + ov * (y1 * sy),
                    };
                    var uv = new[]
                    {
                        new Vector2(u0 / tpx.x, v0 / tpx.y), new Vector2(u1 / tpx.x, v0 / tpx.y),
                        new Vector2(u1 / tpx.x, v1 / tpx.y), new Vector2(u0 / tpx.x, v1 / tpx.y),
                    };
                    AddPoly(mat, p, w.n, uv, vc, partIndex, id, picks);
                }
        }

        static void ScaleBorders(ref int a, ref int b, int total)
        {
            if (a + b <= total) return;
            float k = total / (float)Mathf.Max(1, a + b);
            a = Mathf.FloorToInt(a * k); b = total - a;
        }

        /// <summary>(시작, 끝, 원본 시작, 원본 끝) 목록 — 면 픽셀 기준</summary>
        static List<Vector4> Segments(int total, int a, int b, int src)
        {
            var list = new List<Vector4>();
            if (a > 0) list.Add(new Vector4(0, a, 0, a));
            int mid0 = a, mid1 = total - b, cs = src - a - b;
            if (mid1 > mid0)
            {
                if (cs <= 0) list.Add(new Vector4(mid0, mid1, a, src - b));
                else
                    for (int x = mid0; x < mid1; x += cs)
                    {
                        int e = Mathf.Min(x + cs, mid1);
                        list.Add(new Vector4(x, e, a, a + (e - x)));
                    }
            }
            if (b > 0) list.Add(new Vector4(total - b, total, src - b, src));
            return list;
        }

        /// <summary>면 좌표(own) 축을 위치에서 복원 (평면 면: p = O + ou·own.x + ov·own.y)</summary>
        static bool SolveAxes(FaceW w, out Vector3 ou, out Vector3 ov)
        {
            ou = ov = Vector3.zero;
            // 꼭짓점 0, 1, 3 (사각형) 으로 2×2 풀기
            Vector3 dp1 = w.p[1] - w.p[0], dp2 = w.p[3] - w.p[0];
            Vector2 d1 = w.own[1] - w.own[0], d2 = w.own[3] - w.own[0];
            float det = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(det) < 1e-9f) return false;
            // [dp1 dp2] = [ou ov] [d1 d2]  →  [ou ov] = [dp1 dp2] [d1 d2]^-1
            float i00 = d2.y / det, i01 = -d2.x / det, i10 = -d1.y / det, i11 = d1.x / det;
            ou = dp1 * i00 + dp2 * i10;
            ov = dp1 * i01 + dp2 * i11;
            return true;
        }

        void AddPoly(Material mat, Vector3[] p, Vector3 n, Vector2[] uv, Color vc, int partIndex, int face, List<RSAssembly.PickTri> picks)
        {
            if (!subs.TryGetValue(mat, out var tris))
            {
                tris = new List<int>();
                subs[mat] = tris;
                Materials.Add(mat);
            }
            int b = pos.Count;
            for (int i = 0; i < p.Length; i++)
            {
                pos.Add(p[i]); nrm.Add(n); uvs.Add(uv[i]); cols.Add(vc);
            }
            for (int i = 1; i + 1 < p.Length; i++)
            {
                Vector3 a = p[0], c1 = p[i], c2 = p[i + 1];
                Vector3 cr = Vector3.Cross(c1 - a, c2 - a);
                if (cr.sqrMagnitude < 1e-12f) continue;   // 넓이 0
                if (Vector3.Dot(cr, n) >= 0) { tris.Add(b); tris.Add(b + i); tris.Add(b + i + 1); picks?.Add(new RSAssembly.PickTri { a = a, b = c1, c = c2, part = partIndex, face = face }); }
                else { tris.Add(b); tris.Add(b + i + 1); tris.Add(b + i); picks?.Add(new RSAssembly.PickTri { a = a, b = c2, c = c1, part = partIndex, face = face }); }
            }
        }

        public void Apply(Mesh m)
        {
            m.Clear();
            m.indexFormat = pos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(pos);
            m.SetNormals(nrm);
            m.SetUVs(0, uvs);
            m.SetColors(cols);
            m.subMeshCount = Materials.Count;
            for (int i = 0; i < Materials.Count; i++) m.SetTriangles(subs[Materials[i]], i, true);
            m.RecalculateBounds();
        }
    }
}
