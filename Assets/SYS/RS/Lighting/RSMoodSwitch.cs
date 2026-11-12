// RE:AL STEEL - 낮 / 밤 전환 (실내 조명 분위기)
//
// 시간대(RS 시간대)가 없는 실내 씬에서 '불 켠 낮' 과 '불 끈 밤' 을 오간다.
//  · 밤: 전등(lamps)은 꺼지고, 남겨 둘 약한 불빛(dimLamps — 모니터 대기 화면 등)만 어둡게 남는다
//  · 주광 · 보조광 · 주변광이 달빛 색으로 바뀌고, 볼류메트릭 안개의 '고정 빛' 이 달빛 색이 된다 → 갓레이가 달빛 줄기로
//  · 인스펙터 '밤' 을 켜고 끄면 에디터에서 바로 보인다. 플레이 중에는 '바뀌는 시간' 동안 부드럽게 섞인다
//  · 코드: GetComponent<RSMoodSwitch>().SetNight(true) / Toggle() — 침대에서 잠들 때 등
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("낮 / 밤 전환 (실내)", "불 켠 낮과 불 끈 밤을 오간다.\n" +
        "· 밤에는 전등이 꺼지고 주광 · 주변광 · 갓레이(안개의 고정 빛)가 달빛 색으로 바뀐다\n" +
        "· '밤' 을 켜고 끄면 바로 보인다. 플레이 중에는 부드럽게 섞인다\n" +
        "· 낮 값 · 밤 값은 아래에서 따로 조절한다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/낮 밤 전환 (실내)")]
    public class RSMoodSwitch : MonoBehaviour
    {
        [RSGroup("전환", true)]
        [RSKey, Tooltip("밤 (불 끔)")]
        public bool night = false;
        [Min(0f), Tooltip("플레이 중 바뀌는 시간 (초). 0 = 즉시")]
        public float duration = 1.5f;

        [RSGroup("전등")]
        [Tooltip("밤에 끄는 전등")]
        public Light[] lamps = new Light[0];
        [Tooltip("전등의 낮 세기 (lamps 와 같은 순서 — 메뉴가 채운다)")]
        public float[] lampIntensity = new float[0];
        [Tooltip("밤에도 약하게 남기는 불빛 (모니터 대기 화면 등)")]
        public Light[] dimLamps = new Light[0];
        [Tooltip("남기는 불빛의 낮 세기")]
        public float[] dimIntensity = new float[0];
        [Range(0f, 1f), Tooltip("남기는 불빛의 밤 세기 (낮 대비)")]
        public float nightDimFactor = 0.25f;

        [RSGroup("주광 · 보조광 · 주변광")]
        [Tooltip("주광 (그림자를 만드는 방향광). 밤에는 달빛")]
        public Light key;
        public Color dayKeyColor = new Color(1f, 0.93f, 0.84f);
        public float dayKeyIntensity = 0.55f;
        public Color nightKeyColor = new Color(0.5f, 0.64f, 1f);
        public float nightKeyIntensity = 0.3f;
        [Tooltip("보조광")]
        public Light fill;
        public Color dayFillColor = new Color(0.55f, 0.64f, 0.85f);
        public float dayFillIntensity = 0.18f;
        public Color nightFillColor = new Color(0.3f, 0.4f, 0.7f);
        public float nightFillIntensity = 0.08f;
        [Tooltip("주변광 (Lighting 창의 Ambient Color 를 바꾼다)")]
        public Color dayAmbient = new Color(0.27f, 0.28f, 0.34f);
        public Color nightAmbient = new Color(0.06f, 0.08f, 0.15f);

        [RSGroup("갓레이 (안개의 고정 빛)")]
        [Tooltip("볼류메트릭 안개 상자 ('고정 빛' 이 켜진 것)")]
        public RSFogVolume fog;
        public Color dayFogColor = new Color(1f, 0.9f, 0.72f);
        public float dayFogIntensity = 1.1f;
        [Tooltip("달빛 줄기 색")]
        public Color nightFogColor = new Color(0.62f, 0.76f, 1f);
        [Tooltip("달빛 줄기 세기 (방이 어두워서 낮보다 조금 세게 해야 보인다)")]
        public float nightFogIntensity = 1.5f;

        float blend = -1f;   // 0 = 낮, 1 = 밤

        /// <summary>지금 섞인 정도 (0 낮 ~ 1 밤)</summary>
        public float Blend { get { return Mathf.Clamp01(blend); } }

        public void SetNight(bool value, bool instant = false)
        {
            night = value;
            if (instant) ApplyImmediate(value);
        }
        public void Toggle() { night = !night; }

        /// <summary>섞지 않고 바로 낮 / 밤 값으로</summary>
        public void ApplyImmediate(bool toNight)
        {
            night = toNight;
            blend = toNight ? 1f : 0f;
            Apply(blend);
        }

        [ContextMenu("낮 / 밤 바꾸기")]
        void ToggleFromMenu() { Toggle(); }

        void OnEnable() { blend = night ? 1f : 0f; Apply(blend); }

        void Update()
        {
            float target = night ? 1f : 0f;
            if (!Application.isPlaying || duration <= 0f) blend = target;
            else blend = Mathf.MoveTowards(blend, target, Time.deltaTime / duration);
            Apply(blend);
        }

        void Apply(float t)
        {
            t = Mathf.Clamp01(t);
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;
                float v = (i < lampIntensity.Length ? lampIntensity[i] : 1f) * (1f - t);
                Set(lamps[i], v);
            }
            for (int i = 0; i < dimLamps.Length; i++)
            {
                if (dimLamps[i] == null) continue;
                float v = (i < dimIntensity.Length ? dimIntensity[i] : 1f) * Mathf.Lerp(1f, nightDimFactor, t);
                Set(dimLamps[i], v);
            }
            if (key != null)
            {
                key.color = Color.Lerp(dayKeyColor, nightKeyColor, t);
                key.intensity = Mathf.Lerp(dayKeyIntensity, nightKeyIntensity, t);
            }
            if (fill != null)
            {
                fill.color = Color.Lerp(dayFillColor, nightFillColor, t);
                fill.intensity = Mathf.Lerp(dayFillIntensity, nightFillIntensity, t);
            }
            Color amb = Color.Lerp(dayAmbient, nightAmbient, t);
            if (RenderSettings.ambientLight != amb) RenderSettings.ambientLight = amb;
            if (fog != null)
            {
                fog.fixedLightColor = Color.Lerp(dayFogColor, nightFogColor, t);
                fog.fixedLightIntensity = Mathf.Lerp(dayFogIntensity, nightFogIntensity, t);
            }
        }

        static void Set(Light l, float intensity)
        {
            if (!Mathf.Approximately(l.intensity, intensity)) l.intensity = intensity;
            bool on = intensity > 0.001f;
            if (l.enabled != on) l.enabled = on;
        }
    }
}
