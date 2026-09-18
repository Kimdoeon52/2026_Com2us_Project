# 리얼스틸 — 전투 시스템 코드베이스 지침

이 문서는 이 저장소의 전투 파트에서 코드를 작성할 때 **반드시 따라야 하는 규칙**이다.
여기 적힌 결정은 이미 팀 합의가 끝난 것이므로, 더 나은 방법이 떠올라도 임의로 바꾸지 말고 먼저 물어볼 것.

---

## 0. 프로젝트 컨텍스트

- **엔진**: Unity 6 / C#
- **장르**: 로봇 커스터마이징 RPG. 전투는 **실시간 사이드뷰 격투 게임** (스트리트 파이터 2~4 참고)
- **표현**: 3D 공간 위 2D 픽셀아트 스프라이트 (2.5D, 옥토패스 트래블러 형식)
- **이동**: 좌우 2방향만. 점프 없음. 근거리 교전만
- **담당 범위**: 이 지침이 다루는 것은 전투의 실행 엔진 — 행동(상태기계), 부위 파괴 판정/로직, 히트 판정, 넉백, 장비 연결(조립 로직)
- **인접 담당**: 스킬 데이터(최상희), 전투 결과·데이터 관리(김관현/KKH), 그래픽(심예성)
- **데이터 소유 경계 (2026-09-18 논의, 팀 확인 필요)**:
  - `ActionData`(행동/프레임 데이터) — **NYH 소유**. `ActionState`/`ActionExecutor`가 직접 읽는 실행 계약이라 다른 담당에게 넘기지 않는다.
  - `PartData`/`CoreData`(파츠·코어 원장 스탯) — **NYH가 별도로 만들지 않는다.** KKH가 `PartMasterData`(SO)로 4개 파트 공용 데이터를 이미 만들어뒀으므로, NYH의 `DurabilitySystem`/`RuntimePart`/`RobotAssembler`는 이 데이터를 **참조만** 한다. 로직(부위 파괴 판정 등)은 여전히 NYH 소유, 데이터 정의만 KKH 소유.
  - 데미지 계산식(ActionData.Damage와 PartMasterData의 팔 스탯을 어떻게 합산할지)은 아직 미확정 — KKH·CSH와 확인 전까지 `HitDetection`/`DurabilitySystem` 쪽 계산 로직을 확정하지 말 것.
- **수정 폴더 범위** NYH(남윤호)외 폴더는 사용자의 요청 전까진 보고 어떤게 있는지만 파악하고 수정은 절대 금지

### 이 문서의 상위 소스

| 소스 | 위치 | 용도 |
|---|---|---|
| 기획서 v1000.2 | `리얼스틸 기획서.docx` | 시스템 규칙의 최종 근거 |
| 전투 프레임표 v3 | `리얼스틸_전투_프레임표_v3.xlsx` | 모든 수치의 최종 근거 |

**수치를 코드에 직접 쓰지 말 것.** 프레임·데미지·스턴·넉백은 전부 프레임표에서 나와 ScriptableObject 에셋에 들어간다.
기획서와 이 문서가 충돌하면 기획서가 우선이고, 충돌을 발견하면 코드를 고치기 전에 보고할 것.

---

## 1. 아키텍처 — 3층 구조

```
┌──────────────────────────────────────────────────────────┐
│ 1층 · 데이터 (ScriptableObject)   — 불변. 에셋으로 존재     │
│     ActionData(NYH)    FrameBox(NYH)                       │
│     PartData/CoreData 역할은 KKH의 PartMasterData가 대신함   │
├──────────────────────────────────────────────────────────┤
│ 2층 · 런타임 상태 (순수 C# class) — 가변. 전투 중에만 존재   │
│     RuntimeRobot  RuntimePart  RuntimeCore  ActionState   │
├──────────────────────────────────────────────────────────┤
│ 3층 · 시스템 (MonoBehaviour)      — 규칙. 개수 고정         │
│     ActionExecutor  HitDetection  DurabilitySystem        │
│     KnockbackSystem StunSystem    RobotAssembler          │
└──────────────────────────────────────────────────────────┘
```

### 핵심 원칙

> **기술이 몇 개로 늘어나도 클래스는 늘어나지 않는다. 에셋만 늘어난다.**

잽이든 백스핀 엘보우든 보스 전용 기술이든 전부 `ActionData` 에셋 하나다.
`ActionExecutor`는 그것이 무슨 기술인지 몰라도 선딜 → 판정 → 후딜을 돌릴 수 있어야 한다.

### 폴더 구조

실제 저장소 컨벤션(번호+한글 네이밍, NYH/CSH/KDU/LSH 공통)에 맞춰 아래 경로를 쓴다.

```
Assets/NYH/02. Scripts/Combat/
    Data/       ActionData.cs ✅  FrameBox.cs  CombatEnums.cs ✅
                (PartData.cs / CoreData.cs는 만들지 않음 — KKH의 PartMasterData 참조)
    Runtime/    RuntimeRobot.cs  RuntimePart.cs  RuntimeCore.cs  ActionState.cs ✅
    Systems/    ActionExecutor.cs ✅  HitDetection.cs  DurabilitySystem.cs
                KnockbackSystem.cs  StunSystem.cs  RobotAssembler.cs
    Input/      IInputSource.cs ✅  PlayerInputSource.cs ✅  AIInputSource.cs
    View/       RobotView.cs  BoxDrawer.cs  FrameStepper.cs  RobotMover.cs ✅(임시)

Assets/NYH/04. SO/Combat/
    Actions/    잽.asset  훅.asset  스트레이트.asset  ...
    (Parts/, Cores/ 폴더는 만들지 않음 — Assets/KKH/03.SOData/의 PartMasterData 참조)
```

에셋 파일 이름은 **프레임표 [기술] 칸과 1:1로 같게** 쓴다. 표와 에셋을 대조할 수 있어야 한다.

---

## 2. 금지 사항

아래는 예외 없이 금지한다. 이 중 하나라도 어기는 코드는 작성하지 말 것.

### ❌ 행동마다 클래스를 만들지 말 것

```csharp
// 금지
class JabAction : BaseAction { }
class HookAction : BaseAction { }
```

기본기 5 + 방어 2 + 스킬 4 + 보스 기술 다수 = 수십 개 클래스가 되고, 전부 같은 선딜/판정/후딜 코드를 복사하게 된다.
행동의 차이는 **데이터의 차이**이지 코드의 차이가 아니다.

### ❌ Unity 콜라이더 / `OnTriggerEnter` 로 전투 판정을 만들지 말 것

이유:
1. 물리 판정은 `FixedUpdate`(기본 0.02초 ≈ 3프레임)에서 돌아간다. **활성 2프레임(0.033초)짜리 잽은 물리 스텝 사이로 통과해버린다.**
2. 동시 히트의 처리 순서를 엔진이 정한다. 격투 게임에서 이건 게임 규칙이므로 우리가 정해야 한다.
3. 프레임 단위 enable/disable 토글 시 이벤트가 누락되거나 중복 발생한다.

판정은 **데이터로 들고 매 프레임 직접 AABB 겹침 검사**한다. (§4 참조)

단, 전투와 무관한 용도(맵 경계 트리거, 아이템 픽업 등)에는 콜라이더를 써도 된다.

### ❌ Animator 재생 시간을 타이밍 기준으로 쓰지 말 것

```csharp
// 금지 — 클립 하나만 교체돼도 밸런스가 통째로 틀어진다
if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f) EndAction();
```

**데이터가 진실이고 애니메이션이 따라온다.** 상태기계가 프레임을 세고, Animator에는
`speed = 클립길이 / 데이터길이` 를 넣어 억지로 맞춘다.
Animator Controller에 전이 조건을 주렁주렁 다는 것도 금지. `Play()`로 직접 클립을 재생시킨다.

### ❌ ScriptableObject에 런타임 가변 상태를 넣지 말 것

```csharp
// 금지 — SO는 에셋이다. 플레이 모드에서 깎은 값이 에디터에 영구히 남는다
public class PartData : ScriptableObject { public int currentDurability; }
```

`PartData`(현재는 KKH의 `PartMasterData`)는 `baseDurability`(불변)만 갖고, `currentDurability`는 `RuntimePart`(2층, NYH 소유)가 갖는다.
이 원칙은 NYH가 SO를 직접 안 만들어도 그대로 적용된다 — KKH의 `PartMasterData`를 읽을 때도 거기서 가변 상태를 읽어오면 안 되고, 가변 상태는 항상 `RuntimePart`에서만 관리한다.

### ❌ 플레이어가 부위/행동을 상속하는 구조를 만들지 말 것

```csharp
// 금지 — C#은 다중 상속이 안 되고, 애초에 "플레이어 is a 팔"이 아니다
class Player : Arm, Leg, Head { }
```

플레이어는 부위를 **가진다**. 포함(composition)으로 표현한다.

### ❌ 밸런스 수치를 코드에 하드코딩하지 말 것

`if (damage > 500)`, `yield return new WaitForSeconds(0.2f)` 같은 것 전부 금지.
숫자는 에셋에서 온다.

---

## 3. 프레임 규칙

- **60fps 고정.** 프레임 카운트는 `int`로 센다. 초가 필요하면 `frames / 60f`.
- `Time.deltaTime`을 직접 곱해서 타이밍을 재지 않는다. 프레임 카운터를 1씩 증가시킨다.
- **시작 프레임은 순수 선딜이다.** SF 원본 표기(시작 프레임에 첫 활성 프레임 포함)와 다르니 주의.

```
전체 프레임 = 시작 + 활성 + 회수     (그냥 합. -1 보정 없음)
```

### 고정 타임스텝 — 전투 로직은 모니터 주사율과 분리한다

**이것이 §3에서 가장 중요한 항목이다.**

`Update()` 안에서 프레임 카운터를 그냥 `+1` 하면 **모니터 주사율이 곧 게임 속도**가 된다.
144Hz 모니터에서는 잽이 12/144 = 0.083초 만에 끝나 60Hz 유저보다 2.4배 빠른 게임이 된다.
프레임표의 모든 숫자가 무의미해진다.

```csharp
// 금지 — 주사율에 따라 게임 속도가 달라진다
void Update() { currentFrame++; CheckHit(); }
```

전투 로직은 **1/60초마다 정확히 한 번** 도는 `CombatTick()`에 전부 모은다.

```csharp
public class CombatClock : MonoBehaviour
{
    public const float TICK = 1f / 60f;
    float accumulator;

    void Update()
    {
        accumulator += Time.deltaTime;
        while (accumulator >= TICK)
        {
            CombatTick();            // 프레임 +1, 판정, 넉백, 경직도 — 전부 여기
            accumulator -= TICK;
        }
        // 렌더·보간·UI는 이 바깥. 주사율대로 돌아도 된다
    }
}
```

#### 지켜야 할 것

- **프레임을 세는 코드, 판정, 넉백, 경직도 감소는 전부 `CombatTick()` 안에서만 돈다.**
  `Update()`에 직접 박아두면 나중에 뜯어낼 수 없다.
- 스프라이트 갱신, 카메라, UI, 이펙트는 `CombatTick()` 밖. 주사율대로 돌아도 된다.
- `FixedUpdate()`를 쓰지 않는다. 물리 스텝과 전투 틱은 별개이며, 물리에 묶으면
  Time Manager 설정에 게임 속도가 끌려간다.
- `Time.timeScale = 0`으로 멈출 때 `Time.deltaTime`도 0이 되므로 `CombatClock`이 자연히 정지한다.
  §9의 `FrameStepper`는 이 성질을 이용해 `CombatTick()`을 수동으로 1회 호출하는 방식으로 만든다.
- 히트스톱(적중 시 양쪽 정지)은 `timeScale`이 아니라 **틱 내부의 정지 카운터**로 구현한다.
  `timeScale`을 건드리면 이펙트와 UI까지 같이 멈춘다.

#### 초기 구현 시 허용되는 단순화

프로젝트 초반에는 아래로 시작해도 된다. 단 **로직은 반드시 `CombatTick()`으로 분리해둔다.**
나중에 위 누산기 방식으로 갈아끼울 때 호출 지점만 바꾸면 되도록.

```csharp
Application.targetFrameRate = 60;
QualitySettings.vSyncCount  = 0;
```

### 행동의 3구간

모든 행동 — 잽, 가드, 위닝, 보스 기술까지 전부 — 이 구조를 공유한다.

```
[시작(선딜)] ──▶ [활성(판정 ON)] ──▶ [회수(후딜)]
```

### 상태

```csharp
public enum ActionPhase { Idle, Startup, Active, Recovery, Stagger, Down, Dead }
```

캐릭터는 **항상 정확히 하나의 상태**에 있다.
`bool isAttacking`, `bool isGuarding` 같은 플래그를 늘려가는 방식은 금지 — 조합 폭발로 반드시 깨진다.

### 현재 확정 프레임 (프레임표 v3)

| 기술 | 시작 | 활성 | 회수 | 전체 | 비고 |
|---|---|---|---|---|---|
| 잽 | 4 | 2 | 6 | 12 | 경직도 0 (기획서: 잽은 경직도 안 쌓임) |
| 훅 | 3 | 3 | 14 | 20 | 선딜 최단. 기습기 |
| 스트레이트 | 10 | 4 | 20 | 34 | |
| 어퍼컷 | 13 | 3 | 18 | 34 | 다운 유발 |
| 백스핀 엘보우 | 20 | 4 | 26 | 50 | 다운 유발, **가드 불가** |
| 가드 | 2 | 유지 | 10 | — | 백스핀 엘보우는 못 막음 |
| 위닝 | 3 | 6(무적) | 28 | 37 | 성공 시 스킬 5~8번으로 반격 |

값이 바뀌면 프레임표가 먼저 바뀌고 에셋이 따라간다. 코드는 건드리지 않는다.

---

## 4. 판정 (히트박스 / 허트박스 / 푸시박스)

### 데이터 형태

```csharp
public enum BoxType  { Hit, Hurt, Push }
public enum BodyPart { Head, LeftArm, RightArm, LeftLeg, RightLeg, Core }

[System.Serializable]
public struct FrameBox
{
    public BoxType  type;
    public BodyPart bodyPart;   // 허트박스에서 필수 — 치명타/부위 파괴 판정의 기준
    public Rect     rect;       // 피벗 기준 로컬 좌표
    public int      startFrame;
    public int      endFrame;
}
```

`rect`는 **로컬 좌표**다. 캐릭터가 움직여도 박스를 다시 만들 필요가 없고, 좌우 반전 처리가 한 곳으로 모인다.

### 박스 종류와 단위

| 종류 | 단위 | 역할 |
|---|---|---|
| **Hit** | 활성 구간당 1개 | 공격이 닿는 범위 |
| **Hurt** | **도트 1장당 1세트** | 맞는 범위. 팔을 뻗으면 같이 늘어나야 한다 |
| **Push** | 액션당 1개 | 몸끼리 겹쳐 지나가지 못하게 하는 상자. 넉백이 실제로 미는 대상 |

히트박스를 프레임별로 쪼개지 않는 이유: 활성 구간이 최대 6프레임이고 그 구간의 도트가 1~2장뿐이다.

### 매 프레임 흐름

```
1. 두 캐릭터의 ActionState를 1프레임 전진
2. 각자 현재 프레임의 박스를 GetActiveBoxes(currentFrame)로 꺼냄
3. AABB 겹침 검사
4. 겹쳤으면 히트 처리 (§5)
```

```csharp
static bool Overlap(Rect a, Rect b) =>
    a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
```

### 좌우 반전 — 반드시 처리할 것

`transform.localScale.x = -1`은 스프라이트만 뒤집고 박스 데이터는 건드리지 않는다.
로컬 → 월드 변환 시 방향을 반영하지 않으면 **왼쪽을 볼 때 등 뒤를 때리는 버그**가 난다.

```csharp
float worldX = facingRight ? pos.x + box.rect.x
                           : pos.x - box.rect.x - box.rect.width;
```

이 변환은 **한 군데(`BoxResolver` 같은 단일 지점)에서만** 수행한다. 여러 곳에 흩어지면 반드시 한쪽을 빠뜨린다.

### 판정과 표시는 같은 데이터를 본다 — 필수

```
                ┌── HitDetection  (판정 계산)
GetActiveBoxes ─┤
                └── BoxDrawer     (화면 표시)
```

디버그 표시기가 별도 데이터를 보면 **거짓말하는 표시기**가 된다. 반드시 같은 함수를 통한다.

### 박스 좌표는 엑셀에 넣지 않는다

프레임·데미지·스턴·넉백은 엑셀. **박스 위치·크기는 유니티에서 스프라이트를 보며 맞춘다.**
숫자를 손으로 타이핑해서 주먹 위치를 맞출 수 없다.

---

## 5. 히트 처리 규칙 (기획서 근거)

맞은 쪽의 대응 방식에 따라 **방어자의** 부위 내구도가 깎인다. (기획서 583~588행)

| 대응 | 깎이는 부위 | 비고 |
|---|---|---|
| 가드 | 팔 | 백스핀 엘보우는 가드 불가 |
| 위닝(회피) | 다리 | |
| 치명타 피격 | 머리 | 머리 **허트박스**에 맞았을 때만 |
| 못 막은 유효타 | 코어 HP | |

내구도 감소량은 **공격자의 `ActionData`**가 들고 있다. 스킬 쪽에서 "팔 내구도를 깎는다"를 처리하지 않는다 —
어느 부위가 깎일지는 방어자의 대응이 결정하므로, 판정은 `DurabilitySystem` 한 곳에서만 한다.

### 부위 파괴 (기획서 575~582행)

- 내구도 0 → 해당 부위의 스킬 사용 불가
- 다리 1개 파괴 → 회피 확률 50% / 양다리 파괴 → 회피 불가
- 팔 1개 파괴 → 가드 확률 50% / 양팔 파괴 → 가드 불가
- 파괴된 부위는 반투명 처리 + **그 부위의 허트박스도 제거**

### 행동 잠금은 `RuntimeRobot`이 목록으로 관리

```csharp
class RuntimeRobot
{
    List<ActionData> availableActions;

    void OnPartBroken(BodyPart part) => RebuildAvailableActions();
    void OnPartEquipped(...)         => RebuildAvailableActions();
}
```

- **매 프레임 검사하지 않는다.** 부위 상태가 바뀌는 이벤트에서만 1회 갱신한다.
- 입력·UI·AI가 전부 이 목록 하나를 본다. 실행 직전에 검사하면 UI가 버튼을 회색 처리할 수 없고 AI가 못 쓰는 기술을 고른다.

### 출처 구분 — 코어 고정 vs 파츠

| 출처 | 부위 파괴 시 |
|---|---|
| 코어 고정 (잽, 가드, 위닝, 이동) | 사라지지 않음. 성능만 하향 |
| 파츠 (훅, 스트레이트, 어퍼컷, 백스핀, 스킬 5~8) | 해당 부위 파괴 시 사용 불가 |

양팔이 파괴돼도 잽은 나가야 한다. 안 그러면 공격 수단이 0이 되어 전투가 성립하지 않는다.

---

## 6. 넉백 — SF2 방식

콤보 제한 카운터를 **만들지 않는다.** 콤보를 끊는 것은 거리다.

```
1타 ▸ 거리 0.9 → 2타 ▸ 1.3 → 3타 ▸ 1.7 → 4타 ▸ 사거리 밖, 헛침
```

구현은 이것뿐:

> 공격이 맞은 순간, 양쪽 x 좌표에 `ActionData`의 넉백 값을 더한다.

`comboCount`, `maxComboHits` 같은 변수를 도입하지 말 것. 콤보 길이는 넉백 값과 사거리가 정한다.
다운 유발 기술(`어퍼컷`, `백스핀 엘보우`)이 맞으면 그 자리에서 콤보가 끝난다.

---

## 7. 경직도 (기획서 562~574행)

**히트 스턴과 다른 값이다. 섞지 말 것.**

| | 단위 | 성질 |
|---|---|---|
| 히트 스턴 | 프레임 | 한 대 맞고 굳는 시간. 매번 초기화 |
| 경직도 | 포인트 | 누적 게이지. 안 맞으면 감소 |

- **잽은 경직도를 쌓지 않는다.** 잽 외의 공격만 누적한다.
- 게이지가 한계치(100)에 도달하면 **2초(120프레임) 정지**. 피격 이미지 1장으로 멈춘다.
- 정지 중에만 **필살기** 사용 가능.

---

## 8. 플레이어와 AI

로봇 본체는 하나다. 갈라지는 것은 입력뿐이다.

```csharp
public interface IInputSource
{
    ActionData GetDesiredAction();   // 없으면 null
    float      GetMoveInput();       // -1 ~ 1
}

class PlayerInputSource : IInputSource { }   // 키보드
class AIInputSource     : IInputSource { }   // 보스 패턴
```

적 전용 `EnemyRobot` 클래스를 만들지 말 것. 입력만 바꿔 끼운다.

### 입력 키 (기획서 494~511행)

| 행동 | 키 |
|---|---|
| 이동 | ← → |
| 잽 | D |
| 스트레이트 | Q |
| 훅 | A |
| 어퍼컷 | W |
| 백스핀 엘보우 | S |
| 가드 | C |
| 위닝 | Space |

---

## 9. 디버그 도구 — 판정보다 먼저 만든다

판정을 만들기 전에 판정을 볼 수 있게 만들 것. 안 보이는 것을 디버깅하면 시간이 몇 배로 든다.

### BoxDrawer

- 1단계: `OnDrawGizmos` — Scene 뷰 전용. 빌드에 안 들어감
- 2단계: `GL` 또는 반투명 쿼드로 Game 뷰에도 표시. 토글 키 제공
- 색: Hit = 빨강 / Hurt = 하늘 / Push = 초록

### FrameStepper

```
F2 : 일시정지 (Time.timeScale = 0 → CombatClock이 자연히 멈춘다)
F3 : CombatTick()을 수동으로 1회 호출 → 정확히 1프레임 전진
```

`CombatClock`(§3)이 있으면 F3는 `CombatTick()`을 한 번 부르는 것으로 끝난다.
로직이 `Update()`에 흩어져 있으면 이 도구 자체를 만들 수 없다 — §11에서 `CombatClock`이 먼저인 이유다.

프레임 단위로 멈춰서 보지 않으면 "훅이 3프레임에 나간다"를 검증할 방법이 없다.

### 상태 로그

행동 진행 로그는 단순 `Debug.Log("잽!")`이 아니라 **구간이 구분되게** 찍는다.

```
[잽] Startup 4f → Active 2f → Recovery 6f
```

---

## 10. 애니메이션 연동

- 클립 이름은 프레임표 [애니메이션 클립] 칸과 1:1로 맞춘다
- 모든 클립의 피벗은 **발밑 중앙**으로 통일 (안 맞으면 공격 시 캐릭터가 순간이동)
- 모든 클립의 캔버스 크기 동일 (다르면 박스 좌표가 전부 어긋남)
- 스프라이트 정렬은 Z축이 아니라 `sortingOrder`로 (2.5D에서 Z 정렬은 카메라 각도 변화에 깨짐)
- 재생 속도는 `speed = 클립길이 / 데이터길이`로 데이터에 맞춘다

---

## 11. 작업 순서

현재 단계와 다음 할 일. 순서를 건너뛰지 말 것.

1. ✅ 프레임표 확정 (v3)
2. ⬜ 좌우 이동 + 키 입력 시 디버그 로그
3. ⬜ `CombatClock` — 1/60초 고정 틱 (§3). **전투 로직을 짜기 전에 먼저 만든다.**
   나중에 끼워 넣으려 하면 이미 `Update()`에 흩어진 코드를 전부 뜯어야 한다
4. 🔶 `ActionData` / `ActionState` / `ActionExecutor` — 잽 하나가 3구간으로 도는 것
   - ✅ `ActionData` (SO), `ActionState`, `ActionExecutor`, `IInputSource` 초안 작성 완료
   - ✅ `ActionState`에 스킬 연동용 이벤트 훅(`OnActionBegin`/`OnActionActiveStart`/`OnActionEnd`) 포함 — §13 참조
   - ⬜ 후딜 중 재입력이 무시되는지 실제 플레이로 반드시 확인 (`CanAcceptNewAction` 로직 자체는 구현됨)
   - ⬜ `CombatClock`과 배선 (지금은 `ExecuteTick()`을 누가 호출할지 미연결 상태)
5. ⬜ `BoxDrawer` (Gizmos) + `FrameStepper` — 판정 없이 네모만
6. ⬜ `HitDetection` — AABB 겹침
7. ⬜ `KnockbackSystem` / `StunSystem`
8. ⬜ `RobotAssembler` / `RuntimePart` / `DurabilitySystem` — 부위 파괴
9. ⬜ `AIInputSource` — 보스 패턴
10. ⬜ `IUsable` 인터페이스 확정 + 상희(CSH) 스킬 연동 지점 배선 (§13) — CSH `SkillBase` 쪽이 어느 정도 채워진 뒤 진행

---

## 12. 코드 작성 시 지켜야 할 것

- 주석과 로그는 한국어로 쓴다 (팀 전원 한국어)
- 클래스/변수명은 영어, 프레임표의 기술 이름만 한국어 그대로 사용
- 한 파일 = 한 클래스
- 새 MonoBehaviour를 추가하기 전에 **기존 시스템에 들어갈 자리가 아닌지** 먼저 확인할 것.
  3층의 시스템 개수는 고정이며, 늘어나야 할 이유가 있으면 먼저 물어볼 것
- 수치가 필요한데 프레임표에 없으면 **임의로 지어내지 말고 물어볼 것.**
  밸런스 수치는 기획이 정하는 것이지 프로그래머가 정하는 것이 아니다

---

## 13. 스킬(CSH) 연동 — 이벤트 훅으로만 접촉한다

담당 경계: **NYH는 기본 행동(선딜 → 판정 → 후딜)이 나가는 것까지만 책임진다.**
"맞았을 때 이펙트가 나온다", "스킬이 프레임을 늘리거나 줄인다", "추가 스킬이 발동한다" 같은
효과의 **유무·타이밍 판단은 전부 스킬 담당(최상희/CSH) 몫**이다. NYH 코드는 그 판단을 몰라도 된다.

### 접촉 지점은 이벤트 3개뿐

`ActionState`(2층)가 아래 이벤트를 노출한다. 전투 실행 엔진은 이 이벤트를 **모든 `ActionData`에 대해
무조건 발생**시키고, "이게 무슨 기술인지, 지금 반응해야 하는지"는 구독하는 쪽이 판단한다.

```csharp
public event Action<ActionData> OnActionBegin;        // Startup 진입
public event Action<ActionData> OnActionActiveStart;  // Active 진입 — 이펙트 타점으로 주로 씀
public event Action<ActionData> OnActionEnd;           // Recovery 끝나고 Idle 복귀
```

```csharp
// 예시 — CSH 쪽에서 구독하는 코드 (NYH 폴더에는 절대 이런 분기를 넣지 않는다)
actionState.OnActionActiveStart += (action) =>
{
    if (action.ActionName != "훅") return;       // 어떤 기술인지 판단은 구독자 몫
    if (!내스킬이켜져있음) return;                 // 켜져있는지 판단도 구독자 몫
    이펙트재생();
};
```

### ❌ 금지 — NYH 쪽 코드에 기술 이름으로 분기하는 if문

```csharp
// 금지 — ActionExecutor/ActionState는 무슨 기술인지 몰라야 한다
if (action.ActionName == "훅") { ... }
```

이런 분기가 필요해지는 순간, 그건 스킬 쪽 구독자 코드에 들어가야 할 로직이 엔진에 새어 들어온 것이다.

### 서로 다른 구현 방식이어도 된다 — 접점은 `IUsable` 하나

NYH는 `ActionData`(SO)로, CSH는 `SkillBase`(MonoBehaviour 상속)로 — 서로 다른 방식을 써도 된다.
어느 쪽이 "옳다"를 강요하지 않는다. 대신 둘 다 만족하는 최소 계약만 인터페이스로 둔다.

```csharp
public interface IUsable
{
    int StartupFrames { get; }
    int ActiveFrames  { get; }
    int RecoveryFrames { get; }
}
```

`ActionExecutor`는 구체 타입이 아니라 이 인터페이스로만 다뤄야 한다 (실제 배선은 §11-10, CSH 쪽이
어느 정도 채워진 뒤 진행 — 지금은 빈 껍데기라 연동해도 얻을 게 없다).

### 프레임 값 자체를 스킬이 바꿔야 할 때

`ActionData`는 불변 SO라 스킬이 직접 `startupFrames` 등을 고치면 안 된다 (§2 원칙 재확인).
대신 `RuntimeRobot`(2층, 가변)에 보정치를 두고 거기서만 조정한다.

```csharp
// RuntimeRobot 쪽 — 원본 에셋은 절대 안 건드림
public float FrameSpeedModifier { get; set; } = 1f;
```

### 구독 배선

스킬 쪽이 특정 로봇의 이벤트를 구독하려면 `ActionExecutor.State`(또는 `RuntimeRobot`을 경유한 참조)로
접근한다. 이 참조 경로는 상희 님과 합의 후 고정하고, 합의 전까지 CSH 폴더 코드를 NYH 쪽에서 직접
참조하지 않는다 (수정 금지 범위와 별개로, 결합 방지 차원).
