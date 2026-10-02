// RE:AL STEEL - 젖는 바닥 (요소)
//
// 아무 바닥 오브젝트(ProBuilder 바닥 · 타일 · 대리석 · 철판)에 붙이면 그 위에 반사 겹을 씌운다.
// 겹은 같은 메시를 한 번 더 그리는 자동 생성 자식이고 저장하지 않는다 (원래 머티리얼은 그대로).
// 위를 보는 면에만 생긴다 (벽 · 옆면은 안 젖음).
//
// RS 지형은 이 컴포넌트가 필요 없다 — 지형 브러시의 '젖음 칠' 을 쓴다.
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("젖는 바닥", "이 오브젝트 윗면에 젖은 반사를 씌운다 (원래 머티리얼은 그대로).\n" +
        "· 비 온 뒤 타일 · 철판: 젖음 0, 비 반응 1 → '젖은 바닥 · 반사' 의 비 온 정도를 따른다\n" +
        "· 대리석 · 광택 바닥: 젖음 1, 어둡게 0 → 늘 비치고 어두워지지 않음\n" +
        "· 지붕 밑 바닥: 비 반응 0\n" +
        "· RS 지형은 이게 필요 없다 (지형 브러시 '젖음 칠')")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/젖는 바닥")]
    public class RSWetSurface : MonoBehaviour
    {
        [Range(0f, 1f), Tooltip("항상 이만큼 젖어 있음 (대리석 · 광택 바닥은 1)")]
        public float wetness = 0f;
        [Range(0f, 1f), Tooltip("비(전역 '비 온 정도')에 반응하는 정도. 지붕 밑은 0")]
        public float rainResponse = 1f;
        [Range(0f, 1f), Tooltip("젖을 때 어두워지는 배율. 대리석처럼 원래 반짝이는 바닥은 0")]
        public float darkenScale = 1f;
        [Tooltip("이 바닥 반사에 곱하는 색")]
        public Color tint = Color.white;

        const string ChildName = "__RS_Wet (자동 생성 · 저장 안 됨)";
        MeshFilter srcMf;
        MeshFilter mf;
        MeshRenderer mr;
        MaterialPropertyBlock mpb;

        static readonly int IdAmount = Shader.PropertyToID("_WetAmount");
        static readonly int IdRain = Shader.PropertyToID("_WetRain");
        static readonly int IdVertex = Shader.PropertyToID("_UseVertexWet");
        static readonly int IdTint = Shader.PropertyToID("_WetTint");
        static readonly int IdDarken = Shader.PropertyToID("_WetDarken");

        void OnEnable()
        {
            srcMf = GetComponent<MeshFilter>();
            RSWetState.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            RSWetState.Changed -= Refresh;
            if (mr != null) mr.enabled = false;
        }

        void OnDestroy()
        {
            if (mf != null) { if (Application.isPlaying) Destroy(mf.gameObject); else DestroyImmediate(mf.gameObject); }
        }

        void OnValidate() { if (isActiveAndEnabled) Refresh(); }

        void LateUpdate()
        {
            // ProBuilder 로 모양을 고치면 메시가 바뀔 수 있다
            if (srcMf != null && mf != null && mf.sharedMesh != srcMf.sharedMesh) Refresh();
        }

        public void Refresh()
        {
            if (srcMf == null) srcMf = GetComponent<MeshFilter>();
            var mat = RSWetState.OverlayMaterial;
            if (srcMf == null || srcMf.sharedMesh == null || mat == null)
            {
                if (mr != null) mr.enabled = false;
                return;
            }
            EnsureChild();
            mf.sharedMesh = srcMf.sharedMesh;
            int subs = Mathf.Max(1, srcMf.sharedMesh.subMeshCount);
            var mats = mr.sharedMaterials;
            if (mats.Length != subs || mats[0] != mat)
            {
                mats = new Material[subs];
                for (int i = 0; i < subs; i++) mats[i] = mat;
                mr.sharedMaterials = mats;
            }
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetFloat(IdAmount, wetness);
            mpb.SetFloat(IdRain, rainResponse);
            mpb.SetFloat(IdVertex, 0f);   // 일반 메시의 UV2 는 라이트맵 등 다른 용도
            mpb.SetColor(IdTint, tint);
            mpb.SetFloat(IdDarken, darkenScale);
            mr.SetPropertyBlock(mpb);
            mr.enabled = true;
        }

        void EnsureChild()
        {
            if (mf != null && mr != null) return;
            var t = transform.Find(ChildName);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(ChildName) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
            }
            mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
