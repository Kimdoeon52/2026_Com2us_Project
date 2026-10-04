// RE:AL STEEL - 갓레이 (화면)
//
// 해를 향해(해가 카메라 뒤면 해 반대점으로) 화면을 따라 빛살을 더한다. 건물 · 나무 윤곽 사이로 퍼지는 빛.
// 안개 상자 속 빛기둥(볼류메트릭 안개의 '갓레이' 묶음)과 같이 쓰면 된다 — 둘 다 세기를 따로 조절.
//  · 시간대를 따르면: 아침 · 노을에 강하고 한낮에 약하고, 밤엔 달빛 세기로
//  · 화면 전체 삼각형 하나를 투명 큐 맨 뒤에 더하기로 그린다 (렌더러 기능 불필요). 무게 = 화면 픽셀 × 샘플 수
// 필요: URP 에셋 Opaque Texture · Depth Texture
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("갓레이 (화면)", "해를 향해 건물 · 나무 윤곽 사이로 퍼지는 빛살을 화면에 더한다.\n" +
        "· 해가 화면 근처일 때 가장 잘 보인다. 해가 카메라 뒤면 '해 반대점' 으로 모이는 빛살 (탑다운 카메라에서 흔함)\n" +
        "· 안개 속 빛기둥은 '볼류메트릭 안개' 의 갓레이 묶음 — 둘 다 켜서 섞어 쓴다\n" +
        "· 시간대를 따르면 아침 · 노을 강하게, 한낮 약하게, 밤엔 달빛 세기\n" +
        "· 무거우면 샘플 수를 줄인다 (24 → 12)")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/갓레이 (화면)")]
    public class RSGodRays : MonoBehaviour
    {
        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("세기", true)]
            [RSKey, Range(0f, 3f), Tooltip("빛살 세기. 0 = 끔")]
            public float strength = 0.6f;
            [RSKey, Tooltip("시간대 '햇살 빛줄기 (시각별)' 커브 · 색을 따른다 (아침 · 노을 강하게, 한낮 약하게)")]
            public bool followTimeOfDay = true;
            [Range(0f, 2f), Tooltip("밤(달빛)일 때 세기 배율")]
            public float moonStrength = 0.3f;
            [Tooltip("빛살 색에 곱하는 색")]
            public Color tint = Color.white;

            [RSGroup("모양")]
            [RSKey, Range(0.05f, 1f), Tooltip("빛살 길이 (해까지 거리의 비율). 길수록 길게 뻗고 흐려진다")]
            public float length = 0.6f;
            [Range(0.8f, 1f), Tooltip("감쇠. 1 에 가까울수록 멀리서 온 빛도 남는다")]
            public float decay = 0.94f;
            [Range(0f, 4f), Tooltip("밝기 문턱. 이보다 밝은 곳에서 빛살이 나온다 (HDR — 1 이 흰색 정도)")]
            public float threshold = 0.35f;
            [Range(0f, 2f), Tooltip("하늘에서 나오는 빛살 세기")]
            public float skyWeight = 1f;
            [Range(0f, 2f), Tooltip("밝게 빛나는 곳(햇빛 받은 벽 · 바닥 · 네온)에서 나오는 빛살 세기")]
            public float brightWeight = 0.6f;

            [RSGroup("해가 화면 밖일 때")]
            [Range(0.05f, 2f), Tooltip("해가 화면 밖으로 이만큼(화면 크기 비율) 멀어지면 사라진다")]
            public float offscreenFade = 0.6f;
            [Range(0f, 1f), Tooltip("해가 카메라 뒤일 때 해 반대점으로 모이는 빛살 세기 (0 = 안 그림)")]
            public float antiSolar = 0.5f;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.godRays : look; } }
        public Look LocalLook { get { return look; } }
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }

        [RSGroup("품질")]
        [Range(8, 64), Tooltip("샘플 수. 많을수록 곱고 무겁다 (24 권장, 1060급은 12 ~ 16)")]
        public int samples = 24;
        [Range(1, 8), Tooltip("디더 칸 크기 (화면 픽셀). 도트 느낌은 2")]
        public int ditherPixel = 1;
        [HideInInspector, Tooltip("조절용: 빛살이 어디서 나오는지 주황색으로 크게 보여 준다. 인스펙터 맨 위 버튼으로 켜고 끈다")]
        public bool debugView = false;

        [RSGroup("카메라")]
        [Tooltip("그릴 카메라. 비우면 보이는 카메라(MainCamera 태그 중 Depth 가장 높은 것)")]
        public Camera targetCamera;
        [Tooltip("씬 뷰에서도 보이기")]
        public bool showInSceneView = true;
        [Tooltip("해(메인 라이트). 비우면 RenderSettings.sun → 시간대의 해")]
        public Light sun;

        [SerializeField, HideInInspector] Shader shader;
        const string ShaderName = "RE_AL STEEL/God Rays (Screen)";
        const string ChildName = "__RS_GodRays (자동 생성 · 저장 안 됨)";

        GameObject child;
        MeshRenderer mr;
        Material mat;
        static Mesh tri;
        RSTimeOfDay tod;
        Camera viewCam;
        int nextFind;

        static readonly int IdColor = Shader.PropertyToID("_RayColor");
        static readonly int IdSamples = Shader.PropertyToID("_Samples");
        static readonly int IdLength = Shader.PropertyToID("_Length");
        static readonly int IdDecay = Shader.PropertyToID("_Decay");
        static readonly int IdThreshold = Shader.PropertyToID("_Threshold");
        static readonly int IdWeights = Shader.PropertyToID("_Weights");
        static readonly int IdDither = Shader.PropertyToID("_DitherPixel");
        static readonly int IdDebug = Shader.PropertyToID("_DebugView");

        void Reset() { shader = Shader.Find(ShaderName); }

        void OnEnable()
        {
            if (shader == null) shader = Shader.Find(ShaderName);
            Ensure();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (Application.isPlaying || gameObject.activeInHierarchy) Kill(child);
            else if (mr != null) mr.enabled = false;
            child = null; mr = null;
            Kill(mat); mat = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void Ensure()
        {
            if (shader == null) return;
            if (tri == null)
            {
                tri = new Mesh { name = "RS_FullscreenTri", hideFlags = HideFlags.HideAndDontSave };
                tri.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(3f, -1f, 0f), new Vector3(-1f, 3f, 0f) };
                tri.triangles = new[] { 0, 1, 2 };
                tri.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);   // 어디서 봐도 잘리지 않게
            }
            if (child == null)
            {
                var old = transform.Find(ChildName);
                child = old != null ? old.gameObject : new GameObject(ChildName);
                child.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                child.transform.SetParent(transform, false);
                var mf = child.GetComponent<MeshFilter>();
                if (mf == null) mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = tri;
                mr = child.GetComponent<MeshRenderer>();
                if (mr == null) mr = child.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mr.allowOcclusionWhenDynamic = false;
            }
            if (mr == null) mr = child.GetComponent<MeshRenderer>();
            if (mat == null)
            {
                mat = new Material(shader) { name = "RS_GodRays (자동)", hideFlags = HideFlags.DontSave };
                mr.sharedMaterial = mat;
            }
        }

        // 카메라마다 그리기 직전: 그릴 카메라인지 고르고 값을 넣는다 (컬링 전이라 여기서 켜고 끄면 된다)
        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (this == null) return;
            Ensure();
            if (mr == null || mat == null) return;

            if (Time.frameCount >= nextFind || viewCam == null)
            {
                viewCam = targetCamera != null ? targetCamera : RSAutoFocus.FindViewCamera();
                if (tod == null) tod = FindAnyObjectByType<RSTimeOfDay>();
                nextFind = Time.frameCount + 60;
            }
            bool want = cam == viewCam || (showInSceneView && cam.cameraType == CameraType.SceneView);
            var l = L;
            float k = l.strength;
            Color c = l.tint;
            Light s = sun != null ? sun : (RenderSettings.sun != null ? RenderSettings.sun : (tod != null ? tod.sun : null));
            if (s != null) c *= s.color.linear * s.intensity;
            if (l.followTimeOfDay && tod != null)
            {
                if (tod.IsMoon) k *= l.moonStrength;
                else { k *= tod.ShaftFactor; c *= tod.ShaftColor.linear; }
            }
            want &= (k > 0.001f || debugView) && (s == null || s.isActiveAndEnabled);
            if (mr.enabled != want) mr.enabled = want;
            if (!want) return;

            c *= k; c.a = 1f;
            mat.SetColor(IdColor, c);
            mat.SetFloat(IdSamples, samples);
            mat.SetFloat(IdLength, l.length);
            mat.SetFloat(IdDecay, l.decay);
            mat.SetFloat(IdThreshold, l.threshold);
            mat.SetVector(IdWeights, new Vector4(l.skyWeight, l.brightWeight, l.offscreenFade, l.antiSolar));
            mat.SetFloat(IdDither, ditherPixel);
            mat.SetFloat(IdDebug, debugView ? 1f : 0f);
        }
    }
}
