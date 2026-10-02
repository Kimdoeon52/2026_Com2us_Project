// RE:AL STEEL - 물 반사
//
// 물 셰이더(RE_AL STEEL/Water Pixel Lit)가 주변 풍경(둑 · 소품 · 캐릭터)을 비추게 한다. 씬에 하나만 두면 된다.
//   화면 반사 (기본)  화면에 이미 그려진 것을 반사 방향으로 찾아 비춘다 (젖은 바닥과 같은 방법). 가볍고 오류가 없다.
//                     화면 밖에 있는 것은 못 비추고 하늘색이 된다 — 위에서 내려다보는 카메라라 대부분 화면 안이다.
//   평면 반사 (선택)  수면 높이의 거울 카메라로 한 번 더 그린다. 화면 밖도 비치지만 무겁고,
//                     프로젝트 설정에 따라 Unity 6 에서 렌더 오류가 날 수 있다 → 오류가 나면 스스로 화면 반사로 바꾼다.
// 없거나 꺼져 있으면 물은 하늘(시간대 앰비언트) · 반사 프로브만 비춘다. 이 오브젝트의 Y 위치 = 수면 높이 (평면 반사용).
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common;

namespace RealSteel.Water
{
    [RSSummary("물 반사", "물에 주변 풍경(둑 · 소품 · 캐릭터)을 비춘다. 씬에 하나면 된다.\n" +
        "· 화면 반사(기본): 가볍고 안정적. 화면 밖에 있는 것은 하늘색으로\n" +
        "· 평면 반사: 화면 밖도 비치지만 화면을 한 번 더 그려 무겁다. 오류가 나면 스스로 화면 반사로 바뀐다\n" +
        "· 없거나 끄면 물은 하늘색(시간대 앰비언트)만 비춘다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Water/물 반사")]
    public class RSWaterReflection : MonoBehaviour
    {
        public enum Mode
        {
            [InspectorName("화면 반사 (가볍다 · 기본)")] Screen,
            [InspectorName("평면 반사 (거울 카메라 · 무겁다)")] Planar,
        }

        [Tooltip("화면 반사 = 화면에 보이는 것을 비춤 (가볍다) / 평면 반사 = 거울 카메라로 한 번 더 그림 (화면 밖도 비치지만 무겁다)")]
        public Mode mode = Mode.Screen;

        [RSGroup("화면 반사")]
        [Range(8, 48), Tooltip("반사 걸음 수. 많을수록 정확하고 무겁다 (20 권장)")]
        public int screenSteps = 20;
        [Range(2f, 40f), Tooltip("비추는 최대 거리 (m)")]
        public float screenDistance = 15f;
        [Range(0.05f, 3f), Tooltip("물체 두께로 볼 깊이 (m). 반사가 끊겨 보이면 올리고, 엉뚱한 게 비치면 내린다")]
        public float screenThickness = 0.8f;

        [RSGroup("평면 반사")]
        [RSHelp("반사 화질과 무게. 픽셀아트 화면이라 0.5 배율이면 충분하다.")]
        [Range(0.1f, 1f), Tooltip("반사 텍스처 해상도 (화면 대비). 0.5 권장 — 낮을수록 가볍고 흐릿")]
        public float resolutionScale = 0.5f;
        [Tooltip("반사에 그릴 레이어. 풀 · 먼지처럼 자잘한 건 빼면 가벼워진다")]
        public LayerMask layers = ~0;
        [Tooltip("반사 속에도 그림자를 그린다 (무거움)")]
        public bool renderShadows = false;
        [Tooltip("씬 뷰에서도 반사 (끄면 게임 뷰만)")]
        public bool reflectInSceneView = true;
        [Tooltip("수면 바로 아래가 반사에 비치지 않게 자르는 여유 (m)")]
        public float clipOffset = 0.03f;
        [Tooltip("반사에 그릴 최대 거리 (m). 씬 뷰 카메라는 먼 평면이 아주 멀어서, 그대로 쓰면 비스듬한 근평면 행렬이 깨진다")]
        public float maxDistance = 300f;

        const string CamName = "__RS_WaterReflectionCam (자동 생성)";
        static readonly int TexId = Shader.PropertyToID("_RSReflectionTex");
        static readonly int OnId = Shader.PropertyToID("_RSReflectionOn");
        static readonly int SsrId = Shader.PropertyToID("_RSWaterSSR");
        static readonly int SeeThroughOnId = Shader.PropertyToID("_RSSeeThroughOn");   // 시야 가림 투명 (반사 속엔 구멍 안 뚫음)

        Camera reflCam;
        RenderTexture rt;
        bool failed;

        void OnEnable()
        {
            failed = false;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Shader.SetGlobalFloat(OnId, 0f);
            if (reflCam != null) { if (Application.isPlaying) Destroy(reflCam.gameObject); else DestroyImmediate(reflCam.gameObject); }
            if (rt != null) { rt.Release(); if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt); }
            reflCam = null; rt = null;
        }

        /// <summary>평면 반사가 오류로 꺼져 화면 반사로 대신하는 중인지</summary>
        public bool PlanarFailed => failed;

        void SetScreenMode()
        {
            Shader.SetGlobalVector(SsrId, new Vector4(screenSteps, screenDistance, screenThickness, 0f));
            Shader.SetGlobalFloat(OnId, 2f);
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == null || cam == reflCam) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            // 화면 반사: 셰이더 값만 넣으면 끝 (거울 카메라 없음). 평면 반사가 오류로 꺼졌을 때도 이쪽으로
            if (mode == Mode.Screen || failed)
            {
                if (cam.cameraType == CameraType.SceneView && !reflectInSceneView) { Shader.SetGlobalFloat(OnId, 0f); return; }
                SetScreenMode();
                return;
            }

            // 깊이만 그리는 렌더(색 없음)에는 반사를 끼우지 않는다 —
            // "Attempting to render to a depth only surface with no dummy color attachment" 오류가 난다
            var target = cam.targetTexture;
            if (target != null && (target.format == RenderTextureFormat.Depth || target.format == RenderTextureFormat.Shadowmap)) return;
#if UNITY_EDITOR
            // 씬 뷰 클릭 · 선택(오브젝트 고르기) 때 에디터가 카메라를 한 번 더 그리는데, 이건 화면에 보이는 그림이 아니라
            // 깊이 · ID 만 쓰는 렌더라 반사가 필요 없고 위 오류의 원인이 된다. 화면을 다시 그릴 때(Repaint)만 반사한다.
            var ev = Event.current;
            if (ev != null && ev.type != EventType.Repaint) return;
#endif
            if (cam.cameraType == CameraType.SceneView && !reflectInSceneView) { Shader.SetGlobalFloat(OnId, 0f); return; }

            float h = transform.position.y;
            if (cam.transform.position.y <= h + 0.01f) { Shader.SetGlobalFloat(OnId, 0f); return; }   // 물 밑에서는 반사 안 함
            // 직교 카메라(씬 뷰 2D · 등각 보기)와 아주 작은 창은 반사를 건너뛴다 — 비스듬한 행렬이 깨져
            // "Screen position out of view frustum" 오류가 난다
            if (cam.orthographic || cam.pixelWidth < 16 || cam.pixelHeight < 16) { Shader.SetGlobalFloat(OnId, 0f); return; }

            float seeThrough = Shader.GetGlobalFloat(SeeThroughOnId);
            try
            {
                EnsureCamera();
                EnsureTexture(cam);

                // CopyFrom 은 원래 카메라의 출력 대상(깊이 전용 버퍼 등)까지 복사할 수 있어서 필요한 값만 옮긴다
                reflCam.cameraType = CameraType.Reflection;
                reflCam.enabled = false;
                reflCam.orthographic = false;
                reflCam.usePhysicalProperties = false;
                reflCam.fieldOfView = cam.fieldOfView;
                reflCam.clearFlags = cam.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                reflCam.backgroundColor = cam.backgroundColor;
                reflCam.allowHDR = true;
                reflCam.allowMSAA = false;
                reflCam.rect = new Rect(0f, 0f, 1f, 1f);
                reflCam.targetTexture = rt;
                reflCam.aspect = (float)rt.width / rt.height;
                reflCam.cullingMask = layers;
                reflCam.useOcclusionCulling = false;

                // 거울 행렬
                Vector3 n = Vector3.up;
                Vector4 plane = new Vector4(n.x, n.y, n.z, -h);
                Matrix4x4 R = ReflectionMatrix(plane);
                reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * R;

                // 먼 평면을 줄인 투영에서 출발한다 (씬 뷰 카메라는 먼 평면이 수만 m 라 행렬이 깨짐)
                reflCam.nearClipPlane = Mathf.Max(0.01f, cam.nearClipPlane);
                reflCam.farClipPlane = Mathf.Clamp(Mathf.Min(cam.farClipPlane, maxDistance), reflCam.nearClipPlane + 1f, 100000f);
                reflCam.ResetProjectionMatrix();
                Matrix4x4 baseProj = reflCam.projectionMatrix;

                // 수면 아래를 잘라내는 비스듬한 근평면.
                // 카메라가 수면에 너무 가깝거나 결과 행렬이 이상하면 그냥 투영을 쓴다 (수면 아래가 조금 비칠 뿐)
                Matrix4x4 proj = baseProj;
                if (cam.transform.position.y - (h + clipOffset) > reflCam.nearClipPlane * 2f)
                {
                    Vector4 clip = CameraSpacePlane(reflCam, new Vector3(0f, h + clipOffset, 0f), n);
                    Matrix4x4 ob = reflCam.CalculateObliqueMatrix(clip);
                    if (IsSane(ob)) proj = ob;
                }
                reflCam.projectionMatrix = proj;

                // 컬링 · 셰이더용 위치
                reflCam.transform.position = R.MultiplyPoint(cam.transform.position);
                Vector3 f = R.MultiplyVector(cam.transform.forward), u = R.MultiplyVector(cam.transform.up);
                reflCam.transform.rotation = Quaternion.LookRotation(f, u);

                var ad = reflCam.GetUniversalAdditionalCameraData();
                if (ad != null)
                {
                    ad.renderShadows = renderShadows;
                    ad.renderPostProcessing = false;
                    ad.requiresColorOption = CameraOverrideOption.Off;
                    ad.requiresDepthOption = CameraOverrideOption.Off;
                    ad.antialiasing = AntialiasingMode.None;
                }

                Shader.SetGlobalFloat(SeeThroughOnId, 0f);
                GL.invertCulling = true;
                bool drew = RenderReflection(ctx);
                GL.invertCulling = false;
                Shader.SetGlobalFloat(SeeThroughOnId, seeThrough);
                if (!drew) { SetScreenMode(); return; }   // 못 그렸으면 화면 반사로

                Shader.SetGlobalTexture(TexId, rt);
                Shader.SetGlobalFloat(OnId, 1f);
            }
            catch (Exception e)
            {
                GL.invertCulling = false;
                Shader.SetGlobalFloat(SeeThroughOnId, seeThrough);
                failed = true;
                SetScreenMode();
                Debug.LogWarning("[물 반사] 이 환경에서 평면 반사를 그릴 수 없어 화면 반사로 바꿉니다.\n" + e.Message, this);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 거울 카메라 그리기
        //
        // Unity 6 (URP 17, Render Graph) 에서는 RenderSingleCamera 가 폐기 예정이고, 다른 카메라를 그리는 도중에
        // 부르면 "Attempting to render to a depth only surface with no dummy color attachment" 가 반복해서 뜬다.
        // → Unity 6 방식인 SubmitRenderRequest(SingleCameraRequest) 로 그린다.
        // 그래도 렌더러가 같은 오류를 내면(프로젝트 설정 · 그래픽 API 차이) 반사를 스스로 끄고 하늘 반사로 돌아간다.
        // ─────────────────────────────────────────────────────────────

        static RSWaterReflection rendering;   // 지금 거울 카메라를 그리는 중인 컴포넌트 (로그 감시용)

        bool RenderReflection(ScriptableRenderContext ctx)
        {
            rendering = this;
            Application.logMessageReceived += WatchLog;
            try
            {
#if UNITY_6000_0_OR_NEWER
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (!RenderPipeline.SupportsRenderRequest(reflCam, req)) return false;
                RenderPipeline.SubmitRenderRequest(reflCam, req);
#else
                UniversalRenderPipeline.RenderSingleCamera(ctx, reflCam);
#endif
            }
            finally
            {
                Application.logMessageReceived -= WatchLog;
                rendering = null;
            }
            return !failed;
        }

        static void WatchLog(string message, string stack, LogType type)
        {
            if (rendering == null || type == LogType.Log) return;
            if (message.Contains("depth only surface") || message.Contains("out of view frustum"))
            {
                var r = rendering;
                r.failed = true;
                r.SetScreenMode();
                // 로그 콜백 안에서 또 로그를 찍으면 되부르므로 한 박자 뒤에 안내
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                    Debug.LogWarning("[물 반사] 이 프로젝트 설정에서 거울 카메라 렌더가 오류를 내서 화면 반사로 바꿨습니다 (반사는 그대로 보임). " +
                                     "물 반사 컴포넌트의 모드를 '화면 반사' 로 두면 이 메시지가 다시 안 뜹니다.", r);
#endif
            }
        }

        void EnsureCamera()
        {
            if (reflCam != null) return;
            // 도메인 리로드 뒤 남은 것 정리
            var old = transform.Find(CamName);
            if (old != null) DestroyImmediate(old.gameObject);

            var go = new GameObject(CamName, typeof(Camera));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, false);
            reflCam = go.GetComponent<Camera>();
            reflCam.enabled = false;
            go.AddComponent<UniversalAdditionalCameraData>();
        }

        void EnsureTexture(Camera cam)
        {
            int w = Mathf.Max(16, Mathf.RoundToInt(cam.pixelWidth * resolutionScale));
            int hgt = Mathf.Max(16, Mathf.RoundToInt(cam.pixelHeight * resolutionScale));
            if (rt != null && rt.width == w && rt.height == hgt) return;
            if (rt != null) { rt.Release(); DestroyImmediate(rt); }
            rt = new RenderTexture(w, hgt, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "RS_WaterReflection",
                hideFlags = HideFlags.DontSave,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
            };
            rt.Create();
        }

        static bool IsSane(Matrix4x4 m)
        {
            for (int i = 0; i < 16; i++)
            {
                float v = m[i];
                if (float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) > 1e6f) return false;
            }
            return Mathf.Abs(m.determinant) > 1e-8f;
        }

        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = new Matrix4x4();
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y;     m.m02 = -2f * p.x * p.z;     m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x;     m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z;     m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x;     m.m21 = -2f * p.z * p.y;     m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }

        static Vector4 CameraSpacePlane(Camera c, Vector3 pos, Vector3 normal)
        {
            Matrix4x4 m = c.worldToCameraMatrix;
            Vector3 cp = m.MultiplyPoint(pos);
            Vector3 cn = m.MultiplyVector(normal).normalized;
            return new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn));
        }
    }
}
