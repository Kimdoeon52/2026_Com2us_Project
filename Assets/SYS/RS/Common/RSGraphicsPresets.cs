// RE:AL STEEL - 그래픽 프리셋 에셋 (낮음 · 중간 · 높음 단계 값 + 품질 숫자)
//
// 위치: SYS/Presets/Graphics/Resources/RS_GraphicsPresets.asset  (Resources 폴더라 빌드에서도 읽힌다)
//       에디터를 열면 없을 때 자동으로 만든다. 메뉴: Tools → RE_AL STEEL → 그래픽 프리셋
// 인스펙터: 항목 × 낮음 · 중간 · 높음 표. 고치면 바로 반영된다 (플레이 중이면 지금 단계에도)
//
//  · 위 표 = 각 단계에서 무엇을 켜고 끄는지, 품질을 몇 단계로 할지
//  · 아래 '품질 숫자' = 품질 단계(낮음 · 중간 · 높음)가 실제로 몇인지 (안개 걸음 수 · 그림자 거리 배율 …)
//  · '높음' 은 보통 아트가 정한 그대로(전부 켬 · 품질 높음)로 둔다
// 에셋이 없으면 코드에 적힌 기본값(이 파일의 초깃값)으로 동작한다.
using System;
using UnityEngine;

namespace RealSteel.Common
{
    /// <summary>한 단계(낮음 · 중간 · 높음)의 품질 항목 값</summary>
    [Serializable]
    public class RSGraphicsLevelValues
    {
        public bool fog = true;
        [Range(0, 2)] public int fogQuality = 2;
        public bool screenGodRays = true;
        [Range(0, 2)] public int godRayQuality = 2;
        public bool cloudShadows = true;
        [Range(0f, 1f)] public float lightShafts = 1f;
        public bool characterGlow = true;
        [Range(0, 2)] public int wetFloor = 2;
        [Range(0, 2)] public int water = 2;
        [Range(0, 2)] public int indirect = 2;
        public bool depthOfField = true;
        public bool bloom = true;
        public bool bloomHighQuality = true;
        [Range(0, 2)] public int shadows = 2;

        public void CopyTo(RSGraphicsOptions o)
        {
            o.fog = fog; o.fogQuality = fogQuality;
            o.screenGodRays = screenGodRays; o.godRayQuality = godRayQuality;
            o.cloudShadows = cloudShadows; o.lightShafts = lightShafts; o.characterGlow = characterGlow;
            o.wetFloor = wetFloor; o.water = water; o.indirect = indirect;
            o.depthOfField = depthOfField; o.bloom = bloom; o.bloomHighQuality = bloomHighQuality;
            o.shadows = shadows;
        }

        internal void Clamp()
        {
            fogQuality = Mathf.Clamp(fogQuality, 0, 2);
            godRayQuality = Mathf.Clamp(godRayQuality, 0, 2);
            lightShafts = Mathf.Clamp01(lightShafts);
            wetFloor = Mathf.Clamp(wetFloor, 0, 2);
            water = Mathf.Clamp(water, 0, 2);
            indirect = Mathf.Clamp(indirect, 0, 2);
            shadows = Mathf.Clamp(shadows, 0, 2);
        }
    }

    /// <summary>품질 단계 0 · 1 · 2 (낮음 · 중간 · 높음) 마다의 실수 값</summary>
    [Serializable]
    public struct RSTriFloat
    {
        public float low, mid, high;
        public RSTriFloat(float l, float m, float h) { low = l; mid = m; high = h; }
        public float Get(int q) { return q <= 0 ? low : q == 1 ? mid : high; }
    }

    /// <summary>품질 단계 0 · 1 · 2 (낮음 · 중간 · 높음) 마다의 정수 값</summary>
    [Serializable]
    public struct RSTriInt
    {
        public int low, mid, high;
        public RSTriInt(int l, int m, int h) { low = l; mid = m; high = h; }
        public int Get(int q) { return q <= 0 ? low : q == 1 ? mid : high; }
    }

    [CreateAssetMenu(menuName = "RE_AL STEEL/그래픽 프리셋", fileName = ResourceName, order = 11)]
    public class RSGraphicsPresets : ScriptableObject
    {
        /// <summary>Resources 폴더 안 파일 이름 (확장자 없이)</summary>
        public const string ResourceName = "RS_GraphicsPresets";

        [TextArea(2, 4), Tooltip("메모 (누가 · 언제 · 어떤 기준으로 맞췄는지)")]
        public string note = "";

        // ── 단계별 항목 (인스펙터에서는 표로 보인다) ─────────
        public RSGraphicsLevelValues low = DefaultLow();
        public RSGraphicsLevelValues mid = DefaultMid();
        public RSGraphicsLevelValues high = new RSGraphicsLevelValues();

        // ── 품질 숫자 (품질 단계 낮음 · 중간 · 높음) ─────────
        [Tooltip("안개 걸음 간격 배율. 클수록 가볍고 빛줄기가 거칠다")]
        public RSTriFloat fogStepLengthScale = new RSTriFloat(2.2f, 1.6f, 1f);
        [Tooltip("안개 걸음 수 상한. 0 = 안개 상자 설정 그대로")]
        public RSTriInt fogMaxSteps = new RSTriInt(8, 12, 0);
        [Tooltip("안개를 밝히는 포인트 · 스폿 라이트 최대 수 (0 ~ 8, 0 = 라이트가 안개를 안 밝힘)")]
        public RSTriInt fogMaxLights = new RSTriInt(2, 4, 8);
        [Tooltip("화면 갓레이 샘플 상한. 0 = 갓레이 설정 그대로")]
        public RSTriInt godRaySamples = new RSTriInt(8, 12, 0);
        [Tooltip("그림자 거리 배율 (URP 에셋 값에 곱한다)")]
        public RSTriFloat shadowDistanceScale = new RSTriFloat(0.5f, 0.75f, 1f);

        [Tooltip("젖은 바닥 '화면 반사'(1) 일 때 반사 걸음 수 상한")]
        public int wetMidSteps = 12;
        [Tooltip("젖은 바닥 '화면 반사'(1) 일 때 비추는 최대 거리 (m)")]
        public float wetMidDistance = 12f;
        [Tooltip("간접광 '거칠게'(1) 일 때 칸 크기 배율 (2 = 칸 수 1/4)")]
        public float indirectCoarseScale = 2f;

        [Tooltip("처음 실행할 때: 그래픽 메모리가 이보다 작으면 '낮음' (MB)")]
        public int recommendLowBelowMB = 2000;
        [Tooltip("처음 실행할 때: 그래픽 메모리가 이보다 작으면 '중간' (MB)")]
        public int recommendMidBelowMB = 4000;

        public RSGraphicsLevelValues Get(RSGraphicsLevel l)
        {
            switch (l)
            {
                case RSGraphicsLevel.낮음: return low;
                case RSGraphicsLevel.중간: return mid;
                default: return high;
            }
        }

        public static RSGraphicsLevelValues DefaultLow()
        {
            return new RSGraphicsLevelValues
            {
                fog = false, fogQuality = 0,
                screenGodRays = false, godRayQuality = 0,
                cloudShadows = false, lightShafts = 0f, characterGlow = true,   // 밤 전투 가독성 때문에 밤 빛은 남긴다
                wetFloor = 0, water = 0, indirect = 0,
                depthOfField = false, bloom = true, bloomHighQuality = false,   // 블룸이 없으면 네온 · 발광이 안 빛난다
                shadows = 0,
            };
        }

        public static RSGraphicsLevelValues DefaultMid()
        {
            return new RSGraphicsLevelValues
            {
                fog = true, fogQuality = 1,
                screenGodRays = true, godRayQuality = 1,
                cloudShadows = true, lightShafts = 0.5f, characterGlow = true,
                wetFloor = 1, water = 1, indirect = 1,
                depthOfField = true, bloom = true, bloomHighQuality = false,
                shadows = 1,
            };
        }

        /// <summary>처음 값(이 파일의 초깃값)으로 되돌린다</summary>
        public void ResetToDefaults()
        {
            low = DefaultLow(); mid = DefaultMid(); high = new RSGraphicsLevelValues();
            fogStepLengthScale = new RSTriFloat(2.2f, 1.6f, 1f);
            fogMaxSteps = new RSTriInt(8, 12, 0);
            fogMaxLights = new RSTriInt(2, 4, 8);
            godRaySamples = new RSTriInt(8, 12, 0);
            shadowDistanceScale = new RSTriFloat(0.5f, 0.75f, 1f);
            wetMidSteps = 12; wetMidDistance = 12f; indirectCoarseScale = 2f;
            recommendLowBelowMB = 2000; recommendMidBelowMB = 4000;
            OnValidate();
        }

        void OnValidate()
        {
            if (low == null) low = DefaultLow();
            if (mid == null) mid = DefaultMid();
            if (high == null) high = new RSGraphicsLevelValues();
            low.Clamp(); mid.Clamp(); high.Clamp();
            fogMaxLights.low = Mathf.Clamp(fogMaxLights.low, 0, 8);
            fogMaxLights.mid = Mathf.Clamp(fogMaxLights.mid, 0, 8);
            fogMaxLights.high = Mathf.Clamp(fogMaxLights.high, 0, 8);
            if (wetMidSteps < 4) wetMidSteps = 4;
            if (wetMidDistance < 1f) wetMidDistance = 1f;
            if (indirectCoarseScale < 1f) indirectCoarseScale = 1f;
            RSGraphics.PresetsEdited(this);
        }
    }
}
