// RE:AL STEEL - 시간대 (Time of Day)
//
// 한 장면의 "몇 시인가" 를 정하면 태양(달) 방향 · 색 · 세기, 앰비언트, 안개색, 보조광,
// 구름 그림자 진하기, 햇살 빛줄기 세기 · 색, 후처리 색감(색 필터 · 노출 · 채도 · 비네트),
// 밤에만 켤 오브젝트(창문 불빛 · 가로등)를 한꺼번에 맞춘다.
//
//  · 스테이지 고정: time 만 정해 두고 dayLengthMinutes = 0
//  · 실시간 흐름:   dayLengthMinutes > 0 (게임 속 하루 = 실제 N분)
//  · 스크립트:      SetTime(18f) / SetPreset(Preset.Sunset) / TransitionTo(22f, 3f) / ToggleDayNight(2f)
//
// 색 그라데이션 · 커브는 0시 → 24시 가로축. 해 뜨는 · 지는 시각을 바꾸면 그라데이션도 같이 맞춰 주세요.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("시간대 (Time of Day)", "시각 하나로 해 · 달, 그늘 색(앰비언트), 안개, 구름 그림자, 햇살, 화면 색감, 밤낮 오브젝트를 한꺼번에 맞춘다.\n· 색 그라데이션 · 커브는 가로축이 0시 → 24시\n· 고정 시간대: Day Length Minutes = 0 / 흐르는 시간: 하루가 실제 몇 분인지 입력\n· 스크립트: SetTime(18) · SetPreset(Preset.Sunset) · TransitionTo(22, 3초) · ToggleDayNight(2초)")]
    [ExecuteAlways, DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("RE_AL STEEL/Lighting/시간대 (Time of Day)")]
    public partial class RSTimeOfDay : MonoBehaviour, IRSEditorAnimated, IRSVolumeOwner
    {
        public enum Preset
        {
            [InspectorName("새벽")] Dawn,
            [InspectorName("아침")] Morning,
            [InspectorName("한낮")] Noon,
            [InspectorName("오후")] Afternoon,
            [InspectorName("노을")] Sunset,
            [InspectorName("밤")] Night,
        }

        public static readonly float[] PresetHours = { 5.9f, 8.0f, 12.5f, 15.5f, 18.4f, 22.0f };

        const string PostChildName = "__RS_TimeOfDayPost (자동 생성 · 저장 안 됨)";

        [Range(0f, 24f), Tooltip("지금 시각 (시). 위 슬라이더 · 프리셋 버튼으로 바꾼다")]
        public float time = 10f;
        [Tooltip("게임 속 하루가 실제 몇 분인지. 0 = 시간이 흐르지 않음 (스테이지 고정)")]
        public float dayLengthMinutes = 0f;
        [Tooltip("플레이하지 않아도 에디터에서 시간을 흘려 본다 (Day Length Minutes > 0 일 때)")]
        public bool flowInEditMode = false;

        [RSGroup("해 · 달")]
        [RSHelp("해가 도는 길: 한낮에 Azimuth 방향 · Max Elevation 높이에 오고, 아침→저녁 동안 좌우로 Swing 만큼 돈다. 밤에는 같은 라이트가 달빛이 된다.")]
        [Tooltip("해이자 달로 쓸 Directional Light. 설치 메뉴가 LIGHT_Key_Sun 을 넣어 준다")]
        public Light sun;
        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("해 · 달")]
            [Range(-180f, 180f), Tooltip("해가 한낮에 오는 방향 (Y 회전, 도). 그림자가 떨어지는 방향을 여기서 정한다")]
            public float azimuth = -30f;
            [Range(0f, 90f), Tooltip("아침 → 저녁 동안 해가 좌우로 도는 각도 (도)")]
            public float swing = 70f;
            [Range(10f, 89f), Tooltip("한낮 해 높이 (도). 높을수록 그림자가 짧다. 옥토패스 느낌은 50 ~ 60")]
            [RSKey]
            public float maxElevation = 55f;
            [Range(2f, 40f), Tooltip("해가 이보다 낮게 눕지 않는다 (도) — 새벽 · 노을에 그림자가 끝없이 길어지는 것 방지")]
            public float minElevation = 12f;
            [Range(0f, 24f), Tooltip("해 뜨는 시각. 바꾸면 아래 그라데이션 · 커브도 같이 맞춰 주세요")]
            public float sunriseHour = 5.5f;
            [Range(0f, 24f), Tooltip("해 지는 시각")]
            public float sunsetHour = 19.2f;
            [RSGroup("태양 (시각별)")]
            [RSHelp("햇빛 색은 그라데이션(0 ~ 24시), 세기는 커브(가로 = 시각, 세로 = 세기). 한낮 1.3 ~ 1.6 이 기준.")]
            [Tooltip("햇빛 색 (가로 = 0 ~ 24시)")]
            public Gradient sunColor = DefaultSunColor();
            [Tooltip("햇빛 세기 (가로 = 시각 0 ~ 24, 세로 = 세기). 해 뜨기 전 · 진 뒤는 0")]
            public AnimationCurve sunIntensity = DefaultSunIntensity();
            [Range(0f, 1f), Tooltip("낮 그림자 진하기 (0 = 그림자 없음, 1 = 새까맣게). 옥토패스는 진한 편 0.85 ~ 0.95")]
            [RSKey]
            public float dayShadowStrength = 0.9f;
            [RSGroup("달 (밤)")]
            [RSHelp("해가 진 뒤 같은 Directional Light 를 달빛으로 바꿔 쓴다. 해 세기가 0 근처일 때 바뀌어서 튀지 않는다.")]
            [Tooltip("달빛 색. 옥토패스 밤은 보랏빛 파랑")]
            public Color moonColor = new Color(0.62f, 0.62f, 1f);
            [Range(0f, 2f), Tooltip("달빛 세기. 0.4 ~ 0.7 — 어두워도 형태가 읽히게")]
            [RSKey]
            public float moonIntensity = 0.55f;
            [Range(10f, 89f), Tooltip("한밤 달 높이 (도)")]
            public float moonElevation = 50f;
            [Range(0f, 1f), Tooltip("밤 그림자 진하기")]
            public float nightShadowStrength = 0.6f;
            [RSGroup("앰비언트 · 안개")]
            [RSHelp("그늘 쪽 밝기 · 색 (Trilight): 하늘(위) · 수평(옆) · 지면(아래) 세 방향. 낮엔 햇빛과의 대비, 밤엔 전체 분위기를 정한다.")]
            [Tooltip("앰비언트를 시간대가 정한다 (끄면 Lighting 창 설정 그대로)")]
            public bool driveAmbient = true;
            [Range(0f, 2f), Tooltip("앰비언트 전체 배율. 그늘이 너무 밝으면 낮춘다")]
            [RSKey]
            public float ambientIntensity = 1f;
            [Tooltip("위에서 오는 그늘 색 (가로 = 0 ~ 24시)")]
            public Gradient ambientSky = DefaultAmbientSky();
            [Tooltip("옆에서 오는 그늘 색")]
            public Gradient ambientEquator = DefaultAmbientEquator();
            [Tooltip("아래(바닥 반사)에서 오는 그늘 색")]
            public Gradient ambientGround = DefaultAmbientGround();
            [Tooltip("안개 색을 시간대가 정한다 (안개 켜기 · 거리는 Lighting 창에서)")]
            public bool driveFog = true;
            [Tooltip("안개 색 (가로 = 0 ~ 24시). 낮 따뜻한 연무, 노을 주황, 밤 보라")]
            public Gradient fogColor = DefaultFog();
            [RSGroup("햇살 빛줄기 (시각별)")]
            [RSHelp("햇살 빛줄기의 시각별 세기 · 색. 최종 세기 = 이 커브 × 햇살 컴포넌트의 Intensity. 밤(달빛)엔 자동으로 0.")]
            [Tooltip("햇살 세기 배율 (가로 = 시각). 아침 · 노을 1, 한낮 0.5 정도")]
            public AnimationCurve shaftIntensity = DefaultShaftIntensity();
            [Tooltip("햇살 색 (가로 = 0 ~ 24시)")]
            public Gradient shaftColor = DefaultShaftColor();
            [RSGroup("후처리 색감 (시각별)")]
            [RSHelp("숨은 전역 Volume 을 하나 만들어 기존 Volume(블룸 · 틸트시프트 등) 위에 색감만 덮어쓴다. 색 필터는 화면에 곱해진다.")]
            [Tooltip("화면 색감을 시간대가 정한다")]
            public bool drivePost = true;
            [Tooltip("Volume 우선순위. 기존 Volume 보다 높아야 덮어쓴다")]
            public float postPriority = 50f;
            [Tooltip("화면 색 필터 (가로 = 0 ~ 24시). 밤 보랏빛, 낮 따뜻한 빛. 흰색 = 변화 없음")]
            public Gradient postTint = DefaultPostTint();
            [Tooltip("노출 보정 EV (가로 = 시각). 0 = 그대로")]
            public AnimationCurve postExposure = DefaultPostExposure();
            [Tooltip("채도 (가로 = 시각, -100 ~ 100). 낮 +10, 밤 -8 정도")]
            public AnimationCurve postSaturation = DefaultPostSaturation();
            [Tooltip("화면 가장자리 어둡게 (가로 = 시각, 0 ~ 1). 밤에 진하게")]
            public AnimationCurve postVignette = DefaultPostVignette();
            [Tooltip("비네트 색 (가로 = 0 ~ 24시)")]
            public Gradient vignetteColor = DefaultVignetteColor();
            [RSGroup("보조광 (선택)")]
            [Tooltip("낮 보조광 세기")]
            public float fillDay = 0.2f;
            [Tooltip("밤 보조광 세기")]
            public float fillNight = 0.12f;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.timeOfDay : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }







        [RSGroup("밤낮 오브젝트")]
        [RSHelp("밤이 되면 nightOnly 를 켜고 dayOnly 를 끈다 (낮이면 반대). 창문 불빛 · 가로등 Point Light · 반딧불 등을 넣는다.")]
        [Tooltip("밤에만 켤 오브젝트")]
        public GameObject[] nightOnly = new GameObject[0];
        [Tooltip("낮에만 켤 오브젝트")]
        public GameObject[] dayOnly = new GameObject[0];

        [RSGroup("보조광 (선택)")]
        [RSHelp("그림자 쪽을 살짝 채우는 두 번째 Directional (설치 메뉴가 LIGHT_Fill 을 연결). 색은 하늘 앰비언트를 따른다.")]
        [Tooltip("보조광 (없어도 됨)")]
        public Light fillLight;

        [RSGroup("연결")]
        [RSHelp("설치 메뉴가 자동으로 연결한다. 비어 있으면 그 효과는 시간대를 따르지 않는다.")]
        [Tooltip("구름 그림자 (태양에 붙은 RS Cloud Shadow)")]
        public RSCloudShadow clouds;
        [Tooltip("햇살 빛줄기 (LightShafts 오브젝트)")]
        public RSLightShafts shafts;

        // ── 상태 (읽기 전용) ──
        /// <summary>0 = 밤, 1 = 낮</summary>
        public float DayFactor { get; private set; }
        public bool IsMoon { get; private set; }
        public bool IsNight { get { return IsMoon; } }
        public float Hour { get { return Mathf.Repeat(time, 24f); } }

        float transFrom, transTo, transDur, transT = -1f;
        float appliedTime = -1f;
        int nightState = -1;

        Volume postVolume;
        public Volume OwnedVolume { get { return postVolume; } }
        public string VolumeRole { get { return "시간대 화면 색감 — 색 필터 · 노출 · 채도 · 비네트 (시각별)"; } }
        VolumeProfile postProfile;
        ColorAdjustments postColor;
        Vignette postVig;

        public bool WantsEditorAnimation
        {
            get { return isActiveAndEnabled && ((flowInEditMode && dayLengthMinutes > 0f) || transT >= 0f); }
        }

        // ─────────────────────────────────────────────────────────────
        // 스크립트용
        // ─────────────────────────────────────────────────────────────

        public void SetTime(float hour) { transT = -1f; time = Mathf.Repeat(hour, 24f); Apply(); }
        public void SetPreset(Preset p) { SetTime(PresetHours[(int)p]); }

        /// <summary>seconds 동안 hour 까지 부드럽게 (앞으로 가는 방향)</summary>
        public void TransitionTo(float hour, float seconds)
        {
            if (seconds <= 0f) { SetTime(hour); return; }
            transFrom = Hour;
            transTo = transFrom + Mathf.Repeat(hour - transFrom, 24f);
            transDur = seconds;
            transT = 0f;
        }

        /// <summary>옥토패스의 "시간대 변화" 처럼 낮 ↔ 밤</summary>
        public void ToggleDayNight(float seconds)
        {
            TransitionTo(IsNight ? PresetHours[(int)Preset.Morning] : PresetHours[(int)Preset.Night], seconds);
        }

        // ─────────────────────────────────────────────────────────────

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            RSLightingClock.Register(this);
            nightState = -1;
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

            appliedTime = -1f; Apply();

        }


        void OnDisable()
        {

            RSStageLook.Changed -= OnStageLookChanged;
            RSLightingClock.Unregister(this);
            if (postVolume != null) postVolume.enabled = false;
        }

        void OnDestroy()
        {
            if (postProfile != null) { if (Application.isPlaying) Destroy(postProfile); else DestroyImmediate(postProfile); }
        }

        void OnValidate()
        {

            MigrateLegacy();
#if UNITY_EDITOR
            // OnValidate 안에서 다른 오브젝트를 바꾸면 경고가 뜨므로 한 박자 뒤에
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) { appliedTime = -1f; Apply(); } };
#endif
        }

        void Update()
        {
            if (Application.isPlaying) { Advance(Time.deltaTime); Apply(); return; }
            // 에디터: 시각이 바뀔 때만 적용 (매 프레임 오브젝트를 건드리면 인스펙터가 계속 다시 만들어진다)
            if (!Mathf.Approximately(time, appliedTime)) Apply();
        }

        public void EditorTick(float dt)
        {
            Advance(dt);
            if (!Mathf.Approximately(time, appliedTime)) Apply();
        }

        void Advance(float dt)
        {
            if (transT >= 0f)
            {
                transT += dt;
                float k = Mathf.Clamp01(transT / transDur);
                k = k * k * (3f - 2f * k);
                time = Mathf.Repeat(Mathf.Lerp(transFrom, transTo, k), 24f);
                if (transT >= transDur) transT = -1f;
                return;
            }
            if (dayLengthMinutes > 0f)
                time = Mathf.Repeat(time + dt * 24f / (dayLengthMinutes * 60f), 24f);
        }

        // ─────────────────────────────────────────────────────────────
        // 적용
        // ─────────────────────────────────────────────────────────────

        public void Apply()
        {
            appliedTime = time;
            float h = Hour;
            float t01 = h / 24f;

            // 해 진행도 0 (해돋이) → 1 (해넘이)
            float dayLen = Mathf.Max(0.5f, Mathf.Repeat(L.sunsetHour - L.sunriseHour, 24f));
            float p = Mathf.Repeat(h - L.sunriseHour, 24f) / dayLen;     // 밤이면 1 보다 크다
            float pc = Mathf.Clamp01(p);

            float sunI = Mathf.Max(0f, L.sunIntensity.Evaluate(h));
            float night = NightFactor(h, dayLen);
            float moonI = L.moonIntensity * night;
            IsMoon = moonI > sunI;
            DayFactor = Mathf.Clamp01(sunI / 0.9f);

            Color sc = L.sunColor.Evaluate(t01);

            if (sun != null)
            {
                if (!IsMoon)
                {
                    float elev = Mathf.Max(L.minElevation, Mathf.Sin(pc * Mathf.PI) * L.maxElevation);
                    float az = L.azimuth + Mathf.Lerp(-L.swing, L.swing, pc);
                    sun.transform.rotation = Quaternion.Euler(elev, az, 0f);
                    sun.color = sc;
                    sun.intensity = sunI;
                    sun.shadowStrength = L.dayShadowStrength;
                }
                else
                {
                    float nightLen = 24f - dayLen;
                    float q = Mathf.Clamp01(Mathf.Repeat(h - L.sunsetHour, 24f) / Mathf.Max(0.5f, nightLen));
                    float elev = Mathf.Max(L.minElevation, Mathf.Sin(q * Mathf.PI) * L.moonElevation);
                    float az = L.azimuth + 180f + Mathf.Lerp(-L.swing, L.swing, q) * 0.5f;
                    sun.transform.rotation = Quaternion.Euler(elev, az, 0f);
                    sun.color = L.moonColor;
                    sun.intensity = moonI;
                    sun.shadowStrength = L.nightShadowStrength;
                }
                if (sun.type == LightType.Directional) RenderSettings.sun = sun;
            }

            if (L.driveAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = L.ambientSky.Evaluate(t01) * L.ambientIntensity;
                RenderSettings.ambientEquatorColor = L.ambientEquator.Evaluate(t01) * L.ambientIntensity;
                RenderSettings.ambientGroundColor = L.ambientGround.Evaluate(t01) * L.ambientIntensity;
            }
            if (L.driveFog) RenderSettings.fogColor = L.fogColor.Evaluate(t01);

            if (fillLight != null)
            {
                fillLight.color = L.ambientSky.Evaluate(t01);
                fillLight.intensity = Mathf.Lerp(L.fillNight, L.fillDay, DayFactor);
            }

            if (clouds != null) clouds.strengthScale = Mathf.Lerp(0.25f, 1f, DayFactor);

            if (shafts != null)
            {
                if (shafts.sun == null) shafts.sun = sun;
                shafts.sunColor = L.shaftColor.Evaluate(t01);
                shafts.intensityScale = IsMoon ? 0f : Mathf.Max(0f, L.shaftIntensity.Evaluate(h));
            }

            ApplyPost(t01, h);
            ApplyDayNightObjects();
        }

        void ApplyPost(float t01, float h)
        {
            if (!L.drivePost)
            {
                if (postVolume != null && postVolume.enabled) postVolume.enabled = false;
                return;
            }
            EnsurePost();
            if (postVolume == null) return;
            if (!postVolume.enabled) postVolume.enabled = true;
            postVolume.priority = L.postPriority;

            postColor.colorFilter.Override(L.postTint.Evaluate(t01));
            postColor.postExposure.Override(L.postExposure.Evaluate(h));
            postColor.saturation.Override(Mathf.Clamp(L.postSaturation.Evaluate(h), -100f, 100f));
            postVig.intensity.Override(Mathf.Clamp01(L.postVignette.Evaluate(h)));
            postVig.color.Override(L.vignetteColor.Evaluate(t01));
            postVig.smoothness.Override(0.5f);
        }

        void EnsurePost()
        {
            if (postVolume != null && postProfile != null && postColor != null && postVig != null) return;

            // 도메인 리로드 뒤 남은 자식 재사용
            Transform found = null;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).name == PostChildName) { found = transform.GetChild(i); break; }

            GameObject go;
            if (found != null) go = found.gameObject;
            else
            {
                go = new GameObject(PostChildName);
                go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                go.transform.SetParent(transform, false);
            }
            postVolume = go.GetComponent<Volume>();
            if (postVolume == null) postVolume = go.AddComponent<Volume>();
            postVolume.isGlobal = true;

            if (postProfile == null)
            {
                postProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                postProfile.name = "RS_TimeOfDayPost";
                postProfile.hideFlags = HideFlags.DontSave;
            }
            if (!postProfile.TryGet(out postColor)) postColor = postProfile.Add<ColorAdjustments>(false);
            if (!postProfile.TryGet(out postVig)) postVig = postProfile.Add<Vignette>(false);
            postVolume.sharedProfile = postProfile;
        }

        void ApplyDayNightObjects()
        {
            int s = IsNight ? 1 : 0;
            if (s == nightState) return;     // 바뀔 때만
            nightState = s;
            if (nightOnly != null) foreach (var g in nightOnly) if (g != null && g.activeSelf != IsNight) g.SetActive(IsNight);
            if (dayOnly != null) foreach (var g in dayOnly) if (g != null && g.activeSelf == IsNight) g.SetActive(!IsNight);
        }

        float NightFactor(float h, float dayLen)
        {
            // 해넘이 +0.1h ~ +1.1h 에 걸쳐 차오르고, 해돋이 -1.2h ~ -0.2h 에 걸쳐 빠진다
            float afterSet = Mathf.Repeat(h - L.sunsetHour, 24f);
            float beforeRise = Mathf.Repeat(L.sunriseHour - h, 24f);
            float nightLen = 24f - dayLen;
            if (afterSet > nightLen) return 0f;                      // 낮
            float up = Mathf.Clamp01((afterSet - 0.1f) / 1f);
            float down = Mathf.Clamp01((beforeRise - 0.2f) / 1f);
            float n = Mathf.Min(up, down);
            return n * n * (3f - 2f * n);
        }

        /// <summary>색 그라데이션 · 커브 · 달 · 후처리를 기본값(옥토패스 참고)으로</summary>
        public void ResetLook()
        {
            L.sunColor = DefaultSunColor();
            L.sunIntensity = DefaultSunIntensity();
            L.ambientSky = DefaultAmbientSky();
            L.ambientEquator = DefaultAmbientEquator();
            L.ambientGround = DefaultAmbientGround();
            L.fogColor = DefaultFog();
            L.shaftIntensity = DefaultShaftIntensity();
            L.shaftColor = DefaultShaftColor();
            L.postTint = DefaultPostTint();
            L.postExposure = DefaultPostExposure();
            L.postSaturation = DefaultPostSaturation();
            L.postVignette = DefaultPostVignette();
            L.vignetteColor = DefaultVignetteColor();
            L.moonColor = new Color(0.62f, 0.62f, 1f);
            L.moonIntensity = 0.55f;
            L.dayShadowStrength = 0.9f;
            L.nightShadowStrength = 0.6f;
            L.fillDay = 0.2f; L.fillNight = 0.12f;
            appliedTime = -1f;
            Apply();
        }

        // ─────────────────────────────────────────────────────────────
        // 기본값 — 옥토패스 트래블러 참고
        //  낮: 황금빛 햇살 · 짙은 그늘 대비 · 따뜻한 연무     밤: 보랏빛 푸른 달빛 · 짙은 비네트
        // ─────────────────────────────────────────────────────────────

        static Gradient G(params (float hour, Color c)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) ck[i] = new GradientColorKey(keys[i].c, keys[i].hour / 24f);
            g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static AnimationCurve K(params (float hour, float v)[] keys)
        {
            var ks = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++) ks[i] = new Keyframe(keys[i].hour, keys[i].v);
            var c = new AnimationCurve(ks);
            for (int i = 0; i < ks.Length; i++) SmoothKey(c, i);
            return c;
        }

        static void SmoothKey(AnimationCurve c, int i)
        {
            // 기울기를 이웃 점 기준으로 부드럽게 (에디터 API 없이)
            var k = c.keys;
            float tan = 0f;
            if (i > 0 && i < k.Length - 1) tan = (k[i + 1].value - k[i - 1].value) / Mathf.Max(0.0001f, k[i + 1].time - k[i - 1].time);
            var kk = k[i]; kk.inTangent = tan; kk.outTangent = tan;
            c.MoveKey(i, kk);
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

        public static Gradient DefaultSunColor()
        {
            return G((5.3f, C(1.00f, 0.52f, 0.40f)), (6.6f, C(1.00f, 0.72f, 0.48f)), (8.5f, C(1.00f, 0.86f, 0.62f)),
                     (12.5f, C(1.00f, 0.92f, 0.76f)), (16.0f, C(1.00f, 0.84f, 0.58f)), (17.8f, C(1.00f, 0.64f, 0.36f)),
                     (18.8f, C(1.00f, 0.46f, 0.28f)), (19.4f, C(0.85f, 0.36f, 0.36f)));
        }

        public static AnimationCurve DefaultSunIntensity()
        {
            return K((5.2f, 0f), (6.3f, 0.7f), (8.0f, 1.35f), (12.5f, 1.6f), (16.5f, 1.4f), (18.2f, 0.95f), (19.3f, 0f));
        }

        public static Gradient DefaultAmbientSky()
        {
            return G((0f, C(0.24f, 0.22f, 0.50f)), (4.8f, C(0.25f, 0.23f, 0.50f)), (6.2f, C(0.46f, 0.38f, 0.50f)),
                     (9f, C(0.48f, 0.52f, 0.50f)), (15.5f, C(0.50f, 0.52f, 0.48f)), (18.4f, C(0.52f, 0.38f, 0.42f)),
                     (20f, C(0.26f, 0.23f, 0.52f)), (24f, C(0.24f, 0.22f, 0.50f)));
        }

        public static Gradient DefaultAmbientEquator()
        {
            return G((0f, C(0.16f, 0.14f, 0.34f)), (4.8f, C(0.17f, 0.15f, 0.34f)), (6.2f, C(0.38f, 0.30f, 0.32f)),
                     (9f, C(0.40f, 0.40f, 0.34f)), (15.5f, C(0.42f, 0.40f, 0.32f)), (18.4f, C(0.44f, 0.30f, 0.26f)),
                     (20f, C(0.17f, 0.15f, 0.36f)), (24f, C(0.16f, 0.14f, 0.34f)));
        }

        public static Gradient DefaultAmbientGround()
        {
            return G((0f, C(0.08f, 0.07f, 0.17f)), (4.8f, C(0.09f, 0.08f, 0.17f)), (6.2f, C(0.22f, 0.17f, 0.15f)),
                     (9f, C(0.24f, 0.22f, 0.14f)), (15.5f, C(0.25f, 0.22f, 0.14f)), (18.4f, C(0.26f, 0.17f, 0.12f)),
                     (20f, C(0.09f, 0.08f, 0.18f)), (24f, C(0.08f, 0.07f, 0.17f)));
        }

        public static Gradient DefaultFog()
        {
            return G((0f, C(0.12f, 0.10f, 0.27f)), (4.8f, C(0.14f, 0.11f, 0.28f)), (6.2f, C(0.70f, 0.56f, 0.56f)),
                     (9f, C(0.80f, 0.74f, 0.56f)), (15.5f, C(0.80f, 0.72f, 0.52f)), (18.4f, C(0.86f, 0.54f, 0.38f)),
                     (20f, C(0.16f, 0.12f, 0.30f)), (24f, C(0.12f, 0.10f, 0.27f)));
        }

        public static AnimationCurve DefaultShaftIntensity()
        {
            return K((5.4f, 0f), (6.4f, 0.9f), (8.0f, 1.0f), (11.0f, 0.55f), (13.5f, 0.5f), (16.0f, 0.75f), (18.0f, 1.0f), (19.2f, 0f));
        }

        public static Gradient DefaultShaftColor()
        {
            return G((5.5f, C(1.00f, 0.70f, 0.52f)), (8.0f, C(1.00f, 0.88f, 0.62f)), (12.5f, C(1.00f, 0.94f, 0.78f)),
                     (16.0f, C(1.00f, 0.86f, 0.58f)), (18.4f, C(1.00f, 0.62f, 0.34f)), (19.3f, C(0.95f, 0.45f, 0.35f)));
        }

        public static Gradient DefaultPostTint()
        {
            return G((0f, C(0.74f, 0.70f, 1.00f)), (4.8f, C(0.76f, 0.72f, 1.00f)), (6.3f, C(1.00f, 0.90f, 0.90f)),
                     (9f, C(1.00f, 0.97f, 0.90f)), (15.5f, C(1.00f, 0.96f, 0.88f)), (18.4f, C(1.00f, 0.86f, 0.76f)),
                     (20f, C(0.76f, 0.72f, 1.00f)), (24f, C(0.74f, 0.70f, 1.00f)));
        }

        public static AnimationCurve DefaultPostExposure()
        {
            return K((0f, 0.1f), (5.0f, 0.1f), (7.0f, 0f), (18.5f, 0f), (20.5f, 0.1f), (24f, 0.1f));
        }

        public static AnimationCurve DefaultPostSaturation()
        {
            return K((0f, -8f), (5.0f, -8f), (7.0f, 10f), (17.0f, 10f), (18.6f, 15f), (20.5f, -8f), (24f, -8f));
        }

        public static AnimationCurve DefaultPostVignette()
        {
            return K((0f, 0.42f), (5.0f, 0.42f), (7.0f, 0.3f), (17.5f, 0.3f), (18.8f, 0.36f), (20.5f, 0.42f), (24f, 0.42f));
        }

        public static Gradient DefaultVignetteColor()
        {
            return G((0f, C(0.06f, 0.02f, 0.14f)), (5.0f, C(0.06f, 0.02f, 0.14f)), (7.0f, C(0.10f, 0.06f, 0.02f)),
                     (18.0f, C(0.12f, 0.04f, 0.02f)), (20.5f, C(0.06f, 0.02f, 0.14f)), (24f, C(0.06f, 0.02f, 0.14f)));
        }
    }
}
