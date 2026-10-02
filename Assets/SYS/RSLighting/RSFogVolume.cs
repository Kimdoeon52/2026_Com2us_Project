// RE:AL STEEL - 볼류메트릭 안개 상자 (RSFogVolume)
//
// 이 오브젝트 둘레 상자 안에 빛을 받는 안개를 채운다. 햇빛이 그림자 사이로 빛줄기가 되고,
// 밤에는 캐릭터 빛 · 가로등 둘레가 뿌옇게 번진다. 셰이더: RE_AL STEEL/Fog Volume.
//
// 상자 메시 · 머티리얼은 자동으로 만들고 저장하지 않는다. 설정은 이 컴포넌트에서만 바꾼다.
// 필요: URP 에셋 Depth Texture (물 설치 메뉴가 이미 켬). 무게: 상자가 화면을 덮는 넓이 × 걸음 수.
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("볼류메트릭 안개", "상자 안에 빛을 받는 안개를 채운다.\n" +
        "· 햇빛이 나무 · 건물 그림자 사이로 빛줄기가 되고, 밤엔 달빛 · 캐릭터 빛 · 가로등 둘레가 뿌옇게\n" +
        "· 바닥에 깔리고(높이 감쇠) 바람에 흐른다. 벽 뒤로는 새지 않는다 (깊이 텍스처)\n" +
        "· 시간대 안개색을 따르게 하면 새벽 · 노을 · 밤 색이 자동으로 바뀐다\n" +
        "· 무거우면: 걸음 수를 줄이거나 상자를 필요한 곳만 덮게")]
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("RE_AL STEEL/Lighting/볼류메트릭 안개 상자")]
    public partial class RSFogVolume : MonoBehaviour
    {
        [RSGroup("범위")]
        [RSHelp("상자 크기 (m). 이 오브젝트 위치가 상자 가운데. 스테이지를 덮는 큰 상자 하나 + 골목 · 물가에 작은 상자를 겹쳐도 된다.")]
        [Tooltip("상자 크기 X, Y(높이), Z (m)")]
        [RSKey]
        public Vector3 size = new Vector3(40f, 8f, 40f);
        [Tooltip("상자 가장자리에서 안개가 옅어지는 폭 (m)")]
        public float edgeFade = 3f;

        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("안개")]
            [Tooltip("안개 색 (빛을 받기 전의 색)")]
            [RSKey]
            public Color color = new Color(0.78f, 0.82f, 0.9f);
            [Tooltip("시간대(RS 시간대)가 정한 안개색을 곱한다 — 새벽 · 노을 · 밤 색이 자동으로")]
            public bool followTimeOfDay = true;
            [Range(0f, 1f), Tooltip("밀도 (1/m). 0.05 옅은 연무 · 0.15 짙은 안개")]
            [RSKey]
            public float density = 0.08f;
            [Range(0f, 2f), Tooltip("높이 감쇠. 클수록 바닥에 낮게 깔린다")]
            public float heightFalloff = 0.35f;
            [Tooltip("노이즈 덩어리 크기 (작을수록 큰 덩어리)")]
            public float noiseScale = 0.12f;
            [Range(0f, 1f), Tooltip("노이즈 세기 (0 = 고른 안개)")]
            public float noiseStrength = 0.6f;
            [Tooltip("바람 (m/s) — 안개가 흐르는 방향과 빠르기")]
            public Vector3 wind = new Vector3(0.6f, 0f, 0.2f);
            [RSGroup("빛")]
            [RSHelp("안개가 받는 빛. 빛줄기는 '그림자 세기'와 '앞쪽 산란'이 만든다 — 해를 향해 볼 때 가장 밝다.")]
            [Range(0f, 4f), Tooltip("햇빛 · 달빛 세기")]
            public float sunStrength = 1.2f;
            [Range(0f, 1f), Tooltip("그림자 세기. 1 = 그림자 속은 어둡게 → 빛줄기가 뚜렷")]
            public float shadowStrength = 1f;
            [Range(0f, 0.9f), Tooltip("빛 앞쪽 산란. 0 = 고르게 / 0.8 = 해를 볼 때만 강하게 빛남")]
            public float anisotropy = 0.55f;
            [Range(0f, 2f), Tooltip("주변광(하늘빛) 세기")]
            public float ambientStrength = 0.6f;
            [Tooltip("포인트 · 스폿 라이트도 안개를 밝힌다 (캐릭터 빛 · 가로등)")]
            public bool pointLights = true;
            [Range(0f, 4f), Tooltip("포인트 · 스폿 라이트 세기")]
            public float pointStrength = 1.5f;
        }

        [SerializeField, RSLook] Look look = new Look();
        [RSGroup("스테이지 룩"), Tooltip("켜면 스테이지 룩 프로필을 따르지 않고 이 상자 값만 쓴다 (안개 상자가 여러 개인데 한 곳만 다르게 할 때)")]
        public bool ownLook = false;

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값(이 상자만 따로가 아니면), 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null && !ownLook ? p.fog : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null && !ownLook; } }



        [RSGroup("품질")]
        [Range(4, 48), Tooltip("걸음 수. 많을수록 곱고 무겁다 (16 권장, 느리면 10)")]
        public int steps = 16;
        [Tooltip("카메라에서 이 거리까지만 계산 (m)")]
        public float maxDistance = 60f;
        [Range(1, 8), Tooltip("디더 칸 크기 (화면 픽셀). 도트 느낌은 2")]
        public int ditherPixel = 2;

        [SerializeField, HideInInspector] Shader shader;   // 빌드에 셰이더가 빠지지 않게 참조를 들고 있는다

        const string ShaderName = "RE_AL STEEL/Fog Volume";
        Mesh mesh;
        Material mat;
        MeshFilter mf;
        MeshRenderer mr;
        Vector3 builtSize;

        void Reset() { shader = Shader.Find(ShaderName); }

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            if (shader == null) shader = Shader.Find(ShaderName);
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            mf.hideFlags = HideFlags.HideInInspector;
            mr.hideFlags = HideFlags.HideInInspector;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            builtSize = Vector3.zero;
            Apply();
        }

        /// <summary>스테이지 룩 프로필이 바뀌거나 값이 바뀌면 (인스펙터에서 프로필을 고친 경우 이 컴포넌트의 OnValidate 는 안 불린다)</summary>

        [System.NonSerialized] int lookSeen;
        [System.NonSerialized] RSStageLook lookFrom;
        void OnStageLookChanged()

        {

            if (this == null || !isActiveAndEnabled) return;
            // 다른 묶음(예: 젖은 바닥 슬라이더)만 바뀐 거면 건너뛴다 — 무거운 재계산을 피하려고
            int h = JsonUtility.ToJson(L).GetHashCode();
            if (h == lookSeen && RSStageLook.Current == lookFrom) return;
            lookSeen = h; lookFrom = RSStageLook.Current;

            Apply();

        }


        void OnDisable()
        {

            RSStageLook.Changed -= OnStageLookChanged;
            if (mr != null) mr.sharedMaterial = null;
            if (mf != null && mf.sharedMesh == mesh) mf.sharedMesh = null;
            Kill(mesh); Kill(mat);
            mesh = null; mat = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnValidate()
        {

            MigrateLegacy();
            size = new Vector3(Mathf.Max(0.5f, size.x), Mathf.Max(0.5f, size.y), Mathf.Max(0.5f, size.z));
            if (shader == null) shader = Shader.Find(ShaderName);
        }

        // 머티리얼은 저장 안 하는 런타임 것이라 매 프레임 값을 써도 씬이 수정됨으로 표시되지 않는다
        void Update() { Apply(); }

        void Apply()
        {
            if (shader == null) return;
            if (mat == null)
            {
                mat = new Material(shader) { name = "RS_FogVolume (자동)", hideFlags = HideFlags.DontSave };
                mr.sharedMaterial = mat;
            }
            if (mesh == null || builtSize != size) BuildMesh();

            Color c = L.color;
            if (L.followTimeOfDay)
            {
                // 시간대 안개색의 '색조'만 따른다 (밝기는 햇빛 · 달빛이 정한다)
                Color f = RenderSettings.fogColor;
                c *= f / Mathf.Max(0.05f, f.maxColorComponent);
                c.a = 1f;
            }
            mat.SetVector("_BoxHalf", size * 0.5f);
            mat.SetColor("_FogColor", c);
            mat.SetFloat("_Density", L.density);
            mat.SetFloat("_HeightFalloff", L.heightFalloff);
            mat.SetFloat("_EdgeFade", edgeFade);
            mat.SetFloat("_NoiseScale", L.noiseScale);
            mat.SetFloat("_NoiseStrength", L.noiseStrength);
            mat.SetVector("_Wind", L.wind);
            mat.SetFloat("_Steps", steps);
            mat.SetFloat("_MaxDistance", maxDistance);
            mat.SetFloat("_Anisotropy", L.anisotropy);
            mat.SetFloat("_SunStrength", L.sunStrength);
            mat.SetFloat("_ShadowStrength", L.shadowStrength);
            mat.SetFloat("_AmbientStrength", L.ambientStrength);
            mat.SetFloat("_PointStrength", L.pointStrength);
            mat.SetFloat("_DitherPixel", ditherPixel);
            if (L.pointLights) mat.EnableKeyword("_FOG_POINT_LIGHTS"); else mat.DisableKeyword("_FOG_POINT_LIGHTS");
        }

        void BuildMesh()
        {
            if (mesh == null) mesh = new Mesh { name = "RS_FogBox", hideFlags = HideFlags.DontSave };
            Vector3 h = size * 0.5f;
            var v = new[]
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y,  h.z), new Vector3(h.x, -h.y,  h.z), new Vector3(h.x, h.y,  h.z), new Vector3(-h.x, h.y,  h.z),
            };
            // 바깥을 보는 면 (셰이더는 Cull Front 로 안쪽 면을 그린다)
            var t = new[]
            {
                0, 2, 1, 0, 3, 2,   // -Z
                4, 5, 6, 4, 6, 7,   // +Z
                0, 1, 5, 0, 5, 4,   // -Y
                3, 7, 6, 3, 6, 2,   // +Y
                0, 4, 7, 0, 7, 3,   // -X
                1, 2, 6, 1, 6, 5,   // +X
            };
            mesh.Clear();
            mesh.vertices = v;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            builtSize = size;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
