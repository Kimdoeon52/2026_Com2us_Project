// RE:AL STEEL - 조립품 (RSAssembly)
//
// 건물 · 가구 · 울타리 한 칸처럼 부품(RSPart)을 쌓아 만든 물건 하나. 아래의 부품들을 모아
// 재질별로 합친 메시 하나로 그린다 (드로우콜 = 쓰인 재질 수).
//  · 메시는 저장하지 않고 켤 때마다 부품 데이터에서 다시 만든다 (부품을 언제든 고칠 수 있게)
//  · 면 칠: 반복(재질 타일) · 도안 칸(이 조립품 전용 그림, 픽셀 1:1) · 늘리기(테두리 유지)
//  · 도안 그림은 Aseprite 파일로 내보내고 다시 읽는다 (에디터 인스펙터)
//  · 프리팹으로 저장하면 소품처럼 여러 번 놓을 수 있다
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Build
{
    public enum RSSnap { [InspectorName("1 픽셀 (1/32 m) — 가구")] 픽셀, [InspectorName("0.25 m")] 칸025, [InspectorName("0.5 m — 건물")] 칸05, [InspectorName("1 m")] 칸1 }

    [RSSummary("조립품", "부품을 쌓아 만든 건물 · 가구 · 울타리 한 칸. 부품들을 재질별로 합친 메시 하나로 그린다.\n" +
        "· 씬 뷰 위쪽 도구 막대의 '조립' 도구(이 오브젝트를 고르면 나타남)로 부품 그리기 · 면 밀기 · 칠하기\n" +
        "· 칠: 반복(재질 타일) · 도안 칸(전용 그림을 픽셀 1:1) · 늘리기(테두리 유지)\n" +
        "· 도안 칸 그림은 아래 '도안' 에서 Aseprite 파일로 내보내 그리고, 저장하면 다시 읽는다\n" +
        "· 다 만들면 '프리팹으로 저장' → 소품처럼 여러 번 놓기")]
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Build/조립품")]
    public class RSAssembly : MonoBehaviour
    {
        public const float Pixel = 1f / RSSurface.PixelsPerMeter;
        const string MeshChildName = "__RS_조립 메시 (자동 · 저장 안 됨)";

        [RSGroup("기본", true)]
        [RSKey, Tooltip("칠하기 팔레트에 보일 재질 세트")]
        public RSSurfaceSet palette;
        [RSKey, Tooltip("칠하지 않은 면의 재질 (비우면 회색)")]
        public RSSurface defaultSurface;
        [RSKey, Tooltip("그리기 · 밀기 스냅. 가구는 1 픽셀, 건물은 0.5 m")]
        public RSSnap snap = RSSnap.픽셀;

        [RSGroup("도안")]
        [Tooltip("이 조립품 전용 그림 (도안). 인스펙터 '도안 만들기' 가 만든다")]
        public RSSheet sheet;
        [HideInInspector] public List<string> copyHints = new List<string>();   // "새id>원래id" — 복제한 부품의 도안 칸을 원래 그림으로 채우기

        [RSGroup("게임")]
        [Tooltip("메시 콜라이더를 붙인다 (밟고 부딪혀야 할 때)")]
        public bool colliders = true;
        [Tooltip("그림자를 드리운다")]
        public bool castShadows = true;

        [SerializeField, HideInInspector] Shader shader;

        /// <summary>스냅 간격 (m)</summary>
        public float SnapStep
        {
            get
            {
                switch (snap)
                {
                    case RSSnap.칸025: return 0.25f;
                    case RSSnap.칸05: return 0.5f;
                    case RSSnap.칸1: return 1f;
                    default: return Pixel;
                }
            }
        }

        GameObject meshGo;
        Mesh mesh;
        Material sheetMat, grayMat;
        bool dirty = true;
        bool queued;

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
            // 자식은 지우지 않고 숨기기만 (꺼지는 · 지워지는 중에 자식을 지우면 오류) — 다시 켜면 찾아 쓴다
            if (meshGo != null)
            {
                var r = meshGo.GetComponent<MeshRenderer>(); if (r != null) r.enabled = false;
                var c = meshGo.GetComponent<MeshCollider>(); if (c != null) c.enabled = false;
            }
            meshGo = null;
            Kill(mesh); mesh = null;
            Kill(sheetMat); sheetMat = null;
            Kill(grayMat); grayMat = null;
        }

        void OnValidate() { MarkDirty(); }

        // 컴포넌트만 지운 경우 남는 자동 메시 자식 정리 (오브젝트째 지울 때는 같이 사라진다)
        void OnDestroy()
        {
            var t = transform != null ? transform.Find(MeshChildName) : null;
            if (t == null) return;
            var child = t.gameObject;
            if (Application.isPlaying) { Destroy(child); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (child != null && (child.transform.parent == null || child.transform.parent.GetComponent<RSAssembly>() == null)) DestroyImmediate(child);
            };
#endif
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>다음 기회에 다시 만든다 (부품이 바뀌면 부품이 부른다)</summary>
        public void MarkDirty()
        {
            dirty = true;
#if UNITY_EDITOR
            if (!Application.isPlaying && !queued && this != null)
            {
                queued = true;
                UnityEditor.EditorApplication.delayCall += () => { queued = false; if (this != null && isActiveAndEnabled && dirty) Rebuild(); };
            }
#endif
        }

        void Update()
        {
            if (dirty || AnyPartMoved()) Rebuild();
        }

        readonly List<RSPart> parts = new List<RSPart>();

        bool AnyPartMoved()
        {
            if (Application.isPlaying) return false;
            foreach (var p in parts) if (p != null && p.transform.hasChanged) return true;
            return false;
        }

        /// <summary>이 조립품에 속한 부품 (켜진 것, 하이어라키 순서)</summary>
        public List<RSPart> Parts(List<RSPart> into)
        {
            into.Clear();
            foreach (var p in GetComponentsInChildren<RSPart>(false))
                if (p.isActiveAndEnabled && p.Owner == this) into.Add(p);
            return into;
        }

        public static string CellKey(RSPart p, int face) { return p.Id + ":" + face; }

        public RSSheetCell FindCell(string key) { return sheet != null ? sheet.Find(key) : null; }

        /// <summary>도안 결과 텍스처 (없으면 null)</summary>
        public Texture2D SheetTexture { get { return sheet != null ? sheet.texture : null; } }

        // ─────────────────────────────────────────────────────────────
        // 메시 만들기
        // ─────────────────────────────────────────────────────────────

        /// <summary>피킹용 삼각형 (조립품 로컬)</summary>
        public struct PickTri { public Vector3 a, b, c; public int part; public int face; }
        /// <summary>면 윤곽 (조립품 로컬) — 에디터 강조용</summary>
        public struct FaceOutline { public int part; public int face; public Vector3[] poly; }

        [NonSerialized] public readonly List<PickTri> pickTris = new List<PickTri>();
        [NonSerialized] public readonly List<FaceOutline> outlines = new List<FaceOutline>();
        [NonSerialized] public readonly List<RSPart> builtParts = new List<RSPart>();

        public void Rebuild()
        {
            dirty = false;
            if (this == null) return;
            Parts(parts);
            FixDuplicateIds();
            EnsureMeshObject();
            var b = new RSMeshBuilder(this);
            builtParts.Clear();
            pickTris.Clear();
            outlines.Clear();
            for (int i = 0; i < parts.Count; i++)
            {
                builtParts.Add(parts[i]);
                b.AddPart(parts[i], i, pickTris, outlines);
                parts[i].transform.hasChanged = false;
            }
            b.Apply(mesh);
            var mr = meshGo.GetComponent<MeshRenderer>();
            mr.sharedMaterials = b.Materials.ToArray();
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var mc = meshGo.GetComponent<MeshCollider>();
            if (colliders && mesh.vertexCount > 0)
            {
                if (mc == null) mc = meshGo.AddComponent<MeshCollider>();
                mc.sharedMesh = null;
                mc.sharedMesh = mesh;
            }
            else if (mc != null) Kill(mc);
            meshGo.layer = gameObject.layer;
        }

        void FixDuplicateIds()
        {
            var seen = new Dictionary<string, RSPart>();
            foreach (var p in parts)
            {
                string old = p.Id;
                if (seen.ContainsKey(old))
                {
                    p.NewId();
                    if (copyHints == null) copyHints = new List<string>();
                    copyHints.Add(p.Id + ">" + old);
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(p);
                    UnityEditor.EditorUtility.SetDirty(this);
#endif
                }
                seen[p.Id] = p;
            }
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
                mesh = new Mesh { name = name + " (조립 메시)", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            meshGo.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

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
                grayMat = new Material(sh) { name = "조립 기본 회색", hideFlags = HideFlags.DontSave };
                grayMat.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.6f));
            }
            return grayMat;
        }

        internal Material SheetMaterial()
        {
            var tex = SheetTexture;
            if (tex == null) return null;
            if (sheetMat == null)
            {
                var sh = shader != null ? shader : Shader.Find(RSSurface.ShaderName);
                if (sh == null) return null;
                sheetMat = new Material(sh) { name = name + " (도안)", hideFlags = HideFlags.DontSave };
            }
            if (sheetMat.GetTexture("_BaseMap") != tex) sheetMat.SetTexture("_BaseMap", tex);
            return sheetMat;
        }

        // ─────────────────────────────────────────────────────────────
        // 도안 칸이 필요한 면 (에디터가 도안을 만들 때)
        // ─────────────────────────────────────────────────────────────

        public struct CellNeed { public string key; public Vector2Int size; public string label; public RSPart part; public int face; public RSSurface fallback; }

        public void CollectSheetNeeds(List<CellNeed> into)
        {
            into.Clear();
            Parts(parts);
            FixDuplicateIds();
            var b = new RSMeshBuilder(this);
            foreach (var p in parts) b.CollectNeeds(p, into);
        }

        // ─────────────────────────────────────────────────────────────
        // 피킹 (에디터 도구)
        // ─────────────────────────────────────────────────────────────

        /// <summary>월드 광선이 닿는 가장 가까운 면. 부품 번호 · 면 번호 · 닿은 점 · 법선 (월드)</summary>
        public bool Raycast(Ray worldRay, out RSPart part, out int face, out Vector3 point, out Vector3 normal)
        {
            part = null; face = -1; point = Vector3.zero; normal = Vector3.up;
            if (dirty) Rebuild();
            var w2l = transform.worldToLocalMatrix;
            Vector3 o = w2l.MultiplyPoint3x4(worldRay.origin);
            Vector3 d = w2l.MultiplyVector(worldRay.direction);
            float best = float.MaxValue; int bi = -1;
            for (int i = 0; i < pickTris.Count; i++)
            {
                var t = pickTris[i];
                if (RayTri(o, d, t.a, t.b, t.c, out float dist) && dist < best) { best = dist; bi = i; }
            }
            if (bi < 0) return false;
            var hit = pickTris[bi];
            if (hit.part < 0 || hit.part >= builtParts.Count) return false;
            part = builtParts[hit.part];
            face = hit.face;
            point = transform.TransformPoint(o + d * best);
            normal = transform.TransformDirection(Vector3.Cross(hit.b - hit.a, hit.c - hit.a)).normalized;
            return part != null;
        }

        static bool RayTri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0;
            Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            Vector3 tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv;
            if (u < 0 || u > 1) return false;
            Vector3 qv = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, qv) * inv;
            if (v < 0 || u + v > 1) return false;
            t = Vector3.Dot(e2, qv) * inv;
            return t > 1e-5f;
        }
    }
}
