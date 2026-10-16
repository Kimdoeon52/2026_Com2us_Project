// RE:AL STEEL - RSWetness 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSWetness : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] float rain = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float darken = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float reflection = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float reflectionMin = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Color reflectionTint = new Color(float.NaN, 0f, 0f, 0f);
        [SerializeField, HideInInspector, Obsolete] float highlight = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float highlightSharpness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float roughness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float reflectionStretch = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float surfaceBump = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float puddleInCracks = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float environment = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float puddlePatches = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float puddleScale = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float reflectionContrast = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float reflectionCover = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float edgeBreakup = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float edgeScale = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float followSlope = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float lightReflection = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float lightStreak = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float lightStreakSharpness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float ripple = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float rippleScale = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float rippleSpeed = float.NaN;
        [SerializeField, HideInInspector, Obsolete] int steps = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] float maxDistance = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float thickness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float pixelsPerUnit = float.NaN;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!float.IsNaN(rain) || !float.IsNaN(darken) || !float.IsNaN(reflection) || !float.IsNaN(reflectionMin) || !float.IsNaN(reflectionTint.r) || !float.IsNaN(highlight) || !float.IsNaN(highlightSharpness) || !float.IsNaN(roughness) || !float.IsNaN(reflectionStretch) || !float.IsNaN(surfaceBump) || !float.IsNaN(puddleInCracks) || !float.IsNaN(environment) || !float.IsNaN(puddlePatches) || !float.IsNaN(puddleScale) || !float.IsNaN(reflectionContrast) || !float.IsNaN(reflectionCover) || !float.IsNaN(edgeBreakup) || !float.IsNaN(edgeScale) || !float.IsNaN(followSlope) || !float.IsNaN(lightReflection) || !float.IsNaN(lightStreak) || !float.IsNaN(lightStreakSharpness) || !float.IsNaN(ripple) || !float.IsNaN(rippleScale) || !float.IsNaN(rippleSpeed) || steps != int.MinValue || !float.IsNaN(maxDistance) || !float.IsNaN(thickness) || !float.IsNaN(pixelsPerUnit)) legacyPending = true;
        }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (!float.IsNaN(rain)) look.rain = rain;
            if (!float.IsNaN(darken)) look.darken = darken;
            if (!float.IsNaN(reflection)) look.reflection = reflection;
            if (!float.IsNaN(reflectionMin)) look.reflectionMin = reflectionMin;
            if (!float.IsNaN(reflectionTint.r)) look.reflectionTint = reflectionTint;
            if (!float.IsNaN(highlight)) look.highlight = highlight;
            if (!float.IsNaN(highlightSharpness)) look.highlightSharpness = highlightSharpness;
            if (!float.IsNaN(roughness)) look.roughness = roughness;
            if (!float.IsNaN(reflectionStretch)) look.reflectionStretch = reflectionStretch;
            if (!float.IsNaN(surfaceBump)) look.surfaceBump = surfaceBump;
            if (!float.IsNaN(puddleInCracks)) look.puddleInCracks = puddleInCracks;
            if (!float.IsNaN(environment)) look.environment = environment;
            if (!float.IsNaN(puddlePatches)) look.puddlePatches = puddlePatches;
            if (!float.IsNaN(puddleScale)) look.puddleScale = puddleScale;
            if (!float.IsNaN(reflectionContrast)) look.reflectionContrast = reflectionContrast;
            if (!float.IsNaN(reflectionCover)) look.reflectionCover = reflectionCover;
            if (!float.IsNaN(edgeBreakup)) look.edgeBreakup = edgeBreakup;
            if (!float.IsNaN(edgeScale)) look.edgeScale = edgeScale;
            if (!float.IsNaN(followSlope)) look.followSlope = followSlope;
            if (!float.IsNaN(lightReflection)) look.lightReflection = lightReflection;
            if (!float.IsNaN(lightStreak)) look.lightStreak = lightStreak;
            if (!float.IsNaN(lightStreakSharpness)) look.lightStreakSharpness = lightStreakSharpness;
            if (!float.IsNaN(ripple)) look.ripple = ripple;
            if (!float.IsNaN(rippleScale)) look.rippleScale = rippleScale;
            if (!float.IsNaN(rippleSpeed)) look.rippleSpeed = rippleSpeed;
            if (steps != int.MinValue) look.steps = steps;
            if (!float.IsNaN(maxDistance)) look.maxDistance = maxDistance;
            if (!float.IsNaN(thickness)) look.thickness = thickness;
            if (!float.IsNaN(pixelsPerUnit)) look.pixelsPerUnit = pixelsPerUnit;
            rain = float.NaN;
            darken = float.NaN;
            reflection = float.NaN;
            reflectionMin = float.NaN;
            reflectionTint = new Color(float.NaN, 0f, 0f, 0f);
            highlight = float.NaN;
            highlightSharpness = float.NaN;
            roughness = float.NaN;
            reflectionStretch = float.NaN;
            surfaceBump = float.NaN;
            puddleInCracks = float.NaN;
            environment = float.NaN;
            puddlePatches = float.NaN;
            puddleScale = float.NaN;
            reflectionContrast = float.NaN;
            reflectionCover = float.NaN;
            edgeBreakup = float.NaN;
            edgeScale = float.NaN;
            followSlope = float.NaN;
            lightReflection = float.NaN;
            lightStreak = float.NaN;
            lightStreakSharpness = float.NaN;
            ripple = float.NaN;
            rippleScale = float.NaN;
            rippleSpeed = float.NaN;
            steps = int.MinValue;
            maxDistance = float.NaN;
            thickness = float.NaN;
            pixelsPerUnit = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSWetness 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
