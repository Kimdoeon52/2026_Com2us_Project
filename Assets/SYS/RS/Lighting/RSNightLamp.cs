// RE:AL STEEL - 밤 조명 (RSNightLamp)
//
// 가로등 · 간판 등 · 줄전구처럼 "해가 지면 켜지는" 조명 묶음. 시간대(RSTimeOfDay)의 시각을 읽어
// 점광 세기와 발광 소품(Unlit 머티리얼) 밝기를 같이 올리고 내린다.
//  · 켜지는 시각 ~ 꺼지는 시각 사이에 켜진다 (기본 17.8시 ~ 6.5시). 씬에 시간대가 없으면 늘 켜짐
//  · 낮에도 조금 켜 두려면 '낮 빛 세기' · '낮 발광 밝기'
//  · 발광 색은 머티리얼의 _BaseColor (HDR). 여기서는 밝기만 곱한다 (머티리얼은 안 바꿈)
using UnityEngine;

namespace RealSteel.Lighting
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/밤 조명 (시간대 따라 켜짐)")]
    public class RSNightLamp : MonoBehaviour
    {
        [Tooltip("같이 켜고 끌 점광")]
        public Light[] lights = new Light[0];
        [Tooltip("켜졌을 때 점광 세기 (lights 와 같은 순서)")]
        public float[] intensity = new float[0];
        [Tooltip("발광 소품 (URP Unlit · _BaseColor 를 밝기로 곱한다)")]
        public Renderer[] glows = new Renderer[0];

        [Range(0f, 24f), Tooltip("이 시각부터 켜진다")]
        public float onHour = 17.8f;
        [Range(0f, 24f), Tooltip("이 시각에 꺼진다")]
        public float offHour = 6.5f;
        [Range(0f, 1f), Tooltip("낮 빛 세기 (0 = 낮에는 점광 꺼짐)")]
        public float dayLight = 0f;
        [Range(0f, 1f), Tooltip("낮 발광 밝기 (0 = 낮에는 전구가 꺼진 색)")]
        public float dayGlow = 0.22f;
        [Min(0f), Tooltip("켜지고 꺼지는 데 걸리는 시간 (초)")]
        public float fadeSeconds = 1.2f;
        [Tooltip("시간대와 상관없이 늘 켜 둔다")]
        public bool alwaysOn = false;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        RSTimeOfDay tod;
        float nextFind;
        float level = -1f;
        MaterialPropertyBlock mpb;

        /// <summary>지금 켜진 정도 (0 = 낮, 1 = 밤)</summary>
        public float Level { get { return Mathf.Max(0f, level); } }

        void OnEnable() { level = -1f; tod = null; nextFind = 0f; }

        bool IsOn()
        {
            if (alwaysOn) return true;
            if (tod == null)
            {
                float now = Application.isPlaying ? Time.unscaledTime : (float)(System.DateTime.Now.TimeOfDay.TotalSeconds);
                if (now >= nextFind || now < nextFind - 5f)
                {
                    nextFind = now + 1f;
                    tod = FindFirstObjectByType<RSTimeOfDay>();
                }
                if (tod == null) return true;
            }
            float h = tod.Hour;
            return onHour <= offHour ? (h >= onHour && h < offHour) : (h >= onHour || h < offHour);
        }

        void Update()
        {
            float target = IsOn() ? 1f : 0f;
            if (level < 0f || !Application.isPlaying || fadeSeconds <= 0f) level = target;
            else level = Mathf.MoveTowards(level, target, Time.deltaTime / fadeSeconds);
            Apply();
        }

        void Apply()
        {
            float kl = Mathf.Lerp(dayLight, 1f, level);
            if (lights != null)
                for (int i = 0; i < lights.Length; i++)
                {
                    var l = lights[i];
                    if (l == null) continue;
                    float v = (intensity != null && i < intensity.Length ? intensity[i] : 1f) * kl;
                    if (l.intensity != v) l.intensity = v;
                    bool on = v > 0.001f;
                    if (l.enabled != on) l.enabled = on;
                }
            if (glows == null || glows.Length == 0) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            float kg = Mathf.Lerp(dayGlow, 1f, level);
            foreach (var r in glows)
            {
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColor)) continue;
                Color c = r.sharedMaterial.GetColor(BaseColor);
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, new Color(c.r * kg, c.g * kg, c.b * kg, c.a));
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
