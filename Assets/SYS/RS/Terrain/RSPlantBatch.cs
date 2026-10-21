// RE:AL STEEL - 풀꽃 묶어 그리기 (플레이 중 자동, 씬에 놓을 것 없음)
//
// 풀꽃 하나(RSPlant) = 오브젝트 하나 = 그리기 호출 하나라서, 많이 찍으면 그리기 호출이 그만큼 는다.
// 플레이를 시작하면(씬을 불러올 때마다) 같은 머티리얼 · 레이어의 풀꽃을 16m 칸마다 메시 하나로 합치고,
// 원래 풀꽃은 렌더러만 끈다. 편집 중에는 아무것도 하지 않는다 (지금처럼 하나씩 옮기고 지운다).
//
//  · 셰이더(RE_AL STEEL/Foliage Sprite)는 정점 위치 = 밑동, 나머지(가로 · 세로 · 박자)는 UV 에서 읽는다
//    → 정점을 월드 밑동 위치로 옮겨 합치기만 하면 흔들림 · 눕기 · 빌보드가 똑같다 (지형 풀이 이미 이 방식)
//  · 플레이 중 움직이거나 지울 풀꽃은 RSPlant 의 '따로 그리기'를 켠다
//  · 플레이 중 풀꽃을 새로 찍거나 바꾼 뒤 다시 합치려면 RSPlantBatch.Rebuild(씬)
//
// 플레이 중 Hierarchy 에 '[RS 풀꽃 묶음 · 플레이 중 자동]' 오브젝트가 생긴다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RealSteel.Terrain
{
    [AddComponentMenu("")]   // 메뉴에서 숨김 — 자동으로 생긴다
    public class RSPlantBatch : MonoBehaviour
    {
        /// <summary>묶는 칸 크기 (m). 칸마다 따로 화면 밖 잘라내기가 된다</summary>
        public const float CellSize = 16f;
        const string RootName = "[RS 풀꽃 묶음 · 플레이 중 자동]";

        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<MeshRenderer> hidden = new List<MeshRenderer>();

        /// <summary>합쳐진 풀꽃 수 · 만든 메시 수 (확인용)</summary>
        public int PlantCount { get { return hidden.Count; } }
        public int BatchCount { get { return meshes.Count; } }

        // ─────────────────────────────────────────────────────────────
        // 자동 시작
        // ─────────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) Build(s);
            }
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (Application.isPlaying) Build(s);
        }

        /// <summary>그 씬의 묶음을 지우고 다시 합친다 (플레이 중 풀꽃을 바꾼 뒤)</summary>
        public static void Rebuild(Scene s)
        {
            var old = Find(s);
            if (old != null)
            {
                old.Release();
                DestroyImmediate(old.gameObject);
            }
            Build(s);
        }

        static RSPlantBatch Find(Scene s)
        {
            if (!s.IsValid() || !s.isLoaded) return null;
            foreach (var r in s.GetRootGameObjects())
                if (r.name == RootName) { var b = r.GetComponent<RSPlantBatch>(); if (b != null) return b; }
            return null;
        }

        // ─────────────────────────────────────────────────────────────
        // 합치기
        // ─────────────────────────────────────────────────────────────

        struct Key
        {
            public Material mat; public int layer, cx, cz;
            public override int GetHashCode()
            {
                unchecked { return ((mat != null ? mat.GetInstanceID() : 0) * 397 ^ layer * 31 ^ cx * 7919) ^ cz * 104729; }
            }
            public override bool Equals(object o)
            {
                if (!(o is Key)) return false;
                var k = (Key)o;
                return k.mat == mat && k.layer == layer && k.cx == cx && k.cz == cz;
            }
        }

        static readonly List<RSPlant> found = new List<RSPlant>();
        static readonly List<RSPlant> tmpFound = new List<RSPlant>();
        static readonly List<Vector2> tmpUv0 = new List<Vector2>();
        static readonly List<Vector4> tmpUv1 = new List<Vector4>();
        static readonly List<Color> tmpCol = new List<Color>();
        static readonly List<int> tmpTri = new List<int>();

        static void Build(Scene s)
        {
            if (!Application.isPlaying || !s.IsValid() || !s.isLoaded) return;
            if (Find(s) != null) return;   // 이미 합쳤다

            found.Clear();
            foreach (var r in s.GetRootGameObjects())
            {
                tmpFound.Clear();
                r.GetComponentsInChildren(false, tmpFound);
                found.AddRange(tmpFound);
            }
            if (found.Count == 0) return;

            var groups = new Dictionary<Key, List<RSPlant>>();
            foreach (var p in found)
            {
                if (p == null || !p.isActiveAndEnabled || p.keepSeparate) continue;
                var mr = p.GetComponent<MeshRenderer>();
                var mf = p.GetComponent<MeshFilter>();
                if (mr == null || mf == null || !mr.enabled || mr.sharedMaterial == null) continue;
                var m = mf.sharedMesh;
                if (m == null || m.vertexCount != 4) continue;
                Vector3 w = p.transform.position;
                var k = new Key
                {
                    mat = mr.sharedMaterial, layer = p.gameObject.layer,
                    cx = Mathf.FloorToInt(w.x / CellSize), cz = Mathf.FloorToInt(w.z / CellSize),
                };
                if (!groups.TryGetValue(k, out var list)) groups[k] = list = new List<RSPlant>();
                list.Add(p);
            }
            if (groups.Count == 0) return;

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, s);
            var batch = root.AddComponent<RSPlantBatch>();
            foreach (var g in groups) batch.BuildGroup(g.Key, g.Value);
        }

        void BuildGroup(Key k, List<RSPlant> plants)
        {
            var origin = new Vector3((k.cx + 0.5f) * CellSize, 0f, (k.cz + 0.5f) * CellSize);
            int n = plants.Count;
            var verts = new List<Vector3>(n * 4);
            var normals = new List<Vector3>(n * 4);
            var uv0 = new List<Vector2>(n * 4);
            var uv1 = new List<Vector4>(n * 4);
            var cols = new List<Color>(n * 4);
            var tris = new List<int>(n * 6);
            var used = new List<MeshRenderer>(n);
            Bounds b = default; bool any = false;
            MeshRenderer first = null;

            foreach (var p in plants)
            {
                var mr = p.GetComponent<MeshRenderer>();
                var m = p.GetComponent<MeshFilter>().sharedMesh;
                m.GetUVs(0, tmpUv0); m.GetUVs(1, tmpUv1); m.GetColors(tmpCol); m.GetTriangles(tmpTri, 0);
                if (tmpUv0.Count != 4 || tmpUv1.Count != 4) continue;

                // 정점 위치 = 밑동 (셰이더가 여기서 세운다). 묶음 오브젝트 위치(origin) 기준으로 넣는다
                Vector3 pivot = p.transform.position - origin;
                int bv = verts.Count;
                for (int i = 0; i < 4; i++)
                {
                    verts.Add(pivot);
                    normals.Add(Vector3.up);
                    uv0.Add(tmpUv0[i]);
                    uv1.Add(tmpUv1[i]);
                    cols.Add(tmpCol.Count == 4 ? tmpCol[i] : Color.white);
                }
                foreach (int t in tmpTri) tris.Add(bv + t);

                var wb = mr.bounds;   // 셰이더가 세우는 만큼 넉넉하게 잡혀 있다
                if (!any) { b = wb; any = true; } else b.Encapsulate(wb);
                if (first == null) first = mr;
                used.Add(mr);
            }
            if (used.Count == 0) return;

            var mesh = new Mesh
            {
                name = "RS_PlantBatch",
                hideFlags = HideFlags.DontSave,
                indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(b.center - origin, b.size);
            mesh.UploadMeshData(true);   // 다시 읽을 일 없음 → CPU 쪽 사본 버림
            meshes.Add(mesh);

            var go = new GameObject("풀꽃 " + k.mat.name + " (" + k.cx + ", " + k.cz + ") × " + used.Count);
            go.layer = k.layer;
            go.transform.SetParent(transform, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = k.mat;
            r.shadowCastingMode = ShadowCastingMode.Off;   // 풀꽃은 원래 그림자를 드리우지 않는다
            r.receiveShadows = first.receiveShadows;
            r.lightProbeUsage = first.lightProbeUsage;
            r.reflectionProbeUsage = first.reflectionProbeUsage;
            r.renderingLayerMask = first.renderingLayerMask;

            foreach (var mr in used) { mr.enabled = false; hidden.Add(mr); }
        }

        /// <summary>원래 풀꽃 렌더러를 다시 켜고 메시를 버린다</summary>
        void Release()
        {
            foreach (var mr in hidden) if (mr != null) mr.enabled = true;
            hidden.Clear();
            foreach (var m in meshes) if (m != null) Destroy(m);
            meshes.Clear();
        }

        void OnDestroy() { Release(); }
    }
}
