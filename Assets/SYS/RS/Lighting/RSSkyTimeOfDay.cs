// RE:AL STEEL - 시간대 하늘 (스카이박스를 시간대에 맞춰 바꾼다)
//
// 시간대(RSTimeOfDay)의 시각 · 해/달 빛을 읽어 하늘 머티리얼(RE_AL STEEL/Sky Time Of Day) 값을 넣는다.
//  · 새벽 → 낮 → 노을 → 밤 (별 · 달) 색은 아래 그라데이션 · 커브로 고친다. 가로축 = 0시 → 24시
//  · 해 · 달 위치 = 시간대의 해 빛(sun) 방향. 밤에는 그 빛이 달이 된다 (시간대 규칙 그대로)
//  · 편집 중에는 머티리얼 에셋(MAT_RS_Sky)을 바로 고치고, 플레이 중에는 복사본을 써서 에셋이 안 바뀐다
//  · 씬의 스카이박스(Lighting 창 Environment)를 이 머티리얼로 맞춘다
//  · 게임 카메라(35° 내려다봄)에는 하늘이 직접 안 보인다 — 씬 뷰 · 반사 · 낮은 각도 카메라용.
//    플레이 중 30분(게임 시각)마다 환경광/반사를 다시 계산한다 (끄려면 '환경 갱신' 끄기)
using UnityEngine;

namespace RealSteel.Lighting
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/시간대 하늘 (스카이박스)")]
    public class RSSkyTimeOfDay : MonoBehaviour
    {
        [Tooltip("하늘 머티리얼 (셰이더 RE_AL STEEL/Sky Time Of Day) — 보통 Materials/Sky/MAT_RS_Sky")]
        public Material material;
        [Tooltip("시간대. 비우면 씬에서 찾는다")]
        public RSTimeOfDay timeOfDay;

        [Header("색 (가로축 = 0시 → 24시)")]
        [Tooltip("하늘 꼭대기 색")]
        public Gradient zenith = G(new[] { 0f, 5f, 6.5f, 9f, 15.5f, 17.8f, 19.3f, 24f },
            new[] { C(0.02f, 0.03f, 0.08f), C(0.05f, 0.07f, 0.16f), C(0.24f, 0.33f, 0.56f), C(0.30f, 0.50f, 0.82f),
                    C(0.30f, 0.50f, 0.82f), C(0.32f, 0.38f, 0.64f), C(0.09f, 0.09f, 0.22f), C(0.02f, 0.03f, 0.08f) });
        [Tooltip("지평선 색")]
        public Gradient horizon = G(new[] { 0f, 5.2f, 6.2f, 8f, 16f, 18.3f, 19.4f, 24f },
            new[] { C(0.05f, 0.07f, 0.14f), C(0.20f, 0.18f, 0.30f), C(0.95f, 0.62f, 0.44f), C(0.70f, 0.82f, 0.92f),
                    C(0.72f, 0.82f, 0.90f), C(0.98f, 0.56f, 0.32f), C(0.28f, 0.19f, 0.32f), C(0.05f, 0.07f, 0.14f) });
        [Tooltip("해 쪽 지평선 노을 색")]
        public Gradient glowColor = G(new[] { 0f, 6.2f, 12f, 18.4f, 24f },
            new[] { C(0.40f, 0.30f, 0.50f), C(1f, 0.60f, 0.45f), C(1f, 0.90f, 0.75f), C(1f, 0.48f, 0.25f), C(0.40f, 0.30f, 0.50f) });
        [Tooltip("노을 세기 (0 ~ 2)")]
        public AnimationCurve glowAmount = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(5.2f, 0f), new Keyframe(6.2f, 1f),
            new Keyframe(7.8f, 0.15f), new Keyframe(16.8f, 0.15f), new Keyframe(18.4f, 1.1f), new Keyframe(19.4f, 0.2f), new Keyframe(24f, 0f));
        [Tooltip("별 (0 ~ 1)")]
        public AnimationCurve stars = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(5f, 0.8f), new Keyframe(6.3f, 0f),
            new Keyframe(18.6f, 0f), new Keyframe(19.8f, 1f), new Keyframe(24f, 1f));
        [Tooltip("지평선 아래 (땅) 색 = 지평선 색 × 이 값")]
        [Range(0f, 1f)] public float groundDarken = 0.35f;

        [Header("구름 · 모양")]
        [Range(0f, 1f)] public float clouds = 0.35f;
        [Tooltip("밤 구름 밝기 (낮은 1)")]
        [Range(0f, 1f)] public float nightCloudBright = 0.35f;
        [Tooltip("해 · 달 빛 색을 원반 색에 섞는 정도")]
        [Range(0f, 1f)] public float useLightColor = 0.5f;

        [Header("플레이 중")]
        [Tooltip("게임 시각 30분마다 환경광 · 반사를 다시 계산 (DynamicGI.UpdateEnvironment)")]
        public bool updateEnvironment = true;

        static readonly int IdZenith = Shader.PropertyToID("_ZenithColor"), IdHorizon = Shader.PropertyToID("_HorizonColor"),
            IdGround = Shader.PropertyToID("_GroundColor"), IdLight = Shader.PropertyToID("_LightDir"), IdSun = Shader.PropertyToID("_SunColor"),
            IdMoon = Shader.PropertyToID("_IsMoon"), IdGlowC = Shader.PropertyToID("_GlowColor"), IdGlowA = Shader.PropertyToID("_GlowAmount"),
            IdStars = Shader.PropertyToID("_Stars"), IdClouds = Shader.PropertyToID("_Clouds"), IdCloudB = Shader.PropertyToID("_CloudBright");

        Material runtimeMat;
        float nextFind;
        float lastHour = -999f, lastEnvHour = -999f;
        Vector3 lastDir;
        bool lastMoon;

        static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

        static Gradient G(float[] hours, Color[] cols)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[hours.Length];
            for (int i = 0; i < hours.Length; i++) ck[i] = new GradientColorKey(cols[i], hours[i] / 24f);
            g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        Material Target
        {
            get
            {
                if (material == null) return null;
                if (!Application.isPlaying) return material;
                if (runtimeMat == null) runtimeMat = new Material(material) { name = material.name + " (플레이 중)", hideFlags = HideFlags.DontSave };
                return runtimeMat;
            }
        }

        void OnEnable() { lastHour = -999f; lastEnvHour = -999f; Apply(true); }

        void OnDisable()
        {
            if (runtimeMat != null)
            {
                if (RenderSettings.skybox == runtimeMat) RenderSettings.skybox = material;
                if (Application.isPlaying) Destroy(runtimeMat); else DestroyImmediate(runtimeMat);
                runtimeMat = null;
            }
        }

        void Update() { Apply(false); }

        void OnValidate() { lastHour = -999f; }

        RSTimeOfDay Tod
        {
            get
            {
                if (timeOfDay != null) return timeOfDay;
                float now = Time.realtimeSinceStartup;
                if (now >= nextFind || now < nextFind - 5f)
                {
                    nextFind = now + 1f;
                    timeOfDay = FindFirstObjectByType<RSTimeOfDay>();
                }
                return timeOfDay;
            }
        }

        void Apply(bool force)
        {
            var m = Target;
            if (m == null) return;
            if (RenderSettings.skybox != m) RenderSettings.skybox = m;

            var tod = Tod;
            float h = tod != null ? tod.Hour : 12f;
            Light l = tod != null && tod.sun != null ? tod.sun : RenderSettings.sun;
            Vector3 dir = l != null ? -l.transform.forward : new Vector3(0f, 0.6f, 0.8f).normalized;
            bool moon = tod != null && tod.IsMoon;
            if (!force && Mathf.Abs(h - lastHour) < 0.01f && moon == lastMoon && Vector3.Dot(dir, lastDir) > 0.99999f) return;
            lastHour = h; lastMoon = moon; lastDir = dir;

            float u = Mathf.Repeat(h, 24f) / 24f;
            Color hz = horizon.Evaluate(u);
            m.SetColor(IdZenith, zenith.Evaluate(u));
            m.SetColor(IdHorizon, hz);
            m.SetColor(IdGround, hz * groundDarken);
            m.SetVector(IdLight, dir);
            Color baseC = moon ? new Color(0.86f, 0.9f, 1f) : new Color(1f, 0.93f, 0.78f);
            Color lc = l != null ? l.color : baseC;
            m.SetColor(IdSun, Color.Lerp(baseC, lc, useLightColor));
            m.SetFloat(IdMoon, moon ? 1f : 0f);
            m.SetColor(IdGlowC, glowColor.Evaluate(u));
            m.SetFloat(IdGlowA, Mathf.Max(0f, glowAmount.Evaluate(h)));
            m.SetFloat(IdStars, Mathf.Clamp01(stars.Evaluate(h)));
            m.SetFloat(IdClouds, clouds);
            m.SetFloat(IdCloudB, moon ? nightCloudBright : 1f);

            if (Application.isPlaying && updateEnvironment && Mathf.Abs(Mathf.DeltaAngle(h * 15f, lastEnvHour * 15f)) >= 7.5f)
            {
                lastEnvHour = h;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
