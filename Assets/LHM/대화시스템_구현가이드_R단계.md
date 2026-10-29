# R단계. 기획서 v1000.5 반영 보수

> `대화시스템_구현가이드.md`에 이어 붙일 장. **7단계 완료 후, 8-1단계 전**에 진행한다 (2026-10-29 실제 진행 상태 기준).
> 기준: `대화시스템_단계별_로드맵.md` v8 R단계, `기획서_대사시스템.md` v5

| 순서 | 내용 | 상태 |
|---|---|---|
| R-1 | 세이브 체크섬 (JSON + 체크섬) | 작성 완료 |
| R-2 | 조건 변수 교체 (`match.*` → `hunt.*` 등) | 작성 완료 |
| R-3 | 디버그 키 개발 빌드 전용 | 작성 완료 |
| R-4 | 연출 최대 대기 시간 | **8-1단계에 포함** (8-1을 아직 적용하지 않았으므로 처음부터 넣어 작성) |

---

## R-1. 세이브 체크섬

### R-1-1. 목표

| 항목 | 내용 |
|---|---|
| 만드는 것 | 세이브 파일에 **체크섬**을 붙여, 파일이 손상되거나 손으로 수정되면 알아챈다 |
| 근거 | 기획서 8.b 세이브 파일 보호, 8.f 저장 안정성 (형식은 JSON + 체크섬으로 결정, 2026-10-29) |
| 완료 조건 | 세이브 파일의 값을 메모장으로 바꾸면 `체크섬 불일치` 경고 후 **백업**을 불러온다 |
| 교체 스크립트 | `DialogueSaveSystem.cs` (전체 교체) |
| 다른 스크립트 | 변경 없음. 바깥에서 쓰는 이름(생성자, `MainPath`, `IsBlocked`, `TryLoad`, `Save`, `DeleteAll`)은 6단계와 같다 |

### R-1-2. 적용 방법

1. `Assets/LHM/02.Scripts/Dialogue/DialogueSaveSystem.cs`를 새 파일로 **덮어쓴다**
2. 컴파일이 끝나면 Play → **Shift + F12** 한 번 (기존 세이브 삭제)
   - 6단계에서 만든 세이브에는 체크섬이 없어서 본 파일·백업 모두 "체크섬 없음"으로 거부되고, **저장 금지** 상태가 된다
   - 기획서 8.j: 업데이트 세이브 호환성은 대응하지 않는다. 개발 중 세이브는 지우고 새로 시작한다
3. 이후 F키 시나리오(6-6)를 그대로 해 본다

### R-1-3. 세이브 파일 모양

```json
{
    "checksum": "3f9a…(64자리)",
    "data": {
        "version": 1,
        "savedAt": "2026-10-29 14:40:12",
        "dialogue": { "playOrder": 3, "records": [ … ], "eventMarks": [ … ] },
        "variables": [ … ]
    }
}
```

| 바깥 칸 | 내용 |
|---|---|
| `checksum` | `data`의 내용으로 계산한 64자리 지문 |
| `data` | 6단계의 `SaveFile` 그대로 |

- 6단계 파일 내용을 `data` 안에 **그대로 넣고**, 옆에 `checksum`만 붙인 형태다. 메모장으로 열면 여전히 읽힌다

### R-1-4. 체크섬이란

```
저장할 때:  data 내용 → (계산) → "3f9a…"  → 파일에 함께 기록
읽을 때:    data 내용 → (다시 계산) → 기록된 값과 비교
            같음   → 정상
            다름   → 손상 또는 수정 → 백업으로
```

- 내용이 **한 글자라도** 바뀌면 계산 결과가 완전히 달라진다
- 계산 방법은 **SHA-256** (널리 쓰는 지문 계산 방식). 결과는 항상 64자리 16진수
- 계산할 때 내용 앞에 고정 글자(`ChecksumSalt`)를 붙인다. 내용만 고치고 인터넷의 SHA-256 계산기로 다시 계산해 넣는 쉬운 우회를 막는다

#### 무엇을 막고, 무엇을 막지 않는가 (기획서 8.a, 8.j)

| 막는 것 | 막지 않는 것 |
|---|---|
| 저장 중 강제 종료·디스크 문제로 인한 **손상** | 코드를 뜯어 고정 글자를 알아낸 뒤 계산하는 의도적 변조 |
| 메모장으로 숫자를 바꾸는 **가벼운 수정** | 메모리 변조 툴 |

- 오프라인 싱글 게임이므로 "플레이어가 쉽게 우회하지 못하게" + "손상을 알아채고 복구"가 목표다. 암호 수준의 보안은 비용 대비 효과가 낮다 (기획서 8.j)

### R-1-5. 동작 표

| 상황 | 결과 |
|---|---|
| 본 파일 정상 | 본 파일 사용 |
| 본 파일 **체크섬 불일치** / JSON 오류 / 체크섬 없음 | 노란 경고 → **백업** 사용 |
| 본 파일·백업 모두 실패 | 빨간 에러 → **저장 금지** (Shift+F12 전까지) |
| 버전이 현재보다 높음 | 빨간 에러 → 저장 금지. 백업으로 넘어가지 않음 (6단계와 같음) |
| 저장 | 체크섬 계산 → 임시 파일 → 교체 + 백업 (6단계와 같음) |

### R-1-6. 스크립트 설명 — `DialogueSaveSystem.cs` (6단계에서 바뀐 부분)

#### `SaveEnvelope` (새 클래스)

| 필드 | 설명 |
|---|---|
| `checksum` | 지문 |
| `data` | 실제 세이브 내용 (`SaveFile`) |

- "봉투"라는 뜻. 내용물(`SaveFile`)은 건드리지 않고 겉에 지문만 적는다
- 그래서 `DialogueSaveData.cs`, `Step06_DialogueTest.cs`는 바꿀 필요가 없다. 나중에 게임 세이브에 합칠 때도 `data` 부분만 옮기면 된다

#### `ComputeChecksum(SaveFile data)`

```csharp
string body = ChecksumSalt + JsonUtility.ToJson(data, false);
using (SHA256 sha = SHA256.Create())
{
    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(body));
    ...
    foreach (byte b in hash) builder.Append(b.ToString("x2"));
}
```

| 코드 | 설명 |
|---|---|
| `JsonUtility.ToJson(data, false)` | 내용을 **줄바꿈 없는** JSON 글자로. 파일에는 보기 좋게(들여쓰기) 저장하지만, 계산은 항상 같은 모양으로 해야 결과가 일정하다 |
| `ChecksumSalt + ...` | 고정 글자를 앞에 붙인다 |
| `Encoding.UTF8.GetBytes` | 글자 → 바이트. 한글도 항상 같은 바이트로 바뀌도록 UTF-8로 고정 |
| `SHA256.Create()` / `ComputeHash` | 32바이트 지문 계산. `using`은 계산이 끝나면 내부 자원을 바로 정리한다 |
| `b.ToString("x2")` | 바이트 하나를 16진수 두 글자(`3f`)로. 32바이트 → 64글자 |
| `public static` | 테스트나 다른 시스템(나중의 게임 세이브)에서도 같은 계산을 쓸 수 있게 |

- 들여쓰기·줄바꿈만 바꾼 경우는 내용이 같으므로 통과한다. 의미 있는 값이 바뀔 때만 걸린다

#### `TryRead` (변경)

| 순서 | 검사 | 실패 시 `error` |
|---|---|---|
| 1 | 파일 있음 | `파일 없음` |
| 2 | JSON → `SaveEnvelope` | `JSON 오류 (...)` |
| 3 | `data`가 있고 `version > 0` | `세이브 형식이 아닌 파일` (6단계 형식 파일, `{}` 등) |
| 4 | `checksum`이 있음 | `체크섬 없음` |
| 5 | 다시 계산한 값 == 기록된 값 | `체크섬 불일치 (파일이 손상되었거나 수정됨)` |

- `StringComparison.OrdinalIgnoreCase`: 16진수 대소문자(`3F` / `3f`) 차이는 같은 값으로 본다

#### `Save` (변경)

```csharp
data.version = CurrentVersion;
data.savedAt = ...;
var envelope = new SaveEnvelope { checksum = ComputeChecksum(data), data = data };
```

- **`savedAt`을 먼저 채운 뒤** 체크섬을 계산한다. 순서가 바뀌면 저장된 내용과 지문이 달라 다음 로드에서 항상 실패한다
- 반환값 `bool`: 저장 성공 여부. 6단계 테스트 스크립트가 반환값을 쓰지 않아도 문제없다
- 나머지(임시 파일 → `File.Replace` → 백업)는 6단계와 같다

#### 그대로인 부분

`TryLoad`, `Accept`, `Migrate`, `DeleteAll`의 흐름과 메시지는 6단계(6-9)와 같다.

### R-1-7. Play로 확인

| # | 할 일 | 확인 |
|---|---|---|
| 1 | Shift+F12 → F4 → F1 | 저장됨. F8로 폴더를 열어 파일에 `checksum`과 `data`가 있는지 확인 |
| 2 | Play 다시 | `[Save] 본 파일 불러옴` |
| 3 | 정지 → 파일의 `"playCount": 1`을 `5`로 바꾸고 저장 → Play | 노란 경고 `본 파일을 읽지 못했습니다 — 체크섬 불일치` → `백업 파일 불러옴` |
| 4 | 정지 → 파일의 들여쓰기만 바꾸고 저장 → Play | 정상 로드 (내용이 같으므로) |
| 5 | 정지 → 본 파일·`.bak` 모두 `abc`로 → Play | 빨간 에러, F6 저장 시 `저장하지 않습니다 (기존 파일 보호)` → Shift+F12로 복구 |

### R-1-8. 안 될 때

| 증상 | 원인 | 해결 |
|---|---|---|
| 교체 직후 Play하면 `체크섬 없음` + 저장 금지 | 6단계 형식 세이브가 남아 있음 (정상) | Shift+F12 |
| 저장하고 바로 다시 Play해도 매번 체크섬 불일치 | (정상이라면 발생하지 않음) 체크섬 계산 뒤에 `data`를 바꾸는 코드가 있음 | `Save()`를 고쳤다면 `savedAt` 설정이 체크섬 계산보다 **앞**인지 확인 |
| `SHA256` 관련 컴파일 에러 | `using System.Security.Cryptography;` 누락 | 파일 첫 줄들 확인 |
| `SaveEnvelope` 중복 정의 에러 | 같은 이름의 클래스가 다른 파일에 있음 | 한쪽 삭제 |

---

## R-2. 조건 변수 교체 + R-3. 디버그 키 개발 빌드 전용

### R-2-1. 목표

| 항목 | 내용 |
|---|---|
| 만드는 것 | 경기(`match.*`) 기준 변수를 새 기획서의 **토벌·랭킹·시즌·레이스** 변수로 바꾸고, 테스트 키로 난이도와 순위 구간도 바꿀 수 있게 한다 |
| 근거 | 기획서 v1000.5 r.x 대사 조건 변수, 8.b 디버그 기능 분리 |
| 완료 조건 | 4~6단계 F키 시나리오가 새 변수로 그대로 동작한다. **헬 난이도 승리**에는 전용 반응이 나온다. 배포 빌드에서는 F키가 동작하지 않는다 |
| 교체 데이터 | `variables.json`, `Pools/auction_npc.json`, `Strings/strings_ko.json` |
| 교체 스크립트 | `Step06_DialogueTest.cs` (이름은 그대로, 내용 교체) |
| 다른 스크립트 | 변경 없음. 조건 판정·선택·소비 규칙 코드는 변수 이름을 모른다 (데이터가 정한다) |

### R-2-2. 적용 순서

1. JSON 3개와 `Step06_DialogueTest.cs`를 **한꺼번에** 덮어쓴다
   - 하나만 바꾸면 변수 이름이 어긋나 F키에서 `변수 목록에 없는 변수` 에러가 난다
2. 컴파일 후 Play → **Shift + F12** (기존 세이브의 `match.*` 값이 남아 있으면 경고가 뜬다)
3. 인스펙터: `DialogueTest`의 칸 연결은 **그대로 유지**된다
   - 헤더 이름이 "디버그 토벌 보스", 칸 이름이 Season Boss / Other Boss로 바뀐다. 값(`boss.wheel`, `boss.hammer`)도 그대로 남는다 (R-2-6 `FormerlySerializedAs`)

### R-2-3. 변수 대응표

| 6단계 | R단계 | 비고 |
|---|---|---|
| `match.lastResult` | `hunt.lastResult` | none / win / lose |
| `match.lastOpponent` | `hunt.lastBoss` | 보스 ID |
| — | `hunt.lastDifficulty` | **신규**. none / veryEasy / easy / normal / hard / hell |
| — | `hunt.bossLoseCount` | **신규**. 시즌 대상 보스에게 진 횟수 (3패 후 기믹 힌트, 보스 기준) |
| `match.count` | `hunt.count` | 사건당 1회 판정용 |
| `match.winCount` / `loseCount` | `hunt.winCount` / `loseCount` | |
| — | `rank.band` | **신규**. 1 / 2-10 / 11-30 / 31-100 / 101+ |
| — | `rank.dailyChange` | **신규**. 전일 대비 구간 변화 (+ 상승) |
| — | `rank.top10Reached` | **신규**. no / yes (첫 10위권 진입) |
| — | `season.day`, `season.endingSoon` | **신규**. 시즌 일차, 종료 D-7 |
| — | `time.weekday`, `time.month` | **신규**. 요일, 월 |
| — | `race.id`, `race.stage`, `race.seasonFirst` | **신규**. 레이스, 진행 단계, 시즌 1위 |
| — | `player.usingBasicParts`, `player.coreLevel` | **신규**. 회생 루트, 코어 레벨 |
| `auction.lastResult`, `auction.settleCount` | 그대로 | |
| — | `story.chapter` | **신규**. 대사 전용 값 |

- "예/아니오" 값은 지금 변수 타입(int / enum / string)에 bool이 없어서 **`no` / `yes` enum**으로 만들었다. 오타(`ture`)는 로드 시점에 잡힌다
- 지금 테스트 키로 바꿀 수 있는 것은 토벌·순위·경매 변수뿐이다. 나머지는 실제 게임 시스템이 연결되는 9단계 이후에 값이 들어온다 (그 전에는 기본값)

### R-2-4. 대사 데이터 변경 (`auction_npc.json`)

| 대화 | 변경 |
|---|---|
| 모든 반응 대사 | 조건·`perEvent`의 `match.*` → `hunt.*` |
| `auction_after_win` | `eventGroup: "win_reaction"` 추가 |
| `auction_after_win_hell` | **신규**. 승리 + `hunt.lastDifficulty == hell`. 같은 `win_reaction` 묶음 |

- 헬 승리 반응은 조건이 2개라 일반 승리(1개)보다 **구체적** → 먼저 선택된다 (4단계 규칙)
- 같은 묶음이라 헬로 한 번 이기면 일반 승리 반응은 그 사건에 나오지 않는다 (5단계 규칙)

### R-2-5. 디버그 키

| 키 | 동작 |
|---|---|
| **1 ~ 5** | 다음 토벌 난이도 선택: 아주 쉬움 / 쉬움 / **보통(기본)** / 어려움 / 헬 |
| F1 | 시즌 보스(`boss.wheel`)에게 패배 → `hunt.bossLoseCount` +1 |
| Shift + F1 | 다른 보스에게 패배 → `bossLoseCount`는 그대로 (보스 기준) |
| F2 | 승리 |
| F3 / **Shift + F3** | 경매 유찰 / **판매** |
| F4 ~ F8 | 6단계와 같음 |
| **F9 / Shift + F9** | 순위 구간 한 칸 올리기 / 내리기. `2-10` 이상이 되면 `rank.top10Reached = yes` |
| F12 / Shift + F12 | 6단계와 같음 |

### R-2-5-1. Play로 확인

| # | 키 | 기대 결과 |
|---|---|---|
| 1 | Shift+F12 → F4 | 첫 만남 |
| 2 | F1 → F4 | 바퀴 패배 반응 (6단계와 같음) |
| 3 | 5 → F2 → F4 | **헬 승리 반응** "헬 난이도를 깼다고요?…" — 보고서에 `조건이 더 구체적 (2개 > 1개: auction_after_win)` |
| 4 | F4 | 인사 — 두 승리 반응 모두 `소비됨: 사건당 1회 — [win_reaction] hunt.count=…` |
| 5 | 3 → F2 → F4 | 보통 난이도 승리 → **일반** 승리 반응 (헬 조건 불충족) |
| 6 | F9 4번 → F5 | 로그 변수에 `rank.band=2-10`, `rank.top10Reached=yes` |
| 7 | F1 3번, Shift+F1 1번 → 로그 | `hunt.bossLoseCount=3`, `hunt.loseCount=4` |

## R-3. 디버그 키 개발 빌드 전용

### R-3-1. 무엇이 바뀌었나

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update() { ... }
#endif
```

| 코드 | 설명 |
|---|---|
| `#if ... #endif` | **전처리기 지시문**. 조건이 거짓이면 그 사이 코드가 **아예 컴파일되지 않는다** (빌드 파일에 들어가지 않음) |
| `UNITY_EDITOR` | 에디터에서 Play할 때 참 |
| `DEVELOPMENT_BUILD` | Build Settings에서 **Development Build**를 체크하고 빌드했을 때 참 |
| `\|\|` | 둘 중 하나라도 참이면 |

- 배포 빌드(Development Build 체크 해제)에서는 F키를 읽는 `Update()` 자체가 사라진다. 기획서 8.b "치트 명령은 개발 빌드에서만 컴파일"
- 키 안내 로그도 같은 조건으로 감쌌다
- `if (Debug.isDebugBuild)`처럼 **실행 중에** 막는 방식도 있지만, 그러면 코드는 빌드에 남는다. 아예 빼는 쪽이 안전하다
- 배포 빌드에서 `ready`가 쓰이지 않는다는 노란 경고(CS0414)가 뜰 수 있다. 동작에는 영향 없다

### R-3-2. 스크립트 설명 — `Step06_DialogueTest.cs` (6단계에서 바뀐 부분)

| 변경 | 설명 |
|---|---|
| `Difficulties`, `RankBands` 배열 | 난이도·순위 구간의 허용값을 `variables.json`과 **같은 글자**로 모아 둔다. `static readonly`: 모든 인스턴스가 공유하고 바꿀 수 없다 |
| `RankBands` 순서 | 낮은 순위 → 높은 순위. 배열 번호가 클수록 높은 순위라 "올리기 = +1"이 된다 |
| `[FormerlySerializedAs("mainOpponent")]` | 칸 이름을 `seasonBoss`로 바꿔도 **예전 이름으로 저장된 인스펙터 값**을 이어받는다. 없으면 값이 기본값으로 돌아간다 |
| `difficultyIndex = 2` | 기본 난이도 보통 |
| `SelectDifficulty` | 숫자키로 다음 토벌 난이도를 고른다 |
| `RecordHunt` | 6단계 `RecordMatch`의 새 이름. 난이도 기록, **시즌 보스에게 진 경우에만** `bossLoseCount` +1 |
| `RecordAuction` | 그대로. Shift+F3으로 `sold`도 보낼 수 있다 |
| `ShiftRank(step)` | 현재 구간 번호 → `Mathf.Clamp`로 범위 안에서 한 칸 이동 → `dailyChange`에 이동량, 2-10 이상이면 10위권 표시 |
| `Math.Max(0, Array.IndexOf(...))` | 저장된 값이 목록에 없으면(-1) 0번(101+)으로 본다 |
| `if (next == current) return;` | 끝에서 더 누르면 아무 일도 하지 않는다 (불필요한 저장 방지) |

- `rank.top10Reached`는 한 번 `yes`가 되면 순위가 내려가도 `no`로 돌리지 않는다. "첫 진입 여부"이기 때문이다
- 실제 게임에서는 이 값들을 **00시 랭킹 갱신**이 채운다 (11-2단계). 지금은 F9가 흉내 낸다

### R-2-6. 안 될 때

| 증상 | 원인 | 해결 |
|---|---|---|
| F1에서 `변수 목록에 없는 변수 'hunt.lastResult'` | JSON을 안 바꿨음 | `variables.json` 교체 |
| 로드 시 `변수 목록에 없는 변수 'match.lastResult'` | 대사 풀만 옛날 것 | `auction_npc.json` 교체 |
| `세이브에 있는 'match.count'는 현재 변수 목록에 없어 건너뜁니다` | 옛 세이브 | Shift+F12 (정상 동작) |
| `없는 키 'auction.after_win_hell.1'` | 문자열 테이블 미교체 | `strings_ko.json` 교체 |
| 숫자키가 반응 없음 | 키패드 숫자는 별개 | 키보드 위쪽 숫자열 사용 |
| 인스펙터의 보스 칸이 비어 있음 | 컴포넌트를 새로 붙임 | `boss.wheel` / `boss.hammer` 입력 |
