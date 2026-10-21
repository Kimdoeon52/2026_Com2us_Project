// RE:AL STEEL - 그래픽 설정 적용 (자동 시작, 씬에 놓을 것 없음)
//
// RSGraphics(RSCommon)의 값 중 컴포넌트가 직접 읽지 않는 것을 여기서 화면에 건다.
//   · 후처리: 숨은 전역 Volume(우선순위 1000)이 DOF 끄기 · 블룸 끄기 / 고품질 필터 · 밝기를 덮는다
//            씬의 Volume(VOLUME_Stage · Depth)과 시간대 · 색감 · 자동 초점이 만든 숨은 Volume 은 그대로 두고 위에서 덮기만 한다
//   · 그림자 거리: URP 에셋 값 × 배율. 처음 값을 기억했다가 게임을 끌 때(에디터는 플레이를 멈출 때) 되돌린다
//            → 팀 공용 URP 에셋 파일은 바뀌지 않는다
//   · 화면: 수직 동기화 · 최대 프레임 (해상도 · 전체 화면은 빌드에서만)
// 조명 컴포넌트(안개 · 갓레이 · 구름 · 햇살 · 밤 빛 · 젖은 바닥 · 물 · 간접광)는 RSGraphics.Effective 를 스스로 읽는다.
//
// 플레이 중 Hierarchy 의 DontDestroyOnLoad 아래 '[RS 그래픽 설정]' 오브젝트가 이것이다.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [AddComponentMenu("")]   // 메뉴에서 숨김 — 자동으로 생긴다
    [DefaultExecutionOrder(-250)]
    public class RSGraphicsManager : MonoBehaviour, IRSVolumeOwner
    {
        const float VolumePriority = 1000f;

        public static RSGraphicsManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[RS 그래픽 설정]");
            DontDestroyOnLoad(go);
            go.AddComponent<RSGraphicsManager>();
        }

        Volume volume;
        VolumeProfile profile;
        DepthOfField dof;
        Bloom bloom;
        ColorAdjustments color;
        int appliedRevision = -1;

        public Volume OwnedVolume { get { return volume; } }
        public string VolumeRole { get { return "그래픽 설정 — DOF 끄기 · 블룸 끄기 / 고품질 필터 · 밝기"; } }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            EnsureVolume();
            RSGraphics.Changed += ApplyAll;
            Application.quitting += RestorePipeline;
            ApplyAll();
        }

        void OnDisable()
        {
            RSGraphics.Changed -= ApplyAll;
            Application.quitting -= RestorePipeline;
            RestorePipeline();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (profile != null) Destroy(profile);
        }

        void LateUpdate()
        {
            // 밝기: 시간대가 노출을 쥐고 있는지는 씬에 따라 바뀌므로 가끔 다시 본다
            if (appliedRevision != RSGraphics.Revision || (Time.frameCount % 30) == 0) ApplyPost();
        }

        void ApplyAll()
        {
            ApplyPost();
            ApplyShadows();
            ApplyDisplay();
        }

        // ─────────────────────────────────────────────────────────────
        // 후처리
        // ─────────────────────────────────────────────────────────────

        void EnsureVolume()
        {
            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = VolumePriority;
                volume.weight = 1f;
            }
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "RS_GraphicsSettings";
                profile.hideFlags = HideFlags.DontSave;
                dof = profile.Add<DepthOfField>(false);
                bloom = profile.Add<Bloom>(false);
                color = profile.Add<ColorAdjustments>(false);
            }
            volume.sharedProfile = profile;
        }

        void ApplyPost()
        {
            EnsureVolume();
            appliedRevision = RSGraphics.Revision;
            var g = RSGraphics.Effective;

            // 켜 둔 항목은 덮지 않는다 (아래 Volume 값이 그대로 내려온다). 끈 항목만 덮는다
            dof.mode.overrideState = !g.depthOfField;
            dof.mode.value = DepthOfFieldMode.Off;

            bloom.intensity.overrideState = !g.bloom;
            bloom.intensity.value = 0f;
            bloom.highQualityFiltering.overrideState = g.bloom && !g.bloomHighQuality;
            bloom.highQualityFiltering.value = false;

            // 밝기: 시간대가 노출을 정하는 씬이면 시간대가 더한다 (값이 겹치지 않게 여기선 손대지 않음)
            bool here = !RSTimeOfDay.DrivesExposure && Mathf.Abs(g.brightness) > 0.001f;
            color.postExposure.overrideState = here;
            color.postExposure.value = g.brightness;
        }

        // ─────────────────────────────────────────────────────────────
        // 그림자 거리 (URP 에셋) — 끌 때 되돌린다
        // ─────────────────────────────────────────────────────────────

        static UniversalRenderPipelineAsset touchedAsset;
        static float originalShadowDistance = -1f;

        void ApplyShadows()
        {
            var a = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (a == null) return;
            if (touchedAsset != a)
            {
                RestorePipeline();
                touchedAsset = a;
                originalShadowDistance = a.shadowDistance;
            }
            float d = originalShadowDistance * RSGraphics.ShadowDistanceScale(RSGraphics.Effective.shadows);
            if (!Mathf.Approximately(a.shadowDistance, d)) a.shadowDistance = d;
        }

        /// <summary>URP 에셋을 처음 값으로 (게임 종료 · 플레이 멈춤 때 자동)</summary>
        public static void RestorePipeline()
        {
            if (touchedAsset != null && originalShadowDistance >= 0f) touchedAsset.shadowDistance = originalShadowDistance;
            touchedAsset = null;
            originalShadowDistance = -1f;
        }

        // ─────────────────────────────────────────────────────────────
        // 화면
        // ─────────────────────────────────────────────────────────────

        void ApplyDisplay()
        {
            var g = RSGraphics.Effective;
            int vs = g.vSync ? 1 : 0;
            if (QualitySettings.vSyncCount != vs) QualitySettings.vSyncCount = vs;
            int fps = g.vSync ? -1 : g.targetFps;
            if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
#if !UNITY_EDITOR
            // 에디터 게임 창 크기는 건드리지 않는다
            if (g.width > 0 && g.height > 0)
            {
                if (Screen.width != g.width || Screen.height != g.height || Screen.fullScreenMode != g.fullScreenMode)
                    Screen.SetResolution(g.width, g.height, g.fullScreenMode);
            }
            else if (Screen.fullScreenMode != g.fullScreenMode)
            {
                Screen.fullScreenMode = g.fullScreenMode;
            }
#endif
        }
    }
}
