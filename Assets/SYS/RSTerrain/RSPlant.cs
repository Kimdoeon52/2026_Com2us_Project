// RE:AL STEEL - 풀꽃 하나 (RSPlant)
//
// 스프라이트 시트에서 잘라낸 풀 · 꽃 하나를 지면에 세운다 (카메라를 향한 빌보드).
// 오브젝트 하나 = 포기 하나 → 옮기고 · 복제하고 · 지우는 걸 일반 오브젝트처럼 한다.
// 흔들림 · 캐릭터가 지나가면 눕기는 셰이더(RE_AL STEEL/Foliage Sprite)가 한다 — 같은 머티리얼끼리는 SRP 배처로 싸게 그려진다.
//
// 보통은 "풀꽃 배치" 창(Tools/RE_AL STEEL/Stage)으로 씬 뷰에 클릭해서 찍는다.
// 이 오브젝트의 위치 = 발밑(밑동). 메시는 저장하지 않고 켜질 때마다 만든다.
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    [RSSummary("풀꽃 하나", "시트에서 잘라낸 풀 · 꽃 하나를 세운다. 오브젝트 위치 = 밑동. 바람에 흔들리고 캐릭터가 지나가면 눕는다 (머티리얼에서 조절).\n· 아래 그림을 눌러 다른 풀 · 꽃으로 바꿀 수 있다\n· 여러 개를 찍으려면 Tools/RE_AL STEEL/Stage/풀꽃 배치 창")]
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("RE_AL STEEL/Terrain/풀꽃 하나")]
    public class RSPlant : MonoBehaviour
    {
        [Tooltip("풀 · 꽃을 그린 시트")]
        public Texture2D sheet;
        [Tooltip("시트 안에서 이 풀 · 꽃의 위치 (픽셀, 왼쪽 아래 기준)")]
        public RectInt rect;
        [Tooltip("픽셀 / m. 지형 머티리얼 PPU(기본 32)와 같으면 도트 크기가 바닥과 맞는다")]
        public float ppu = 32f;
        [Tooltip("RE_AL STEEL/Foliage Sprite 머티리얼 (시트마다 하나 — 여러 포기가 같이 쓴다)")]
        public Material material;

        [RSGroup("모양")]
        [Tooltip("크기 배율")]
        public float scale = 1f;
        [Tooltip("좌우 뒤집기")]
        public bool flip = false;
        [Tooltip("색조 (포기마다 조금씩 다르게 하면 자연스럽다)")]
        public Color tint = Color.white;
        [Range(0f, 1f), Tooltip("흔들림 박자 어긋남 (포기마다 다르게)")]
        public float phase = 0.5f;

        [System.NonSerialized] Mesh mesh;

        void OnEnable() { Build(); }
        void OnValidate()
        {
#if UNITY_EDITOR
            // OnValidate 안에서 메시를 바꾸면 경고가 뜬다 → 다음 틱에
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
#endif
        }

        void OnDisable()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh == mesh) mf.sharedMesh = null;
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            mesh = null;
        }

        /// <summary>메시 · 머티리얼을 다시 붙인다 (값을 바꾼 뒤)</summary>
        public void Build()
        {
            var mf = GetComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>();
            if (mf == null || mr == null) return;
            // 바뀐 것만 쓴다 (매번 쓰면 씬이 계속 수정됨으로 표시된다)
            if (mf.hideFlags != HideFlags.HideInInspector) mf.hideFlags = HideFlags.HideInInspector;
            if (mr.shadowCastingMode != ShadowCastingMode.Off) mr.shadowCastingMode = ShadowCastingMode.Off;
            if (!mr.receiveShadows) mr.receiveShadows = true;
            if (material != null && mr.sharedMaterial != material) mr.sharedMaterial = material;

            if (sheet == null || rect.width <= 0 || rect.height <= 0) { mf.sharedMesh = null; return; }

            if (mesh == null) mesh = new Mesh { name = "RS_Plant", hideFlags = HideFlags.DontSave };
            float W = sheet.width, H = sheet.height;
            float s = Mathf.Max(0.01f, scale) / Mathf.Max(1f, ppu);
            float w2 = rect.width * s * 0.5f, h = rect.height * s;
            float u0 = (rect.xMin + 0.01f) / W, u1 = (rect.xMax - 0.01f) / W;
            float v0 = (rect.yMin + 0.01f) / H, v1 = (rect.yMax - 0.01f) / H;
            if (flip) { float t = u0; u0 = u1; u1 = t; }

            // 네 정점 모두 밑동(원점)에 두고, 세우는 건 셰이더가 한다
            var z = Vector3.zero;
            mesh.Clear();
            mesh.SetVertices(new[] { z, z, z, z });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) });
            mesh.SetUVs(1, new[] { new Vector4(-w2, 0f, h, phase), new Vector4(w2, 0f, h, phase), new Vector4(w2, h, h, phase), new Vector4(-w2, h, h, phase) });
            mesh.SetColors(new[] { tint, tint, tint, tint });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            float pad = Mathf.Max(w2, h) * 2f + 0.3f;
            mesh.bounds = new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(pad * 2f, h * 2f + pad, pad * 2f));
            mf.sharedMesh = mesh;
        }

        /// <summary>세워졌을 때 높이 (m)</summary>
        public float Height { get { return rect.height * Mathf.Max(0.01f, scale) / Mathf.Max(1f, ppu); } }

        void OnDrawGizmosSelected()
        {
            float h = Height, w = rect.width * Mathf.Max(0.01f, scale) / Mathf.Max(1f, ppu);
            Gizmos.color = new Color(0.4f, 0.95f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * h * 0.5f, new Vector3(w, h, w));
        }
    }
}
