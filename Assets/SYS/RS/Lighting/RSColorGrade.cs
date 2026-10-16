// RE:AL STEEL - 색감 (시간대별 암부 · 명부 색 · 색온도 · LUT)
//
// 색감 프리셋 에셋(RSColorGradeProfile)에 적힌 시각별 색감을, 시간대(RS 시간대)의 지금 시각에 맞춰 섞어서 화면에 입힌다.
// 효과는 URP 기본 후처리(Split Toning · Shadows Midtones Highlights · White Balance · Color Lookup) 그대로.
//
// 자기 전용 숨은 Volume 에 이 네 효과만 넣는다. 씬의 다른 Volume(블룸 · DOF · 색 보정 등)은 건드리지 않는다.
// LUT 는 URP 가 두 장을 섞어 주지 않아서, 앞뒤 시각의 LUT 를 셰이더로 섞은 텍스처를 만들어 넣는다.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("색감 (시간대별)", "시각마다 어두운 곳 · 밝은 곳의 색, 색온도, LUT 를 바꾼다. 값은 '색감 프리셋' 에셋에 있다.\n" +
        "· 시각 추가 · 수정은 아래 타임라인에서 (기획 · 그래픽 누구나)\n" +
        "· 스테이지별 색감 = 프리셋 에셋을 복제해서 바꿔 끼우기\n" +
        "· 세기 0 = 끔. 씬의 다른 Volume 값은 건드리지 않는다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/색감 (시간대별)")]
    public partial class RSColorGrade : MonoBehaviour, IRSEditorAnimated, IRSVolumeOwner
    {
        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [Tooltip("시각별 색감이 적힌 프리셋 에셋. 비어 있으면 인스펙터의 '기본 프리셋 만들기'")]
            [RSKey]
            public RSColorGradeProfile profile;
            [RSGroup("세기")]
            [Range(0f, 1f), Tooltip("전체 세기. 0 = 끔, 1 = 프리셋 그대로")]
            [RSKey]
            public float strength = 1f;
            [RSGroup("어두운 곳 · 밝은 곳 경계 (Shadows Midtones Highlights)")]
            [RSHelp("어디까지를 '어두운 곳' · '밝은 곳' 으로 볼지. 보통 그대로 둔다.")]
            [Range(0f, 1f), Tooltip("어두운 곳이 끝나기 시작하는 밝기")]
            public float shadowsStart = 0f;
            [Range(0f, 1f), Tooltip("어두운 곳이 완전히 끝나는 밝기")]
            public float shadowsEnd = 0.3f;
            [Range(0f, 1f), Tooltip("밝은 곳이 시작하는 밝기")]
            public float highlightsStart = 0.55f;
            [Range(0f, 1f), Tooltip("밝은 곳이 가장 강해지는 밝기")]
            public float highlightsEnd = 1f;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.colorGrade : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }

        [Tooltip("시간대. 비우면 씬에서 찾는다")]
        public RSTimeOfDay timeOfDay;


        [RSGroup("시간대 없이 쓸 때")]
        [Tooltip("시간대를 따르지 않고 아래 시각으로 고정")]
        public bool fixedHour = false;
        [Range(0f, 24f), Tooltip("고정 시각")]
        public float hour = 12f;


        [RSGroup("고급")]
        [Tooltip("숨은 Volume 우선순위 (씬 Volume 보다 높게)")]
        public float priority = 55f;

        [SerializeField, HideInInspector] Shader lutBlendShader;   // 빌드에 셰이더가 빠지지 않게

        const string ChildName = "__RS_ColorGrade (자동 생성 · 저장 안 됨)";
        const string LutShaderName = "Hidden/RE_AL STEEL/LUT Blend";

        Volume vol;
        public Volume OwnedVolume { get { return vol; } }
        public string VolumeRole { get { return "색감 — 어두운 곳 · 밝은 곳 색(Split Toning · SMH) · 색온도 · LUT (시각별)"; } }
        VolumeProfile vp;
        SplitToning split;
        ShadowsMidtonesHighlights smh;
        WhiteBalance wb;
        ColorLookup lookup;
        RenderTexture lutRT;
        Material lutMat;
        readonly RSColorGradeProfile.Key cur = new RSColorGradeProfile.Key();

        float appliedHour = -1f, appliedStrength = -1f;
        int appliedVersion = -1;

        /// <summary>지금 적용 중인 시각</summary>
        public float CurrentHour
        {
            get { return fixedHour || timeOfDay == null ? hour : timeOfDay.Hour; }
        }

        /// <summary>URP 에셋이 요구하는 LUT 크기 (Grading LUT Size, 기본 32)</summary>
        public static int RequiredLutSize
        {
            get { var a = UniversalRenderPipeline.asset; return a != null ? a.colorGradingLutSize : 32; }
        }

        /// <summary>LUT 를 못 쓰는 이유 (없으면 null) — 인스펙터가 보여 준다</summary>
        public string LutProblem { get; private set; }
        string warnedLut;

        /// <summary>지금 섞여서 적용 중인 값 (인스펙터 미리보기용)</summary>
        public RSColorGradeProfile.Key Current => cur;

        public bool WantsEditorAnimation => isActiveAndEnabled && NeedsApply();

        void Reset() { lutBlendShader = Shader.Find(LutShaderName); }

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            if (lutBlendShader == null) lutBlendShader = Shader.Find(LutShaderName);
            if (timeOfDay == null) timeOfDay = FindAnyObjectByType<RSTimeOfDay>();
            RSLightingClock.Register(this);
            appliedHour = -1f;
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

            OnValidate(); Apply();

        }


        void OnDisable()
        {

            RSStageLook.Changed -= OnStageLookChanged;
            RSLightingClock.Unregister(this);
            if (vol != null) vol.enabled = false;
        }

        void OnDestroy()
        {
            if (vol != null) Kill(vol.gameObject);
            Kill(vp); Kill(lutMat);
            if (lutRT != null) { lutRT.Release(); Kill(lutRT); }
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnValidate()
        {

            MigrateLegacy();
            L.shadowsEnd = Mathf.Max(L.shadowsStart, L.shadowsEnd);
            L.highlightsEnd = Mathf.Max(L.highlightsStart, L.highlightsEnd);
            appliedHour = -1f;
        }

        void Update() { if (NeedsApply()) Apply(); }
        public void EditorTick(float dt) { if (NeedsApply()) Apply(); }

        bool NeedsApply()
        {
            if (L.profile != null && L.profile.version != appliedVersion) return true;
            if (!Mathf.Approximately(L.strength, appliedStrength)) return true;
            return Mathf.Abs(CurrentHour - appliedHour) > 0.005f;
        }

        /// <summary>지금 시각의 색감을 바로 다시 입힌다</summary>
        public void Apply()
        {
            float h = CurrentHour;
            appliedHour = h; appliedStrength = L.strength;
            appliedVersion = L.profile != null ? L.profile.version : -1;

            if (L.profile == null || L.strength <= 0f || !L.profile.Evaluate(h, cur, out var before, out var after, out float t))
            {
                if (vol != null && vol.enabled) vol.enabled = false;
                return;
            }

            Ensure();
            if (!vol.enabled) vol.enabled = true;
            vol.priority = priority;
            vol.weight = L.strength;

            split.shadows.Override(cur.shadowTint);
            split.highlights.Override(cur.highlightTint);
            split.balance.Override(cur.balance);

            smh.shadows.Override(new Vector4(cur.shadows.r, cur.shadows.g, cur.shadows.b, cur.shadowsBrightness));
            smh.midtones.Override(new Vector4(cur.midtones.r, cur.midtones.g, cur.midtones.b, cur.midtonesBrightness));
            smh.highlights.Override(new Vector4(cur.highlights.r, cur.highlights.g, cur.highlights.b, cur.highlightsBrightness));
            smh.shadowsStart.Override(L.shadowsStart);
            smh.shadowsEnd.Override(L.shadowsEnd);
            smh.highlightsStart.Override(L.highlightsStart);
            smh.highlightsEnd.Override(L.highlightsEnd);

            wb.temperature.Override(cur.temperature);
            wb.tint.Override(cur.tint);

            ApplyLut(before, after, t);
        }

        void ApplyLut(RSColorGradeProfile.Key a, RSColorGradeProfile.Key b, float t)
        {
            Texture2D la = a != null && a.lutAmount > 0f ? a.lut : null;
            Texture2D lb = b != null && b.lutAmount > 0f ? b.lut : null;
            if (la == null && lb == null)
            {
                lookup.active = false;
                return;
            }

            // LUT 크기 = 세로 픽셀 수 (가로 = 크기²). URP 는 이 크기가 URP 에셋의 Grading LUT Size 와 다르면
            // 경고 없이 LUT 를 무시한다 → 여기서 먼저 검사해 알려 준다
            int want = RequiredLutSize;
            string problem = null;
            if (la != null && (la.height != want || la.width != want * want)) problem = la.name;
            else if (lb != null && (lb.height != want || lb.width != want * want)) problem = lb.name;
            if (problem != null)
            {
                LutProblem = $"LUT '{problem}' 크기가 URP 에셋의 Grading LUT Size({want}) 와 다릅니다 — 가로 {want * want} x 세로 {want} 이어야 합니다. 'LUT 작업 이미지' 부터 다시 만들거나 URP 에셋 값을 맞추세요.";
                if (warnedLut != problem) { Debug.LogWarning("[색감] " + LutProblem, this); warnedLut = problem; }
                lookup.active = false;
                return;
            }
            LutProblem = null; warnedLut = null;
            int size = want;

            if (lutMat == null)
            {
                if (lutBlendShader == null) lutBlendShader = Shader.Find(LutShaderName);
                if (lutBlendShader == null) { lookup.active = false; return; }
                lutMat = new Material(lutBlendShader) { hideFlags = HideFlags.DontSave, name = "RS_LutBlend (자동)" };
            }
            if (lutRT == null || lutRT.height != size)
            {
                if (lutRT != null) { lutRT.Release(); Kill(lutRT); }
                lutRT = new RenderTexture(size * size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "RS_LutBlend", hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false,
                };
                lutRT.Create();
            }

            // LUT 가 없는 쪽은 '보정 없음' 으로 섞는다
            lutMat.SetTexture("_LutA", la != null ? la : Texture2D.whiteTexture);
            lutMat.SetTexture("_LutB", lb != null ? lb : Texture2D.whiteTexture);
            lutMat.SetVector("_Params", new Vector4(
                la != null ? a.lutAmount : 0f,
                lb != null ? b.lutAmount : 0f,
                t, size));
            Graphics.Blit(null, lutRT, lutMat);

            lookup.active = true;
            lookup.texture.Override(lutRT);
            lookup.contribution.Override(1f);
        }

        void Ensure()
        {
            if (vol == null)
            {
                var old = transform.Find(ChildName);
                if (old != null) vol = old.GetComponent<Volume>();
                if (vol == null)
                {
                    var go = new GameObject(ChildName) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
                    go.transform.SetParent(transform, false);
                    vol = go.AddComponent<Volume>();
                }
                vol.isGlobal = true;
            }
            if (vp == null)
            {
                vp = ScriptableObject.CreateInstance<VolumeProfile>();
                vp.name = "RS_ColorGrade (런타임)";
                vp.hideFlags = HideFlags.DontSave;
            }
            if (split == null && !vp.TryGet(out split)) split = vp.Add<SplitToning>(false);
            if (smh == null && !vp.TryGet(out smh)) smh = vp.Add<ShadowsMidtonesHighlights>(false);
            if (wb == null && !vp.TryGet(out wb)) wb = vp.Add<WhiteBalance>(false);
            if (lookup == null && !vp.TryGet(out lookup)) lookup = vp.Add<ColorLookup>(false);
            if (vol.sharedProfile != vp) vol.sharedProfile = vp;
        }

        /// <summary>LUT 작업 이미지를 찍는 동안 LUT 만 잠깐 끄기 (에디터 도구용)</summary>
        public void SetLutSuspended(bool suspended)
        {
            if (lookup == null) return;
            if (suspended) lookup.active = false;
            else { appliedHour = -1f; Apply(); }
        }
    }
}
