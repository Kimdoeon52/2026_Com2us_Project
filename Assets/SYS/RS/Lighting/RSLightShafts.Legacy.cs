// RE:AL STEEL - RSLightShafts 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSLightShafts : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] int count = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] Vector2 length = new Vector2(float.NaN, 0f);
        [SerializeField, HideInInspector, Obsolete] Vector2 width = new Vector2(float.NaN, 0f);
        [SerializeField, HideInInspector, Obsolete] float minSteepness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float intensity = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float intensityVariation = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Color tint = new Color(float.NaN, 0f, 0f, 0f);
        [SerializeField, HideInInspector, Obsolete] float edgeSoftness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float topFade = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float bottomFade = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float noiseAmount = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float noiseScale = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float noiseSpeed = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float nearFade = float.NaN;
        [SerializeField, HideInInspector, Obsolete] int steps = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] int motesPerShaft = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] Vector2 moteSize = new Vector2(float.NaN, 0f);
        [SerializeField, HideInInspector, Obsolete] float moteBrightness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float moteDrift = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Vector2 lifetime = new Vector2(float.NaN, 0f);
        [SerializeField, HideInInspector, Obsolete] float fadeTime = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float drift = float.NaN;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (count != int.MinValue || !float.IsNaN(length.x) || !float.IsNaN(width.x) || !float.IsNaN(minSteepness) || !float.IsNaN(intensity) || !float.IsNaN(intensityVariation) || !float.IsNaN(tint.r) || !float.IsNaN(edgeSoftness) || !float.IsNaN(topFade) || !float.IsNaN(bottomFade) || !float.IsNaN(noiseAmount) || !float.IsNaN(noiseScale) || !float.IsNaN(noiseSpeed) || !float.IsNaN(nearFade) || steps != int.MinValue || motesPerShaft != int.MinValue || !float.IsNaN(moteSize.x) || !float.IsNaN(moteBrightness) || !float.IsNaN(moteDrift) || !float.IsNaN(lifetime.x) || !float.IsNaN(fadeTime) || !float.IsNaN(drift)) legacyPending = true;
        }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (count != int.MinValue) look.count = count;
            if (!float.IsNaN(length.x)) look.length = length;
            if (!float.IsNaN(width.x)) look.width = width;
            if (!float.IsNaN(minSteepness)) look.minSteepness = minSteepness;
            if (!float.IsNaN(intensity)) look.intensity = intensity;
            if (!float.IsNaN(intensityVariation)) look.intensityVariation = intensityVariation;
            if (!float.IsNaN(tint.r)) look.tint = tint;
            if (!float.IsNaN(edgeSoftness)) look.edgeSoftness = edgeSoftness;
            if (!float.IsNaN(topFade)) look.topFade = topFade;
            if (!float.IsNaN(bottomFade)) look.bottomFade = bottomFade;
            if (!float.IsNaN(noiseAmount)) look.noiseAmount = noiseAmount;
            if (!float.IsNaN(noiseScale)) look.noiseScale = noiseScale;
            if (!float.IsNaN(noiseSpeed)) look.noiseSpeed = noiseSpeed;
            if (!float.IsNaN(nearFade)) look.nearFade = nearFade;
            if (steps != int.MinValue) look.steps = steps;
            if (motesPerShaft != int.MinValue) look.motesPerShaft = motesPerShaft;
            if (!float.IsNaN(moteSize.x)) look.moteSize = moteSize;
            if (!float.IsNaN(moteBrightness)) look.moteBrightness = moteBrightness;
            if (!float.IsNaN(moteDrift)) look.moteDrift = moteDrift;
            if (!float.IsNaN(lifetime.x)) look.lifetime = lifetime;
            if (!float.IsNaN(fadeTime)) look.fadeTime = fadeTime;
            if (!float.IsNaN(drift)) look.drift = drift;
            count = int.MinValue;
            length = new Vector2(float.NaN, 0f);
            width = new Vector2(float.NaN, 0f);
            minSteepness = float.NaN;
            intensity = float.NaN;
            intensityVariation = float.NaN;
            tint = new Color(float.NaN, 0f, 0f, 0f);
            edgeSoftness = float.NaN;
            topFade = float.NaN;
            bottomFade = float.NaN;
            noiseAmount = float.NaN;
            noiseScale = float.NaN;
            noiseSpeed = float.NaN;
            nearFade = float.NaN;
            steps = int.MinValue;
            motesPerShaft = int.MinValue;
            moteSize = new Vector2(float.NaN, 0f);
            moteBrightness = float.NaN;
            moteDrift = float.NaN;
            lifetime = new Vector2(float.NaN, 0f);
            fadeTime = float.NaN;
            drift = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSLightShafts 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
