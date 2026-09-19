# 전투 행동 파트(NYH) 연동 인터페이스 가이드

- **작성자**: 전투 DB & 배틀 매니저 담당 (KKH)
- **대상자**: 전투 행동 / 전투 실행 엔진 담당 (NYH)
- **제공 스크립트**:
  - [CombatDataHub.cs](file:///d:/3%ED%95%99%EB%85%84/2026_Com2us_Project/Assets/KKH/02.Scripts/CombatDataHub.cs) (데이터 허브 싱글톤)
  - [BattleManager.cs](file:///d:/3%ED%95%99%EB%85%84/2026_Com2us_Project/Assets/KKH/02.Scripts/BattleManager.cs) (전투 라이프사이클 매니저)
  - [HitResolutionResult.cs](file:///d:/3%ED%95%99%EB%85%84/2026_Com2us_Project/Assets/KKH/02.Scripts/HitResolutionResult.cs) (피격 결과 DTO)

---

## 1. 전투 시작 시 스탯 수신 (남윤호 파트 연동)

전투 시작 시 `CombatDataHub.Instance`를 통해 기획서 공식으로 계산된 종합 스탯을 바로 가져올 수 있음.

```csharp
// 1. 이동속도: (왼다리Speed + 오른다리Speed) / 2
float moveSpeed = CombatDataHub.Instance.GetFinalMoveSpeed("Player");

// 2. 총 공격력: 기본Atk + (왼팔Atk + 오른팔Atk) / 2
int totalAtk = CombatDataHub.Instance.GetTotalAttackPower("Player");

// 3. 코어 체력: 현재 체력 및 최대 체력
int hp = CombatDataHub.Instance.GetCurrentHp("Player");
int maxHp = CombatDataHub.Instance.GetMaxHp("Player");

// 4. 부위별 실시간 내구도 및 파손 여부 (Head, LeftArm, RightArm, LeftLeg, RightLeg)
int leftArmDur = CombatDataHub.Instance.GetPartDurability("Player", BodyPart.LeftArm);
bool isBroken = CombatDataHub.Instance.IsPartBroken("Player", BodyPart.RightArm);
```

---

## 2. 행동 시작 전 부위 파손 검사 (`CanExecuteAction`)

`ActionExecutor`에서 입력을 받아 기술을 시작하기 직전에 아래 1줄을 호출하면 파손된 부위의 기술 시전을 차단할 수 있음.

```csharp
// ActionExecutor에서 기술 실행 직전:
if (CombatDataHub.Instance != null && !CombatDataHub.Instance.CanExecuteAction("Player", requestedAction))
{
    // 코어 고정기(잽 등)는 항상 true 반환함.
    // 파츠 기술(훅, 스트레이트 등)은 해당 부위가 파손(내구도 0)되었으면 false를 반환함.
    return;
}
```

---

## 3. 타격 적중 시 대미지 감쇄 연산 (`ProcessHit`)

공격자의 히트박스가 상대 허트박스에 닿았을 때 호출하면 기획서 공식에 따른 최종 대미지 감쇄 및 부위 내구도 소모를 자동 처리해서 결과를 돌려줌.

```csharp
HitResolutionResult result = CombatDataHub.Instance.ProcessHit(
    attackerId: "Player",
    defenderId: "Enemy",
    attackAction: currentAction,
    isGuarding: defenderIsGuarding,
    isWeaving: defenderIsWeaving
);

// 1. 회피(위빙) 성공 시:
if (result.IsEvaded)
{
    // 피해 0, 이펙트: 회피
}
// 2. 가드 성공 시:
else if (result.IsGuarded)
{
    // 코어 피해 0, 양팔 내구도 5:5 분산 소모됨
    // 소모량: result.LeftArmDurabilityDamage, result.RightArmDurabilityDamage
}
// 3. 일반/치명타 적중 시:
else
{
    // 코어 피해: result.DamageToHp
    // 치명타 여부: result.IsCritical
    // 경직도 누적치: result.StaggerAdded
    // 다운 유발 여부: result.CausesKnockdown
    // 넉백 거리: result.KnockbackDistance
}
```

---

## 4. 위빙(회피) 시도 시 다리 내구도 차감 및 페널티 (`ProcessWeavingAttempt`)

회피 키 입력 시 호출하면 다리 내구도 5를 차감하고, 다리 한쪽 파손 시 50% 실패 확률을 판정해줌.

```csharp
bool canWeave = CombatDataHub.Instance.ProcessWeavingAttempt("Player");

if (!canWeave)
{
    // 다리 파손 페널티로 회피 실패함 -> 무적 켜지 않고 피격 허용
}
```

---

## 5. 이벤트 구독 (UI 및 연출 연동)

```csharp
// 체력 변경 이벤트:
CombatDataHub.Instance.OnHpChanged += (fighterId, curHp, maxHp) => {
    // HP바 갱신
};

// 부위 내구도 변경 이벤트:
CombatDataHub.Instance.OnPartDurabilityChanged += (fighterId, part, curDur, maxDur) => {
    // 파츠 HUD 갱신
};

// 전투 시작 및 종료 이벤트:
BattleManager.Instance.OnBattleStarted += () => {
    // FIGHT 연출
};

BattleManager.Instance.OnBattleFinished += (playerWon, winnerId, settlement) => {
    // 승패 연출 및 결과창 출력
};
```
