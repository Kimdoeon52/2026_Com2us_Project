// RE:AL STEEL - RSTimeOfDay 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSTimeOfDay : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] float azimuth = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float swing = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float maxElevation = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float minElevation = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float sunriseHour = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float sunsetHour = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Gradient sunColor;
        [SerializeField, HideInInspector, Obsolete] AnimationCurve sunIntensity;
        [SerializeField, HideInInspector, Obsolete] float dayShadowStrength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Color moonColor = new Color(float.NaN, 0f, 0f, 0f);
        [SerializeField, HideInInspector, Obsolete] float moonIntensity = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float moonElevation = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float nightShadowStrength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] bool driveAmbient;
        [SerializeField, HideInInspector, Obsolete] float ambientIntensity = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Gradient ambientSky;
        [SerializeField, HideInInspector, Obsolete] Gradient ambientEquator;
        [SerializeField, HideInInspector, Obsolete] Gradient ambientGround;
        [SerializeField, HideInInspector, Obsolete] bool driveFog;
        [SerializeField, HideInInspector, Obsolete] Gradient fogColor;
        [SerializeField, HideInInspector, Obsolete] AnimationCurve shaftIntensity;
        [SerializeField, HideInInspector, Obsolete] Gradient shaftColor;
        [SerializeField, HideInInspector, Obsolete] bool drivePost;
        [SerializeField, HideInInspector, Obsolete] float postPriority = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Gradient postTint;
        [SerializeField, HideInInspector, Obsolete] AnimationCurve postExposure;
        [SerializeField, HideInInspector, Obsolete] AnimationCurve postSaturation;
        [SerializeField, HideInInspector, Obsolete] AnimationCurve postVignette;
        [SerializeField, HideInInspector, Obsolete] Gradient vignetteColor;
        [SerializeField, HideInInspector, Obsolete] float fillDay = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float fillNight = float.NaN;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!float.IsNaN(azimuth) || !float.IsNaN(swing) || !float.IsNaN(maxElevation) || !float.IsNaN(minElevation) || !float.IsNaN(sunriseHour) || !float.IsNaN(sunsetHour) || !float.IsNaN(dayShadowStrength) || !float.IsNaN(moonColor.r) || !float.IsNaN(moonIntensity) || !float.IsNaN(moonElevation) || !float.IsNaN(nightShadowStrength) || !float.IsNaN(ambientIntensity) || !float.IsNaN(postPriority) || !float.IsNaN(fillDay) || !float.IsNaN(fillNight)) legacyPending = true;
        }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (!float.IsNaN(azimuth)) look.azimuth = azimuth;
            if (!float.IsNaN(swing)) look.swing = swing;
            if (!float.IsNaN(maxElevation)) look.maxElevation = maxElevation;
            if (!float.IsNaN(minElevation)) look.minElevation = minElevation;
            if (!float.IsNaN(sunriseHour)) look.sunriseHour = sunriseHour;
            if (!float.IsNaN(sunsetHour)) look.sunsetHour = sunsetHour;
            if (RSLegacyUtil.IsSet(sunColor)) look.sunColor = sunColor;
            if (sunIntensity != null && sunIntensity.length > 0) look.sunIntensity = sunIntensity;
            if (!float.IsNaN(dayShadowStrength)) look.dayShadowStrength = dayShadowStrength;
            if (!float.IsNaN(moonColor.r)) look.moonColor = moonColor;
            if (!float.IsNaN(moonIntensity)) look.moonIntensity = moonIntensity;
            if (!float.IsNaN(moonElevation)) look.moonElevation = moonElevation;
            if (!float.IsNaN(nightShadowStrength)) look.nightShadowStrength = nightShadowStrength;
            look.driveAmbient = driveAmbient;
            if (!float.IsNaN(ambientIntensity)) look.ambientIntensity = ambientIntensity;
            if (RSLegacyUtil.IsSet(ambientSky)) look.ambientSky = ambientSky;
            if (RSLegacyUtil.IsSet(ambientEquator)) look.ambientEquator = ambientEquator;
            if (RSLegacyUtil.IsSet(ambientGround)) look.ambientGround = ambientGround;
            look.driveFog = driveFog;
            if (RSLegacyUtil.IsSet(fogColor)) look.fogColor = fogColor;
            if (shaftIntensity != null && shaftIntensity.length > 0) look.shaftIntensity = shaftIntensity;
            if (RSLegacyUtil.IsSet(shaftColor)) look.shaftColor = shaftColor;
            look.drivePost = drivePost;
            if (!float.IsNaN(postPriority)) look.postPriority = postPriority;
            if (RSLegacyUtil.IsSet(postTint)) look.postTint = postTint;
            if (postExposure != null && postExposure.length > 0) look.postExposure = postExposure;
            if (postSaturation != null && postSaturation.length > 0) look.postSaturation = postSaturation;
            if (postVignette != null && postVignette.length > 0) look.postVignette = postVignette;
            if (RSLegacyUtil.IsSet(vignetteColor)) look.vignetteColor = vignetteColor;
            if (!float.IsNaN(fillDay)) look.fillDay = fillDay;
            if (!float.IsNaN(fillNight)) look.fillNight = fillNight;
            azimuth = float.NaN;
            swing = float.NaN;
            maxElevation = float.NaN;
            minElevation = float.NaN;
            sunriseHour = float.NaN;
            sunsetHour = float.NaN;
            sunColor = null;
            sunIntensity = null;
            dayShadowStrength = float.NaN;
            moonColor = new Color(float.NaN, 0f, 0f, 0f);
            moonIntensity = float.NaN;
            moonElevation = float.NaN;
            nightShadowStrength = float.NaN;
            ambientIntensity = float.NaN;
            ambientSky = null;
            ambientEquator = null;
            ambientGround = null;
            fogColor = null;
            shaftIntensity = null;
            shaftColor = null;
            postPriority = float.NaN;
            postTint = null;
            postExposure = null;
            postSaturation = null;
            postVignette = null;
            vignetteColor = null;
            fillDay = float.NaN;
            fillNight = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSTimeOfDay 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
