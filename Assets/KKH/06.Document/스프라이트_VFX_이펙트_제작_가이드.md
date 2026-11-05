# 2D 스프라이트 VFX 이펙트 제작 가이드 (Sprite Flipbook / Particle)

본 문서는 `Assets/Sprites/Vfx/` 경로에 위치한 스프라이트 시트 리소스 3종을 유니티(Unity) 2D 이펙트 프리팹으로 제작하고, 1보스(정크 휠러)의 기믹 패턴에 연동하는 표준 작업 절차를 설명합니다.

---

## 1. 대상 스프라이트 리소스 현황

| 파일명 | 프레임 수 | 시트 규격 | 추천 연출 용도 |
| :--- | :---: | :---: | :--- |
| **`Vfx_Crack.png`** | **12 프레임** (`Vfx_Crack_0` ~ `11`) | 2 × 6 (216 × 64 px) | • **지면 균열 / 크랙 전조 연출**<br>• 드리블 충격파 전조 바닥 균열 (`tellCrackVfxPrefab`) |
| **`Vfx_Crack_Small.png`** | **53 프레임** (`Vfx_Crack_Small_0` ~ `52`) | 다열 시트 (약 72 × 48 px) | • **소형 바닥 균열 / 잔여 파편 연출**<br>• 충격파 지나간 후 지면 잔여 크랙 연출 |
| **`Vfx_Slam_Sprak.png`** | **28 프레임** (`Vfx_Slam_Sprak_0` ~ `27`) | 4 × 7 (128 × 128 px) | • **슬램 착지 충격파 / 지면 스파크**<br>• 슬램 패턴 착지 임팩트 (`groundImpactVfxPrefab`)<br>• 못판 펑크 파열 연출 (`nailPunctureVfxPrefab`) |

> [!NOTE]
> 세 텍스처 모두 메타 파일(`.meta`)에 이미 `Sprite Mode: Multiple`로 프레임 슬라이스(Slice) 작업이 완료되어 있습니다.  
> 따라서 **텍스처 슬라이스를 다시 할 필요 없이 바로 애니메이션 제작 단계부터 진행**하시면 됩니다.

---

## 2. 작업 방식 선택

유니티에서 2D 스프라이트 시트로 이펙트를 제작하는 방식은 크게 **2가지**가 있습니다:

1. **[방식 1 - 적극 권장] Animator + SpriteRenderer 플립북 방식**
   * **특징**: 프레임 단위의 도트/카툰 스프라이트가 원본 해상도와 타이밍 그대로 재생됨. 가장 직관적이고 제작이 빠름.
   * **적용 추천**: `Vfx_Crack`, `Vfx_Slam_Sprak`, `Vfx_Crack_Small` 3종 모두 이 방식이 가장 깔끔하게 나옵니다.
2. **[방식 2] ParticleSystem (Texture Sheet Animation) 방식**
   * **특징**: 파티클 시스템을 통해 재생 속도, 알파 페이드, 크기 확장/축소, 틴트 색상 변화 등을 동적으로 조절할 수 있음.

---

## 3. [방식 1] Animator + SpriteRenderer 플립북 제작 절차 (권장)

### Step 1. 스프라이트 애니메이션 클립(.anim) 자동 생성

1. **Project** 창에서 `Assets/Sprites/Vfx/` 폴더로 이동합니다.
2. `Vfx_Crack.png` 왼쪽의 **`▶` (펼침 화살표)**를 클릭하여 슬라이스된 스프라이트 목록(`Vfx_Crack_0` ~ `Vfx_Crack_11`)을 엽니다.
3. `Vfx_Crack_0`을 클릭한 뒤 `Shift` 키를 누른 채 마지막 프레임(`Vfx_Crack_11`)을 클릭하여 **전체 프레임을 선택**합니다.
4. 선택한 프레임들을 **Hierarchy 창** 또는 **Scene 창**으로 그대로 드래그 앤 드롭합니다.
5. 유니티가 애니메이션 클립 저장 팝업 창을 띄웁니다:
   * **저장 위치**: `Assets/KKH/03.Animation/VFX/` (폴더가 없으면 생성)
   * **파일명**: `Anim_Vfx_Crack.anim` (스몰 크랙: `Anim_Vfx_Crack_Small.anim`, 슬램 스파크: `Anim_Vfx_Slam_Spark.anim`)
6. 저장을 누르면 Hierarchy 창에 동일한 이름의 게임오브젝트가 생성되고, 자동으로 `SpriteRenderer`와 `Animator` 컴포넌트가 부착됩니다.

---

### Step 2. 단발성 재생을 위한 루프 해제 (중요!)

이펙트는 1회 재생 후 끝나야 하므로 애니메이션 루프를 반드시 꺼야 합니다.

1. Project 창에서 생성된 `.anim` 파일(`Anim_Vfx_Crack.anim`)을 클릭합니다.
2. **Inspector** 창에서 **`Loop Time` 체크박스를 해제**합니다. (체크 해제 = 1회만 재생)

---

### Step 3. 애니메이션 속도(FPS / Samples) 튜닝

1. 상단 메뉴에서 **Window > Animation > Animation**을 열어 Animation 창을 엽니다.
2. Hierarchy 창에서 생성한 게임오브젝트를 선택합니다.
3. Animation 창 우측 상단의 점 세 개(`⋮`) 메뉴를 클릭하고 **`Show Sample Rate`**를 켭니다.
4. **`Samples`** 수치를 이펙트 성격에 맞게 조정합니다:
   * **`Vfx_Crack` (12프레임)**: **`20 ~ 24`** 권장 (재생 시간: 약 0.5 ~ 0.6초)
   * **`Vfx_Crack_Small` (53프레임)**: **`45 ~ 60`** 권장 (재생 시간: 약 0.9 ~ 1.1초)
   * **`Vfx_Slam_Sprak` (28프레임)**: **`24 ~ 30`** 권장 (재생 시간: 약 0.9 ~ 1.1초)

---

### Step 4. SpriteRenderer 정렬 레이어 및 머티리얼 설정

이펙트가 보스 스프라이트나 바닥 타일맵 뒤로 가려지지 않도록 전면 표시 설정을 합니다.

1. 게임오브젝트의 **`SpriteRenderer`** 컴포넌트를 확인합니다:
   * **Sorting Layer**: `Default` 또는 `Foreground`
   * **Order in Layer**: **`5` 이상** (보스가 일반적으로 0~2이므로, 5 이상으로 설정해야 보스 앞이나 발밑에 선명히 보임)
   * **Material**: `Sprites-Default` (기본값 유지)

---

### Step 5. 프로젝트 자동 수명 회수(`BossVfxHelper`) 호환성

우리 프로젝트의 [`BossVfxHelper.SpawnVfx()`](file:///d:/KDU/2026_Com2us_Project/Assets/KKH/02.Scripts/03.Logic/Boss/Visual/BossVfxHelper.cs#L120-L138)는:
```csharp
// 파티클이 없으면 Animator 클립 길이 검사
var animator = instance.GetComponentInChildren<Animator>();
if (animator != null && animator.runtimeAnimatorController != null)
{
    var clips = animator.runtimeAnimatorController.animationClips;
    // 클립 재생 길이에 맞춰 Destroy(instance, maxClipLength) 자동 예약
}
```
`Animator`가 붙은 프리팹을 스폰하면 **애니메이션 클립의 실제 길이를 자동으로 계산하여 재생 완료 후 메모리에서 안전하게 파괴(Destroy)**합니다.  
따라서 별도의 수명 파괴 스크립트를 추가하지 않아도 완벽하게 자동 회수됩니다.

---

### Step 6. 프리팹(Prefab)으로 저장

1. Hierarchy 창에서 설정이 완료된 게임오브젝트의 이름을 알기 쉽게 변경합니다:
   * 예: `FX_Crack_Floor`, `FX_Crack_Small`, `FX_Slam_Spark`
2. 해당 오브젝트를 Project 창의 **`Assets/KKH/04.Prefab/VFX/`** 폴더(또는 `Assets/KKH/04.Prefab/BOSS/First_Boss/`)로 드래그 앤 드롭하여 **오리지널 프리팹(Original Prefab)**으로 저장합니다.
3. 저장이 완료되면 Hierarchy에 남아있는 임시 오브젝트는 삭제합니다.

---

## 4. [방식 2] ParticleSystem (Texture Sheet Animation) 제작 절차 (대안)

파티클의 이동, 크기 축소, 알파 페이드아웃 효과를 함께 주고 싶을 경우 사용하는 절차입니다.

> [!WARNING]
> **지면 균열(Crack) 파티클 필수 주의사항**:
> 1. **`Render Mode`**: 기본값인 `Billboard`로 두면 3D 틸트 카메라 환경에서 균열이 바닥에 눕지 않고 수직으로 꼿꼿이 서서 캐릭터 뒤로 솟구치게 됩니다. 반드시 **`Horizontal Billboard`**로 설정해야 지면에 평평하게 깔립니다.
> 2. **`Mesh Type`**: 파티클 시스템 텍스처 시트에 스프라이트를 등록할 때는 스프라이트 임포터의 `Mesh Type`이 **`Full Rect`**여야 직사각형 UV가 정상 매핑되며 빨간 에러 메시지가 뜨지 않습니다. (현재 3종 스프라이트 모두 `Full Rect` 및 `Center` 피벗으로 자동 교정 완료됨)

1. Hierarchy에서 **우클릭 > Effects > Particle System**을 생성합니다.
2. **Main 모듈**:
   * `Duration`: `0.6` (재생 시간에 맞춤)
   * `Looping`: **`False`** (체크 해제)
   * `Start Lifetime`: `0.6`
   * `Start Speed`: **`0`** (위아래 이동 방지, 제자리 고정)
   * `Start Size`: **`1.5 ~ 2.0`** (캐릭터 발밑에 어울리는 적정 크기)
   * `Play on Awake`: `True`
3. **Emission 모듈**:
   * `Rate over Time`: `0`
   * `Bursts`: `+` 클릭 (Time: `0`, Count: `1`)
4. **Shape 모듈**: 체크 **해제** (제자리 생성)
5. **Texture Sheet Animation 모듈**:
   * 체크 활성화
   * `Mode`: **`Sprites`** 선택
   * `+` 버튼을 눌러 슬라이스된 스프라이트(`_0` ~ 끝 프레임)를 순서대로 등록
   * `Time Mode`: `Lifetime`
   * `Cycles`: `1`
6. **Renderer 모듈**:
   * `Render Mode`: **`Horizontal Billboard`** (지면에 눕히기)
   * `Order in Layer`: **`5`**
7. 프리팹으로 저장합니다.

---

## 5. 1보스(Junk Wheeler) 기믹 인스펙터 연동 가이드

완성된 프리팹은 [Boss1_JunkWheeler.prefab](file:///d:/KDU/2026_Com2us_Project/Assets/KKH/04.Prefab/BOSS/First_Boss/Boss1_JunkWheeler.prefab) 또는 씬(`Battle_Boss_Test.unity`) 내 보스 오브젝트의 컴포넌트 슬롯에 바로 할당하여 사용할 수 있습니다:

### 1) 드리블 충격파 기믹 ([`JunkWheelerDribbleShockwavePatternGimmick`](file:///d:/KDU/2026_Com2us_Project/Assets/KKH/02.Scripts/03.Logic/Boss/JunkWheeler/Gimmicks/JunkWheelerDribbleShockwavePatternGimmick.cs))
* **`Tell Crack Vfx Prefab`**: 제작한 **`FX_Crack_Floor`** 프리팹 할당
  * 보스가 드리블 도약 전 0.6초간 바닥이 찌이익 갈라지는 전조 이펙트로 재생됩니다.

### 2) 내려찍기(슬램) 기믹 ([`JunkWheelerSlamPatternGimmick`](file:///d:/KDU/2026_Com2us_Project/Assets/KKH/02.Scripts/03.Logic/Boss/JunkWheeler/Gimmicks/JunkWheelerSlamPatternGimmick.cs))
* **`Ground Impact Vfx Prefab`**: 제작한 **`FX_Slam_Spark`** 프리팹 할당
  * 보스가 공중에서 지면으로 쾅! 하고 급강하 착지하는 순간 지면에 폭발적인 스파크와 충격파로 재생됩니다.
* **`Nail Puncture Vfx Prefab`**: 제작한 **`FX_Slam_Spark`** 또는 **`FX_Crack_Small`** 할당
  * 2회차 이후 못판에 펑크가 나서 터질 때 발생하는 파편/스파크 연출로 재생됩니다.

---

## 6. 요약 체크리스트

- [ ] 세 스프라이트 모두 `Sprite Mode: Multiple`로 슬라이스되어 있는지 확인 (완료됨)
- [ ] 슬라이스된 스프라이트 전체를 드래그하여 `.anim` 클립 생성
- [ ] 생성된 `.anim` 클립의 **`Loop Time = False`** 확인 (필수)
- [ ] Animation 창에서 `Samples`(FPS) 속도 조정
- [ ] `SpriteRenderer`의 **`Order in Layer = 5` 이상** 설정
- [ ] 프리팹으로 저장 후 보스 인스펙터 슬롯에 바인딩

