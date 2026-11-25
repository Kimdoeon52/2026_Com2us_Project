마에스트로 키 포즈 추출 (2026-11-25, B-2)
- 모션시트_베기.png = 사용자 제공 모션 시트(베기 6컷 줄)
- poses.cfg 한 줄 = 이름|시트|자를 칸 x,y,w,h|가슴x,아래y(0=자동)|지울 다각형(변환좌표)|변환 ox,oy,배율|지울 다각형(시트좌표)
- PowerShell:  Add-Type -Path PoseExtract.cs -ReferencedAssemblies System.Drawing; [PoseExtract]::Run("poses.cfg", "out", 40)
  (cfg의 시트 경로는 절대 경로로 바꿔서 실행)
- 결과 png를 240x170 캔버스(가슴 x=120, 맨 아래 줄 = 바닥)에 옮겨 Assets/NYH/03. Image/Boss/Maestro/Poses/ 에 넣고
  Unity 메뉴 NYH/보스3/키 포즈 굽기 → 씬 저장
