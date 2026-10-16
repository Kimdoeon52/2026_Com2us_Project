// RE:AL STEEL - 스테이지 룩 프로필 (에셋)
//
// 한 스테이지의 '룩' 값을 에셋 하나에 모은다: 시간대 색 · 해 · 달, 구름 그림자, 햇살, 안개, 젖은 바닥, 간접광, 색감, 캐릭터 밤 빛.
// 씬에는 '스테이지 룩' 컴포넌트(RSStageLookBinding)가 이 에셋을 가리키기만 한다.
//
//  · 다른 스테이지에 같은 룩: 에셋을 그대로 끼운다 (폐철장_밤 → 항구 스테이지)
//  · 비슷한 룩: 에셋 복제 후 몇 칸만 바꾼다
//  · 여러 사람이 동시에: 씬 대신 에셋이 바뀌므로 씬 머지 충돌이 줄어든다
//  · 프로필이 없으면 각 컴포넌트가 자기 값(예전 방식)을 쓴다
//
// 씬에만 의미 있는 것(어느 라이트가 해인지, 밤에 켤 오브젝트, 지형 범위, 지금 시각)은 프로필에 넣지 않는다.
using System;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [CreateAssetMenu(menuName = "RE_AL STEEL/스테이지 룩 프로필", fileName = "RS_StageLook", order = 10)]
    public class RSStageLook : ScriptableObject
    {
        [TextArea(2, 4), Tooltip("이 룩 설명 (어느 스테이지 · 시간대 · 날씨용인지)")]
        public string note = "";

        [RSLook] public RSTimeOfDay.Look timeOfDay = new RSTimeOfDay.Look();
        [RSLook] public RSColorGrade.Look colorGrade = new RSColorGrade.Look();
        [RSLook] public RSCloudShadow.Look clouds = new RSCloudShadow.Look();
        [RSLook] public RSLightShafts.Look shafts = new RSLightShafts.Look();
        [RSLook] public RSFogVolume.Look fog = new RSFogVolume.Look();
        [RSLook] public RSWetness.Look wetness = new RSWetness.Look();
        [RSLook] public RSIndirectLight.Look indirect = new RSIndirectLight.Look();
        [RSLook] public RSCharacterGlow.Look characterGlow = new RSCharacterGlow.Look();
        [RSLook] public RSGodRays.Look godRays = new RSGodRays.Look();

        // ─────────────────────────────────────────────────────────────

        /// <summary>지금 씬에 연결된 프로필 (스테이지 룩 컴포넌트가 정한다). 없으면 null</summary>
        public static RSStageLook Current { get; private set; }

        /// <summary>프로필이 바뀌거나(다른 에셋으로) 값이 바뀌면</summary>
        public static event Action Changed;

        static int revision;
        /// <summary>바뀔 때마다 1 씩 오르는 번호 (매 프레임 비교용)</summary>
        public static int Revision { get { return revision; } }

        internal static void SetCurrent(RSStageLook p)
        {
            if (Current == p) return;
            Current = p;
            Raise();
        }

        static void Raise()
        {
            revision++;
            var h = Changed;
            if (h != null) h();
        }

#if UNITY_EDITOR
        [NonSerialized] bool raisePending;
#endif

        void OnValidate()
        {
            if (this != Current) return;
#if UNITY_EDITOR
            // OnValidate 안에서 라이트 · 다른 오브젝트를 바꾸면 경고가 뜨므로 한 박자 뒤에 알린다
            if (raisePending) return;
            raisePending = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                raisePending = false;
                if (this == Current) { Raise(); UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
            };
#else
            Raise();
#endif
        }

        /// <summary>값을 코드로 바꾼 뒤 부른다 (에디터 도구 · 연출 스크립트)</summary>
        public void NotifyChanged()
        {
            if (this == Current) Raise();
        }
    }
}
