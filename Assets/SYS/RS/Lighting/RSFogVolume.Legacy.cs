// RE:AL STEEL - RSFogVolume 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSFogVolume : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] Color color = new Color(float.NaN, 0f, 0f, 0f);
        [SerializeField, HideInInspector, Obsolete] bool followTimeOfDay;
        [SerializeField, HideInInspector, Obsolete] float density = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float heightFalloff = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float noiseScale = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float noiseStrength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Vector3 wind = new Vector3(float.NaN, 0f, 0f);
        [SerializeField, HideInInspector, Obsolete] float sunStrength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float shadowStrength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float anisotropy = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float ambientStrength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] bool pointLights;
        [SerializeField, HideInInspector, Obsolete] float pointStrength = float.NaN;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!float.IsNaN(color.r) || !float.IsNaN(density) || !float.IsNaN(heightFalloff) || !float.IsNaN(noiseScale) || !float.IsNaN(noiseStrength) || !float.IsNaN(wind.x) || !float.IsNaN(sunStrength) || !float.IsNaN(shadowStrength) || !float.IsNaN(anisotropy) || !float.IsNaN(ambientStrength) || !float.IsNaN(pointStrength)) legacyPending = true;
        }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (!float.IsNaN(color.r)) look.color = color;
            look.followTimeOfDay = followTimeOfDay;
            if (!float.IsNaN(density)) look.density = density;
            if (!float.IsNaN(heightFalloff)) look.heightFalloff = heightFalloff;
            if (!float.IsNaN(noiseScale)) look.noiseScale = noiseScale;
            if (!float.IsNaN(noiseStrength)) look.noiseStrength = noiseStrength;
            if (!float.IsNaN(wind.x)) look.wind = wind;
            if (!float.IsNaN(sunStrength)) look.sunStrength = sunStrength;
            if (!float.IsNaN(shadowStrength)) look.shadowStrength = shadowStrength;
            if (!float.IsNaN(anisotropy)) look.anisotropy = anisotropy;
            if (!float.IsNaN(ambientStrength)) look.ambientStrength = ambientStrength;
            look.pointLights = pointLights;
            if (!float.IsNaN(pointStrength)) look.pointStrength = pointStrength;
            color = new Color(float.NaN, 0f, 0f, 0f);
            density = float.NaN;
            heightFalloff = float.NaN;
            noiseScale = float.NaN;
            noiseStrength = float.NaN;
            wind = new Vector3(float.NaN, 0f, 0f);
            sunStrength = float.NaN;
            shadowStrength = float.NaN;
            anisotropy = float.NaN;
            ambientStrength = float.NaN;
            pointStrength = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSFogVolume 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
