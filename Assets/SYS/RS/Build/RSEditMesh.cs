// RE:AL STEEL - 편집 메시 (RSEditMesh)
//
// 점 · 면을 직접 고치는 메시 하나 + 면마다 칠. 가구 · 울타리 · 건물 겉면을 씬에서 바로 만들고 고친다.
//  · 만들기: D = 바닥이나 면 위에 끌어 그리기 (상자 · 원기둥 · 판 · 계단 · 쐐기 · 지붕, Shift+A = 도형 메뉴)
//  · 모양: 씬 뷰 '편집 메시' 도구 — 점 · 선 · 면 고르기, G 이동 · R 회전 · S 크기 (축 X/Y/Z · 숫자 입력),
//          E 밀어내기, K 자르기, X 지우기, M 합치기, Shift+D 복제
//  · 칠: 면을 클릭해 재질을 붙인다. 반복(재질 타일, 1m = 32px) 또는 딱 맞게(그림 한 장을 그 면에 꼭 맞게)
//  · 다른 프로그램에서 만든 모델도 '편집 메시로 가져오기' 로 바꿔서 그대로 칠한다
//  · 메시는 저장하지 않고 켤 때마다 점 · 면 데이터에서 다시 만든다 (재질별로 합친 메시 하나 = 드로우콜 재질 수)
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Build
{
    public enum RSEditPaintMode
    {
        [InspectorName("반복 (재질 타일, 1m = 32px)")] 반복,
        [InspectorName("딱 맞게 (그림 한 장을 면에 꼭 맞게)")] 딱맞게,
    }

    /// <summary>면 하나: 점 번호 (바깥에서 볼 때 유니티 앞면 순서) + 칠</summary>
    [Serializable]
    public class RSEFace
    {
        public int[] v = new int[0];
        public RSSurface surface;
        public RSEditPaintMode mode = RSEditPaintMode.반복;
        [Range(0, 3)] public int rotate;
        public bool flip;
        public Vector2Int offset;
        [Tooltip("딱 맞게: 같은 번호의 면들이 그림 한 장을 나눠 쓴다 (0 = 이 면 혼자)")]
        public int fitGroup;

        public RSEFace Clone()
        {
            var f = new RSEFace();
            f.v = (int[])v.Clone();
            f.CopyPaint(this);
            return f;
        }

        public void CopyPaint(RSEFace o)
        {
            surface = o.surface; mode = o.mode; rotate = o.rotate; flip = o.flip; offset = o.offset; fitGroup = o.fitGroup;
        }
    }

    [RSSummary("편집 메시", "점 · 면을 직접 고치고 면마다 재질을 붙이는 메시 (가구 · 울타리 · 건물 겉면).\n" +
        "· 씬 뷰 도구 막대의 '편집 메시' 도구(이 오브젝트를 고르면 나타남): 1 점 · 2 선 · 3 면, G 이동 · R 회전 · S 크기\n" +
        "  (누른 뒤 X/Y/Z = 축 고정, 숫자 = 정확한 값, Ctrl = 스냅 끄기), E 밀어내기, K 자르기, X 지우기\n" +
        "· D = 그려서 만들기 (바닥이나 면 위를 끌기 → 높이 → 클릭), Shift+A = 도형 메뉴\n" +
        "· P 또는 Tab = 칠하기: 면 클릭 = 재질 붙이기, Shift = 부품 전체, Alt = 집기\n" +
        "· 다른 모델은 메뉴 GameObject → RE_AL STEEL → 선택한 모델을 편집 메시로")]
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Build/편집 메시")]
    public class RSEditMesh : MonoBehaviour
    {
        public const float Pixel = 1f / RSSurface.PixelsPerMeter;
        const string MeshChildName = "__RS_편집 메시 (자동 · 저장 안 됨)";

        [RSGroup("기본", true)]
        [RSKey, Tooltip("칠하기 팔레트에 보일 재질 세트")]
        public RSSurfaceSet palette;
        [RSKey, Tooltip("칠하지 않은 면의 재질 (비우면 회색)")]
        public RSSurface defaultSurface;
        [RSKey, Range(0f, 90f), Tooltip("이 각도보다 완만하게 꺾인 면끼리는 부드럽게 이어 그린다 (0 = 모두 각지게). 둥근 기둥 · 곡선 손잡이는 30~45")]
        public float smoothAngle = 30f;

        [RSGroup("게임")]
        [Tooltip("메시 콜라이더를 붙인다 (밟고 부딪혀야 할 때)")]
        public bool colliders = true;
        [Tooltip("그림자를 드리운다")]
        public bool castShadows = true;

        [HideInInspector] public List<Vector3> verts = new List<Vector3>();
        [HideInInspector] public List<RSEFace> faces = new List<RSEFace>();
        [SerializeField, HideInInspector] int nextGroup = 1;
        [SerializeField, HideInInspector] Shader shader;

        /// <summary>에디터 끌기 중: 콜라이더 다시 굽기를 건너뛴다</summary>
        [NonSerialized] public bool skipCollider;

        /// <summary>데이터가 바뀔 때마다 오른다 (부품 계산 캐시용)</summary>
        [NonSerialized] public int version;

        GameObject meshGo;
        Mesh mesh;
        Material grayMat;
        bool dirty = true;
        bool queued;

        public int NewGroup() { return nextGroup++; }

        void Reset() { shader = Shader.Find(RSSurface.ShaderName); }

        void OnEnable()
        {
            if (shader == null) shader = Shader.Find(RSSurface.ShaderName);
            queued = false;
            dirty = true;
            Rebuild();
        }

        void OnDisable()
        {
            if (meshGo != null)
            {
                var r = meshGo.GetComponent<MeshRenderer>(); if (r != null) r.enabled = false;
                var c = meshGo.GetComponent<MeshCollider>(); if (c != null) c.enabled = false;
            }
            meshGo = null;
            Kill(mesh); mesh = null;
            Kill(grayMat); grayMat = null;
        }

        void OnValidate() { MarkDirty(); }

        void OnDestroy()
        {
            var t = transform != null ? transform.Find(MeshChildName) : null;
            if (t == null) return;
            var child = t.gameObject;
            if (Application.isPlaying) { Destroy(child); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (child != null && (child.transform.parent == null || child.transform.parent.GetComponent<RSEditMesh>() == null)) DestroyImmediate(child);
            };
#endif
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>다음 기회에 다시 만든다</summary>
        public void MarkDirty()
        {
            dirty = true;
            version++;
#if UNITY_EDITOR
            if (!Application.isPlaying && !queued && this != null)
            {
                queued = true;
                UnityEditor.EditorApplication.delayCall += () => { queued = false; if (this != null && isActiveAndEnabled && dirty) Rebuild(); };
            }
#endif
        }

        void Update() { if (dirty) Rebuild(); }

        // ─────────────────────────────────────────────────────────────
        // 기하 도우미
        // ─────────────────────────────────────────────────────────────

        public bool ValidFace(int f)
        {
            if (f < 0 || f >= faces.Count) return false;
            var vv = faces[f].v;
            if (vv == null || vv.Length < 3) return false;
            for (int i = 0; i < vv.Length; i++) if (vv[i] < 0 || vv[i] >= verts.Count) return false;
            return true;
        }

        /// <summary>면 법선 (정규화 전 길이 = 넓이의 2배)</summary>
        public Vector3 FaceNormalRaw(int f)
        {
            var vv = faces[f].v;
            Vector3 n = Vector3.zero;
            for (int i = 0; i < vv.Length; i++)
            {
                Vector3 a = verts[vv[i]], b = verts[vv[(i + 1) % vv.Length]];
                n += Vector3.Cross(a, b);
            }
            return n;
        }

        public Vector3 FaceNormal(int f) { var n = FaceNormalRaw(f); return n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up; }

        public Vector3 FaceCenter(int f)
        {
            var vv = faces[f].v;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < vv.Length; i++) c += verts[vv[i]];
            return vv.Length > 0 ? c / vv.Length : c;
        }

        /// <summary>반복 칠의 면 좌표축 (u = 바깥에서 볼 때 오른쪽, v = 위)</summary>
        public static void Frame(Vector3 n, out Vector3 u, out Vector3 v) { RSShapes.Frame(n, Vector3.up, Vector3.right, out u, out v); }

        // 부품 (점을 공유하는 면 묶음)
        int[] partCache; int partVersion = -1; int partCount;

        /// <summary>면마다 부품 번호. 점을 공유하면 같은 부품</summary>
        public int[] Parts(out int count)
        {
            if (partCache == null || partVersion != version || partCache.Length != faces.Count)
            {
                partCache = RSEditOps.Components(this, out partCount);
                partVersion = version;
            }
            count = partCount;
            return partCache;
        }

        // ─────────────────────────────────────────────────────────────
        // 딱 맞게: 그림 한 장이 덮는 영역
        // ─────────────────────────────────────────────────────────────

        public struct FitRegion { public Vector3 u, v; public Vector2 min, max; public int firstFace; }

        /// <summary>면이 쓰는 '딱 맞게' 영역 (같은 fitGroup 면 전체). 크기 = (max - min) m</summary>
        public FitRegion GetFitRegion(int f)
        {
            int g = faces[f].fitGroup;
            int first = f;
            if (g != 0)
                for (int i = 0; i < faces.Count; i++) if (faces[i].fitGroup == g && ValidFace(i)) { first = i; break; }
            var r = new FitRegion { firstFace = first };
            Frame(FaceNormal(first), out r.u, out r.v);
            r.min = new Vector2(float.MaxValue, float.MaxValue); r.max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < faces.Count; i++)
            {
                if (!(i == f || (g != 0 && faces[i].fitGroup == g)) || !ValidFace(i)) continue;
                foreach (int vi in faces[i].v)
                {
                    var p = verts[vi];
                    var q = new Vector2(Vector3.Dot(p, r.u), Vector3.Dot(p, r.v));
                    r.min = Vector2.Min(r.min, q); r.max = Vector2.Max(r.max, q);
                }
            }
            return r;
        }

        /// <summary>'딱 맞게' 영역을 1m = 32px 로 잰 그림 크기 (돌리기 반영)</summary>
        public Vector2Int FitPixels(int f)
        {
            var r = GetFitRegion(f);
            Vector2 s = (r.max - r.min) * RSSurface.PixelsPerMeter;
            var px = new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(s.x)), Mathf.Max(1, Mathf.RoundToInt(s.y)));
            return (faces[f].rotate & 1) == 1 ? new Vector2Int(px.y, px.x) : px;
        }

        // ─────────────────────────────────────────────────────────────
        // 메시 만들기
        // ─────────────────────────────────────────────────────────────

        /// <summary>피킹용 삼각형 (로컬)</summary>
        public struct PickTri { public Vector3 a, b, c; public int face; }
        [NonSerialized] public readonly List<PickTri> pickTris = new List<PickTri>();

        [NonSerialized] public int builtTriangles;
        [NonSerialized] public int builtMaterials;

        readonly List<Vector3> bPos = new List<Vector3>();
        readonly List<Vector3> bNrm = new List<Vector3>();
        readonly List<Vector2> bUv = new List<Vector2>();
        readonly List<Color32> bCol = new List<Color32>();
        readonly List<int> triIdx = new List<int>();

        public void Rebuild()
        {
            dirty = false;
            if (this == null) return;
            EnsureMeshObject();

            int nf = faces.Count;
            var fn = new Vector3[nf];
            var ok = new bool[nf];
            var vertFaces = new List<int>[verts.Count];
            for (int f = 0; f < nf; f++)
            {
                ok[f] = ValidFace(f);
                if (!ok[f]) continue;
                Vector3 raw = FaceNormalRaw(f);
                if (raw.sqrMagnitude < 1e-16f) { ok[f] = false; continue; }
                fn[f] = raw.normalized;
                foreach (int vi in faces[f].v)
                {
                    if (vertFaces[vi] == null) vertFaces[vi] = new List<int>(4);
                    if (!vertFaces[vi].Contains(f)) vertFaces[vi].Add(f);
                }
            }

            // 딱 맞게 영역 (그룹마다 한 번)
            var fits = new Dictionary<int, FitRegion>();

            bPos.Clear(); bNrm.Clear(); bUv.Clear(); bCol.Clear();
            pickTris.Clear();
            var bySurface = new Dictionary<RSSurface, List<int>>();
            var order = new List<RSSurface>();
            List<int> grayTris = null;   // 재질 없는 면
            float cosSmooth = Mathf.Cos(Mathf.Clamp(smoothAngle, 0f, 90f) * Mathf.Deg2Rad) - 1e-4f;

            for (int f = 0; f < nf; f++)
            {
                if (!ok[f]) continue;
                var face = faces[f];
                var surf = face.surface != null ? face.surface : defaultSurface;
                List<int> tl;
                if (surf == null)
                {
                    if (grayTris == null) { grayTris = new List<int>(); order.Add(null); }
                    tl = grayTris;
                }
                else if (!bySurface.TryGetValue(surf, out tl)) { tl = new List<int>(); bySurface[surf] = tl; order.Add(surf); }

                Vector3 n = fn[f];
                Vector2Int texPx = surf != null ? surf.TexturePixels : new Vector2Int(RSSurface.PixelsPerMeter, RSSurface.PixelsPerMeter);

                FitRegion fr = default;
                Vector3 fu, fv;
                if (face.mode == RSEditPaintMode.딱맞게)
                {
                    int gk = face.fitGroup != 0 ? face.fitGroup : -(f + 1);
                    if (!fits.TryGetValue(gk, out fr)) { fr = GetFitRegion(f); fits[gk] = fr; }
                    fu = fr.u; fv = fr.v;
                }
                else Frame(n, out fu, out fv);

                int baseIdx = bPos.Count;
                var vv = face.v;
                for (int i = 0; i < vv.Length; i++)
                {
                    Vector3 p = verts[vv[i]];
                    // 부드럽게: 이 점을 쓰는 면 중 각도가 작은 면들의 법선 평균
                    Vector3 sn = n;
                    var lst = vertFaces[vv[i]];
                    if (lst != null && lst.Count > 1 && smoothAngle > 0.01f)
                    {
                        sn = Vector3.zero;
                        foreach (int g in lst) if (Vector3.Dot(fn[g], n) >= cosSmooth) sn += fn[g];
                        sn = sn.sqrMagnitude > 1e-12f ? sn.normalized : n;
                    }
                    bPos.Add(p); bNrm.Add(sn); bCol.Add(new Color32(255, 255, 255, 255));
                    bUv.Add(face.mode == RSEditPaintMode.딱맞게 ? FitUV(p, fr, face) : RepeatUV(p, fu, fv, face, texPx));
                }

                Triangulate(vv, n, triIdx);
                for (int t = 0; t < triIdx.Count; t += 3)
                {
                    int a = triIdx[t], b = triIdx[t + 1], c = triIdx[t + 2];
                    tl.Add(baseIdx + a); tl.Add(baseIdx + b); tl.Add(baseIdx + c);
                    pickTris.Add(new PickTri { a = verts[vv[a]], b = verts[vv[b]], c = verts[vv[c]], face = f });
                }
            }

            mesh.Clear();
            mesh.indexFormat = bPos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(bPos);
            mesh.SetNormals(bNrm);
            mesh.SetUVs(0, bUv);
            mesh.SetColors(bCol);
            mesh.subMeshCount = Mathf.Max(1, order.Count);
            if (order.Count == 0) mesh.SetTriangles(new int[0], 0, false);
            var mats = new Material[order.Count];
            builtTriangles = 0;
            for (int s = 0; s < order.Count; s++)
            {
                var tl = order[s] == null ? grayTris : bySurface[order[s]];
                mesh.SetTriangles(tl, s, false);
                builtTriangles += tl.Count / 3;
                mats[s] = SurfaceMaterial(order[s]);
            }
            builtMaterials = order.Count;
            mesh.RecalculateBounds();

            var mr = meshGo.GetComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var mc = meshGo.GetComponent<MeshCollider>();
            if (skipCollider) { }
            else if (colliders && bPos.Count > 0)
            {
                if (mc == null) mc = meshGo.AddComponent<MeshCollider>();
                mc.sharedMesh = null;
                mc.sharedMesh = mesh;
            }
            else if (mc != null) Kill(mc);
            meshGo.layer = gameObject.layer;
        }

        static Vector2 RepeatUV(Vector3 p, Vector3 u, Vector3 v, RSEFace face, Vector2Int texPx)
        {
            float x = Vector3.Dot(p, u) * RSSurface.PixelsPerMeter;
            float y = Vector3.Dot(p, v) * RSSurface.PixelsPerMeter;
            if (face.flip) x = -x;
            for (int r = 0; r < (face.rotate & 3); r++) { float t = x; x = -y; y = t; }
            x += face.offset.x; y += face.offset.y;
            return new Vector2(x / texPx.x, y / texPx.y);
        }

        static Vector2 FitUV(Vector3 p, FitRegion r, RSEFace face)
        {
            Vector2 size = r.max - r.min;
            float x = size.x > 1e-6f ? (Vector3.Dot(p, r.u) - r.min.x) / size.x : 0f;
            float y = size.y > 1e-6f ? (Vector3.Dot(p, r.v) - r.min.y) / size.y : 0f;
            if (face.flip) x = 1f - x;
            for (int k = 0; k < (face.rotate & 3); k++) { float t = x; x = 1f - y; y = t; }
            const float e = 1e-5f;
            return new Vector2(Mathf.Clamp(x, 0f, 1f - e), Mathf.Clamp(y, 0f, 1f - e));
        }

        /// <summary>다각형을 삼각형으로 (귀 자르기). 결과 = 면 안 번호 3개씩, 감기 방향 유지</summary>
        public void Triangulate(int[] vv, Vector3 n, List<int> outTris)
        {
            outTris.Clear();
            int c = vv.Length;
            if (c == 3) { outTris.Add(0); outTris.Add(1); outTris.Add(2); return; }
            Frame(n, out var u, out var v);
            var p2 = new Vector2[c];
            for (int i = 0; i < c; i++) { var p = verts[vv[i]]; p2[i] = new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)); }
            RSEditOps.EarClip(p2, outTris);
        }

        void EnsureMeshObject()
        {
            if (meshGo == null)
            {
                var t = transform.Find(MeshChildName);
                meshGo = t != null ? t.gameObject : new GameObject(MeshChildName);
                meshGo.transform.SetParent(transform, false);
                meshGo.transform.localPosition = Vector3.zero;
                meshGo.transform.localRotation = Quaternion.identity;
                meshGo.transform.localScale = Vector3.one;
                if (meshGo.GetComponent<MeshFilter>() == null) meshGo.AddComponent<MeshFilter>();
                if (meshGo.GetComponent<MeshRenderer>() == null) meshGo.AddComponent<MeshRenderer>();
            }
            meshGo.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            meshGo.GetComponent<MeshRenderer>().enabled = true;
            var col = meshGo.GetComponent<MeshCollider>();
            if (col != null) col.enabled = true;
            if (mesh == null)
            {
                mesh = new Mesh { name = name + " (편집 메시)", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            meshGo.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        /// <summary>지금 그려진 메시 (에디터에서 굽기 · 미리보기용)</summary>
        public Mesh BuiltMesh { get { if (dirty) Rebuild(); return mesh; } }

        internal Material SurfaceMaterial(RSSurface s)
        {
            if (s == null) s = defaultSurface;
            if (s != null)
            {
                var m = s.GetMaterial(shader);
                if (m != null) return m;
            }
            if (grayMat == null)
            {
                var sh = shader != null ? shader : Shader.Find(RSSurface.ShaderName);
                if (sh == null) return null;
                grayMat = new Material(sh) { name = "편집 메시 기본 회색", hideFlags = HideFlags.DontSave };
                grayMat.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.6f));
            }
            return grayMat;
        }

        // ─────────────────────────────────────────────────────────────
        // 피킹
        // ─────────────────────────────────────────────────────────────

        /// <summary>월드 광선이 닿는 가장 가까운 면. 닿은 점 · 면 법선 (월드)</summary>
        public bool Raycast(Ray worldRay, out int face, out Vector3 point, out Vector3 normal, out float distance)
        {
            face = -1; point = Vector3.zero; normal = Vector3.up; distance = float.MaxValue;
            if (dirty) Rebuild();
            var w2l = transform.worldToLocalMatrix;
            Vector3 o = w2l.MultiplyPoint3x4(worldRay.origin);
            Vector3 d = w2l.MultiplyVector(worldRay.direction);
            float best = float.MaxValue; int bi = -1;
            for (int i = 0; i < pickTris.Count; i++)
            {
                var t = pickTris[i];
                if (RSEditOps.RayTri(o, d, t.a, t.b, t.c, out float dist) && dist < best) { best = dist; bi = i; }
            }
            if (bi < 0) return false;
            var hit = pickTris[bi];
            face = hit.face;
            point = transform.TransformPoint(o + d * best);
            normal = transform.localToWorldMatrix.inverse.transpose.MultiplyVector(Vector3.Cross(hit.b - hit.a, hit.c - hit.a)).normalized;
            distance = Vector3.Distance(worldRay.origin, point);
            return true;
        }
    }
}
