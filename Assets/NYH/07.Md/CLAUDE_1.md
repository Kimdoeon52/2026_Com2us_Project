# 리얼스틸 — 전투 시스템 코드베이스 지침

이 문서는 이 저장소의 전투 파트에서 코드를 작성할 때 **반드시 따라야 하는 규칙**이다.
여기 적힌 결정은 이미 팀 합의가 끝난 것이므로, 더 나은 방법이 떠올라도 임의로 바꾸지 말고 먼저 물어볼 것.

---

## 0. 프로젝트 컨텍스트

- **엔진**: Unity 6 / C#
- **장르**: 로봇 커스터마이징 RPG. 전투는 **실시간 사이드뷰 PvE 보스전** (할로우나이트·스컬 참고. 2026-09-29 확정 — 이전의 "격투 게임 미러매치"(스트리트 파이터 2~4) 전제는 폐기됐다. 플레이어와 보스는 서로 다른 기술 풀을 가진다. 자세한 배경은 §15)
- **표현**: 3D 공간 위 2D 픽셀아트 스프라이트 (2.5D, 옥토패스 트래블러 형식)
- **이동**: 좌우 이동 + **점프·대시 포함** (2026-09-29 변경 — "점프 없음" 결정 폐기). 공중 상태를 상태기계에 어떻게 반영할지는 미정, §15 참고
- **담당 범위** (2026-10-06 재확인): 플레이어 이동·조작, 기본 공격(D)과 판정(히트/허트/푸시박스), 행동 상태기계, 부위 파괴 판정/로직, 장비 연결(조립 로직), **그리고 일반 적(몹) AI·일반 적 제작**. 보스 자체의 기믹/스킬 데이터는 범위 밖(§15), 보스가 쓰는 입력원(`IInputSource` 구현체) 자체는 일반 적 AI와 같은 자리에서 다룰 가능성 있음 — 확정 전까지 임의로 합치지 말 것
- **인접 담당**: 스킬 데이터(최상희/CSH), 전투 결과·데이터 관리(김관현/KKH), 그래픽(심예성). 각 폴더와의 실제 연동 현황(공개 API, 문서-코드 불일치, 미해결 지점)은 `KKH_CSH_연동_현황.md`에 기록 — 거기 "미정" 항목들이 §13·§14보다 더 최신 사실 확인본이다
- **데이터 소유 경계 (2026-09-18 논의, 팀 확인 필요)**:
  - `ActionData`(행동/프레임 데이터) — **NYH 소유**. `ActionState`/`ActionExecutor`가 직접 읽는 실행 계약이라 다른 담당에게 넘기지 않는다.
  - `PartData`/`CoreData`(파츠·코어 원장 스탯) — **NYH가 별도로 만들지 않는다.** KKH가 `PartMasterData`(SO)로 4개 파트 공용 데이터를 이미 만들어뒀으므로, NYH의 `DurabilitySystem`/`RuntimePart`/`RobotAssembler`는 이 데이터를 **참조만** 한다. 로직(부위 파괴 판정 등)은 여전히 NYH 소유, 데이터 정의만 KKH 소유.
  - 데미지 계산식(ActionData.Damage와 PartMasterData의 팔 스탯을 어떻게 합산할지)은 아직 미확정 — KKH·CSH와 확인 전까지 `HitDetection`/`DurabilitySystem` 쪽 계산 로직을 확정하지 말 것.
  - **플레이어 vs 보스 비대칭 (2026-09-29 확인, 설계 미정)**: 보스의 "파츠"는 경매장 구매로 보스가 쓰는 스킬 풀을 결정하는 전투 시작 전 고정 세팅이다. 전투 중 플레이어가 보스 파츠를 파괴하는 것은 **불가능**하다 — 부위 내구도/파괴 시스템은 **플레이어 로봇에만** 적용된다. `RuntimeRobot`을 플레이어/보스 공용으로 쓸지, 내구도 추적 여부를 인스턴스별 플래그로 끌지는 미정. §15 참고.
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
│     KnockbackSystem RobotAssembler                        │
└──────────────────────────────────────────────────────────┘
(StunSystem은 2026-09-29 경직 시스템 삭제로 제외, §7)
```

### 핵심 원칙

> **기술이 몇 개로 늘어나도 클래스는 늘어나지 않는다. 에셋만 늘어난다.**

D든 파츠 액티브(Q/W/E/R)든 보스 전용 기술이든 전부 `ActionData` 에셋 하나다.
`ActionExecutor`는 그것이 무슨 기술인지 몰라도 선딜 → 판정 → 후딜을 돌릴 수 있어야 한다.

### 폴더 구조

실제 저장소 컨벤션(번호+한글 네이밍, NYH/CSH/KDU/LSH 공통)에 맞춰 아래 경로를 쓴다.

```
Assets/NYH/02. Scripts/Combat/
    Data/       ActionData.cs ✅  FrameBox.cs  CombatEnums.cs ✅
                (PartData.cs / CoreData.cs는 만들지 않음 — KKH의 PartMasterData 참조)
    Runtime/    RuntimeRobot.cs  RuntimePart.cs  RuntimeCore.cs  ActionState.cs ✅
    Systems/    ActionExecutor.cs ✅  HitDetection.cs  DurabilitySystem.cs
                KnockbackSystem.cs  RobotAssembler.cs  (StunSystem.cs는 2026-09-29 경직 삭제로 미생성, §7)
    Input/      IInputSource.cs ✅  PlayerInputSource.cs ✅  AIInputSource.cs
    View/       RobotView.cs  BoxDrawer.cs  FrameStepper.cs  RobotMover.cs ✅(임시)

Assets/NYH/04. SO/Combat/
    Actions/    D.asset  왼팔액티브.asset  오른팔액티브.asset  왼다리액티브.asset  오른다리액티브.asset  회피.asset  ... (2026-09-29 기준. 이름은 파츠 스킬 확정 후 갱신)
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

모든 행동 — D, 파츠 액티브(Q/W/E/R), 회피, 보스 기술까지 전부 — 이 구조를 공유한다.

```
[시작(선딜)] ──▶ [활성(판정 ON)] ──▶ [회수(후딜)]
```

### 상태

```csharp
public enum ActionPhase { Idle, Startup, Active, Recovery, Stagger, Down, Dead }
```

캐릭터는 **항상 정확히 하나의 상태**에 있다.
`bool isAttacking`, `bool isGuarding` 같은 플래그를 늘려가는 방식은 금지 — 조합 폭발로 반드시 깨진다.

### 현재 확정 프레임 — 0929 기획서로 전면 교체 (2026-09-29)

잽/훅/스트레이트/어퍼컷/백스핀 엘보우/가드/위닝 7개는 **전부 폐기됐다.** 아래가 새 구조다.

| 행동 | 시작 | 활성 | 회수 | 전체 | 비고 |
|---|---|---|---|---|---|
| D (고정 기본공격) | 6(임시) | 2(임시) | 10(임시) | 18(임시) | 쿨타임 0.5초 / 데미지 10 / 사거리 1.2 (기획서 확정값. 프레임 분배는 아직 기획 미확정 — `Assets/NYH/04. SO/Combat/Actions/D.asset`에 2026-10-06 **임시값**으로 뼈대만 만들어둠, 개발일지 2026-10-06 참고. `animationClipName`은 신규 클립이 없어 씬 Player가 이미 쓰는 구 컨트롤러(`08-999.OldPlayerAnim/AnimatorControlle.controller`)의 `Jap` 상태를 임시로 재사용 — 진짜 D 클립이 나오면 교체. 쿨타임 필드는 `ActionData`에 아직 없음 — 별도 과제) |
| Q (왼팔 액티브) | 미정 | 미정 | 미정 | 미정 | 파츠 소속. 수치·설계 일정 전부 미정 |
| W (오른팔 액티브) | 미정 | 미정 | 미정 | 미정 | 위와 동일 |
| E (왼다리 액티브) | 미정 | 미정 | 미정 | 미정 | 위와 동일 |
| R (오른다리 액티브) | 미정 | 미정 | 미정 | 미정 | 위와 동일 |
| 머리 패시브 | — | — | — | — | 입력 없음, 자동 적용 |
| 회피 (구 위닝) | — | 무적 0.3초 | — | 0.5초 | 전체 0.5초 / 무적 0.05~0.35초 / 자원 소모 없음 / 쿨타임 1.0초 / 공중 사용 불가 |

- **가드는 기동 행동에서 삭제됐다.** 나중에 스킬로 재도입될 수 있다 (§15).
- **경직 시스템은 완전히 삭제됐다** (§7).
- Q/W/E/R 수치가 나오기 전까지 `ActionExecutor`/`ResolvedAction` 등 엔진 코드는 **더미 값으로 구조만 먼저 검증**한다 — "기술이 늘어나도 클래스는 안 늘어난다"(§2) 원칙대로, 수치 미정이 구조 작업을 막지 않는다.
- 값이 바뀌면 프레임표가 먼저 바뀌고 에셋이 따라간다. 코드는 건드리지 않는다.

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

### 박스 창은 절대 프레임이 아니라 구간 앵커로 찍는다 (2026-09-28 추가)

`FrameBox`에 `startFrame/endFrame` 절대값을 직접 쓰는 것은 **"활성 구간이 언제인가"를 프레임 값에서
손으로 계산해 옮겨적은 사본**이다. 선딜 하나만 바뀌어도 조용히 어긋나고 에러가 안 난다.

```csharp
public FrameAnchor anchor;   // WholeAction / Startup / Active / Recovery
public int startOffset;      // 그 구간 진입 후 몇 프레임째부터 (0 = 구간 시작)
public int length;           // 몇 프레임. 0 = 구간 끝까지 자동으로 따라감
```

- **히트박스는 `Active` 앵커에 `length = 0`이 기본이다** — "활성 구간 전체".
- 몸통 허트·푸시박스는 `WholeAction`.
- `anchor = Legacy`는 변환 전 기존 에셋용 폴백이다. 새로 찍는 박스에 쓰지 말 것.

절대 창은 `ActionState.Begin()`에서 `ResolvedAction`이 1회 계산한다. 확정 프레임과 박스 창이 같은
자리에서 같이 정해지므로 둘이 따로 놀 수 없다. 자세한 배경·리스크는 `프레임_타임라인_개편_계획.md`.

### 프레임을 바꾸는 기술은 `FrameAdjustment`로만 (2026-09-28 추가)

선딜·활성·후딜·전체 속도를 바꾸는 장비/스킬은 **`ActionData`를 고치지 않는다**(§2). 보정값을
`FrameAdjustment`에 담아 `ResolvedAction.Resolve()`에 넘기면 프레임과 박스 창이 같이 확정된다.

- 보정을 옮겨 담는 유일한 지점은 `ActionState.BuildAdjustment()` 하나다.
- `FrameAdjustment.None`을 쓸 것. `new FrameAdjustment()`는 `SpeedMultiplier`가 0이라 행동 시간이 0이 된다.
- 원본 활성이 1 이상인 기술은 보정 후에도 **최소 1프레임**이 보장된다 (0이면 판정이 영영 안 켜진다).

### 대기·이동 중의 박스 (2026-09-21 추가)

`FrameBox`는 원래 `ActionData`에만 있어서, 걷거나 가만히 서 있을 때(`ActionState.CurrentAction == null`)는
Hit/Hurt/Push 박스가 전부 없었다 — 서 있는 상대를 때려도 판정이 안 뜨고 몸통끼리도 안 부딪히는 구멍이었다.

`ActionState.GetActiveBoxes()`가 이걸 메운다: 행동 중이면 그 행동의 박스, 아니면 `IdleAction`
(`Idle.asset` — Hit박스 없이 Hurt+Push만 담은 전용 `ActionData`)의 박스를 대신 반환한다.
`BoxDrawer`/`HitDetection`은 전부 이 함수 하나만 봐야 한다 — `CurrentAction`을 직접 들여다보지 않는다.

### 박스 좌표는 엑셀에 넣지 않는다

프레임·데미지·스턴·넉백은 엑셀. **박스 위치·크기는 유니티에서 스프라이트를 보며 맞춘다.**
숫자를 손으로 타이핑해서 주먹 위치를 맞출 수 없다.

---

## 5. 히트 처리 규칙 (2026-09-29 전면 재설계 — 기존 방식 폐기)

### 기존 방식은 폐기됐다

"가드=팔 / 위닝=다리 / 치명타=머리" — **방어자의 대응 방식**으로 어느 부위가 깎일지 정하던 기존
로직은 0929 기획서로 완전히 뒤집혔다. 새 기준은 다음과 같다.

| 판정 | 기준 |
|---|---|
| 파츠 내구도 감소 | **보스의 특정 스킬**에 맞았을 때만. 그 스킬이 지정한 부위가 깎인다 (기획서 977~980행) |
| 일반 피격 | 코어 HP만 감소, 파츠 내구도는 그대로 |
| 적용 대상 | **플레이어 로봇에만 적용.** 보스는 파츠 내구도/파괴 개념이 없다 (아래 "플레이어 vs 보스 비대칭" 참고) |

→ `ActionData`(또는 보스 전용 스킬 데이터)에 "이 스킬이 맞았을 때 상대의 어느 부위를 깎는지" 필드가
새로 필요하다. 필드명·소유 위치 전부 미정 — **이번 재설계에서 가장 먼저 설계해야 할 자리다** (§15).

### 플레이어 vs 보스 비대칭 (2026-09-29 확인)

보스의 "파츠"는 경매장 구매로 보스가 어떤 스킬을 쓰는지 결정하는 **전투 시작 전 고정 세팅**이다.
전투 중 플레이어가 보스의 파츠를 파괴하는 행동은 **불가능**하다. 즉:

- 부위 파괴 판정(`RuntimeRobot.availableActions`, 내구도 0 → 스킬 봉인)은 **플레이어 로봇에만** 돈다.
- 보스는 스킬 풀이 전투 중 안 바뀐다 — 내구도 추적 자체가 필요 없을 가능성이 높다.
- `RuntimeRobot`을 플레이어/보스 공용으로 쓸지, 내구도 추적 여부를 인스턴스별 플래그로 끌지는
  **미정** — 결정 전까지 "로봇 본체는 하나, 입력만 다르다"(§8) 원칙을 내구도 시스템에까지
  그대로 확장하지 말 것.

### 부위 파괴 (기획서 981~990행 — 페널티 단순화됨)

- 내구도 0 → 해당 부위의 스킬(Q/W/E/R 중 하나)만 사용 불가
- **회피·가드 확률에 페널티를 주는 로직은 삭제됐다** (기존 "다리 1개 파괴 → 회피 50%" 등은 폐기)
- 파괴된 부위는 반투명 처리 + **그 부위의 허트박스도 제거** (이 부분은 유지)

### 행동 잠금은 `RuntimeRobot`이 목록으로 관리 (✅ 2026-10-06 구현)

`Assets/NYH/02. Scripts/Combat/Runtime/RuntimeRobot.cs`. `ActionExecutor.Init()`에서 생성되어
`ActionExecutor.Robot`으로 노출된다 — `OnPartEquipped`은 아직 안 만듦(장비 교체가 전투 중 안 생기므로
우선순위 낮음), `OnPartBroken`은 KKH `CombatDataHub.OnPlayerPartBroken`/`OnEnemyPartBroken` 이벤트를
그대로 구독해서 돈다.

```csharp
class RuntimeRobot
{
    IReadOnlyList<ActionData> AvailableActions;
    bool IsAvailable(ActionData action);

    void OnPartBroken(BodyPart part) => RebuildAvailableActions();
}
```

- **매 프레임 검사하지 않는다.** 부위 상태가 바뀌는 이벤트에서만 1회 갱신한다.
- 입력·UI·AI가 전부 이 목록 하나를 본다. 실행 직전에 검사하면 UI가 버튼을 회색 처리할 수 없고 AI가 못 쓰는 기술을 고른다.
  `AIInputSource.GetDesiredAction()`이 이미 이 경로로 물어보게 연결됨.
- **구독 타이밍 주의**: `CombatDataHub`는 씬에 없으면 자동 생성이 안 돼서(§14), `PlayerRobotBootstrap.Awake`
  시점(생성자가 도는 시점)엔 아직 `CombatDataHub`가 없는 게 오히려 정상이다. 그래서 생성자에서 한 번에
  구독을 끝내지 않고 `EnsureSubscribed()`를 `ActionExecutor.ExecuteTick()`마다 가볍게 재시도한다
  (이미 구독됐으면 bool 체크 한 줄로 끝남).
- **지금은 실전 테스트가 안 됨**: `ActionData`에 "이 공격이 상대의 어느 부위를 깎는지" 필드가 아직 없고
  (§15 "보스 스킬 타격 부위" 미정 항목), `HitDetection.ResolveHit`도 `ProcessHit`에 `hitPart`를 항상
  기본값(`Core`)으로 보내서, 지금 플레이로는 부위 내구도가 절대 안 깎인다 — `RuntimeRobot`이 목록을
  갱신할 일 자체가 생기지 않는다. 확인하려면 `CombatDataHub.Instance.ApplyPartHit(fighterId, part,
  partDamage, 0)`을 테스트 코드로 직접 불러서 강제로 깨뜨려봐야 한다. "타격 부위" 필드 설계가 끝나야
  정식으로 검증 가능 — §11-13 참고.

### 출처 구분 — 코어 고정 vs 파츠 (갱신)

| 출처 | 부위 파괴 시 |
|---|---|
| 코어 고정 (D 기본공격, 회피, 이동) | 사라지지 않음 |
| 파츠 (Q/W/E/R) | 해당 부위 파괴 시 사용 불가 |

파츠가 전부 파괴돼도 D는 나가야 한다. 안 그러면 공격 수단이 0이 되어 전투가 성립하지 않는다.

### 가드·위닝 판정 방법 — 삭제됨 (2026-09-29)

가드는 기동 행동에서 완전히 빠졌다 (나중에 스킬로 재도입될 수 있음, §15). `ActionExecutor.IsGuarding`은
걷어낼 후보다. `CombatDataHub.ProcessHit`의 `isGuarding` 인자도 마찬가지인데, 이건 **KKH 소유 API**라
시그니처를 바꾸기 전에 먼저 확인할 것 (§0, §14).

"위닝"은 "회피"로 이름이 바뀌었을 뿐, 무적 판정 방식(`IsInvincibleDuringActive` 플래그 — "지금 활성
구간에 무적인 행동 중인가"로 판정하고 기술 이름으로 분기하지 않는다, §13 원칙과 동일)은 그대로 재사용
가능하다.

---

## 6. 넉백 — 삭제됨 (2026-10-06)

구 "SF2 방식"(거리로 콤보를 끊는 설계 — 1타 0.9/2타 1.3/3타 1.7/4타 사거리 밖)은 **삭제됐다.**
미러매치 격투 게임 전제 시절 설계였고, 근거로 들었던 다운 유발 기술(`어퍼컷`/`백스핀 엘보우`)도
이미 폐기된 기술이다. 기획서 전체에 "넉백"이라는 단어 자체가 없어 0929 보스전 전환 이후
재검토된 적 없이 방치돼 있던 것으로 확인됨 — 코드에 이 설계로 만들어진 부분이 있으면 제거 대상.

넉백 자체(피격 시 밀려나는 것)를 PvE 보스전에서 어떤 수치/규칙으로 다시 넣을지는 **미정** — §15 참고.

---

## 7. 경직도 — 삭제됨 (2026-09-29)

0929 기획서에서 "경직 시스템 없음"이 명시되면서 **경직도 게이지 전체가 완전히 삭제됐다.**
`ActionData.staggerValue` 필드, "잽은 경직도 안 쌓임" 같은 서술, `StunSystem` 작업(구 작업순서
§11-7)은 전부 무효다 — 관련 코드가 남아있으면 제거 대상.

히트 스턴(한 대 맞고 굳는 시간, 프레임 단위)이 별도로 필요한지는 이번 재설계에서 다시 정해야 한다 —
경직도 게이지와 묶여있던 개념이라 자동으로 남는 게 아니다. 필요 여부는 미정 (§15).

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

### 입력 키 (리얼스틸 기획서(1) §6-12 기준, 2026-09-29 전면 교체 · 2026-10-02 차지/대시/점프 입력 수신 추가)

기존 잽(D)/스트레이트(Q)/훅(A)/어퍼컷(W)/백스핀 엘보우(S)/가드/위닝(Space) 키 배치는 **전부 폐기됐다.**

| 행동 | 키 | 비고 |
|---|---|---|
| 좌우 이동 | ← → | |
| 대시(달리기) | ←← / →→ | 더블탭. `IInputSource.GetDashInput()`로 입력 수신은 됨(2026-10-02) — `RobotMover` 연결은 아직 안 함 |
| 점프 | ↑ | `IInputSource.GetJumpInput()`로 입력 수신은 됨(2026-10-02) — 점프 물리·공중 상태는 미정, §5·§15 참고 |
| 회피 (구 위닝) | Space | 무적 0.3초 / 전체 0.5초 / 자원 소모 없음 / 쿨타임 1초 / 공중 불가 |
| D (고정 기본공격) | D | 파츠 무관, 부위 파괴돼도 항상 사용 가능 |
| 왼팔 액티브 | Q 짧게/길게 | 짧게=일반판정(`leftArmSkill`) / 길게=강화판정(`leftArmSkillCharged`, 실린더 1발 소모 예정). 수치 미정 |
| 오른팔 액티브 | W 짧게/길게 | 왼팔과 동일 구조(`rightArmSkill`/`rightArmSkillCharged`) |
| 왼다리 액티브 | E | 파츠 소속. 수치 미정 |
| 오른다리 액티브 | R | 파츠 소속. 수치 미정 |
| 머리 패시브 | (자동 적용) | 입력 없음 |
| 가드 | **삭제됨** | 기동 행동에서 완전히 빠짐. 나중에 스킬로 재도입될 수 있음 (2026-09-29 확인, §15) |

`PlayerInputSource`의 기존 5개 액션 필드(`jab`/`straight`/`hook`/`uppercut`/`backspinElbow`)와
`guard`/`weaving` 필드, `ActionExecutor.IsGuarding`은 이 표 기준으로 다시 짰다 (완료, §15 참고).

#### Q/W 짧게·길게(차지) 판정 (2026-10-02 추가)

기획서 §6-11(실린더)·그래픽 §7-1에 따라 팔 스킬은 **누르고 있던 시간**으로 일반/강화를 가른다.
`PlayerInputSource.UpdateArmCharge()`가 `GetKeyDown`에 누른 시각을 기록해두고, `GetKeyUp` 시점에
`CHARGE_HOLD_SECONDS`(임시값 0.3초 — 기획서에 정확한 기준 없음, §12)와 비교해서 결정한다.

- **아직 안 한 것(연결 단계)**: 실제 실린더 보유량 확인(`CombatDataHub` 쪽 — 아직 KKH 코드에 없음, §14),
  "탄 없으면 길게 눌러도 일반판정"으로 떨어지는 진짜 폴백, 차지 중 포즈 연출(`RobotView`/CSH).
  지금은 강화판정용 에셋(`leftArmSkillCharged` 등)이 인스펙터에 비어 있으면 그냥 일반판정으로
  대체하는 임시 안전장치만 있다 — 이건 실린더 체크가 아니라 "에셋이 아직 없을 때" 처리다.
- 대시(`GetDashInput`)도 같은 이유로 더블탭 인식만 있고 `RobotMover` 이동속도 반영은 안 했다.
- 점프(`GetJumpInput`)도 입력 버퍼링만 있고 실제로 띄우는 로직은 없다 — `RobotMover`가 아직 Y축을
  전혀 다루지 않는다.

---

## 9. 디버그 도구 — 판정보다 먼저 만든다

판정을 만들기 전에 판정을 볼 수 있게 만들 것. 안 보이는 것을 디버깅하면 시간이 몇 배로 든다.

### BoxDrawer ✅ (2026-09-21 완료)

- 1단계: `OnDrawGizmos` — Scene 뷰 전용. 빌드에 안 들어감
- 2단계: `GL` 또는 반투명 쿼드로 Game 뷰에도 표시. 토글 키 제공 — 아직 안 함(필요해지면 진행)
- 색: Hit = 빨강 / Hurt = 하늘 / Push = 초록

### FrameStepper ✅ (2026-09-21 완료)

```
F2 : 일시정지 (Time.timeScale = 0 → CombatClock이 자연히 멈춘다)
F3 : CombatTick()을 수동으로 1회 호출 → 정확히 1프레임 전진
```

`CombatClock`(§3)이 있으면 F3는 `CombatTick()`을 한 번 부르는 것으로 끝난다.
로직이 `Update()`에 흩어져 있으면 이 도구 자체를 만들 수 없다 — §11에서 `CombatClock`이 먼저인 이유다.

프레임 단위로 멈춰서 보지 않으면 "훅이 3프레임에 나간다"를 검증할 방법이 없다.

**주의 (실제로 겪은 문제)**: F3로 `CombatClock.Tick()`만 부르면 논리(`ActionState`)는 전진하는데
그림(`Animator`)은 안 움직인다 — `Animator`는 `Update()`의 `Time.deltaTime`으로만 재생되는데
`Time.timeScale = 0`이면 그게 0이라 자동으로는 전혀 안 움직이기 때문이다. `RobotView.AdvanceOneTick()`
(`animator.Update(CombatClock.TICK)`)을 F3에서 같이 호출해서 그림도 정확히 1틱 밀어줘야 한다.

### ActionDataEditor ✅ (2026-09-21 추가 — 원래 계획엔 없던 도구)

`FrameBox` 좌표를 인스펙터에 숫자로 타이핑하는 대신 Scene 뷰에서 마우스로 드래그해 맞추는
커스텀 에디터. `Assets/NYH/02. Scripts/Combat/Editor/ActionDataEditor.cs` ("Editor" 폴더라 빌드 제외).
`ActionData` 에셋을 선택하면 인스펙터 하단에 미리보기/편집 UI가 뜨고, Scene 뷰에서 박스 중심을
끌면 이동, 모서리를 끌면 크기 조절이 된다.

**주의 (실제로 겪은 문제)**: `Handles`는 기본적으로 zTest가 걸려있어서 바닥(`Ground`)이나 캐릭터
스프라이트 같은 3D 지오메트리에 가려지면 그려놓고도 화면엔 안 보인다. `Handles.zTest =
CompareFunction.Always`로 강제해야 항상 맨 위에 그려진다 — Scene 뷰 커스텀 Handle 도구를 만들 때마다
걸리는 문제라 기록해둔다.

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
2. ✅ 좌우 이동 + 키 입력 시 디버그 로그
3. ✅ `CombatClock` — 1/60초 고정 틱 (§3)
4. 🔶 `ActionData` / `ActionState` / `ActionExecutor` — 행동 하나가 3구간으로 도는 것 (검증 당시엔 잽으로 만들었으나, 잽 자체는 2026-09-29 폐기 — §11-11에서 D/Q/W/E/R로 전환)
   - ✅ `ActionData` (SO), `ActionState`, `ActionExecutor`, `IInputSource` 초안 작성 완료
   - ✅ `ActionState`에 스킬 연동용 이벤트 훅(`OnActionBegin`/`OnActionActiveStart`/`OnActionEnd`) 포함 — §13 참조
   - ✅ `CombatClock`과 배선 (`PlayerRobotBootstrap.OnEnable`에서 `ExecuteTick` 구독)
   - ✅ `fighterId` 배선 (`ActionExecutor.FighterId`) — KKH `CombatDataHub` 조회 키로 씀 (§14)
   - 🗑️ (2026-09-29) ~~가드 판정 (`ActionExecutor.IsGuarding`, 방향 기반)~~ — 가드 자체가 기동 행동에서 삭제되어 이 항목 폐기. 코드 제거 대상 (§5·§8)
   - ⬜ 후딜 중 재입력이 무시되는지 실제 플레이로 반드시 확인 (`CanAcceptNewAction` 로직 자체는 구현됨)
   - ✅ (2026-10-06) `CombatDataHub.CanExecuteAction` 게이트 — `ActionExecutor.GateByPartBroken()` 추가해서 대체 여부와 무관하게 **최종적으로 Begin()에 넘어가는 모든 행동**이 이 체크를 거치게 함. CSH의 `ResolveReplacement`는 "대체된 행동"만 따로 한 번 더 체크하지만(대체 실패 시 원본으로 폴백), 그 원본도 이제 `GateByPartBroken`을 거치므로 구멍이 막힘
5. ✅ `BoxDrawer` (Gizmos) + `FrameStepper` — 판정 없이 네모만. `ActionDataEditor`(박스 드래그 편집기)도 추가 제작
6. 🔶 `HitDetection` — AABB 겹침. 선행 조건(`FrameBox`, `GetActiveBoxes`, `IsInvincibleDuringActive`)은 준비됨. `isGuarding` 관련 부분은 §5·§8 갱신에 맞춰 재검토 필요
7. 🔶 `KnockbackSystem` (`StunSystem`은 2026-09-29 경직 시스템 삭제로 작업 목록에서 제외, §7)
8. 🔶 (2026-10-06) `RuntimeRobot`(행동 잠금 목록, §5) 완성. `RobotAssembler`/`RuntimePart`/`DurabilitySystem`은 아직 ⬜ — KKH 쪽 동급 데이터(`CombatantSnapshot`/`PartRuntimeState`/`CombatantBuilder`)는 이미 있음, NYH가 할 일은 실제 장착 파츠로 조립해서 `BattleManager.InitializeBattle`에 등록하는 것 (§14). **단 §5 "플레이어 vs 보스 비대칭"이 정해지기 전까지 보스 쪽 내구도 조립 로직은 보류**
9. 🔶 (2026-10-06) `AIInputSource` 1차 버전 작성 완료(`Combat/Input/AIInputSource.cs`) — "사거리 밖이면 접근, 안이면 공격"만 하는 최소 규칙. 보스 기믹 같은 복잡한 패턴은 아직 없음. 보스용과 일반 적용을 같은 클래스로 계속 묶을지는 여전히 미정 — 지금은 구조 검증용 하나로 공용. ⬜ 씬에서 `DummyJabInputSource` → `AIInputSource` 교체 아직 안 함(사용자가 Unity에서 직접)
10. ✅ (2026-10-06 결정) CSH 연동 방식 확정 — `IUsable`/`SkillBase`는 CSH가 이미 폐기하고 "장비 효과"(`EquipmentEffectSet`/`ResolvedModifiers`) + 리졸버 주입(`SetModifierResolver`/`SetEffects`, 이미 `ActionState.cs`/`ActionExecutor.cs`에 병합됨) 체계로 교체함. §13 원안(이벤트 구독)으로 되돌리지 않고 **CSH가 만든 방식을 그대로 따라간다.** 프레임 보정 필드·`ActionTag` 매칭·`KnockbackMultiplier` 등 CSH 쪽 빈 자리는 CSH 책임 — NYH가 먼저 요구하지 않음
11. ✅ (2026-09-29) D/Q/W/E/R 액션 구조 전환 — 기존 5종 기본기 에셋·키매핑 제거 + D 슬롯 신설 + Q/W/E/R 슬롯 구조 (§3·§8). 2026-10-02: Q/W 짧게·길게(차지) 입력 수신 + 대시·점프 입력 수신까지 추가 — 전부 `PlayerInputSource`의 "입력 수신"만 완료고, 실제 물리/실린더 연결은 미완료(아래 참고)
12. ✅ (2026-10-02) 점프·대시를 `RobotMover`에 실제로 반영 — Y축 포물선 점프(`jumpVelocity`/`gravity`, 수치 전부 임시) + 대시 중 `moveSpeed` 배율(`dashSpeedMultiplier`, 수치 임시). **같은 작업 중 발견: 방향 전환이 "상대 위치 기준"(옛 SF2 미러매치 가정)으로 박혀 있던 걸 "이동 입력 방향 기준"(기획서 그래픽 §7-1 "방향 전환 | 반대 방향 입력")으로 교체함 — 할로우나이트/스컬류 보스전엔 전자가 안 맞음.** 이동 로직 자체는 여전히 `Update()`에서 돈다(`CombatTick` 이관은 별도 확인 필요 — §15).
13. ⬜ 보스 스킬의 "타격 부위" 데이터 표현 + `DurabilitySystem` 재설계 (§5·§15)
14. ⏸️ (2026-10-06 결정) 실린더 시스템 — `CombatDataHub.CanExecuteAction(fighterId, source, requiredPart, cylinderCost)` 오버로드는 이미 존재하지만, 이걸 Q/W 강화판정에 실제로 연결하는 작업은 **KKH·CSH 쪽이 알아서 진행** — NYH가 먼저 나서서 연결하지 않는다. 요청이 오면 그때 배선
15. ⬜ (2026-10-02 신규) 투사체(발사체) 스킬 아키텍처 — 로켓/앵커 너클 등 "원거리 기물" 태그 스킬은 지금 구조(캐릭터에 고정된 로컬 `FrameBox`)로 못 만듦. 설계 먼저 논의 (§15)

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

### 2026-10-06 갱신 — 아래 "이벤트 3개로만" 계획은 실제 코드와 다르다

이 절은 원래 계획(이벤트 구독 방식)을 적어둔 것이고, **실제로 CSH가 이미 구현해서 2026-09-21에
NYH 파일에 직접 병합한 방식은 이것과 다르다** — 상세 조사 결과는 `KKH_CSH_연동_현황.md` 참고.
요약: `SkillBase`(MonoBehaviour)는 이미 삭제됐고 `IUsable`은 만들어진 적이 없다. CSH는 "스킬"이 아니라
"장비 효과(Equipment Effect, SO 기반)" 체계로 선회했고, `ActionState.cs`/`ActionExecutor.cs`에
`#region 최. 추가`로 `SetModifierResolver`/`SetEffects`를 직접 추가해서 **이벤트 구독이 아니라
함수(Func) 직접 호출(pull) 방식**으로 1차 연동을 끝냈다. 아래 "이벤트 3개" 자체는 코드에 그대로
남아있고 여전히 발행되지만, **지금 구독자가 없다.**

### 결정 (2026-10-06, 남윤호) — CSH가 만든 방식 그대로 탄다

NYH 쪽에서 이벤트 구독 원안으로 되돌리자고 요구하지 않는다. CSH가 이미 넣어놓은
`SetModifierResolver`/`SetEffects`(리졸버 직접 호출) 방식을 **그대로 전제로 작업한다.** 아래
"접촉 지점 (원안 — 이벤트 3개)"과 `IUsable` 초안은 실행된 적 없는 옛 계획이므로 참고만 하고,
새 코드를 짤 때 그 모양으로 다시 맞추려 하지 않는다. `ResolvedModifiers`에 프레임 보정 필드가
없는 것, `ActionTag` 매칭이 비활성인 것, `KnockbackMultiplier`가 안 쓰이는 것 등은 CSH 쪽 작업
범위이므로 NYH가 먼저 나서서 고치거나 요구하지 않는다 — 필요해지면 CSH가 알아서 채울 자리다.

### 접촉 지점 (원안 — 이벤트 3개)

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
    if (action.ActionName != "왼팔 액티브") return;  // 어떤 기술인지 판단은 구독자 몫 (예시 이름, 실제 명칭 미정)
    if (!내스킬이켜져있음) return;                     // 켜져있는지 판단도 구독자 몫
    이펙트재생();
};
```

### ❌ 금지 — NYH 쪽 코드에 기술 이름으로 분기하는 if문

```csharp
// 금지 — ActionExecutor/ActionState는 무슨 기술인지 몰라야 한다
if (action.ActionName == "훅") { ... }
```

이런 분기가 필요해지는 순간, 그건 스킬 쪽 구독자 코드에 들어가야 할 로직이 엔진에 새어 들어온 것이다.
(참고: 지금 CSH의 `ActionModifierData.Matches`도 `ActionTag` 매칭을 주석처리해두고 `requiredPart`만
보고 있어서, 이 원칙 자체는 지켜지고 있다 — `KKH_CSH_연동_현황.md` 참고.)

### 서로 다른 구현 방식이어도 된다 — 접점은 `IUsable` 하나 (원안, 미착수)

NYH는 `ActionData`(SO)로, CSH는 `SkillBase`(MonoBehaviour 상속)로 — 서로 다른 방식을 써도 된다는
것이 원래 계획이었다. 그런데 **CSH의 `SkillBase`는 2026-09-21에 삭제됐고, `IUsable`은 양쪽 다
구현한 적이 없다.** 아래 인터페이스 초안은 참고용으로만 남겨둔다 — 지금 실제로 쓰이는 접점은
`EquipmentEffectSet`/`ResolvedModifiers`(CSH)와 `ActionState.SetModifierResolver`(NYH)다.

```csharp
public interface IUsable
{
    int StartupFrames { get; }
    int ActiveFrames  { get; }
    int RecoveryFrames { get; }
}
```

### 프레임 값 자체를 스킬이 바꿔야 할 때

`ActionData`는 불변 SO라 스킬이 직접 `startupFrames` 등을 고치면 안 된다 (§2 원칙 재확인).
**실제로는** `RuntimeRobot`이 아니라 CSH의 `ResolvedModifiers`(struct)가 이 역할을 하도록
이미 연결돼 있다 — `ActionState.Begin()`이 `modifierResolver(action)`로 매번 새로 받아서
`ResolvedAction.Resolve()`에 넘긴다. 단 `ResolvedModifiers`에는 아직 **프레임(선딜/활성/후딜) 보정
필드가 없어서**, 지금은 `BuildAdjustment()`가 항상 `FrameAdjustment.None`을 반환한다 — "선딜을
줄이는 스킬"은 CSH 쪽에 필드가 추가되기 전까지 구현 불가능하다 (`KKH_CSH_연동_현황.md` 참고).

### 구독 배선

스킬 쪽이 특정 로봇의 이벤트를 구독하려면 `ActionExecutor.State`(또는 `RuntimeRobot`을 경유한 참조)로
접근한다는 것이 원안이었다. **실제로는 CSH가 `EffectContext.State`로 이미 같은 경로를 뚫어뒀지만,
현재 유일한 구현체(`ActionModifierData.Instance`)는 `OnEquip()`에서 아무것도 구독하지 않는다** —
이벤트 구독 자체가 지금 코드베이스 어디에도 없는 상태. **(2026-10-06 결정) 통일을 NYH가 요구하지
않는다** — 리졸버 주입이 CSH가 택한 방식이므로 그대로 두고, 이벤트 3개가 나중에 다른 용도(예:
`RobotView`의 애니메이션 재생처럼 NYH 자체 구독자)로 쓰이는 건 상관없다.

---

## 14. KKH 데이터 연동 지점 (2026-09-21 작성 / 2026-10-06 전면 재확인)

KKH가 이미 만들어둔 `Assets/KKH/02.Scripts/` 쪽 API. NYH는 이 계약을 **참조만** 하고 KKH 폴더는 건드리지 않는다 (§0).
아래 표는 2026-10-06에 실제 코드(`CombatDataHub.cs`/`BattleManager.cs`/`CombatCalculator.cs`)를 grep해서
재확인한 내용이다 — 이전 버전은 `BattleManager.InitializeBattle`을 "아직 아무도 안 부름"이라고 적어뒀는데
**이미 `DummyBattleBootstrap.cs`가 부르고 있어 사실이 바뀌어 있었다.** 더 자세한 조사 과정·불일치 목록은
`KKH_CSH_연동_현황.md` 참고.

| KKH 쪽 | 하는 일 | NYH가 호출/참조하는 지점 |
|---|---|---|
| `CombatDataHub.Instance`(싱글톤) | 스탯 조회 + 판정 연산 창구 | 사용 중 |
| `CombatDataHub.CanExecuteAction(fighterId, action)` | 부위 파손 시 기술 시전 차단 | ✅ (2026-10-06) `ActionExecutor.GateByPartBroken()`이 `Begin()`에 넘어가는 모든 행동에 대해 호출 — CSH의 `ResolveReplacement`(대체 행동만 체크)와 별개로 공통 게이트 완성 |
| `CombatDataHub.ProcessHit(attackerId, defenderId, action, isGuarding, isWeaving, hitPart)` | 데미지 계산 + 이벤트 발행 | ✅ `HitDetection.cs`가 호출 중. `isGuarding`은 항상 `false` 고정(가드 삭제, §5·§15). **단, 코드상 가드 분산/치명타/경직/다운 결과 필드(`IsCritical`/`StaggerAdded`/`CausesKnockdown`)는 KKH 쪽이 대입하는 곳이 없어 항상 기본값 — 믿고 읽으면 안 됨** |
| `CombatDataHub.ProcessWeavingAttempt(fighterId)` | (문서에만 있음) 위빙 시도 처리 | ❌ **코드에 존재하지 않음.** `KKH_Integration_Guide.md`가 구버전 기준으로 적어둔 것 — 호출하면 컴파일 에러 |
| `CombatDataHub.InitializeBossBattle(player, bossSnapshot)` | 보스전 모드로 전투 등록 | ⏸️ (2026-10-06 결정) NYH 쪽에서 호출하는 곳 없음 — **KKH·CSH가 알아서 연결하도록 둔다, NYH가 먼저 나서서 부르지 않음** |
| `CombatDataHub.CanExecuteAction(fighterId, source, requiredPart, cylinderCost)` (실린더 체크 포함 오버로드) | 실린더(탄) 소모 게이트 | ⏸️ (2026-10-06 결정) NYH는 `ActionData` 오버로드(실린더 체크 없음)만 씀 — **실제 연결은 KKH·CSH 책임, NYH가 먼저 배선하지 않음** |
| `CombatDataHub.GetFinalMoveSpeed(fighterId)` 등 스탯 조회 | 실시간 스탯 공급 | ⬜ `RobotMover`가 아직 하드코딩값(`moveSpeed`) 씀 — 연결 안 함 |
| `BattleManager.InitializeBattle(playerSnapshot, enemySnapshot, ...)` / `StartBattle()` | `CombatDataHub`에 두 파이터 스탯 등록 | ✅ `DummyBattleBootstrap.cs`가 호출 중(더미 스냅샷). **단 일반 적(로봇 vs 로봇) 모드만 지원 — 보스 오버로드 없음** |
| `ActionData`의 필드 중 KKH가 실제로 읽는 것: `ActionName`/`Source`/`RequiredPart`/`Damage`/`KnockbackDistance` (딱 5개, `CombatCalculator.cs`/`CombatDataHub.cs` grep 재확인) | 계산에 그대로 씀 | 필드명 바꾸면 KKH 쪽도 깨짐, 바꾸기 전 확인 필수 |
| `IsGuardable`/`StaggerValue`/`CausesKnockdown` | (예전엔 "KKH 계약"이라 적어뒀음) | **실제로는 KKH 코드 어디서도 안 읽음 — 2026-10-06 grep으로 확인.** `ActionData.cs` 주석의 "계약" 서술은 틀렸다. 그래도 지우기 전에 KKH(김관현)에게 한 번 확인은 할 것(문서가 틀렸을 뿐 실수로 의존하는 다른 코드가 있을 가능성은 낮지만 0은 아님) |

각 로봇은 `fighterId`("Player"/"Enemy")를 들고 있어야 위 API들이 어느 쪽인지 구분한다 —
`ActionExecutor.FighterId`, `PlayerRobotBootstrap`의 인스펙터 필드로 배선됨 (§11-4).

**주의**: `CombatCalculator`의 데미지 계산식은 KKH가 이미 코드로 구현해뒀지만, §0에 적힌 대로
**아직 팀 합의로 확정된 값이 아니다.** 그대로 믿고 연동만 하되, 실제 수치가 이상하면 "우리가 고칠
문제"가 아니라 "확인해야 할 문제"로 다룰 것.

**주의 2**: KKH 쪽 연동 가이드 문서(`NYH_Integration_Guide.md`)는 2026-09-19 작성 후 갱신된 적이 없어
2026-09-29 PvE 보스전 전환 이전 버전(가드/위빙/경직 전제) 그대로다. **그 문서의 API 설명보다 이 표와
`KKH_CSH_연동_현황.md`를 우선시할 것.**

---

## 15. 2026-09-29 재설계 — 확정 사항과 미정 사항

0929 전투 시스템 개편(현재는 `Assets/NYH/07.Md/리얼스틸 기획서 (1).md` 1000.3v, 특히 901~995행
"실시간 전투 시스템" 절에 반영됨 — 과거 작업 당시 참조했던 `0929기획서.md`는 이후 삭제되고 이 파일로
통합됐다) 반영 후 남윤호가 직접 확인한 내용을 정리한다. 이 문서 여기저기 흩어진 "2026-09-29" 표시는
전부 이 절을 가리킨다.

### 확정된 것

| 항목 | 확정 내용 |
|---|---|
| 5종 기본기 | 잽/훅/스트레이트/어퍼컷/백스핀 엘보우 전부 폐기 |
| 경직 시스템 | 완전 삭제 (§7) |
| 장르 | PvE 보스전 (할로우나이트·스컬 참고). 미러매치 대전격투 아님 |
| 이동 | 점프·대시가 실제 전투에 포함됨 |
| 가드 | 기동 행동에서 삭제. 나중에 스킬로 재도입 가능성 있음 |
| 보스 파츠 | 경매장 구매로 보스 스킬 풀만 결정. 전투 중 플레이어가 보스 파츠를 파괴하는 것은 불가능 — 내구도 시스템은 플레이어 전용 |

### 아직 미정 — 임의로 정하지 말고 확인할 것

- Q/W/E/R 파츠 액티브 스킬의 프레임·데미지·쿨타임 수치와 설계 일정
- 점프 물리 자체는 `RobotMover`에 생겼지만(2026-10-02, 수치 전부 임시) 공중 상태를 `ActionPhase`에
  반영할지(공중에서 피격 시 처리, 공중 중 행동 제약 등)는 여전히 미정
- 보스 스킬이 "타격 부위"를 어떤 필드로 표현할지, 어느 쪽(NYH/KKH) 소유인지
- `RuntimeRobot`을 플레이어/보스 공용으로 쓸지, 내구도 추적 여부를 인스턴스별로 끌지
- `CombatDataHub.ProcessHit`의 `isGuarding` 인자 처리 — KKH와 협의 필요 (§0, §14)
- 히트 스턴(프레임 단위 경직)이 경직 게이지 삭제 후에도 별도로 필요한지
- 넉백 수치·규칙 (§6에서 구 SF2 방식 삭제, 2026-10-06) — PvE 보스전에서 피격 시 밀려나는 거리·조건을
  어떻게 다시 정할지 기획 확인 필요. `KnockbackSystem`(코드)은 `ActionData.KnockbackDistance`를
  그대로 적용하는 범용 로직이라 당장 깨지지 않지만, 주석이 삭제된 §6을 근거로 들고 있어 정리 필요

### 걷어낸 코드 (2026-09-29 완료)

- ✅ 잽/훅/스트레이트/어퍼컷/백스핀 엘보우/가드 `ActionData` 에셋 삭제 (`Jap`/`Hook`/`Straight`/`Uppercut`/`BackspinElbow`/`Guard.asset`)
- ✅ `PlayerInputSource`를 D/Q/W/E/R/회피 키 매핑으로 전면 교체 (구 필드 `jab`/`straight`/`hook`/`uppercut`/`backspinElbow`/`guard` 제거)
- ✅ `ActionExecutor.IsGuarding`/`UpdateGuardState()` 제거, `Init()`에서 `mover` 파라미터 제거 (`PlayerRobotBootstrap` 호출부도 갱신)
- ✅ `HitDetection`의 `isGuarding`은 이제 항상 `false` 고정 (KKH `ProcessHit` 시그니처는 안 건드림)
- ✅ `ActionPhase.Stagger` 제거 (참조 0건 확인 후 삭제)
- ⚠️ `ActionData.staggerValue`/`isGuardable` 필드는 **삭제하지 않고 남겨둠** — KKH의 `CombatCalculator.EvaluateHit`이 이 두 필드를 직접 읽는 계약이라(§0·§14) 지우면 KKH 쪽 컴파일이 깨진다. 완전히 정리하려면 KKH와 먼저 확인할 것. 코드에는 "2026-09-29 기준 무효화됨" 주석을 남겨둠
- 남은 것: `Weaving.asset`(회피로 개명 예정, 프레임 수치는 §3 표대로 갱신 필요), Battle.unity 씬의 `PlayerInputSource` 인스펙터 필드 재연결(스크립트 필드명이 바뀌어서 기존 연결이 끊어짐 — D 에셋은 아직 없으므로 프레임 수치가 나온 뒤에 연결할 것)

### 2026-10-02 업데이트 — 입력 레이어 + 점프/대시 물리 + 방향 전환 정정

- ✅ `IInputSource`에 `GetJumpInput()`/`GetDashInput()` 추가, `PlayerInputSource`에 Q/W 짧게·길게(차지,
  `CHARGE_HOLD_SECONDS` 임시 0.3초) 판정 + 대시 더블탭 판정(`DOUBLE_TAP_WINDOW_SECONDS` 임시 0.3초) 구현
- ✅ `RobotMover`에 점프(포물선, `jumpVelocity`/`gravity` 전부 임시값) + 대시 중 `moveSpeed` 배율
  (`dashSpeedMultiplier` 임시값) 반영
- 🔧 **버그 발견 및 수정**: `RobotMover`의 좌우 반전이 "상대 위치 기준"(옛 SF2 미러매치 가정,
  "뒷걸음질쳐도 상대를 계속 바라본다")으로 박혀 있었다. 리얼스틸 기획서(1) 그래픽 §7-1 "방향 전환 |
  반대 방향 입력"과 맞지 않아서 "이동 입력 방향 기준"으로 교체함 — 할로우나이트/스컬류 보스전은
  미러매치가 아니므로 상대를 강제로 계속 쳐다볼 이유가 없다. **교훈: 기존 코드가 새 설계와 충돌하지
  않는지는 "구조적으로 안 막혀 있다"만으로 판단하면 안 되고, 실제 장르 기준 문서(그래픽 섹션 등)와
  문장 단위로 대조해야 한다.**
- 남은 것: 점프/대시 수치는 전부 임시값(체감 테스트 필요). 이동 로직은 여전히 `Update()` 기반 —
  `CombatTick` 이관 여부는 별도 확인 필요. Push박스 겹침 판정이 점프로 인한 Y 변화를 전혀 안 보는 것도
  (보스 위로 뛰어넘기 등에서) 재검토 필요.

---

## 16. 작업 로그 (포트폴리오용)

날짜별 진행 상황·문제/해결 과정을 **여기 말고** `Assets/NYH/07.Md/개발일지.md`에 자세히 적는다.
이 문서(CLAUDE.md)는 "지금 지켜야 할 규칙" 중심으로 짧게 유지하고, 서사(무엇을 시도했고 왜 이렇게
바꿨는지, 삽질 과정)는 개발일지 쪽에 쌓는다. 이 문서가 바뀔 때(규칙 추가/변경)는 여기서도 날짜를 남긴다.

### 개발일지에 뭘 적어야 하는가

나중에 포트폴리오에서 "이 프로젝트에서 어떤 문제를 겪었고 어떻게 해결했는지"를 물어볼 때 바로 꺼내
쓸 수 있게 아래 형식으로 하루치씩 쌓는다:

- **날짜 + 한 줄 요약** — 오늘 뭘 했는지
- **한 일** — 만든 파일/기능 목록 (짧게)
- **문제 → 원인 → 해결** — 이게 핵심. "뭐가 안 됐는지 / 왜 그랬는지 / 어떻게 고쳤는지" 3단으로.
  겪지 않은 문제는 억지로 안 만든다 — 있었던 것만 적는다
- **설계 결정과 이유** — 여러 방법 중에 왜 이걸 골랐는지 (예: 가드를 버튼 대신 방향 판정으로 바꾼 이유)
- **다음에 할 일** — 다음 세션에 뭘 이어서 할지

코드 조각은 필요할 때만, 짧게. 전체 diff를 옮겨 적지 않는다 — 포트폴리오에서 필요한 건
"무슨 일이 있었는가"지 "정확히 몇 줄을 고쳤는가"가 아니다.
