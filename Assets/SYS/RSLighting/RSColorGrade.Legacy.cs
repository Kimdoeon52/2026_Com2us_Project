// RE:AL STEEL - RSColorGrade 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSColorGrade : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] RSColorGradeProfile profile;
        [SerializeField, HideInInspector, Obsolete] float strength = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float shadowsStart = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float shadowsEnd = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float highlightsStart = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float highlightsEnd = float.NaN;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!float.IsNaN(strength) || !float.IsNaN(shadowsStart) || !float.IsNaN(shadowsEnd) || !float.IsNaN(highlightsStart) || !float.IsNaN(highlightsEnd)) legacyPending = true;
        }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (profile != null) look.profile = profile;
            if (!float.IsNaN(strength)) look.strength = strength;
            if (!float.IsNaN(shadowsStart)) look.shadowsStart = shadowsStart;
            if (!float.IsNaN(shadowsEnd)) look.shadowsEnd = shadowsEnd;
            if (!float.IsNaN(highlightsStart)) look.highlightsStart = highlightsStart;
            if (!float.IsNaN(highlightsEnd)) look.highlightsEnd = highlightsEnd;
            profile = null;
            strength = float.NaN;
            shadowsStart = float.NaN;
            shadowsEnd = float.NaN;
            highlightsStart = float.NaN;
            highlightsEnd = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSColorGrade 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
