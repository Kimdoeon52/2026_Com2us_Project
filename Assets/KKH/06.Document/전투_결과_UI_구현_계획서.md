# 전투 결과 UI (BattleResultUI) 구현 계획서

## 1. 개요 및 목적
본 계획서는 리얼스틸 프로젝트의 핵심 기획 요소인 **하드코어 정산(판돈 골드, 코어 성장 EXP, 파츠 잔여 내구도 및 영구 파괴)**을 플레이어에게 극적으로 전달하는 전투 피날레 화면인 **`BattleResultUI`**의 설계, 연출 및 **유니티 UI 오브젝트 생성 경로, 부착 컴포넌트, 세부 인스펙터 설정 규격**을 정의한다.

백엔드 정산기(`BattleSettlementProcessor`)가 산출한 `BattleSettlementResult` 데이터를 정적 이벤트(`BattleManager.OnBattleFinished`)를 통해 수신하여, 승리/패배에 맞춘 역동적인 비주얼 연출과 정보를 표시한다.

---

## 2. 연동 데이터 및 인터페이스 계약

### 2.1 이벤트 소스 (`BattleManager.cs`)
```csharp
public static event Action<bool, string, BattleSettlementResult> OnBattleFinished;
```

### 2.2 입력 데이터 DTO (`BattleSettlementResult.cs`)
| 필드명 | 타입 | 기획 및 화면 표시 역할 |
|---|---|---|
| `PlayerWon` | `bool` | 승리(VICTORY - 네온 골드/시안) / 패배(DEFEAT - 크림슨 레드/노이즈) 분기 |
| `GoldDelta` | `int` | 판돈 골드 획득/차감 (+100G 또는 -100G) 수치 롤링 표시 |
| `CoreExpGained` | `int` | 코어 획득 EXP 수치 및 코어 레벨업 게이지 상승 애니메이션 |
| `CoreUnstable` | `bool` | 패배 시 코어 방전 및 불안정(Unstable) 경고 뱃지 |
| `FinalPartDurabilities` | `Dictionary<string, int>` | 머리, 코어, 왼팔, 오른팔, 다리 5개 부위의 최종 잔여 내구도 표시 (승리 시 파손 부위는 1로 응급 복구) |
| `DestroyedPartIDs` | `List<string>` | 패배 시 하드코어 확률로 영구 파괴(소멸)된 부품 ID 목록 (붉은 해골 경고) |
| `SummaryMessage` | `string` | 정산 결과 한 줄 요약 텍스트 |

---

## 3. UI/UX 화면 구성 및 시퀀스 연출

### 3.1 화면 레이아웃 와이어프레임
```
+-------------------------------------------------------------+
|                        [ DARK DIMMED ]                      |
|                                                             |
|                ★ ★ ★   V I C T O R Y   ★ ★ ★               |
|                    "정크 휠러 격파 완료!"                     |
|                                                             |
|  +---------------------------+  +------------------------+  |
|  |     [ 보상 및 성장 ]      |  |   [ 하드코어 파츠 상태 ] |  |
|  |                           |  |                        |  |
|  | • 획득 골드: +150 G       |  |  [머리] [코어] [왼팔]   |  |
|  | • 코어 EXP:  +85 EXP      |  |  [오른팔] [다리]        |  |
|  |   [=========>     ] Lv.4  |  |                        |  |
|  +---------------------------+  | ※ 영구 파괴 부품 없음   |  |
|                                 +------------------------+  |
|                                                             |
|          [  정비소(거점)로 복귀  ]     [  재 도 전  ]        |
+-------------------------------------------------------------+
```

### 3.2 단계별 연출 타임라인 (DOTween 기반)
1. **[Phase 1] 0.0s ~ 0.3s (화면 암전 및 배너 강타)**:
   - 배경 딤드(CanvasGroup Alpha: 0 ➔ 1) 페이드인.
   - `VICTORY` / `DEFEAT` 배너가 화면 중앙에 도장 찍히듯 스케일 펀치(`DOPunchScale`).
2. **[Phase 2] 0.3s ~ 0.9s (보상 숫자 카운팅 롤링)**:
   - 획득 골드 및 코어 EXP 수치가 `0`에서 목표 수치까지 빠르게 카운팅 롤링(`DOVirtual.Int`).
   - 코어 경험치 슬라이더 게이지가 전진 애니메이션.
3. **[Phase 3] 0.9s ~ 1.5s (파츠 상태 및 영구 파괴 슬롯 공개)**:
   - 5개 부위 파츠의 잔여 내구도 슬롯이 좌측부터 순차 등장(`DOFade`).
   - 패배 시 영구 파괴 부품이 존재할 경우 붉은색 경고 점멸 연출.
4. **[Phase 4] 1.5s 이후 (인터랙션 버튼 활성화)**:
   - `[기지로 귀환]` 버튼이 팝업되며 클릭 가능 상태로 전환 (재도전은 기획 변경으로 제거).

---

## 4. 유니티 오브젝트 생성 경로 및 컴포넌트 구조 (Hierarchy Recipe)

유니티 Hierarchy 창에서 우클릭 메뉴(`UI > ...` 또는 `Create Empty`)를 통해 생성해야 하는 경로와 부착할 컴포넌트 목록:

```text
Canvas (우클릭 > UI > Canvas)
[컴포넌트: Canvas, Canvas Scaler, Graphic Raycaster]
└── BattleResultPanel (우클릭 > Create Empty)
    [컴포넌트: RectTransform, Canvas Group, BattleResultUI (스크립트)]
      ├── DimmedBackground (우클릭 > UI > Image)
      │   [컴포넌트: RectTransform, Image]
      └── WindowFrame (우클릭 > UI > Image)
          [컴포넌트: RectTransform, Image, Outline (선택)]
            ├── HeaderGroup (우클릭 > Create Empty)
            │   [컴포넌트: RectTransform]
            │     ├── BannerBackground (우클릭 > UI > Image)
            │     │   [컴포넌트: RectTransform, Image]
            │     ├── TitleText (우클릭 > UI > Text - TextMeshPro)
            │     │   [컴포넌트: RectTransform, TextMeshPro - Text (UI)]
            │     └── SubtitleText (우클릭 > UI > Text - TextMeshPro)
            │         [컴포넌트: RectTransform, TextMeshPro - Text (UI)]
            ├── ContentGroup (우클릭 > Create Empty)
            │   [컴포넌트: RectTransform]
            │     ├── LeftSection_Rewards (우클릭 > Create Empty)
            │     │   [컴포넌트: RectTransform]
            │     │     ├── SectionHeader (우클릭 > UI > Text - TextMeshPro)
            │     │     │   [컴포넌트: TextMeshPro - Text (UI)]
            │     │     ├── DividerLine (우클릭 > UI > Image)
            │     │     │   [컴포넌트: Image]
            │     │     ├── GoldRow (우클릭 > Create Empty)
            │     │     │   [컴포넌트: RectTransform, Horizontal Layout Group]
            │     │     │     ├── GoldLabel (우클릭 > UI > Text - TextMeshPro)
            │     │     │     └── GoldValueText (우클릭 > UI > Text - TextMeshPro)
            │     │     ├── ExpRow (우클릭 > Create Empty)
            │     │     │   [컴포넌트: RectTransform, Horizontal Layout Group]
            │     │     │     ├── ExpLabel (우클릭 > UI > Text - TextMeshPro)
            │     │     │     └── ExpValueText (우클릭 > UI > Text - TextMeshPro)
            │     │     ├── CoreExpSlider (우클릭 > UI > Slider)
            │     │     │   [컴포넌트: RectTransform, Slider]
            │     │     └── CoreStatusText (우클릭 > UI > Text - TextMeshPro)
            │     │         [컴포넌트: TextMeshPro - Text (UI)]
            │     └── RightSection_HardcoreParts (우클릭 > Create Empty)
            │         [컴포넌트: RectTransform]
            │           ├── SectionHeader (우클릭 > UI > Text - TextMeshPro)
            │           │   [컴포넌트: TextMeshPro - Text (UI)]
            │           ├── DividerLine (우클릭 > UI > Image)
            │           │   [컴포넌트: Image]
            │           ├── PartSlotContainer (우클릭 > Create Empty)
            │           │   [컴포넌트: RectTransform, Grid Layout Group]
            │           └── WarningGroup (우클릭 > UI > Image)
            │               [컴포넌트: RectTransform, Image]
            │                 └── DestroyedWarningText (우클릭 > UI > Text - TextMeshPro)
            │                     [컴포넌트: TextMeshPro - Text (UI)]
            └── ButtonGroup (우클릭 > Create Empty)
                [컴포넌트: RectTransform]
                  └── Btn_ReturnBase (우클릭 > UI > Button - TextMeshPro)
                      [컴포넌트: RectTransform, Image, Button, 자식 Text (TMP)]
```

---

## 5. UI 오브젝트별 세부 생성 및 인스펙터 설정 레시피

### 5.1 캔버스 및 루트 패널
| 오브젝트명 | 유니티 생성 경로 (Menu Path) | 필수 부착 컴포넌트 (Add Component) | 세부 프로퍼티 설정값 |
|---|---|---|---|
| **Canvas** | Hierarchy 우클릭 ➔ `UI > Canvas` | `Canvas`<br>`CanvasScaler`<br>`GraphicRaycaster` | • Render Mode: `Screen Space - Overlay`<br>• Sort Order: `100`<br>• UI Scale Mode: `Scale With Screen Size`<br>• Reference Resolution: `1920 x 1080`<br>• Match: `0.5` (Width-Height 균등) |
| **BattleResultPanel** | Canvas 우클릭 ➔ `Create Empty` | `RectTransform`<br>`CanvasGroup`<br>`BattleResultUI` | • Anchor: `Stretch - Stretch` (Min: 0, 0 / Max: 1, 1)<br>• Left/Top/Right/Bottom: `0, 0, 0, 0`<br>• Pivot: `(0.5, 0.5)`<br>• CanvasGroup: Alpha `1.0`, Blocks Raycasts `true`<br>• BattleResultUI 스크립트 연결 |

### 5.2 배경 암전 및 윈도우 프레임
| 오브젝트명 | 유니티 생성 경로 (Menu Path) | 필수 부착 컴포넌트 (Add Component) | 세부 프로퍼티 설정값 |
|---|---|---|---|
| **DimmedBackground** | Panel 우클릭 ➔ `UI > Image` | `RectTransform`<br>`Image` | • Anchor: `Stretch - Stretch` (0,0 ~ 1,1)<br>• Left/Top/Right/Bottom: `0, 0, 0, 0`<br>• Image Color: `#000000` (검정), Alpha: `220` (`#000000DC`)<br>• Raycast Target: `true` (배경 클릭 차단) |
| **WindowFrame** | Panel 우클릭 ➔ `UI > Image` | `RectTransform`<br>`Image`<br>`Outline` (선택) | • Anchor: `Middle - Center`<br>• Width: `1000`, Height: `680`, PosX: `0`, PosY: `0`<br>• Image Color: `#12161E` (다크 메탈 네이비)<br>• Outline Effect Color: `#283648`, Distance: `(2, -2)` |

### 5.3 상단 배너 그룹 (`HeaderGroup`)
| 오브젝트명 | 유니티 생성 경로 (Menu Path) | 필수 부착 컴포넌트 (Add Component) | 세부 프로퍼티 설정값 |
|---|---|---|---|
| **HeaderGroup** | WindowFrame 우클릭 ➔ `Create Empty` | `RectTransform` | • Anchor: `Top - Center`<br>• Width: `900`, Height: `130`, PosX: `0`, PosY: `-75` |
| **BannerBackground** | HeaderGroup 우클릭 ➔ `UI > Image` | `RectTransform`<br>`Image` | • Anchor: `Middle - Center`<br>• Width: `820`, Height: `95`<br>• Image Color: `#1AF3B3` (승리용) 또는 `#FF3838` (패배용), Alpha: `60` |
| **TitleText** | HeaderGroup 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Anchor: `Top - Center`<br>• Width: `800`, Height: `65`, PosY: `-10`<br>• Font Size: `56`, Font Style: `Bold`<br>• Alignment: `Center/Middle`<br>• Text: `"VICTORY"` (초기값)<br>• Color: `#1AF3B3` (네온 민트) |
| **SubtitleText** | HeaderGroup 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Anchor: `Bottom - Center`<br>• Width: `800`, Height: `35`, PosY: `15`<br>• Font Size: `18`, Alignment: `Center/Middle`<br>• Color: `#A6B8CA`, Text: `"작전 성공 — 목표 대상을 완파했습니다."` |

### 5.4 좌측 보상 및 성장 섹션 (`LeftSection_Rewards`)
| 오브젝트명 | 유니티 생성 경로 (Menu Path) | 필수 부착 컴포넌트 (Add Component) | 세부 프로퍼티 설정값 |
|---|---|---|---|
| **LeftSection_Rewards** | ContentGroup 우클릭 ➔ `Create Empty` | `RectTransform` | • Anchor: `Middle - Left`<br>• Width: `430`, Height: `340`, PosX: `225`, PosY: `-20` |
| **SectionHeader** | LeftSection 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Font Size: `22`, Font Style: `Bold`<br>• Color: `#E0ECF8`, Text: `"보상 및 코어 성장"` |
| **DividerLine** | LeftSection 우클릭 ➔ `UI > Image` | `Image` | • Width: `410`, Height: `2`, Color: `#2A3644` |
| **GoldRow** | LeftSection 우클릭 ➔ `Create Empty` | `RectTransform`<br>`HorizontalLayoutGroup` | • Width: `410`, Height: `45`<br>• Child Alignment: `Middle Left`, Spacing: `10` |
| **GoldLabel** | GoldRow 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Font Size: `18`, Color: `#B0C0D0`, Text: `"획득 판돈 :"` |
| **GoldValueText** | GoldRow 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Font Size: `28`, Font Style: `Bold`<br>• Color: `#FFD700` (골드), Text: `"+150 G"` |
| **ExpRow** | LeftSection 우클릭 ➔ `Create Empty` | `RectTransform`<br>`HorizontalLayoutGroup` | • Width: `410`, Height: `45`<br>• Child Alignment: `Middle Left`, Spacing: `10` |
| **ExpLabel** | ExpRow 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Font Size: `18`, Color: `#B0C0D0`, Text: `"코어 경험치 :"` |
| **ExpValueText** | ExpRow 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Font Size: `26`, Font Style: `Bold`<br>• Color: `#00E5FF` (시안), Text: `"+120 EXP"` |
| **CoreExpSlider** | LeftSection 우클릭 ➔ `UI > Slider` | `Slider`<br>(*Handle 자식은 Delete 삭제) | • Width: `390`, Height: `22`<br>• Interactable: `false`, Transition: `None`<br>• Background Image Color: `#151B24`<br>• Fill Area/Fill Image Color: `#00E5FF` |
| **CoreStatusText** | LeftSection 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Font Size: `17`, Font Style: `Bold`<br>• Text: `"● 코어 정상 가동 (Stable)"` |

### 5.5 우측 하드코어 파츠 섹션 (`RightSection_HardcoreParts`)
| 오브젝트명 | 유니티 생성 경로 (Menu Path) | 필수 부착 컴포넌트 (Add Component) | 세부 프로퍼티 설정값 |
|---|---|---|---|
| **RightSection_HardcoreParts** | ContentGroup 우클릭 ➔ `Create Empty` | `RectTransform` | • Anchor: `Middle - Right`<br>• Width: `450`, Height: `340`, PosX: `-225`, PosY: `-20` |
| **SectionHeader** | RightSection 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Font Size: `22`, Font Style: `Bold`<br>• Color: `#E0ECF8`, Text: `"부품 내구도 & 하드코어 정산"` |
| **DividerLine** | RightSection 우클릭 ➔ `UI > Image` | `Image` | • Width: `430`, Height: `2`, Color: `#2A3644` |
| **PartSlotContainer** | RightSection 우클릭 ➔ `Create Empty` | `RectTransform`<br>`GridLayoutGroup` | • Width: `430`, Height: `160`<br>• Cell Size: `(210, 46)`, Spacing: `(10, 8)`<br>• Constraint: `Fixed Column Count = 2` |
| **WarningGroup** | RightSection 우클릭 ➔ `UI > Image` | `RectTransform`<br>`Image` | • Width: `430`, Height: `65`<br>• Image Color: `#381010` (다크 레드 경고창) |
| **DestroyedWarningText** | WarningGroup 우클릭 ➔ `UI > Text - TextMeshPro` | `TextMeshProUGUI` | • Anchor: `Stretch - Stretch`<br>• Font Size: `15`, Font Style: `Bold`<br>• Color: `#FF4D4D`, Alignment: `Center/Middle`<br>• Wrapping: `Enabled` |

### 5.6 하단 인터랙션 버튼 (`ButtonGroup`)
| 오브젝트명 | 유니티 생성 경로 (Menu Path) | 필수 부착 컴포넌트 (Add Component) | 세부 프로퍼티 설정값 |
|---|---|---|---|
| **ButtonGroup** | WindowFrame 우클릭 ➔ `Create Empty` | `RectTransform` | • Anchor: `Bottom - Center`, PosY: `45`<br>• Width: `400`, Height: `65` |
| **Btn_ReturnBase** | ButtonGroup 우클릭 ➔ `UI > Button - TextMeshPro` | `Button`<br>`Image`<br>자식 `TextMeshProUGUI` | • Anchor: `Middle - Center`, Width: `320`, Height: `52`<br>• Normal Color: `#233244`, Highlighted: `#33465E`<br>• 자식 Text: `"기지로 귀환"`, Font Size: `22`, Bold |

---

## 6. `BattleResultUI.cs` 인스펙터 필드 바인딩 매핑

`BattleResultPanel`에 붙어 있는 `BattleResultUI` 컴포넌트의 인스펙터 필드에 드래그하여 연결할 대상 오브젝트:

| 인스펙터 필드명 (`BattleResultUI`) | 연결할 대상 Hierarchy 오브젝트 | 필수 컴포넌트 타입 |
|---|---|---|
| `resultPanelRoot` | `BattleResultPanel` | `GameObject` |
| `mainCanvasGroup` | `BattleResultPanel` | `CanvasGroup` |
| `titleText` | `TitleText` | `TextMeshProUGUI` |
| `subtitleText` | `SubtitleText` | `TextMeshProUGUI` |
| `bannerBackgroundImage` | `BannerBackground` | `Image` |
| `goldValueText` | `GoldValueText` | `TextMeshProUGUI` |
| `expValueText` | `ExpValueText` | `TextMeshProUGUI` |
| `coreExpSlider` | `CoreExpSlider` | `Slider` |
| `coreStatusText` | `CoreStatusText` | `TextMeshProUGUI` |
| `partSlotContainer` | `PartSlotContainer` | `Transform` |
| `partSlotPrefab` | `PartSlotItem.prefab` (아래 7장 참조) | `GameObject` (프리팹) |
| `destroyedWarningText` | `DestroyedWarningText` | `TextMeshProUGUI` |
| `destroyedWarningPanel` | `WarningGroup` | `GameObject` |
| `btnReturnBase` | `Btn_ReturnBase` | `Button` |
| `lobbySceneName` | `"Lobby"` | `string` |

---

## 7. 부품 슬롯 템플릿 프리팹 제작 규격 (`PartSlotItem.prefab`)

부품 슬롯은 `PartSlotContainer` 하위에 동적으로 생성되는 개별 파츠 내구도 카드입니다.

* **제작 방법**:
  1. Hierarchy에서 우클릭 ➔ `Create Empty` 선택 후 이름을 `PartSlotItem`으로 변경.
  2. `Add Component` ➔ `Image` 추가:
     - Color: `#161D26` (어두운 메탈 그레이 슬롯 배경)
     - Width: `210`, Height: `46`
  3. `Add Component` ➔ `Horizontal Layout Group` 추가:
     - Padding: `Left 12, Right 12, Top 0, Bottom 0`
     - Child Alignment: `Middle Left`
     - Child Force Expand: `Width/Height 둘 다 OFF`
  4. 자식 1 생성: `PartSlotItem` 우클릭 ➔ `UI > Text - TextMeshPro` (이름: `PartNameText`)
     - Width: `95`, Height: `30`
     - Font Size: `16`, Color: `#9BB0C4`, Text: `"HEAD"`
  5. 자식 2 생성: `PartSlotItem` 우클릭 ➔ `UI > Text - TextMeshPro` (이름: `PartDurabilityText`)
     - Width: `85`, Height: `30`
     - Font Size: `18`, Font Style: `Bold`, Alignment: `Right/Middle`
     - Text: `"35/35"`
  6. 완성된 `PartSlotItem` 오브젝트를 `Assets/KKH/04.Prefab/` 폴더로 드래그하여 프리팹화 후 씬의 오브젝트는 삭제.

---

## 8. 실기 검증 절차 (`Battle_Test_KKH.unity`)

1. **에디터 플레이 모드 실행**:
   - `Assets/KKH/01.Scene/Battle_Test_KKH.unity` 씬 실행.
2. **원클릭 결과창 호출 검증**:
   - **`F5`** 입력 (또는 OnGUI `[F5] 승리 결과 UI 테스트` 클릭):
     - 네온 민트빛 `VICTORY` 배너 강타 연출 확인
     - 판돈 `+150 G`, 코어 경험치 `+120 EXP` 실시간 롤링 확인
     - 5개 부위 파츠 정상 내구도 및 영구 파괴 없음 확인
   - **`F6`** 입력 (또는 OnGUI `[F6] 패배 결과 UI 테스트` 클릭):
     - 크림슨 레드 `DEFEAT` 배너 연출 확인
     - 판돈 `-100 G`, 코어 경험치 `+20 EXP` 확인
     - 코어 상태 `<color=red>● 코어 불안정 (Unstable)</color>` 확인
     - 붉은색 경고 패널: `[경고] 영구 파손된 부품 소멸: PART_ARM_L_01` 정상 노출 확인
3. **버튼 인터랙션 검증**:
   - `[기지로 귀환]` 클릭 시 콘솔 안내 및 거점(Lobby) 씬 전환 정상 작동 확인.
