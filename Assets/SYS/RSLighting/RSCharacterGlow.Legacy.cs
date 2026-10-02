// RE:AL STEEL - RSCharacterGlow 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSCharacterGlow : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] Color color = new Color(float.NaN, 0f, 0f, 0f);
        [SerializeField, HideInInspector, Obsolete] float nightIntensity = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float dayIntensity = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float range = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float height = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float towardCamera = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float flicker = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float flickerSpeed = float.NaN;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!float.IsNaN(color.r) || !float.IsNaN(nightIntensity) || !float.IsNaN(dayIntensity) || !float.IsNaN(range) || !float.IsNaN(height) || !float.IsNaN(towardCamera) || !float.IsNaN(flicker) || !float.IsNaN(flickerSpeed)) legacyPending = true;
        }

        void OnValidate() { MigrateLegacy(); }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (!float.IsNaN(color.r)) look.color = color;
            if (!float.IsNaN(nightIntensity)) look.nightIntensity = nightIntensity;
            if (!float.IsNaN(dayIntensity)) look.dayIntensity = dayIntensity;
            if (!float.IsNaN(range)) look.range = range;
            if (!float.IsNaN(height)) look.height = height;
            if (!float.IsNaN(towardCamera)) look.towardCamera = towardCamera;
            if (!float.IsNaN(flicker)) look.flicker = flicker;
            if (!float.IsNaN(flickerSpeed)) look.flickerSpeed = flickerSpeed;
            color = new Color(float.NaN, 0f, 0f, 0f);
            nightIntensity = float.NaN;
            dayIntensity = float.NaN;
            range = float.NaN;
            height = float.NaN;
            towardCamera = float.NaN;
            flicker = float.NaN;
            flickerSpeed = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSCharacterGlow 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
