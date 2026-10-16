// RE:AL STEEL - RSCloudShadow 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSCloudShadow : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] float strength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float coverage = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float softness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] int bands = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] int detail = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] int blobs = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] int seed = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] float dappleStrength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float dappleCoverage = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float dappleSoftness = float.NaN;
        [SerializeField, HideInInspector, Obsolete] int dappleSize = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] int dappleDetail = int.MinValue;
        [SerializeField, HideInInspector, Obsolete] float cloudSize = float.NaN;
        [SerializeField, HideInInspector, Obsolete] Vector2 wind = new Vector2(float.NaN, 0f);
        [SerializeField, HideInInspector, Obsolete] float pixelSize = float.NaN;
        [SerializeField, HideInInspector, Obsolete] bool pixelated;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!float.IsNaN(strength) || !float.IsNaN(coverage) || !float.IsNaN(softness) || bands != int.MinValue || detail != int.MinValue || blobs != int.MinValue || seed != int.MinValue || !float.IsNaN(dappleStrength) || !float.IsNaN(dappleCoverage) || !float.IsNaN(dappleSoftness) || dappleSize != int.MinValue || dappleDetail != int.MinValue || !float.IsNaN(cloudSize) || !float.IsNaN(wind.x) || !float.IsNaN(pixelSize)) legacyPending = true;
        }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (!float.IsNaN(strength)) look.strength = strength;
            if (!float.IsNaN(coverage)) look.coverage = coverage;
            if (!float.IsNaN(softness)) look.softness = softness;
            if (bands != int.MinValue) look.bands = bands;
            if (detail != int.MinValue) look.detail = detail;
            if (blobs != int.MinValue) look.blobs = blobs;
            if (seed != int.MinValue) look.seed = seed;
            if (!float.IsNaN(dappleStrength)) look.dappleStrength = dappleStrength;
            if (!float.IsNaN(dappleCoverage)) look.dappleCoverage = dappleCoverage;
            if (!float.IsNaN(dappleSoftness)) look.dappleSoftness = dappleSoftness;
            if (dappleSize != int.MinValue) look.dappleSize = dappleSize;
            if (dappleDetail != int.MinValue) look.dappleDetail = dappleDetail;
            if (!float.IsNaN(cloudSize)) look.cloudSize = cloudSize;
            if (!float.IsNaN(wind.x)) look.wind = wind;
            if (!float.IsNaN(pixelSize)) look.pixelSize = pixelSize;
            look.pixelated = pixelated;
            strength = float.NaN;
            coverage = float.NaN;
            softness = float.NaN;
            bands = int.MinValue;
            detail = int.MinValue;
            blobs = int.MinValue;
            seed = int.MinValue;
            dappleStrength = float.NaN;
            dappleCoverage = float.NaN;
            dappleSoftness = float.NaN;
            dappleSize = int.MinValue;
            dappleDetail = int.MinValue;
            cloudSize = float.NaN;
            wind = new Vector2(float.NaN, 0f);
            pixelSize = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSCloudShadow 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
