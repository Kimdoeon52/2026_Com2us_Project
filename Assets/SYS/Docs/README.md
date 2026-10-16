# SYS — RE:AL STEEL 스테이지 그래픽 도구

2.5D 옥토패스(HD-2D) 느낌의 스테이지를 만드는 도구 모음. 메뉴는 **Tools > RE_AL STEEL > Stage**, 오브젝트는 **GameObject > RE_AL STEEL**.

## 폴더 (10-16 정리: 도구 = `RS/`, 아트 = 바깥)

**`RS/` — 직접 만든 도구 (코드 · 셰이더)**. 기능마다 폴더 하나, 각 폴더 안 `Editor/` 는 에디터 전용 코드.

| 폴더 | 들어 있는 것 | 어셈블리 |
|---|---|---|
| `RS/Common/` | 공용 코드 — 인스펙터 설명 표기, 접는 인스펙터, 폴더 경로 `RSPaths`, 젖음 상태, 표면(Surface) | RSCommon.Runtime / .Editor |
| `RS/Terrain/` | 지형 · 지형 요소 6종 · 외곽 · 풀 레이어 · 풀꽃 · 흩뿌리기 | RSTerrain.Runtime / .Editor |
| `RS/Lighting/` | 시간대 · 구름 그림자 · 햇살 · 갓레이 · 안개 · 자동 초점 · 캐릭터 빛 · 색감 · 간접광 · 젖은 바닥 · 스테이지 룩 | RSLighting.Runtime / .Editor |
| `RS/Water/` | 물 반사 | RSWater.Runtime / .Editor |
| `RS/Build/` | 조립 도구 · 편집 메시 · 시트 | RSBuild.Runtime / .Editor |
| `RS/Character/` | 테스트 캐릭터 · 시야 가림 · 시간 전환 키 · 피격 번쩍임(SpriteFlash) | Assembly-CSharp (Input System 때문) |
| `RS/StageSetup/Editor/` | 픽셀 선명하게 · 카메라+조명+포스트 · UV 밀도 · 포트폴리오 캡처 | RSPixelStageTools.Editor |
| `RS/Shaders/` | 셰이더 (.shader). 이름은 모두 `RE_AL STEEL/...`. 외부 · AI 모델은 **Prop Pixel Lit** (UV / 트라이플래너 · 윗면 덮개) | — |
| `RS/Shaders/Include/` | 여러 셰이더가 같이 쓰는 코드 (.hlsl) — 시야 가림, 간접광, 화면 반사 | — |

**바깥 — 아트 에셋 · 데이터** (도구가 새로 만드는 에셋도 여기로 들어온다)

| 폴더 | 들어 있는 것 |
|---|---|
| `3D_Models/` | 외부 · AI 모델 (FBX · OBJ) |
| `Materials/` | 머티리얼. `Props/` 외부 · AI 모델용 (Prop Pixel Lit — `MAT_Prop_기본` 을 복제해서 시작), `Stage/` 샘플(지형 칠 기본값이 이름으로 찾음), `Build/` 조립 도구가 만든 것 |
| `Textures/` | 텍스처. `Build/` 조립 · 표면용 |
| `Prefabs/` | `Build/` 조립 도구로 저장한 프리팹, `Examples/` 확인용 (RS_GodRayTestRoom) |
| `Presets/` | `ColorGrade/` 색감 프리셋, `LUT/`, `StageLook/` 스테이지 룩 |
| `Data/` | `Terrain/` 지형 브러시 결과, `Surfaces/` 표면 세트 |
| `Scenes/` | 테스트 씬 (+ Volume 프로필) |
| `Docs/` | 이 안내 |

## 규칙

- 파일은 **Unity 프로젝트 창에서** 옮긴다 (.meta 가 같이 움직여야 연결이 안 끊긴다).
- 도구가 에셋을 만드는 폴더는 `RS/Common/Editor/RSPaths.cs` 한곳에 있다. 폴더 구조를 바꾸면 여기만 고친다. (`RSPaths.Root` = SYS, `RSPaths.ToolsRoot` = SYS/RS)
- 셰이더는 이름(`RE_AL STEEL/...`)으로 찾으므로 셰이더 파일을 옮겨도 된다. 단 `RS/Shaders/Include/` 위치를 바꾸면 셰이더의 `#include "Include/..."` 도 같이 고친다.
- 기획 문서(각 도구 설명)는 Claude 프로젝트 문서 `RS_Lighting_옥토패스조명`, `RS_Terrain_범용지형도구`, `SYS_폴더_전체지도` 에 있다.
