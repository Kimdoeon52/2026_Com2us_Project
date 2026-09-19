using UnityEngine;

/// <summary>
/// 전투 시스템 전역에서 쓰는 enum 모음.
/// CLAUDE.md §1, §5 참조 — 이 enum들은 데이터(SO)와 런타임 상태가 공통으로 참조한다.
/// </summary>

/// <summary>
/// 행동의 진행 상태. 캐릭터는 항상 정확히 하나의 상태에만 있다.
/// bool 플래그(isAttacking, isGuarding ...)를 늘리는 방식은 금지 — 조합 폭발로 반드시 깨진다.
/// </summary>
public enum ActionPhase
{
    Idle, //대기
    Startup, //선딜
    Active, //활성 프레임
    Recovery, //후딜
    Stagger,
    Down, //다운
    Dead //사망
}

/// <summary>
/// 로봇의 부위. 허트박스 태그 / 부위 파괴 / 내구도 감소 대상 판정에 공통으로 쓰인다.
/// </summary>
public enum BodyPart
{
    Head,    
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
    Core
}

/// <summary>
/// 이 행동이 어디서 나오는가 — 부위 파괴 시 사라지는지 여부를 가른다. (CLAUDE.md §5 "출처 구분")
/// 코어 고정: 잽, 가드, 위닝, 이동 — 부위가 파괴돼도 사라지지 않는다. 성능만 하향.
/// 파츠: 훅, 스트레이트, 어퍼컷, 백스핀, 스킬 5~8 — 해당 부위 파괴 시 사용 불가.
/// </summary>
public enum ActionSource
{
    CoreFixed,
    Part
}
