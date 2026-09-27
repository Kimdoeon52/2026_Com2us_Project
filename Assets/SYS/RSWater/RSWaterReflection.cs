// RE:AL STEEL - 물 반사 (평면 반사)
//
// 수면 높이의 거울 카메라로 주변 풍경(둑 · 소품 · 캐릭터)을 한 번 더 그려서, 물 셰이더(RE_AL STEEL/Water Pixel Lit)가
// 물결에 맞춰 일렁이게 비춘다. 씬에 하나만 두면 된다. 이 오브젝트의 Y 위치 = 수면 높이.
// 없거나 꺼져 있으면 물은 하늘(시간대 앰비언트) · 반사 프로브만 비춘다.
//
// 비용: 화면을 한 번 더 그린다 (해상도 배율로 조절). 반사할 레이어를 줄이면 가벼워진다.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common;

namespace RealSteel.Water
{
    [RSSummary("물 반사", "수면 높이에 거울 카메라를 두고 주변 풍경을 한 번 더 그려서 물에 비춘다. 씬에 하나면 된다.\n" +
        "· 이 오브젝트의 Y 위치 = 수면 높이 (아래 버튼으로 RS 지형 수면에 맞출 수 있다)\n" +
        "· 없거나 끄면 물은 하늘색(시간대 앰비언트)만 비춘다\n" +
        "· 화면을 한 번 더 그리므로 해상도 배율 · 레이어로 무게를 조절한다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Water/물 반사")]
    public class RSWaterReflection : MonoBehaviour
    {
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

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (failed || cam == null || cam == reflCam) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
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

                reflCam.CopyFrom(cam);
                reflCam.cameraType = CameraType.Reflection;
                reflCam.enabled = false;
                reflCam.targetTexture = rt;
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
#pragma warning disable 618
                UniversalRenderPipeline.RenderSingleCamera(ctx, reflCam);
#pragma warning restore 618
                GL.invertCulling = false;
                Shader.SetGlobalFloat(SeeThroughOnId, seeThrough);

                Shader.SetGlobalTexture(TexId, rt);
                Shader.SetGlobalFloat(OnId, 1f);
            }
            catch (Exception e)
            {
                GL.invertCulling = false;
                Shader.SetGlobalFloat(SeeThroughOnId, seeThrough);
                failed = true;
                Shader.SetGlobalFloat(OnId, 0f);
                Debug.LogWarning("[물 반사] 이 환경에서 평면 반사를 그릴 수 없어 끕니다 (물은 하늘 반사로 동작).\n" + e.Message, this);
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
