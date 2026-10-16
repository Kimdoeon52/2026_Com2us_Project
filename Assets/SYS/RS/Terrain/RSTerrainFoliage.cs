// RE:AL STEEL - RS Terrain : 풀 · 꽃 메시 (RSFoliage 결과를 조각별 메시로)
//
// RSTerrain 의 일부 (partial). 요소는 RSFoliage.cs.
// 지형을 다시 만들 때 · 브러시 스트로크가 끝날 때 · RSFoliage 값을 바꿀 때 다시 심는다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSteel.Terrain
{
    public partial class RSTerrain
    {
        [System.NonSerialized] readonly List<GameObject> foliageGos = new List<GameObject>();

        void ClearFoliage()
        {
            foreach (var go in foliageGos)
            {
                if (go == null) continue;
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null) SafeDestroy(mf.sharedMesh);
                SafeDestroy(go);
            }
            foliageGos.Clear();
        }

        /// <summary>풀 · 꽃만 다시 심는다 (지형 모양은 그대로)</summary>
        public void RebuildFoliage()
        {
            if (hF == null) return;
            BuildFoliage();
        }

        void BuildFoliage()
        {
            ClearFoliage();
            var list = GetComponentsInChildren<RSFoliage>(false);
            Transform root = null;
            foreach (var f in list)
            {
                if (!IsLive(f)) continue;
                var sink = new RSFoliage.Sink();
                if (f.Plant(this, sink) == 0) continue;
                if (root == null) root = EnsureGenRoot();
                int n = 0;
                foreach (var kv in sink.tiles) MakeFoliage(root, f, kv.Value, n++);
            }
        }

        void MakeFoliage(Transform root, RSFoliage f, RSFoliage.Sink.Buffer b, int index)
        {
            if (b.v.Count == 0) return;
            var go = new GameObject("Foliage_" + f.name + "_" + index);
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.transform.SetParent(root, false);

            var mesh = new Mesh { name = "RST_Foliage", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            var nrm = new List<Vector3>(b.v.Count);
            for (int i = 0; i < b.v.Count; i++) nrm.Add(Vector3.up);
            mesh.SetVertices(b.v);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, b.uv);
            mesh.SetUVs(1, b.quad);
            mesh.SetColors(b.col);
            mesh.SetTriangles(b.t, 0);
            mesh.RecalculateBounds();
            // 정점이 전부 발밑에 모여 있고 셰이더가 세운다 → 경계 상자를 포기 크기만큼 키워야 화면 끝에서 안 사라진다
            var bb = mesh.bounds;
            float pad = Mathf.Max(b.maxW, b.maxH) * 2f + 0.5f;
            bb.Expand(new Vector3(pad, b.maxH * 2f + 0.5f, pad));
            mesh.bounds = bb;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = f.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            foliageGos.Add(go);
        }
    }
}
