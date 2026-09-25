# RS Lighting — 옥토패스풍 조명 (시간대 · 구름 그림자 · 햇살)

설치: **Tools → RE_AL STEEL → Stage → 옥토패스 조명 (시간대 · 구름 그림자 · 햇살)**
기존 태양(`LIGHT_Key_Sun` 또는 씬의 Directional)을 찾아 연결하고, 없으면 새로 만든다. 다시 실행해도 중복 생성하지 않는다.

```
LIGHTING_TimeOfDay        RSTimeOfDay   — 시각 하나로 전부 조절
  └ LightShafts           RSLightShafts — 햇살 빛줄기 (위치 = 영역 중심 · 지면 높이)
LIGHT_Key_Sun             + RSCloudShadow — 구름 그림자 (라이트 쿠키)
```

## 시간대 (RSTimeOfDay)
- 인스펙터 맨 위 시계 · 슬라이더 · 프리셋 버튼 (새벽 · 아침 · 한낮 · 오후 · 노을 · 밤)
- `Day Length Minutes` 0 = 고정 (스테이지별 시간대), N = 게임 속 하루가 실제 N분
- `Flow In Edit Mode` 켜면 플레이 안 해도 에디터에서 시간이 흐름
- Directional 하나를 낮엔 해, 밤엔 달로 쓴다 (세기가 0 근처일 때 바뀌어서 튀지 않음)
- 색은 그라데이션(가로 = 0~24시): 태양색 · 앰비언트(하늘/수평/지면, Trilight) · 안개색. 세기는 커브
- 보조광(`LIGHT_Fill`)이 있으면 하늘색 · 밤낮 세기를 따라감
- 스크립트: `SetTime(18f)`, `SetPreset(RSTimeOfDay.Preset.Sunset)`, `TransitionTo(22f, 3f)` (3초 동안 부드럽게)

## 구름 그림자 (RSCloudShadow)
- 태양에 구름 무늬 쿠키를 씌워 바람 방향으로 흘린다 → 지형 · 로봇 · 소품 전부 가려짐
- `Coverage`(구름 양, 실제 면적 비율) · `Strength`(진하기) · `Softness` · `Bands`(0 = 부드럽게, 2~4 = 계단식)
- `Cloud Size`(무늬 한 장 크기 m) · `Wind`(m/s) · `Resolution` + `Pixelated`(칸 경계 또렷)
- 밤엔 시간대가 자동으로 옅게 만듦
- 안 보이면: URP Asset → Lighting → **Light Cookies** 확인. 커스텀 셰이더는 `#pragma multi_compile_fragment _ _LIGHT_COOKIES` 필요 (RS 셰이더 3종은 추가됨)

## 햇살 빛줄기 (RSLightShafts)
- 영역(`Area`) 안에 `Count` 개가 생겼다 사라졌다 다른 자리에 다시 생김. 방향은 태양을 따름 (`Min Steepness` 보다 눕지 않음)
- 세기 · 색: 시간대가 정함 — 아침 · 노을에 진하고 한낮엔 옅고 밤엔 없음. `Intensity` · `Tint` 로 추가 조절
- 모양은 머티리얼 `MAT_RS_LightShaft` (셰이더 `RE_AL STEEL/Light Shaft`): 가장자리 · 위아래 흐려짐 · 결(줄무늬) · 밝기 계단
- 가산 합성 · 그림자 없음 · 전체 드로우콜 1

## 파일
```
RSLighting/
  RSLighting.Runtime.asmdef
  RSTimeOfDay.cs  RSCloudShadow.cs  RSLightShafts.cs  RSLightingClock.cs
  LightShaft.shader
  Editor/ RSLighting.Editor.asmdef  RSLightingEditor.cs (설치 메뉴 · 시간대 인스펙터)
```
