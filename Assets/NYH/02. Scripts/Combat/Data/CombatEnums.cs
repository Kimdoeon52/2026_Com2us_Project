using UnityEngine;

/// <summary>
/// 전투 시스템 전역에서 쓰는 enum 모음.
/// CLAUDE.md §1, §5 참조 — 이 enum들은 데이터(SO)와 런타임 상태가 공통으로 참조한다.
/// 한 파일에 여러 enum을 몰아둔 이유: 이것들은 전부 "전투 시스템 전체가 공유하는 어휘"라서
/// ActionData, ActionState, HitDetection 등 여러 파일에서 동시에 참조한다. 각자 자기 파일에
/// 하나씩 흩어두면 어디서 정의됐는지 찾기 번거롭고, 특정 enum을 참조하는 파일들이
/// "이 enum이 정의된 파일"에 의존성을 갖게 되어 파일 간 결합이 불필요하게 늘어난다.
/// </summary>

/// <summary>
/// 행동의 진행 상태. 캐릭터는 항상 정확히 하나의 상태에만 있다.
/// bool 플래그(isAttacking, isGuarding ...)를 늘리는 방식은 금지 — 조합 폭발로 반드시 깨진다.
/// </summary>
public enum ActionPhase
{
    Idle,     // 대기 — 아무 행동도 안 하는 중. 이동 가능(CanMove), 새 행동 입력 가능(CanAcceptNewAction)한 유일한 상태
    Startup,  // 선딜 — 판정 없는 준비 동작. ActionData.StartupFrames만큼 지속
    Active,   // 활성 — 실제로 Hit 판정이 켜지는 구간. ActionData.ActiveFrames만큼 지속
    Recovery, // 후딜 — 행동을 회수하는 구간. 이 동안 재입력은 무시된다(§11-4)
    // Stagger(경직)는 2026-09-29 경직 시스템 전체 삭제로 제거됨 — CLAUDE_1.md §7·§15
    Down,     // 다운 — 다운 유발 기술(CausesKnockdown)에 맞아 쓰러진 상태 (아직 진입 코드 없음)
    Dead      // 사망 — 코어 HP 0. ActionState.Kill()로만 진입하고 되돌아오지 않는다. 판정·이동·입력 전부 정지
}

/// <summary>
/// 로봇의 부위. 허트박스 태그 / 부위 파괴 / 내구도 감소 대상 판정에 공통으로 쓰인다.
/// KKH의 PartMasterData.slotType도 이 enum을 그대로 참조한다 — NYH가 값을 바꾸면 KKH 쪽도 깨진다(§14)
/// </summary>
public enum BodyPart
{
    // 2026-09-29 기준: 내구도 감소는 "공격자(보스) 스킬이 지정한 부위"가 기준이다 (CLAUDE_1.md §5·§15).
    // 예전의 "가드=팔 / 위닝=다리 / 치명타=머리"(방어자 대응 기준) 매핑은 폐기됐다.
    Head,     // 치명타 판정 기준. 이 부위 Hurt박스에 맞아야만 크리티컬로 침
    LeftArm,  // 왼팔 액티브(Q) 슬롯. 내구도 0이면 Q 사용 불가
    RightArm, // 오른팔 액티브(W) 슬롯. 내구도 0이면 W 사용 불가
    LeftLeg,  // 왼다리 액티브(E) 슬롯. 내구도 0이면 E 사용 불가
    RightLeg, // 오른다리 액티브(R) 슬롯. 내구도 0이면 R 사용 불가
    Core      // 팔다리·머리가 아닌 "본체" — 부위 지정이 없는 일반 피격은 여기 HP만 깎인다
}

/// <summary>
/// 이 행동이 어디서 나오는가 — 부위 파괴 시 사라지는지 여부를 가른다. (CLAUDE.md §5 "출처 구분")
/// 코어 고정: D(기본공격), 회피, 이동 — 부위가 파괴돼도 사라지지 않는다.
/// 파츠: 왼팔/오른팔/왼다리/오른다리 액티브(Q/W/E/R) — 해당 부위 파괴 시 사용 불가.
/// (2026-09-29: 잽/훅/스트레이트/어퍼컷/백스핀 엘보우 폐기, CLAUDE_1.md §15)
/// </summary>
public enum ActionSource
{
    CoreFixed, // 양팔이 다 파괴돼도 이 행동은 계속 나가야 한다 — 안 그러면 공격 수단이 0이 되어 전투 자체가 성립하지 않음
    Part       // ActionData.RequiredPart로 지정된 부위가 파괴되면 이 행동은 CombatDataHub.CanExecuteAction에서 차단된다
}

/// <summary>
/// 판정 상자의 종류 (CLAUDE.md §4).
/// Hit=공격이 닿는 범위 / Hurt=맞는 범위 / Push=몸끼리 겹쳐 지나가지 못하게 미는 상자.
/// </summary>
public enum BoxType
{
    Hit,  // 활성 구간당 1개가 보통 — 그 프레임 동안만 존재하는 공격의 "칼날" 부분
    Hurt, // 도트 1장당 1세트 — 팔을 뻗으면 맞는 범위도 같이 늘어나야 하므로 그림이 바뀔 때마다 새로 등록
    Push  // 액션당 1개 — 몸통 크기는 기술 내내 거의 안 바뀌므로 하나만 두면 충분
}

/// <summary>
/// AI가 기술을 고를 때 "상대가 어디 있을 때만 쓸지" 제한 (EnemyAIProfile.SkillEntry).
/// </summary>
public enum AITargetCondition
{
    Any,      // 상대 상태와 무관
    Grounded, // 상대가 땅에 있을 때만
    Airborne  // 상대가 공중에 있을 때만
}

/// <summary>
/// FrameBox가 "어느 구간에 붙는 박스인가"를 가리키는 기준 (CLAUDE.md §3 3구간).
/// 절대 프레임 숫자 대신 이 값을 쓰면, 선딜·활성·후딜 프레임이 바뀌어도 박스가 알아서 따라온다 —
/// 프레임 값과 박스 창이 따로 놀다 조용히 어긋나는 문제를 구조적으로 막기 위한 것이다.
/// </summary>
public enum FrameAnchor
{
    Legacy,      // 앵커 이전 방식 — startFrame/endFrame 절대값을 그대로 쓴다. 변환 전 기존 에셋이 여기 해당
    WholeAction, // 행동 전체 (선딜~후딜). 몸통 허트박스·푸시박스처럼 내내 존재하는 박스용
    Startup,     // 선딜 구간 기준
    Active,      // 활성 구간 기준 — 히트박스의 기본 자리
    Recovery     // 후딜 구간 기준
}
