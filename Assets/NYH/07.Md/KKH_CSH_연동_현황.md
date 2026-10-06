# KKH·CSH 연동 현황 — 실측 조사 기록

작성: 2026-10-06 · 대상: 남윤호(NYH) — 플레이어 이동/조작, 기본 공격(D)·판정, 행동 상태기계,
부위 파괴 로직, 장비 연결(조립), **일반 적(몹) AI·일반 적 제작**
방법: `Assets/KKH/06.Document/*.md` 전체 + `Assets/KKH/02.Scripts` 핵심 11개 파일,
`Assets/CSH/02.Scripts` 전체(9개 파일) + git 커밋 이력을 직접 읽고 NYH 코드(`ActionState.cs`,
`ActionExecutor.cs`, `HitDetection.cs`, `DummyBattleBootstrap.cs`)와 대조해서 확인한 내용.
**문서상 "계획/가이드"가 아니라 지금 코드가 실제로 하는 일 기준으로 적는다.**

---

## 목차

0. 결정 사항 (2026-10-06, 남윤호)
1. NYH 역할 재확인
2. KKH 연동 현황
3. CSH 연동 현황
4. 종합 — 뭐가 안 맞는가
5. 상희 님·관현 님과 논의가 필요한 질문 (남은 것)

---

## 0. 결정 사항 (2026-10-06, 남윤호)

이 문서 조사 직후 바로 정한 것 — 전부 CLAUDE_1.md에도 반영됨.

1. **일반 적(몹) 제작/AI는 KKH의 `CombatantSnapshot`/`CombatantBuilder.CreateNPCPreset`/
   `RobotVsRobot` 모드에 그대로 올라탄다.** 별도 데이터 구조를 새로 만들지 않는다 (§1 참고).
2. **CSH 연동은 CSH가 이미 만든 방식(리졸버 주입, `SetModifierResolver`/`SetEffects`)을
   그대로 따라간다.** §13 원안(이벤트 구독)으로 되돌리자고 요구하지 않는다. CSH 쪽 빈 자리
   (프레임 보정 필드 없음, `ActionTag` 매칭 비활성, `KnockbackMultiplier` 미사용)는 CSH 책임 —
   NYH가 먼저 나서서 고치거나 요구하지 않는다.
3. **보스전 초기화(`InitializeBossBattle`)와 실린더 게이트 연결은 KKH·CSH가 알아서 진행한다.**
   NYH는 먼저 나서서 배선하지 않고, 요청이 오면 그때 받는다.

아래 §5의 질문 목록 중 이 결정으로 해소된 항목은 취소선으로 표시해뒀다.

## 1. NYH 역할 재확인 (2026-10-06)

기획서 팀 명단(1막 기준)엔 "그리드/부위파괴/행동"으로 적혀 있지만, 그리드가 폐기된 뒤 실제 작업
범위는 다음으로 재확인됨 — CLAUDE_1.md §0에도 반영:

- 플레이어 이동·조작 (`RobotMover`, `PlayerInputSource`)
- 기본 공격(D)과 판정 (`HitDetection`, `FrameBox`, AABB)
- 행동 상태기계 (`ActionState`, `ActionExecutor`, `ActionData`)
- 부위 파괴 판정/로직 (`DurabilitySystem` 예정)
- 장비 연결(조립 로직) (`RobotAssembler` 예정)
- **일반 적(몹) AI + 일반 적 제작** (신규 — `AIInputSource`, 일반 적용 로봇 조립)

스킬 데이터는 CSH, 전투 결과·DB는 KKH 소유. 보스 자체(기믹/스킬 밸런스)는 NYH 범위 밖이지만,
보스가 쓰는 입력원 구조(`IInputSource` 구현체)는 일반 적 AI와 겹칠 수 있어 설계 시 같이 고려.

---

## 2. KKH 연동 현황

### 2-1. 문서 현황

| 문서 | 최신성 | 비고 |
|---|---|---|
| `BattleManager_Architecture.md` (v5.7, 2026-10-05) | **최신** | 전체 아키텍처·API 설계. Phase 2~4 체크리스트는 전부 `[ ]`(미완료) — 문서 작성자도 "설계만 끝났고 구현 검증 전"이라고 명시 |
| `Battle_Team_3Way_Collaboration_Guide.md` (v3.1) | 최신에 가까움 | 3인 협업 시나리오·호출 순서 |
| `Combat_HP_Durability_HUD_Design.md` | UI 전용 | NYH 직접 연동 대상 아님 |
| `NYH_Integration_Guide.md` | **구버전 (2026-09-19, 갱신 없음)** | 09-29 보스전 전환 **이전** 버전. 가드/위빙/경직 전제로 적혀 있어 지금 코드와 다른 부분 많음 (2-3 참고) |
| `TODO_CombatDataHub.md` (2026-09-18) | **구버전** | `EvaluateHit` 등 지금 코드에 없는 메서드명을 그대로 적어둠. 참고만, 메서드명은 믿지 말 것 |
| `개발일지.md` | — | 09-19 이후 어느 시점에 "복싱 기반(잽/훅/가드/경직) 폐기 → 2.5D 실시간 액션 PvE 보스전"으로 전환했다는 기록 있음(NYH 쪽 09-29 전환과 같은 시점대) |

### 2-2. 실제로 쓸 수 있는 공개 API (코드 확인, 2026-10-06)

**`CombatDataHub`** (`Assets/KKH/02.Scripts/04.Manager/CombatDataHub.cs`)

| 멤버 | 상태 |
|---|---|
| `static CombatDataHub Instance` | 사용 중 |
| `HitResolutionResult ProcessHit(attackerId, defenderId, ActionData, isGuarding, isWeaving, hitPart = Core)` | ✅ `HitDetection.cs`가 호출 중 |
| `HitResolutionResult ProcessHit(attackerId, defenderId, rawDamage, targetPart, partDamage, isBasicAttackD, GimmickTag, targetGimmickId)` | 미사용(원시 데미지 오버로드) |
| `bool CanExecuteAction(fighterId, ActionData action)` | ✅ `ActionExecutor`가 "대체 행동"에만 호출(아래 3-4 참고) |
| `bool CanExecuteAction(fighterId, ActionSource, BodyPart requiredPart, int cylinderCost = 0)` | ❌ 미사용 — 실린더 체크 포함 오버로드인데 NYH가 안 씀 |
| `void InitializeRobotBattle(CombatantSnapshot player, CombatantSnapshot enemy)` | 직접 호출 안 함(`BattleManager.InitializeBattle`이 대신 호출해줌) |
| `void InitializeBossBattle(CombatantSnapshot player, BossSnapshot boss)` | ❌ NYH 쪽 호출 지점 없음(KKH 테스터만 호출) |
| `float GetFinalMoveSpeed(fighterId)` | ❌ `RobotMover`가 아직 하드코딩값 사용, 연결 안 함 |
| `CombatantSnapshot GetSnapshot(fighterId)` | ✅ `ActionExecutor.IsPartBroken`(CSH 삽입 코드)에서 사용 |
| `bool IsPartBroken(fighterId, BodyPart)` | ✅ 위와 동일 |
| 이벤트 다수(`OnPlayerHpChanged` 등, L60-79) | ❌ 전부 미구독 — UI/연출 트리거용인데 아직 아무도 안 들음 |
| `ProcessWeavingAttempt(fighterId)` | ❌ **코드 자체가 없음.** `NYH_Integration_Guide.md` §4에만 존재 — 호출하면 컴파일 에러 |

**`BattleManager`** (`Assets/KKH/02.Scripts/04.Manager/BattleManager.cs`)

| 멤버 | 상태 |
|---|---|
| `void InitializeBattle(playerSnapshot, enemySnapshot, betGold=100, playerObj=null, enemyObj=null)` | ✅ `DummyBattleBootstrap.cs`가 호출 중. **보스 오버로드 없음 — 로봇 vs 로봇 전용** |
| `void StartBattle()` | ✅ `DummyBattleBootstrap.cs`가 호출 중 |
| `void EndBattle(playerWon, winnerId)` | 미사용. 참고: 이 메서드가 `BattleSettlementProcessor.ProcessSettlement`를 부를 때 `coreMaster`/`partMasterLookup`을 안 채워서 코어 EXP·등급별 파괴확률이 실제로는 적용 안 되는 상태(KKH 쪽 이슈, NYH 책임 아님 — 정산 결과가 이상하면 참고) |

### 2-3. 문서(`NYH_Integration_Guide.md`) vs 실제 코드 — 불일치

1. **`ProcessWeavingAttempt`가 코드에 없음** (위 참고). 다리 내구도 깎는 위빙 페널티 로직 자체가 `CombatCalculator`에 없음.
2. **가드 분산 소모 서술이 거짓**: 문서는 "가드 성공 시 `LeftArmDurabilityDamage`/`RightArmDurabilityDamage`로 소모량 확인"이라 하지만, 이 두 필드는 구버전 레거시로만 선언돼 있고 어디서도 값이 대입되지 않음(항상 0). 애초에 NYH 쪽도 가드를 `isGuarding=false` 고정으로 보내고 있어 이 경로는 안 씀.
3. **치명타/경직/다운 필드가 항상 기본값**: `HitResolutionResult.IsCritical`/`StaggerAdded`/`CausesKnockdown`은 `CombatCalculator` 어디서도 대입되지 않음(선언만 존재). 이 값들을 읽고 뭔가 분기하면 항상 `false`/`0`만 나옴.
4. **근본 원인**: 가이드 문서가 2026-09-19(복싱/가드/경직 전제) 작성 후 갱신 안 됨. 반면 `CombatDataHub.cs`와 `BattleManager_Architecture.md`(v5.7)는 2026-10-05에 최신 구조로 갱신됨 — **가이드 문서만 구버전에 멈춰 있는 상태.**

### 2-4. ActionData 필드 — KKH가 실제로 읽는 것 (grep 전수 확인)

`CombatCalculator.cs` + `CombatDataHub.cs`가 `ActionData`에서 실제로 읽는 멤버는 **5개뿐**:
`ActionName`, `Source`, `RequiredPart`, `Damage`, `KnockbackDistance`.

**안 읽는 것** (NYH `ActionData.cs` 주석은 "KKH 계약"이라 적어놨지만 실제로는 미사용 — 주석이 틀림):
`IsGuardable`, `StaggerValue`, `CausesKnockdown`, 프레임 필드들, `IsInvincibleDuringActive`,
`AnimationClipName`, `FrameBoxes`.

### 2-5. 일반 적(몹) vs 보스 — KKH 쪽 구조

**완전히 다른 DTO로 나뉨.**
- `CombatantSnapshot` — **플레이어 + 일반 적 NPC 공용**. 코어 HP + 5부위 내구도 규격. `CombatantBuilder.CreateNPCPreset(NPCType)`로 NPC_A/B/C 프리셋 생성 가능.
- `BossSnapshot` — **보스 전용**. 본체 HP + `gimmickStates` 딕셔너리(범용 기믹) + 페이즈/그로기. 5부위 내구도 개념 없음(보스 파츠는 전투 중 파괴 불가 — 기획서와 일치).
- `CombatDataHub`는 `BattleMode`(`RobotVsRobot`/`RobotVsBoss`)로 나눠서 내부 계산 분기(`EvaluateRobotAttack` vs `EvaluatePlayerAttackOnBoss`/`EvaluateBossAttack`)가 완전히 다름.
- **지금 NYH가 실제로 돌리는 건 `RobotVsRobot` 모드 하나뿐** — 일반 적(몹) 전투는 이 모드로 바로 맞고, 보스전으로 가려면 `InitializeBossBattle` 연동이 따로 필요함(아직 없음).

→ **일반 적 AI/제작(NYH 신규 역할)은 `CombatantSnapshot` + `RobotVsRobot` 모드 쪽에 그대로 올라타면 되는 구조.** `CombatantBuilder.CreateNPCPreset`을 활용하면 될 가능성이 높음 — 직접 `PartData`를 새로 만들 필요는 없어 보임(§0 데이터 소유 경계와 일치).

### 2-6. 아직 NYH가 안 걸어놓은 것 중 KKH 쪽이 필요로 하는 것

1. 보스전 초기화(`InitializeBossBattle`) — 보스전을 만들려면 필수.
2. 실린더 소모 체크 오버로드 연결 — Q/W 강화판정 탄 소모가 지금 안 걸려 있을 가능성.
3. 이벤트 구독(`OnPlayerPartBroken` 등) — UI/연출 트리거용, 급하지 않지만 문서가 요청.

---

## 3. CSH 연동 현황

### 3-0. 가장 중요한 선행 사실 — `SkillBase`는 이미 삭제됨

git 이력(`git log --all -- Assets/CSH`): `1d1834f`→`a54d032`→`d99d287`(SkillBase 계열 만들던 중) →
**`8738b0f "part skill"`(2026-09-21)에서 SkillBase 계열 전체 삭제 + "장비 효과" 체계로 교체.**

삭제된 것: `SkillBase.cs`(MonoBehaviour), `AttackSkillBase.cs`, `DefenseSkillBase.cs`, `SkillData.cs`,
`attackSkills/a1.cs`, `AABB.cs`, `DummyParts.cs`. **지금 저장소에 `SkillBase`도 `IUsable`도 없음.**

같은 커밋에서 CSH가 NYH 소유 파일도 직접 수정함(주석에 "최. 추가"/"최.추가" 서명):
`ActionState.cs`, `ActionExecutor.cs`, `DummyBattleBootstrap.cs`. (코드로 직접 확인됨 — 2-1 참고)

### 3-1. 현재 CSH 폴더 구조

```
Assets/CSH/
├── 02.Scripts/
│   ├── 00.struct,class,interface/
│   │   ├── ActionTag.cs             비트플래그 enum(None/Attack/Weave/Skill) — 매칭 로직은 비활성
│   │   ├── EffectContext.cs         readonly struct. Executor/SourcePart/State/FighterId 노출
│   │   ├── EffectInstance.cs        abstract class. OnEquip/OnUnequip/Contribute/Replace
│   │   ├── EquipmentEffectApplier.cs static. 전투 시작 전 1회 적용
│   │   ├── EquipmentEffectSet.cs    로봇 1체의 효과 묶음, Resolve()/ResolveReplacement()
│   │   └── ResolvedModifiers.cs     struct. HitBoxWidthAdd/HeightAdd, KnockbackMultiplier (프레임 필드 없음)
│   └── 01_/                         (구 "01.Skill"에서 이름만 바뀜, 내용은 전혀 다름)
│       ├── ActionModifierData.cs    EquipmentEffect 구현체. targetTags 매칭은 주석처리됨(비활성)
│       ├── EquipmentEffect.cs       abstract ScriptableObject
│       └── EquipmentEffectTable.cs  partID → EquipmentEffect[] 매핑 SO
└── 04.SO/  (테스트 에셋)
```

### 3-2. 지금 CSH가 만든 게 "스킬"이 아니라 "장비 효과"다

트리거는 "스킬 사용 이벤트"가 아니라 **"어떤 파츠를 장착했는가"**. 전투 시작 시
`EquipmentEffectApplier.Apply`가 장착 파츠별로 테이블에서 효과를 찾아 1회 등록한다.
`ActionModifierData.cs` 자체 주석: "순수 수치 보정(이동속도/공격력 등)은 여기 안 넣는다 — 이미
KKH의 `CombatantBuilder`/`CombatantSnapshot`이 처리한다. 이 효과는 '행동 자체가 달라지는' 경우
전용"이라고 스스로 범위를 한정해둠. 즉 지금 이 체계가 다루는 건 **히트박스 크기 변경, 넉백 배율,
행동 대체(Replace)** 뿐이다.

### 3-3. NYH가 계획한 "이벤트 구독" vs CSH가 실제로 한 "리졸버 주입"

NYH의 §13 원안: 양쪽이 각자 방식으로 만들고, `ActionState`의 이벤트 3개(`OnActionBegin`/
`OnActionActiveStart`/`OnActionEnd`)만 구독해서 접점을 둔다 — 느슨한 결합.

**실제로 CSH가 한 것**: `ActionState.cs`에 `SetModifierResolver(Func<ActionData, ResolvedModifiers>)`를,
`ActionExecutor.cs`에 `SetEffects(EquipmentEffectSet)` / `ResolveReplacement(ActionData)`를 직접
추가(`#region 최. 추가`/`#region 최.추가`로 표시돼 있음, 코드 확인됨). `ActionState.Begin()`은
이벤트를 발행하는 것과 별개로, **매번 `modifierResolver(action)`를 직접 호출**해서 보정값을 받아온다.

→ 이벤트 3개는 코드에 그대로 있고 여전히 발행되지만 **구독자가 없다.** CSH가 선택한 건 pub-sub이
아니라 **함수 포인터 주입 + 직접 호출(pull)** 방식이고, 이미 NYH 소유 파일 안에 병합되어 있다.
(참고: `프레임_타임라인_개편_계획.md` P12가 2026-09-28에 이 `Func<ActionData, ResolvedModifiers>`
계약을 미리 언급하고 있었음 — 완전히 기습은 아니었지만, §13 문서 자체는 그 이후로 안 갱신됐다.)

### 3-4. 구독 "준비"는 돼 있는데 아무도 안 씀

`EffectContext.State`가 `ActionState`를 그대로 노출하고 있어서 `ctx.State.OnActionBegin += ...`
구독이 문법적으로 가능하다. `EffectInstance.OnEquip()`도 정확히 "구독 시작 시점"으로 설계된
자리다. 하지만 **현재 유일한 구현체인 `ActionModifierData.Instance`는 `OnEquip()`을 override하지
않아서 실제로 구독하는 코드가 전혀 없다.**

### 3-5. 알아둬야 할 구체적 결함/빈 자리

| 항목 | 내용 |
|---|---|
| `ActionTag` 매칭 비활성 | `ActionModifierData.Matches()`의 태그 비교 줄이 주석처리됨 — 지금은 `requiredPart`만으로 거름. 기술 이름 분기 금지(§13) 원칙엔 어긋나지 않지만, "태그로 매칭"이라는 설계 의도가 실제로는 꺼져 있다 |
| 프레임 보정 필드 없음 | `ResolvedModifiers`에 선딜/활성/후딜 보정 필드가 없어서 `ActionState.BuildAdjustment()`가 항상 `FrameAdjustment.None` 반환 — "스킬이 프레임을 늘리거나 줄인다"(§13 목표)는 지금 구조로 불가능 |
| `KnockbackMultiplier` 죽은 값 | `ActionModifierData`/`ResolvedModifiers`에 선언·계산은 되는데, `KnockbackSystem.cs`는 `ActionData.KnockbackDistance`만 읽고 이 배율을 전혀 참조하지 않음 — 계산은 되는데 아무 효과도 없음 |
| `CanExecuteAction` 게이트 위치 | `ActionExecutor.ResolveReplacement`가 "대체된 행동"에 대해서만 `CombatDataHub.CanExecuteAction`을 호출함 — 원래 요청한 행동(대체 안 된 일반적인 경우)은 이 게이트를 거치지 않음. §11-4 체크리스트가 "아직 안 부름"이라고 적어둔 항목이 부분적으로만 해소된 상태 |

---

## 4. 종합 — 뭐가 안 맞는가

1. **§13 원안(SkillBase + IUsable + 이벤트 구독)이 전제부터 깨짐.** SkillBase는 삭제됐고 IUsable은
   없다. CSH는 "스킬"이 아니라 "장비 효과" 패러다임으로 갔고, 이벤트 구독이 아니라 리졸버 주입으로
   이미 연동을 끝냈다.
2. **CSH가 이미 NYH 소유 파일(`ActionState.cs`/`ActionExecutor.cs`)을 직접 수정했다.** §13의
   "합의 전까지 CSH 폴더 코드를 NYH 쪽에서 직접 참조하지 않는다"는 결합 방지 원칙과 반대 방향의
   결합(CSH → NYH)이 이미 생겨 있다. `프레임_타임라인_개편_계획.md`가 이 계약(`Func<ActionData,
   ResolvedModifiers>`)을 09-28에 예고하긴 했지만, "이벤트 구독으로 간다"던 §13 자체가 그 이후로
   갱신되지 않아 두 문서가 서로 다른 그림을 그리고 있었다.
3. **KKH 쪽은 가이드 문서만 구버전에 멈춰 있고, 코드는 오히려 최신이다.** `NYH_Integration_Guide.md`를
   그대로 따라가면 존재하지 않는 메서드(`ProcessWeavingAttempt`)를 부르거나, 항상 기본값만 나오는
   필드(치명타/경직/다운/가드분산)를 믿게 된다.
4. **일반 적(몹) AI/제작이라는 NYH의 새 역할은 구조적으로 걸릴 자리가 이미 있다** — KKH의
   `CombatantSnapshot` + `RobotVsRobot` 모드 + `CombatantBuilder.CreateNPCPreset`. 보스전과
   완전히 다른 DTO라 서로 안 섞여서, 지금 당장은 "로봇 vs 로봇" 경로만 제대로 다지면 충분해 보임.

---

## 5. 상희 님·관현 님과 논의가 필요한 질문

**CSH(최상희)에게** — ~~1, 2번은 NYH 쪽에서 더 묻지 않기로 함 (§0 결정 2: CSH 방식 그대로 탄다)~~
1. ~~"장비 효과" 체계를 스킬 시스템의 최종 형태로 계속 쓸 것인지, `SkillBase`/`IUsable` 복원 계획~~
   — NYH는 어느 쪽이든 CSH가 만든 대로 따라간다. 질문 자체를 안 함.
2. ~~`SetModifierResolver`/`SetEffects` 방식 유지 vs 이벤트 구독으로 전환~~ — 유지로 확정(NYH 측 결정).
3. `ResolvedModifiers`에 프레임(선딜/활성/후딜) 보정 필드를 언제 추가할 수 있는지는 **여전히 유효한
   질문** — NYH의 `BuildAdjustment()`가 이걸 기다리고 있어서, 실제로 선딜/후딜을 바꾸는 스킬이
   필요해지는 시점엔 CSH에게 물어야 함. 지금 당장 급한 건 아님.
4. `ActionTag` 매칭 비활성화, `KnockbackMultiplier` 미사용 — **CSH 책임 영역이라 질문 취소.** 필요해
   지면 CSH가 알아서 챙길 것.

**KKH(김관현)에게**
1. `ProcessWeavingAttempt`를 구현할 계획이 있는지, 아니면 위빙 페널티(다리 내구도 소모) 자체가
   폐기된 설계인지 — 폐기라면 `NYH_Integration_Guide.md`도 같이 정리 필요. (여전히 유효 — 가이드
   문서가 틀린 내용을 적고 있다는 사실 자체는 알려줄 가치 있음)
2. 치명타(`IsCritical`)/경직(`StaggerAdded`)/다운(`CausesKnockdown`)/가드분산 필드를 실제로 채울
   계획이 있는지, 없다면 `HitResolutionResult`에서 레거시로 정리할지. (여전히 유효)
3. `IsGuardable`/`StaggerValue`를 NYH `ActionData`에서 지워도 KKH 쪽 빌드에 영향이 없는지 최종
   확인(2026-10-06 grep 기준으로는 안 읽지만, 재확인 요청). (여전히 유효)
4. ~~보스전 초기화를 NYH가 직접 호출할지, `BattleManager` 보스 오버로드가 생길지~~ — **질문 취소
   (§0 결정 3): KKH·CSH가 알아서 진행, NYH는 기다린다.**
5. ~~실린더 소모 체크를 NYH가 직접 연결해도 되는지~~ — **질문 취소 (§0 결정 3): 같은 이유.**
6. `CombatantBuilder.CreateNPCPreset(NPCType)`을 일반 적 제작에 그대로 쓰기로 **이미 결정함**(§0
   결정 1) — 질문이 아니라 통지: "이걸로 간다"만 전달하면 됨. 혹시 NPC_A/B/C 외에 더 세분화된
   등급이 필요해지면 그때 추가 요청.
