# SYS — RE:AL STEEL 스테이지 그래픽 도구

2.5D 옥토패스(HD-2D) 느낌의 스테이지를 만드는 도구 모음. 메뉴는 **Tools > RE_AL STEEL > Stage**, 오브젝트는 **GameObject > RE_AL STEEL**.

## 폴더

| 폴더 | 들어 있는 것 | 누가 만지나 |
|---|---|---|
| `Shaders/` | 셰이더 (.shader). 이름은 모두 `RE_AL STEEL/...` | 프로그래머 |
| `Shaders/Include/` | 여러 셰이더가 같이 쓰는 코드 (.hlsl) — 시야 가림, 간접광, 화면 반사 | 프로그래머 |
| `Materials/` | 도구가 쓰는 머티리얼 (물, 젖은 바닥, 빛줄기). 도구가 새로 만드는 머티리얼도 여기 | 그래픽 |
| `Materials/Stage/` | 스테이지 샘플 머티리얼 (지형 칠 기본값이 이름으로 찾아 씀) | 그래픽 |
| `Textures/` | 텍스처 | 그래픽 |
| `Presets/ColorGrade/` | 시간대별 색감 프리셋 — 스테이지별로 복제해서 바꿔 끼우기 | 기획 · 그래픽 |
| `Presets/LUT/` | LUT 이미지 (색감 인스펙터가 만들고 읽음) | 그래픽 |
| `Data/Terrain/` | 지형 브러시로 깎고 칠한 결과 | 기획 · 그래픽 |
| `RSCommon/` | 공용 코드 (인스펙터 설명 표기, 폴더 경로 `RSPaths`, 젖음 상태) | 프로그래머 |
| `RSLighting/` | 시간대 · 안개 · 빛줄기 · 구름 그림자 · 초점 · 색감 · 간접광 · 젖은 바닥 | 프로그래머 |
| `RSTerrain/` | 지형 · 풀 · 흩뿌리기 · 구덩이/둔덕 등 지형 요소 | 프로그래머 |
| `RSWater/` | 물 반사 | 프로그래머 |
| `RSCharacter/` | 테스트용 캐릭터, 시야 가림, 스프라이트 번쩍임 | 프로그래머 |
| `Editor/` | 픽셀 아트 설정 · 스테이지 후처리 · UV 도구 | 프로그래머 |
| `Scenes/`, `Sprites/` | 테스트 씬, 스프라이트 원본 | 모두 |

## 규칙

- 파일은 **Unity 프로젝트 창에서** 옮긴다 (.meta 가 같이 움직여야 연결이 안 끊긴다).
- 도구가 에셋을 만드는 폴더는 `RSCommon/Editor/RSPaths.cs` 한곳에 있다. 폴더 구조를 바꾸면 여기만 고친다.
- 셰이더는 이름(`RE_AL STEEL/...`)으로 찾으므로 셰이더 파일을 옮겨도 된다. 단 `Shaders/Include/` 위치를 바꾸면 셰이더의 `#include "Include/..."` 도 같이 고친다.
- 기획 문서(각 도구 설명)는 Claude 프로젝트 문서 `RS_Lighting_옥토패스조명`, `RS_Terrain_범용지형도구`, `SYS_폴더_전체지도` 에 있다.
