# 전투 결과 & 일일 결산 UI (`BattleResultUI` - Daily Report 스타일) 개편 구현 가이드

## 1. 개요 및 개편 방향
본 문서는 기존의 좌/우 2단 분할형 결과창을 레퍼런스 시안(**DAILY REPORT / 다크 택티컬 리포트 카드 스타일**)에 맞춰 전면 개편하기 위한 **에셋 파일명 규격, Unity Hierarchy 오브젝트 구조, 인스펙터 수치 레시피, 그리고 `BattleResultUI.cs` 바인딩 설계**를 정의한다.

---

## 2. 화면 구성 요소 분해 및 에셋/오브젝트 파일명 정리

레퍼런스 이미지를 구성하는 모든 시각 요소를 **기존 프로젝트 에셋(`Assets/Sprites/UI/`), 프리팹(Prefab), 스크립트(Script), Hierarchy 오브젝트명**으로 일대일 매칭한 실제 에셋 매핑 규격이다.

### 2.1 실제 UI 스프라이트 매핑표 (`Assets/Sprites/UI/` 기반)

> [!IMPORTANT]
> 신규 임의 경로 에셋 대신, 현재 프로젝트의 공식 UI 라이브러리인 `Assets/Sprites/UI/` 경로 내 에셋을 100% 활용하도록 일대일 대체 매핑 완료.

| 레퍼런스 시각 요소 | 실제 에셋 경로 (`Assets/Sprites/UI/...`) | 규격(해상도) | Image Type 및 셋팅 가이드 |
|---|---|---|---|
| **외곽 노란색 꺾쇠 (`ㄱ`, `┘`)** | `Frame/Corner/corner_v01.png` | 340 x 340 | • **Image Type**: `Simple`<br>• **우상단(`TR`)**: `Rotation Z = -90` (또는 Scale X = -1)<br>• **우하단(`BR`)**: `Rotation Z = 180` (또는 Scale X=-1, Y=-1)<br>• **Color**: 기본 택티컬 옐로우 (`#FFE800`) 내장 |
| **메인 리포트 카드 배경** | `BG/Panel/bg_panelLarge.png` | 1480 x 1620 | • **Image Type**: `Sliced` (또는 `Simple`)<br>• 다크 차콜 (`#232426`) 배경 패널 |
| **메인 리포트 외곽 프레임** | `Frame/Panel/panelLarge/frame_panelLarge_v1.png` (또는 `v2`) | 1480 x 1620 | • **Image Type**: `Sliced`<br>• `bg_panelLarge` 위에 오버레이하여 택티컬 보더 라인 연출 |
| **헤더 다이아몬드 아이콘 (`◆`)** | `BG/Swatch/swatch_v1.png` | 120 x 120 | • **Image Type**: `Simple`<br>• **Size**: 12 x 12 px, **Rotation Z**: `45`<br>• **Color**: 기본 노란색 내장 (`#FFE800`) |
| **헤더 리포트 데코 아이콘 (선택)** | `Icon/Material/ic_assessment.png` | 96 x 96 | • 리포트/차트 아이콘 (타이틀 좌측 장식) |
| **헤더 캘린더 아이콘 (선택)** | `Icon/Material/ic_calendar_today.png` | 96 x 96 | • DAY 결산 텍스트 앞 캘린더 아이콘 |
| **지표 행 베이스 카드 (선택)** | `BG/Button/selectItem/bg_selectItem_enabled.png` | 64 x 64 | • **Image Type**: `Sliced`<br>• 각 지표 행 호버 또는 기본 다크 배경 `#1A1B1D` |
| **행 구분선 (1px Divider)** | `BG/Swatch/swatch_v2.png` (또는 `v3`) | 120 x 120 | • **Image Type**: `Simple`<br>• Height `1px`로 지정하여 다크 그레이 구분선 표현 |
| **하단 브리핑 컬러 바 (`┃`)** | `BG/Swatch/swatch_v1.png` | 120 x 120 | • **Image Type**: `Simple`<br>• Width `4px`, Height `36px`<br>• **Image Color**: 레드(`#EF4444`), 오렌지(`#F97316`), 시안(`#06B6D4`) 틴트 적용 |
| **우하단 스탬프/바코드 박스** | `BG/Panel/bg_panelSmall.png` (또는 `Frame/Panel/panelMedium/frame_panelMedium_v1.png`) | 480 x 480 | • **Image Type**: `Sliced`<br>• Size `390 x 110`, 우하단 리포트 스탬프 컨테이너 |
| **전체 화면 클릭 진행 버튼** | `BG/Button/button/bg_button_primary_enabled.png` (선택 버튼 필요 시) | 360 x 120 | • 결과 확인/다음 날 진행 전용 버튼 필요 시 활용 |

### 2.2 프로젝트 프리팹 및 스크립트 규격

| 분류 | 권장 경로 (`Assets/KKH/...`) | 파일명 (File Name) | 역할 및 설명 |
|---|---|---|---|
| **메인 프리팹** | `04.Prefab/UI/` | `BattleResultCanvas.prefab` | 결과 UI 전체를 담는 최상위 Canvas 프리팹 |
| **행(Row) 프리팹** | `04.Prefab/UI/` | `UI_ReportMetricRow.prefab` | 상단 4개 지표 행 (수입 / 지출 / 기체 마모 / 활동 시간) 재사용 템플릿 |
| **하단 알림 프리팹** | `04.Prefab/UI/` | `UI_ReportBriefingItem.prefab` | 하단 3개 컬러 바 브리핑 항목 (`D-2`, `OMEN`, `SHOP` 등) 템플릿 |
| **컨트롤러 스크립트** | `02.Scripts/05.UI/` | `BattleResultUI.cs` | 정산 데이터 매핑 및 DOTween 롤링 연출 제어 |
| **데이터 DTO** | `02.Scripts/03.Logic/` | `BattleSettlementProcessor.cs` | 정산 결과 데이터 구조체 (`BattleSettlementResult`) |

---

### 2.2 레퍼런스 UI 영역별 오브젝트 매핑표

```
+-----------------------------------------------------------------------------------+
|                                [ Bg_WatermarkText ]                               |
|  +-----------------------------------------------------------------------------+ ┐|
|  | ◆ DAILY REPORT     DAY 12 결산                                  (HeaderBar) | ||
|  +-----------------------------------------------------------------------------+  |
|  | INCOME                                                                      |  |
|  | 수입                                                                +1,200  |  |
|  | --------------------------------------------------------------------------- |  |
|  | EXPENSE                                                                     |  |
|  | 지출                                                                  -350  |  |
|  | --------------------------------------------------------------------------- |  |
|  | WEAR                                                                        |  |
|  | 기체 마모                                                              +6%  |  |
|  | --------------------------------------------------------------------------- |  |
|  | ACTIVITY                                                                    |  |
|  | 활동 시간                                                          20H 00M  |  |
|  |                                                                             |  |
|  | TODAY · DAY 13                                                              |  |
|  | ┃ D-2      3부 리그 2라운드 · 경기장                                        |  |
|  | ┃ OMEN     고물상: 회로 부품 무더기 · 재료 +28%                             |  |
|  | ┃ SHOP     상점 재입고 (오전 9시)                                           |  |
|  +-----------------------------------------------------------------------------+ ┘|
|                                                        +------------------------+ |
|                                                        | REPORT NO.     DR-0012 | |
|                                                        | |||||||||||||||||||||| | |
|  화면 아무 곳이나 눌러 계속...                         | AUTO SAVE // DAY 13    | |
+-----------------------------------------------------------------------------------+
```

| 레퍼런스 화면 요소 | Hierarchy 오브젝트명 | 텍스트/이미지 초기값 예시 | 컬러 코드 (HEX) |
|---|---|---|---|
| **배경 암전 + 전체 클릭 영역** | `DimmedBackground` | 단색 Image + `Button` (전체 화면 클릭 진행) | `#0B0D10` (Alpha `235`) |
| **상단 배경 워터마크** | `Txt_BgWatermark` | `"DAY"` (또는 `"REPORT"`, Outline 전용) | `#FFFFFF` (Alpha `12`) |
| **메인 리포트 카드 프레임** | `ReportWindowFrame` | 다크 차콜 패널 + 1px 테두리 | 배경 `#121417` / 선 `#282C34` |
| **우상단/우하단 노란 꺾쇠** | `Img_CornerBracket_TR`, `BR` | `ㄱ`, `┘` 모양 코너 장식 | `#EAB308` (택티컬 옐로우) |
| **상단 헤더 바** | `HeaderBar` | 상단 어두운 타이틀 스트립 | `#090A0C` |
| **헤더 마름모 아이콘** | `Img_HeaderDiamond` | `◆` (회전된 정사각형 Image 가능) | `#FACC15` (네온 옐로우) |
| **헤더 영문 타이틀** | `Txt_HeaderTitle` | `"DAILY REPORT"` (전투 시 `"BATTLE REPORT"`) | `#F3F4F6` (화이트 그레이) |
| **헤더 한글 서브타이틀** | `Txt_HeaderSubtitle` | `"DAY 12 결산"` (또는 `"전투 정산 결과"`) | `#9CA3AF` (뮤트 그레이) |
| **지표 1: 수입 (판돈 획득)** | `Row_Income` | `"INCOME"` / `"수입"` / `"+1,200"` | 수치: `#34D399` (에메랄드 그린) |
| **지표 2: 지출 (수리/판돈손실)** | `Row_Expense` | `"EXPENSE"` / `"지출"` / `"-350"` | 수치: `#F87171` (코랄 레드) |
| **지표 3: 기체 마모 (내구도 손상)**| `Row_Wear` | `"WEAR"` / `"기체 마모"` / `"+6%"` | 수치: `#FB923C` (앰버 오렌지) |
| **지표 4: 활동 시간 (전투 시간)** | `Row_Activity` | `"ACTIVITY"` / `"활동 시간"` / `"20H 00M"` | 수치: `#F9FAFB` (퓨어 화이트) |
| **하단 섹션 소제목** | `Txt_BriefingHeader` | `"TODAY · DAY 13"` | `#EAB308` (택티컬 옐로우) |
| **브리핑 항목 1 (일정/경고)** | `BriefingItem_1_Schedule` | 좌측 바 + `"D-2"` + `"3부 리그 2라운드 · 경기장"` | 좌측 바: `#EF4444` (레드) |
| **브리핑 항목 2 (징조/파츠)** | `BriefingItem_2_Omen` | 좌측 바 + `"OMEN"` + `"고물상: 회로 부품 무더기 · 재료 +28%"` | 좌측 바: `#F97316` (오렌지) |
| **브리핑 항목 3 (상점/코어)** | `BriefingItem_3_Shop` | 좌측 바 + `"SHOP"` + `"상점 재입고 (오전 9시)"` | 좌측 바: `#06B6D4` (시안 블루) |
| **우하단 바코드 박스** | `BarcodeStampBox` | `"REPORT NO. DR-0012"` / 바코드 / `"AUTO SAVE..."` | 테두리 `#374151` / 텍스트 `#9CA3AF` |
| **좌하단 계속 안내 문구** | `Txt_ContinuePrompt` | `"화면 아무 곳이나 눌러 계속..."` | `#4ADE80` 또는 `#9CA3AF` (점멸) |

---

## 3. Unity Hierarchy 전체 트리 구조 (생成 레시피)

유니티 에디터에서 그대로 따라 만들 수 있는 계층 구조다.

```text
BattleResultCanvas (Canvas, CanvasScaler, GraphicRaycaster)
└── BattleResultPanel (RectTransform, CanvasGroup, BattleResultUI)
    ├── DimmedBackground (Image, Button -> 화면 아무 곳이나 클릭 시 닫기/귀환 연동)
    ├── Txt_BgWatermark (TextMeshProUGUI -> 상단 배경 거대 "DAY" 워터마크)
    │
    ├── ReportWindowContainer (RectTransform -> 중앙 메인 리포트 영역, 1280 x 720)
    │   ├── Img_CornerBracket_TR (Image -> 우상단 노란색 ㄱ자 브래킷)
    │   ├── Img_CornerBracket_BR (Image -> 우하단 노란색 ┘자 브래킷)
    │   │
    │   └── ReportWindowFrame (Image, Outline -> 본체 다크 박스 #121417)
    │       ├── HeaderBar (Image -> 상단 헤더 바 #090A0C, Height: 64)
    │       │   ├── Img_HeaderDiamond (Image -> 14x14, Rotation Z: 45, #FACC15)
    │       │   ├── Txt_HeaderTitle (TextMeshProUGUI -> "DAILY REPORT")
    │       │   └── Txt_HeaderSubtitle (TextMeshProUGUI -> "DAY 12 결산")
    │       │
    │       ├── MetricsListGroup (RectTransform, VerticalLayoutGroup -> 상단 4행 지표)
    │       │   ├── Row_Income (RectTransform -> Height: 88)
    │       │   │   ├── LabelGroup (RectTransform)
    │       │   │   │   ├── Txt_EnLabel (TextMeshProUGUI -> "INCOME")
    │       │   │   │   └── Txt_KrLabel (TextMeshProUGUI -> "수입")
    │       │   │   ├── Txt_IncomeValue (TextMeshProUGUI -> "+1,200")
    │       │   │   └── Img_Divider (Image -> 하단 1px 구분선 #24282E)
    │       │   │
    │       │   ├── Row_Expense (RectTransform -> Height: 88)
    │       │   │   ├── LabelGroup (Txt_EnLabel "EXPENSE" / Txt_KrLabel "지출")
    │       │   │   ├── Txt_ExpenseValue (TextMeshProUGUI -> "-350")
    │       │   │   └── Img_Divider (Image -> 하단 1px 구분선 #24282E)
    │       │   │
    │       │   ├── Row_Wear (RectTransform -> Height: 88)
    │       │   │   ├── LabelGroup (Txt_EnLabel "WEAR" / Txt_KrLabel "기체 마모")
    │       │   │   ├── Txt_WearValue (TextMeshProUGUI -> "+6%")
    │       │   │   └── Img_Divider (Image -> 하단 1px 구분선 #24282E)
    │       │   │
    │       │   └── Row_Activity (RectTransform -> Height: 88)
    │       │       ├── LabelGroup (Txt_EnLabel "ACTIVITY" / Txt_KrLabel "활동 시간")
    │       │       └── Txt_ActivityValue (TextMeshProUGUI -> "20H 00M")
    │       │
    │       └── BriefingGroup (RectTransform -> 하단 TODAY 브리핑 영역)
    │           ├── Txt_BriefingHeader (TextMeshProUGUI -> "TODAY · DAY 13")
    │           ├── BriefingList (RectTransform, VerticalLayoutGroup)
    │           │   ├── BriefingItem_1_Schedule (RectTransform -> Height: 42)
    │           │   │   ├── Img_AccentBar (Image -> Width: 4, #EF4444 레드)
    │           │   │   ├── Txt_Tag (TextMeshProUGUI -> "D-2")
    │           │   │   └── Txt_Desc (TextMeshProUGUI -> "3부 리그 2라운드 · 경기장")
    │           │   ├── BriefingItem_2_Omen (RectTransform -> Height: 42)
    │           │   │   ├── Img_AccentBar (Image -> Width: 4, #F97316 오렌지)
    │           │   │   ├── Txt_Tag (TextMeshProUGUI -> "OMEN")
    │           │   │   └── Txt_Desc (TextMeshProUGUI -> "고물상: 회로 부품 무더기 · 재료 +28%")
    │           │   └── BriefingItem_3_Shop (RectTransform -> Height: 42)
    │           │       ├── Img_AccentBar (Image -> Width: 4, #06B6D4 시안)
    │           │       ├── Txt_Tag (TextMeshProUGUI -> "SHOP")
    │           │       └── Txt_Desc (TextMeshProUGUI -> "상점 재입고 (오전 9시)")
    │
    ├── BarcodeStampBox (Image, Outline -> 우하단 바코드 스탬프 영역)
    │   ├── Txt_ReportNoLabel (TextMeshProUGUI -> "REPORT NO.")
    │   ├── Txt_ReportNoValue (TextMeshProUGUI -> "DR-0012")
    │   ├── Img_Barcode (Image -> 바코드 텍스처 또는 스트라이프)
    │   └── Txt_AutoSaveInfo (TextMeshProUGUI -> "AUTO SAVE // DAY 13 06:00")
    │
    └── Txt_ContinuePrompt (TextMeshProUGUI -> 좌하단 "화면 아무 곳이나 눌러 계속...")
```

---

## 4. 오브젝트별 인스펙터 수치 레시피 (1920x1080 기준)

### 4.1 메인 리포트 프레임 & 헤더 (`ReportWindowContainer`)
| 오브젝트명 | Anchor / Pivot | Pos (X, Y) | Size (W, H) | 스프라이트 및 비주얼 셋팅 레시피 |
|---|---|---|---|---|
| **ReportWindowContainer** | `Middle - Center` `(0.5, 0.5)` | `(-20, 25)` | `1360 x 740` | 투명 컨테이너 |
| **ReportWindowFrame** | `Stretch - Stretch` | `(0, 0, 0, 0)` | 전체 채움 | • **Source Image**: `Assets/Sprites/UI/BG/Panel/bg_panelLarge.png`<br>• **Type**: `Sliced`<br>• **(옵션 오버레이)**: `Frame/Panel/panelLarge/frame_panelLarge_v1.png` |
| **Img_CornerBracket_TR** | `Top - Right` `(1, 1)` | `(18, 18)` | `36 x 36` | • **Source Image**: `Assets/Sprites/UI/Frame/Corner/corner_v01.png`<br>• **Rotation Z**: `-90` (우상단 `ㄱ` 방향)<br>• 자체 노란색(`#FFE800`) 내장 |
| **Img_CornerBracket_BR** | `Bottom - Right` `(1, 0)` | `(18, -18)` | `36 x 36` | • **Source Image**: `Assets/Sprites/UI/Frame/Corner/corner_v01.png`<br>• **Rotation Z**: `180` (우하단 `┘` 방향)<br>• 자체 노란색(`#FFE800`) 내장 |
| **HeaderBar** | `Top - Stretch` `(0.5, 1)` | `PosY: 0` | `Height: 68` | `Image`: `#090B0E` (완전 다크 블랙 스트립) |
| **Img_HeaderDiamond** | `Middle - Left` `(0, 0.5)` | `(32, 0)` | `12 x 12` | • **Source Image**: `Assets/Sprites/UI/BG/Swatch/swatch_v1.png`<br>• **Rotation Z**: `45` (마름모 포인트 `◆`) |
| **Txt_HeaderTitle** | `Middle - Left` `(0, 0.5)` | `(58, 0)` | `280 x 40` | Font Size `24`, Bold, Character Spacing `12`, Color `#F3F4F6` |
| **Txt_HeaderSubtitle** | `Middle - Left` `(0, 0.5)` | `(320, 0)` | `300 x 40` | Font Size `20`, Regular, Color `#D1D5DB` |

### 4.2 상단 4대 지표 리스트 (`MetricsListGroup`)
* **위치 및 레이아웃**:
  - Anchor: `Top - Stretch`, Left `48`, Right `48`, PosY `-88`, Height `360`
  - `VerticalLayoutGroup`: Spacing `0`, Child Alignment `Upper Center`, Control Child Size (`Width: ON, Height: OFF`)
* **개별 행(`Row_Income`, `Row_Expense`, `Row_Wear`, `Row_Activity`) 내부 규격 (Height: `88`)**:
  - **행 베이스 (선택)**: `Image` Source `Assets/Sprites/UI/BG/Button/selectItem/bg_selectItem_enabled.png` (Type: `Sliced`, Alpha 약 `0.2f`)
  - **`Txt_EnLabel`** (좌상단): Anchor `Top-Left`, Pos `(0, -14)`, Font Size `13`, Character Spacing `8`, Color `#7A828E`
  - **`Txt_KrLabel`** (좌하단): Anchor `Bottom-Left`, Pos `(0, 16)`, Font Size `22`, Bold, Color `#E5E7EB`
  - **`Txt_Value`** (우측 수치): Anchor `Middle-Right`, Pos `(0, 0)`, Width `400`, Alignment `Right/Middle`, Font Size `36`, Bold
    - 수입(`Txt_IncomeValue`): `#34D399` (`"+1,200"`)
    - 지출(`Txt_ExpenseValue`): `#F87171` (`"-350"`)
    - 기체 마모(`Txt_WearValue`): `#FB923C` (`"+6%"`)
    - 활동 시간(`Txt_ActivityValue`): `#FFFFFF` (`"20H 00M"`)
  - **`Img_Divider`** (하단 구분선, 1~3행에만 존재):
    - Anchor `Bottom-Stretch`, PosY `0`, Height `1`
    - **Source Image**: `Assets/Sprites/UI/BG/Swatch/swatch_v2.png` (Color: `#24282E`)

### 4.3 하단 브리핑 리스트 (`BriefingGroup`)
* **위치**: Anchor `Bottom - Stretch`, Left `48`, Right `48`, PosY `36`, Height `210`
* **`Txt_BriefingHeader`**:
  - Anchor `Top - Left`, Pos `(0, 0)`, Font Size `15`, Bold, Character Spacing `10`, Color `#EAB308` (`"TODAY · DAY 13"`)
* **개별 브리핑 아이템 (`BriefingItem_1~3`, Height: `44`, Spacing: `12`)**:
  - **`Img_AccentBar`**:
    - Anchor `Stretch - Left`, Width `4`, PosX `0`
    - **Source Image**: `Assets/Sprites/UI/BG/Swatch/swatch_v1.png`
    - **Color**:
      - 1번 바: `#EF4444` (레드)
      - 2번 바: `#F97316` (오렌지)
      - 3번 바: `#06B6D4` (시안)
  - **`Txt_Tag`**: Anchor `Top - Left`, Pos `(18, -2)`, Font Size `15`, Bold, Character Spacing `6`, Color `#E5E7EB` (`"D-2"`, `"OMEN"`, `"SHOP"`)
  - **`Txt_Desc`**: Anchor `Bottom - Left`, Pos `(110, 4)`, Font Size `20`, Medium, Color `#F3F4F6`

### 4.4 우하단 바코드 스탬프 (`BarcodeStampBox`) & 계속 프롬프트
* **`BarcodeStampBox`**:
  - Anchor: `Bottom - Right` `(1, 0)`, Pos `(-50, 28)`, Size `390 x 110`
  - **Source Image**: `Assets/Sprites/UI/BG/Panel/bg_panelSmall.png` (Type: `Sliced`, Alpha `230`)
  - `Outline`: `#374151` `(1, -1)`
  - **`Txt_ReportNoLabel`**: 좌상단 `(16, -10)`, Font Size `12`, Color `#6B7280`, Text `"REPORT NO."`
  - **`Txt_ReportNoValue`**: 우상단 `(-16, -10)`, Font Size `13`, Bold, Color `#E5E7EB`, Text `"DR-0012"`
  - **`Img_Barcode`**: 중앙 `(0, -4)`, Size `356 x 42`, Color `#D1D5DB` (TMP Monospace 폰트 `|| ||| | ||| |||| |` 또는 스트라이프 스프라이트)
  - **`Txt_AutoSaveInfo`**: 좌하단 `(16, 8)`, Font Size `11`, Character Spacing `4`, Color `#6B7280`, Text `"AUTO SAVE // DAY 13 06:00"`
* **`Txt_ContinuePrompt`**:
  - Anchor: `Bottom - Left` `(0, 0)`, Pos `(42, 24)`, Size `500 x 36`
  - Font Size `20`, Color `#6EE7B7` (또는 `#9CA3AF`), DOTween `DOFade(0.25f, 0.8f).SetLoops(-1, LoopType.Yoyo)`로 부드럽게 점멸.

---

## 5. 전투 정산 데이터(`BattleSettlementResult`) ↔ Daily Report UI 매핑 규격

기존 전투 정산 데이터와 일일 결산 데이터를 모두 호환할 수 있도록 다음과 같이 매핑한다.

| UI 필드 (레퍼런스 기준) | 전투 정산(`BattleSettlementResult`) 연동 시 | 일일 결산(`DailyReportData`) 연동 시 |
|---|---|---|
| **`Txt_HeaderTitle`** | `"DAILY REPORT"` (또는 `"BATTLE REPORT"`) | `"DAILY REPORT"` |
| **`Txt_HeaderSubtitle`** | `"DAY {currentDay} 결산"` (승/패 표시 포함 가능) | `"DAY {currentDay} 결산"` |
| **`Txt_IncomeValue` (수입)** | 승리 보상 골드 (`+{IncomeGold:N0}`) | 일일 총 수입 (`+{IncomeGold:N0}`) |
| **`Txt_ExpenseValue` (지출)** | 패배 판돈 차감 또는 수리비 (`-{ExpenseGold:N0}`) | 일일 총 지출 (`-{ExpenseGold:N0}`) |
| **`Txt_WearValue` (기체 마모)** | 전투 전후 5부위 총 내구도 손실률 (`+{WearPercent}%`) | 일일 기체 마모도 증가량 (`+{WearPercent}%`) |
| **`Txt_ActivityValue` (활동 시간)**| 전투 소요 시간 또는 활동 시간 (`"{Hours:00}H {Minutes:00}M"`) | 당일 누적 활동 시간 (`"20H 00M"`) |
| **`BriefingItem_1` (레드 바)** | 다음 리그 일정 (`D-2`) 또는 **영구 파괴 부품/코어 불안정 경고** | `D-2` 다음 경기 일정 안내 |
| **`BriefingItem_2` (오렌지 바)** | `OMEN` 고물상/파츠 상태 브리핑 | `OMEN` 고물상 재료/시세 이벤트 |
| **`BriefingItem_3` (시안 바)** | `SHOP` 상점 재입고 / 코어 EXP 성장 요약 | `SHOP` 상점 재입고 안내 |
| **`Txt_ReportNoValue`** | `"DR-{currentDay:0000}"` | `"DR-{currentDay:0000}"` |

---

## 6. `BattleResultUI.cs` 개편 인스펙터 변수 설계안

구현 시 `BattleResultUI.cs`의 직렬화 필드를 아래 구조로 정리하면 인스펙터에서 1:1로 깔끔하게 연결된다.

```csharp
[Header("=== 1. 루트 패널 및 배경 클릭 ===")]
[SerializeField] private GameObject resultPanelRoot;
[SerializeField] private CanvasGroup mainCanvasGroup;
[SerializeField] private Button btnFullScreenContinue; // DimmedBackground 또는 전체 화면 클릭 버튼
[SerializeField] private TextMeshProUGUI txtContinuePrompt; // "화면 아무 곳이나 눌러 계속..."

[Header("=== 2. 상단 헤더 바 (DAILY REPORT) ===")]
[SerializeField] private TextMeshProUGUI txtHeaderTitle;    // "DAILY REPORT"
[SerializeField] private TextMeshProUGUI txtHeaderSubtitle; // "DAY 12 결산"
[SerializeField] private TextMeshProUGUI txtBgWatermark;    // 배경 "DAY" 워터마크

[Header("=== 3. 4대 핵심 결산 지표 (수입 / 지출 / 마모 / 활동시간) ===")]
[SerializeField] private TextMeshProUGUI txtIncomeValue;    // "+1,200" (#34D399)
[SerializeField] private TextMeshProUGUI txtExpenseValue;   // "-350" (#F87171)
[SerializeField] private TextMeshProUGUI txtWearValue;      // "+6%" (#FB923C)
[SerializeField] private TextMeshProUGUI txtActivityValue;  // "20H 00M" (#FFFFFF)

[Header("=== 4. 하단 TODAY 브리핑 섹션 ===")]
[SerializeField] private TextMeshProUGUI txtBriefingHeader; // "TODAY · DAY 13"
[SerializeField] private Image imgBriefingBar1;             // #EF4444
[SerializeField] private TextMeshProUGUI txtBriefingTag1;   // "D-2"
[SerializeField] private TextMeshProUGUI txtBriefingDesc1;  // "3부 리그 2라운드 · 경기장"
[SerializeField] private Image imgBriefingBar2;             // #F97316
[SerializeField] private TextMeshProUGUI txtBriefingTag2;   // "OMEN"
[SerializeField] private TextMeshProUGUI txtBriefingDesc2;  // "고물상: 회로 부품 무더기 · 재료 +28%"
[SerializeField] private Image imgBriefingBar3;             // #06B6D4
[SerializeField] private TextMeshProUGUI txtBriefingTag3;   // "SHOP"
[SerializeField] private TextMeshProUGUI txtBriefingDesc3;  // "상점 재입고 (오전 9시)"

[Header("=== 5. 우하단 바코드 스탬프 ===")]
[SerializeField] private TextMeshProUGUI txtReportNoValue;  // "DR-0012"
[SerializeField] private TextMeshProUGUI txtAutoSaveInfo;   // "AUTO SAVE // DAY 13 06:00"
```

---

## 7. DOTween 시퀀스 연출 가이드

1. **0.0s ~ 0.25s (리포트 창 오픈)**:
   - `mainCanvasGroup.DOFade(1f, 0.25f)`와 동시에 `ReportWindowContainer`가 Y축 아래에서 위로 짧게 슬라이드(`DOAnchorPosY(25f, 0.25f).From(new Vector2(-20f, -15f)).SetEase(Ease.OutCubic)`).
2. **0.25s ~ 0.85s (4대 지표 카운팅 롤링)**:
   - `txtIncomeValue`: `0` ➔ `+1,200` (`DOVirtual.Int` 천 단위 콤마 포맷 `"+{0:N0}"`)
   - `txtExpenseValue`: `0` ➔ `-350` (`DOVirtual.Int` `"-{0:N0}"`)
   - `txtWearValue`: `0%` ➔ `+6%` (`DOVirtual.Int` `"+{0}%"`)
3. **0.75s ~ 1.15s (하단 TODAY 브리핑 순차 등장)**:
   - `BriefingItem_1` ➔ `BriefingItem_2` ➔ `BriefingItem_3`가 0.12초 간격으로 좌측에서 우측으로 페이드 슬라이드 등장.
4. **1.15s 이후 (바코드 스탬프 & 계속 안내 활성화)**:
   - 우하단 `BarcodeStampBox`가 스탬프 찍히듯 등장하고, 좌하단 `Txt_ContinuePrompt`(`"화면 아무 곳이나 눌러 계속..."`)가 점멸을 시작하며 화면 클릭(`btnFullScreenContinue`)이 활성화됨.
