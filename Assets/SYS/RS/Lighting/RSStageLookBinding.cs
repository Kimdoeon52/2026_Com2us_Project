// RE:AL STEEL - 스테이지 룩 (씬에 하나)
//
// 이 씬이 쓸 '스테이지 룩 프로필' 에셋을 정한다. 시간대 · 구름 그림자 · 햇살 · 안개 · 젖은 바닥 · 간접광 · 색감 · 캐릭터 밤 빛이
// 모두 이 프로필 값을 쓴다 (각 컴포넌트 인스펙터에서 고쳐도 프로필 에셋에 저장된다).
// 비우면 각 컴포넌트가 자기 값을 쓴다 (예전 방식).
//
// 스크립트에서 룩 바꾸기: GetComponent<RSStageLookBinding>().profile = 다른프로필;  (바로 반영)
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("스테이지 룩", "이 씬의 룩(시간대 색 · 안개 · 젖은 바닥 · 간접광 · 색감 …)을 담은 프로필 에셋을 끼운다.\n" +
        "· 다른 스테이지와 같은 룩 = 같은 에셋 끼우기 / 비슷한 룩 = 에셋 복제 후 수정\n" +
        "· 각 컴포넌트 인스펙터에서 고쳐도 값은 에셋에 저장된다 (씬은 안 바뀜 → 씬 충돌 줄어듦)\n" +
        "· 비우면 각 컴포넌트가 자기 값을 쓴다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [DefaultExecutionOrder(-300)]
    [AddComponentMenu("RE_AL STEEL/Lighting/스테이지 룩")]
    public class RSStageLookBinding : MonoBehaviour
    {
        [RSKey, Tooltip("이 씬이 쓸 스테이지 룩 프로필. 비우면 각 컴포넌트 값")]
        public RSStageLook profile;

        public static RSStageLookBinding Active { get; private set; }

        RSStageLook applied;

        void OnEnable()
        {
            if (Active != null && Active != this)
                Debug.LogWarning("[스테이지 룩] 씬에 '스테이지 룩' 이 둘 이상입니다. 마지막에 켜진 것을 씁니다: " + name, this);
            Active = this;
            Push();
        }

        void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
                RSStageLook.SetCurrent(null);
            }
            applied = null;
        }

        void OnValidate() { if (isActiveAndEnabled && Active == this) Push(); }

        void Update()
        {
            if (Active == this && applied != profile) Push();
        }

        void Push()
        {
            applied = profile;
            RSStageLook.SetCurrent(profile);
        }
    }
}
