# Git Commit Guidelines

## 1. 언어 규칙 (Language Rule)
- 모든 Git 커밋 메시지는 **반드시 한국어(한글)**로만 작성합니다.
- 영문 메시지 작성을 엄격히 금지합니다. (고유명사, 클래스명, 메서드명만 영문 허용)

## 2. 제목 형식 (Subject Format)
- 형식: `[타입] 한글 요약`
- **주의: 타입 뒤에 콜론(`:`)을 붙이지 않습니다.**
- 올바른 예시: `[feat] 전투 체력 및 파츠 내구도 HUD 연동 구현`
- 잘못된 예시: `feat: ...`, `[feat]: ...`, `Update FighterStatusHUD.cs`

## 3. 타입 종류 (Commit Types)
- `feat`: 새로운 기능 추가
- `fix`: 버그 수정
- `refactor`: 코드 리팩토링 (기능 변경 없음)
- `docs`: 문서 수정 (README, 설계서, 가이드 등)
- `chore`: 빌드, 패키지, 단순 설정 변경
- `test`: 테스트 코드 추가 및 수정
- `style`: 코드 서식, 세미콜론 등 (동작 영향 없음)

## 4. 본문 형식 (Body Format)
- 제목과 본문 사이에 1줄 공백을 둡니다.
- 변경된 파일과 핵심 변경 내용을 글머리 기호(`-`)로 간결하게 요약합니다.
- 예시:
  ```text
  [feat] 전투 체력 및 파츠 내구도 HUD 연동 구현

  - FighterStatusHUD: 이벤트 구독, 자가 진단 및 자동 위젯 수집 로직 구현
  - PartGaugeWidget: 부위별 기본 라벨 자동 부여 및 내구도 색상 갱신
  - CombatDataHub: 파츠 피격 시 코어 체력 동반 차감 및 리셋 API 추가
  ```
