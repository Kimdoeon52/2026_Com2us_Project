// RE:AL STEEL - RSIndirectLight 구버전 씬 값 옮기기 (2026-10-01 스테이지 룩 프로필 도입)
//
// 예전엔 룩 값이 컴포넌트 맨 위에 바로 있었다. 지금은 Look(중첩 클래스) 안에 있다.
// 예전 씬을 열면 같은 이름의 숨은 필드로 값을 받아 Look 으로 옮기고 비운다 (값이 사라지지 않게).
// 모든 씬 · 프리팹을 한 번씩 열어 저장한 뒤에는 이 파일을 지워도 된다.
#pragma warning disable 612, 618
using System;
using UnityEngine;

namespace RealSteel.Lighting
{
    public partial class RSIndirectLight : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, Obsolete] float occlusion = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float occlusionReach = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float underCover = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float bounce = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float bounceReach = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float bounceHeight = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float saturation = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float terrain = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float buildings = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float characters = float.NaN;
        [SerializeField, HideInInspector, Obsolete] float foliage = float.NaN;
        [SerializeField, HideInInspector, Obsolete] bool includeSources;
        [SerializeField, HideInInspector, Obsolete] bool includeLights;
        [SerializeField, HideInInspector, Obsolete] float lightSpill = float.NaN;

        [NonSerialized] bool legacyMoved;
        /// <summary>구버전 값을 방금 옮겼는지 (에디터가 씬을 '저장 필요' 로 표시할 때 씀)</summary>
        public bool LegacyMoved { get { return legacyMoved; } set { legacyMoved = value; } }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        [NonSerialized] bool legacyPending;

        // 불러오는 중(직렬화 콜백)에는 유니티 API 를 부를 수 없어서, 옛 값이 있다는 표시만 하고 옮기기는 OnEnable · OnValidate 에서
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!float.IsNaN(occlusion) || !float.IsNaN(occlusionReach) || !float.IsNaN(underCover) || !float.IsNaN(bounce) || !float.IsNaN(bounceReach) || !float.IsNaN(bounceHeight) || !float.IsNaN(saturation) || !float.IsNaN(terrain) || !float.IsNaN(buildings) || !float.IsNaN(characters) || !float.IsNaN(foliage) || !float.IsNaN(lightSpill)) legacyPending = true;
        }

        /// <summary>옛 값이 있으면 Look 으로 옮긴다 (OnEnable · OnValidate 맨 앞에서 부름)</summary>
        void MigrateLegacy()
        {
            if (!legacyPending) return;
            legacyPending = false;
            if (look == null) look = new Look();
            if (!float.IsNaN(occlusion)) look.occlusion = occlusion;
            if (!float.IsNaN(occlusionReach)) look.occlusionReach = occlusionReach;
            if (!float.IsNaN(underCover)) look.underCover = underCover;
            if (!float.IsNaN(bounce)) look.bounce = bounce;
            if (!float.IsNaN(bounceReach)) look.bounceReach = bounceReach;
            if (!float.IsNaN(bounceHeight)) look.bounceHeight = bounceHeight;
            if (!float.IsNaN(saturation)) look.saturation = saturation;
            if (!float.IsNaN(terrain)) look.terrain = terrain;
            if (!float.IsNaN(buildings)) look.buildings = buildings;
            if (!float.IsNaN(characters)) look.characters = characters;
            if (!float.IsNaN(foliage)) look.foliage = foliage;
            look.includeSources = includeSources;
            look.includeLights = includeLights;
            if (!float.IsNaN(lightSpill)) look.lightSpill = lightSpill;
            occlusion = float.NaN;
            occlusionReach = float.NaN;
            underCover = float.NaN;
            bounce = float.NaN;
            bounceReach = float.NaN;
            bounceHeight = float.NaN;
            saturation = float.NaN;
            terrain = float.NaN;
            buildings = float.NaN;
            characters = float.NaN;
            foliage = float.NaN;
            lightSpill = float.NaN;
            legacyMoved = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (this != null) UnityEditor.EditorUtility.SetDirty(this); };
            Debug.Log("[RE:AL STEEL] '" + name + "' 의 RSIndirectLight 예전 값을 새 구조(Look)로 옮겼습니다. 씬을 저장하세요.", this);
#endif
        }
    }
}
