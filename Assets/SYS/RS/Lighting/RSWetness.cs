// RE:AL STEEL - 젖은 바닥 · 반사 설정 (씬에 하나)
//
// 젖은 바닥이 어떻게 보일지(어둡게 · 반사 · 햇빛 반짝임 · 물결)와 '비 온 정도' 를 정한다.
// 어디가 젖었는지는 따로 정한다:
//   · RS 지형: 지형 브러시 '젖음 칠' 로 칠한 곳 (+ 비 온 정도만큼 전체)
//   · 그 밖의 바닥(ProBuilder 바닥 · 대리석 등): 그 오브젝트에 '젖는 바닥' 을 붙인다
//
// 반사는 화면 공간 반사(SSR): 화면에 이미 그려진 것(벽 · 캐릭터 · 소품)을 바닥에 비춘다.
// 화면 밖에 있는 것은 못 비추고 대신 하늘색(시간대 앰비언트)이 비친다 — 위에서 내려다보는 카메라라 대부분 화면 안이다.
// 필요 설정: URP 에셋의 Opaque Texture · Depth Texture (물 설치 · 스테이지 연출 한 번에 설치 메뉴가 켠다)
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("젖은 바닥 · 반사", "젖은 바닥이 어떻게 보일지와 '비 온 정도' 를 정한다 (씬에 하나).\n" +
        "· 어디가 젖었나: RS 지형은 브러시 '젖음 칠', 다른 바닥은 '젖는 바닥' 컴포넌트\n" +
        "· 비 온 정도를 올리면 칠 안 한 바닥도 전부 젖는다 (지붕 밑은 '젖는 바닥' 의 비 반응 0)\n" +
        "· 반사는 화면에 보이는 것만 비춘다 (화면 밖 = 하늘색). 무거우면 반사 걸음 수를 줄인다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/젖은 바닥 · 반사")]
    public partial class RSWetness : MonoBehaviour
    {
        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("날씨")]
            [Range(0f, 1f), Tooltip("비 온 정도. 0 = 칠한 곳만 젖음 / 1 = 모든 바닥이 흠뻑")]
            [RSKey]
            public float rain = 0f;
            [RSGroup("젖은 모습")]
            [RSHelp("젖으면 어두워지고(물이 스며서) 주변이 비친다. 반사 세기는 비스듬히 볼수록 강해진다(프레넬).")]
            [Range(0f, 1f), Tooltip("젖은 곳이 어두워지는 정도")]
            [RSKey]
            public float darken = 0.35f;
            [Range(0f, 1f), Tooltip("반사 세기")]
            [RSKey]
            public float reflection = 0.75f;
            [Range(0f, 1f), Tooltip("정면에서도 남는 반사 (0 = 비스듬히 볼 때만 비침)")]
            public float reflectionMin = 0.15f;
            [Tooltip("반사에 곱하는 색 (물웅덩이를 살짝 푸르게 등)")]
            public Color reflectionTint = Color.white;
            [Range(0f, 3f), Tooltip("해 · 달 반짝임 세기")]
            public float highlight = 1f;
            [Range(8f, 256f), Tooltip("반짝임 크기 (클수록 작고 날카로움)")]
            public float highlightSharpness = 80f;
            [RSGroup("번짐 · 요철 (현실 물 느낌)")]
            [RSHelp("진짜 젖은 길은 거울처럼 또렷하지 않다: 반사가 세로로 번지고, 돌 윗면은 반짝이고, 틈에는 물이 고여 또렷하다.\n" +
                "요철은 바닥 텍스처의 밝기로 자동 계산 (따로 노멀맵 필요 없음). 위 '프리셋' 버튼으로 한 번에 맞출 수 있다.")]
            [Range(0f, 1f), Tooltip("번짐 (거칠기). 0 = 거울 / 0.3 ~ 0.5 = 젖은 돌길 / 1 = 뿌옇게")]
            [RSKey]
            public float roughness = 0.35f;
            [Range(1f, 5f), Tooltip("반사가 세로로 길게 번지는 배율 (젖은 길 특유의 세로 번짐). 1 = 동그랗게")]
            public float reflectionStretch = 2.5f;
            [Range(0f, 1f), Tooltip("바닥 요철. 바닥 텍스처 밝기를 높이로 봐서 반사 · 반짝임을 흔든다. 0 = 평평한 거울")]
            public float surfaceBump = 0.6f;
            [Range(0f, 1f), Tooltip("틈새 물 고임. 주변보다 어두운 틈은 물이 고여 더 젖고 또렷하게 비친다")]
            public float puddleInCracks = 0.5f;
            [Range(0f, 1f), Tooltip("주변 빛 반사. 화면 밖으로 나간 반사를 그쪽 화면 가장자리의 먼 풍경 색(불빛 · 안개)으로 채운다. 0 = 앰비언트 색만")]
            public float environment = 0.35f;
            [RSGroup("얼음이 아니라 젖은 길처럼")]
            [RSHelp("고르게 매끈하고 반사가 바닥을 덮으면 얼음 · 유리처럼 보인다. 젖은 길은:\n" +
                "· 웅덩이와 덜 젖은 곳이 섞여 있고  · 불빛 같은 밝은 것만 또렷이 비치고  · 물막 아래로 바닥이 그대로 보인다")]
            [Range(0f, 1f), Tooltip("물웅덩이 얼룩. 0 = 고르게 젖음(매끈 → 얼음 느낌) / 올릴수록 웅덩이(잘 비침)와 덜 젖은 곳(뿌옇게)이 뚜렷")]
            public float puddlePatches = 0.7f;
            [Range(0.05f, 2f), Tooltip("웅덩이 크기 (1m 에 몇 번). 작을수록 큰 웅덩이")]
            public float puddleScale = 0.35f;
            [Range(0f, 1f), Tooltip("밝은 것만 비치기. 올릴수록 불빛 · 밝은 물체만 비치고 어두운 배경은 안 비친다 (뿌연 막이 사라짐)")]
            public float reflectionContrast = 0.7f;
            [Range(0f, 1f), Tooltip("반사 불투명도 — 반사가 바닥을 가리는 정도. 낮게(0.2) = 물막 아래 바닥이 보임(젖은 길) / 높게 = 거울 · 얼음")]
            public float reflectionCover = 0.2f;
            [RSGroup("모양")]
            [RSHelp("칠한 경계를 물웅덩이처럼 들쭉날쭉하게 만들고, 반사는 고인 물처럼 수평으로 비춘다.\n" +
                "지형은 삼각형마다 면 방향이 달라서, 면을 그대로 따르면 반사가 삼각형 조각으로 깨진다.")]
            [Range(0f, 1f), Tooltip("경계 흔들림. 0 = 칠한 모양 그대로(둥근 붓 자국) / 올릴수록 물웅덩이처럼 들쭉날쭉")]
            public float edgeBreakup = 0.5f;
            [Range(0.2f, 4f), Tooltip("경계 무늬 촘촘함 (1m 에 몇 번 굽이치나). 작을수록 큰 웅덩이 모양")]
            public float edgeScale = 1.2f;
            [Range(0f, 1f), Tooltip("바닥 기울기를 따라 비치기. 0 = 수평 거울(권장, 삼각형 안 보임) / 1 = 면 방향 그대로")]
            public float followSlope = 0f;
            [RSGroup("라이트 반사")]
            [RSHelp("가로등 · 네온 · 캐릭터 밤 빛 같은 라이트가 젖은 바닥에 길쭉하게 비친다. 라이트는 화면에 보이는 물체가 아니라서\n" +
                "화면 반사로는 안 비치고 여기서 따로 그린다. 라이트 범위(Range) 안의 바닥에만 비친다.")]
            [Range(0f, 4f), Tooltip("포인트 · 스폿 라이트 반사 세기. 0 = 끔")]
            [RSKey]
            public float lightReflection = 1.5f;
            [Range(0f, 0.6f), Tooltip("세로 번짐 (카메라 쪽으로 길게). 0 = 작은 점 / 0.15 ≈ 높이 2.5m 가로등이 3m 빛줄기 / 올릴수록 길다. 해 · 달에도 적용")]
            public float lightStreak = 0.15f;
            [Range(20f, 2000f), Tooltip("빛줄기 가로 폭 (클수록 가늘다)")]
            public float lightStreakSharpness = 300f;
            [RSGroup("물결")]
            [Range(0f, 1f), Tooltip("반사를 흔드는 잔물결 세기 (비 올 때 올린다)")]
            public float ripple = 0.2f;
            [Range(0.5f, 10f), Tooltip("물결 촘촘함")]
            public float rippleScale = 3f;
            [Range(0f, 5f), Tooltip("물결 속도")]
            public float rippleSpeed = 1f;
            [RSGroup("품질 · 픽셀")]
            [RSHelp("반사 계산 방법. 걸음 수가 많을수록 정확하고 무겁다. 픽셀 크기를 맞추면 반사도 도트로 끊긴다.")]
            [Range(8, 48), Tooltip("반사 걸음 수 (20 권장, 느리면 12)")]
            public int steps = 20;
            [Range(2f, 40f), Tooltip("비추는 최대 거리 (m)")]
            public float maxDistance = 12f;
            [Range(0.05f, 3f), Tooltip("물체 두께로 볼 깊이 (m). 반사가 끊겨 보이면 올리고, 엉뚱한 게 비치면 내린다")]
            public float thickness = 0.6f;
            [Range(0f, 64f), Tooltip("픽셀 밀도 (칸/m). 지형 PPU 와 같게(32) 두면 도트 반사. 0 = 부드럽게")]
            public float pixelsPerUnit = 32f;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.wetness : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }









        [RSGroup("품질 · 픽셀")]
        [Tooltip("반사 겹 머티리얼 (RE_AL STEEL/Wet Reflection). 비우면 자동으로 만든다")]
        public Material overlayMaterial;
        [SerializeField, HideInInspector] Shader shader;

        const string ShaderName = "RE_AL STEEL/Wet Reflection";
        Material runtimeMat;

        public static RSWetness Active { get; private set; }

        /// <summary>실제로 쓰는 반사 겹 머티리얼</summary>
        public Material Overlay
        {
            get
            {
                if (overlayMaterial != null) return overlayMaterial;
                if (runtimeMat == null)
                {
                    if (shader == null) shader = Shader.Find(ShaderName);
                    if (shader == null) return null;
                    runtimeMat = new Material(shader) { name = "RS_WetReflection (자동)", hideFlags = HideFlags.DontSave };
                }
                return runtimeMat;
            }
        }

        static readonly int IdGlobal = Shader.PropertyToID("_RSWetGlobal");
        static readonly int IdLook = Shader.PropertyToID("_RSWetLook");
        static readonly int IdTint = Shader.PropertyToID("_RSWetTintGlobal");
        static readonly int IdRipple = Shader.PropertyToID("_RSWetRipple");
        static readonly int IdTrace = Shader.PropertyToID("_RSWetTrace");
        static readonly int IdShape = Shader.PropertyToID("_RSWetShape");
        static readonly int IdGlint = Shader.PropertyToID("_RSWetGlint");
        static readonly int IdGloss = Shader.PropertyToID("_RSWetGloss");
        static readonly int IdEnv = Shader.PropertyToID("_RSWetEnv");
        static readonly int IdPuddle = Shader.PropertyToID("_RSWetPuddle");

        void Reset() { shader = Shader.Find(ShaderName); }

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            if (shader == null) shader = Shader.Find(ShaderName);
            Active = this;
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
            if (Active == this) Active = null;
            Shader.SetGlobalFloat(IdGlobal, 0f);
            Shader.SetGlobalVector(IdLook, Vector4.zero);   // 반사 · 어둡게 0 → 겹이 아무것도 안 그림
            RSWetState.Set(0f, null);
        }

        void OnDestroy()
        {
            if (runtimeMat != null) { if (Application.isPlaying) Destroy(runtimeMat); else DestroyImmediate(runtimeMat); }
        }

        void OnValidate() { MigrateLegacy(); if (isActiveAndEnabled) Apply(); }

        void Update() { Apply(); }

        public enum Preset { 빗길, 은은하게, 거울웅덩이 }

        /// <summary>느낌 프리셋. 비 온 정도 · 품질 · 픽셀 설정은 그대로 둔다</summary>
        public void ApplyPreset(Preset p)
        {
            switch (p)
            {
                case Preset.빗길:        // 비 오는 밤 돌길 (Replaced 식): 어둡고 강한 반사, 세로 번짐, 돌마다 반짝임, 불빛 빛줄기
                    L.darken = 0.45f; L.reflection = 1f; L.reflectionMin = 0.15f; L.reflectionTint = Color.white;
                    L.highlight = 1.5f; L.highlightSharpness = 60f;
                    L.lightReflection = 2.5f; L.lightStreak = 0.25f; L.lightStreakSharpness = 200f;
                    L.roughness = 0.35f; L.reflectionStretch = 3f; L.surfaceBump = 0.8f; L.puddleInCracks = 0.6f; L.environment = 0.35f;
                    L.puddlePatches = 0.7f; L.puddleScale = 0.35f; L.reflectionContrast = 0.7f; L.reflectionCover = 0.2f;
                    L.edgeBreakup = 0.6f; L.ripple = 0.1f;
                    break;
                case Preset.은은하게:    // 비 그친 뒤 (옥토패스 식): 살짝 어둡고 은은한 반사
                    L.darken = 0.3f; L.reflection = 0.6f; L.reflectionMin = 0.1f; L.reflectionTint = Color.white;
                    L.highlight = 1f; L.highlightSharpness = 80f;
                    L.lightReflection = 1.2f; L.lightStreak = 0.12f; L.lightStreakSharpness = 300f;
                    L.roughness = 0.25f; L.reflectionStretch = 1.5f; L.surfaceBump = 0.4f; L.puddleInCracks = 0.3f; L.environment = 0.3f;
                    L.puddlePatches = 0.5f; L.puddleScale = 0.3f; L.reflectionContrast = 0.5f; L.reflectionCover = 0.2f;
                    L.edgeBreakup = 0.5f; L.ripple = 0.15f;
                    break;
                case Preset.거울웅덩이:  // 고인 물: 또렷한 거울
                    L.darken = 0.4f; L.reflection = 1f; L.reflectionMin = 0.5f; L.reflectionTint = Color.white;
                    L.highlight = 1.5f; L.highlightSharpness = 150f;
                    L.lightReflection = 2f; L.lightStreak = 0.08f; L.lightStreakSharpness = 500f;
                    L.roughness = 0.02f; L.reflectionStretch = 1f; L.surfaceBump = 0.1f; L.puddleInCracks = 0.2f; L.environment = 0.6f;
                    L.puddlePatches = 0f; L.puddleScale = 0.35f; L.reflectionContrast = 0.2f; L.reflectionCover = 0.8f;
                    L.edgeBreakup = 0.4f; L.ripple = 0.05f;
                    break;
            }
            Apply();
        }

        public void Apply()
        {
            Shader.SetGlobalFloat(IdGlobal, L.rain);
            Shader.SetGlobalVector(IdLook, new Vector4(L.darken, L.reflection, L.reflectionMin, L.highlight));
            Shader.SetGlobalColor(IdTint, L.reflectionTint);
            Shader.SetGlobalVector(IdRipple, new Vector4(L.ripple, L.rippleScale, L.rippleSpeed, L.highlightSharpness));
            Shader.SetGlobalVector(IdTrace, new Vector4(L.steps, L.maxDistance, L.thickness, L.pixelsPerUnit));
            Shader.SetGlobalVector(IdShape, new Vector4(L.edgeBreakup, L.edgeScale, L.followSlope, 0f));
            Shader.SetGlobalVector(IdGlint, new Vector4(L.lightReflection, L.lightStreak, L.lightStreakSharpness, 0f));
            Shader.SetGlobalVector(IdGloss, new Vector4(L.roughness, L.reflectionStretch, L.surfaceBump, L.puddleInCracks));
            Shader.SetGlobalFloat(IdEnv, L.environment);
            Shader.SetGlobalVector(IdPuddle, new Vector4(L.puddlePatches, L.puddleScale, L.reflectionContrast, L.reflectionCover));
            RSWetState.Set(L.rain, Overlay);
        }
    }
}
