# ==============================================================================
# [한글 커밋 메시지 자동 생성 및 커밋 도구 (git-commit.ps1)]
# AGENTS.md / GEMINI.md 가이드라인 규격 준수:
# - 제목 형식: [타입] 한글 요약 (예: [feat] 전투 체력 및 파츠 내구도 HUD 연동 구현)
# - 타입: feat, fix, refactor, docs, chore, test, style
# - 본문: 변경 내용을 글머리 기호(-)로 핵심만 한글로 간결하게 작성
# ==============================================================================

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Type = "",

    [Parameter(Position = 1)]
    [string]$Title = "",

    [switch]$AutoAdd = $false,
    [switch]$Yes = $false,
    [switch]$DryRun = $false
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 1. Git 상태 확인
$staged = git diff --cached --name-status
if (-not $staged) {
    $unstaged = git status -s
    if (-not $unstaged) {
        Write-Host "커밋할 변경 사항이 없습니다." -ForegroundColor Yellow
        exit 0
    }

    Write-Host "[안내] 스테이징(git add)된 파일이 없습니다." -ForegroundColor Yellow
    Write-Host "현재 변경된 파일 목록:" -ForegroundColor Gray
    git status -s

    $shouldAdd = $Yes
    if (-not $shouldAdd) {
        $confirm = Read-Host "모든 변경 사항을 스테이징(git add -A)하시겠습니까? (Y/N)"
        if ($confirm -match '^[Yy]') { $shouldAdd = $true }
    }

    if ($shouldAdd) {
        git add -A
        $staged = git diff --cached --name-status
    } else {
        Write-Host "작업이 취소되었습니다." -ForegroundColor Red
        exit 0
    }
}

# 2. 스테이징된 파일 목록 분석 및 본문 글머리 기호(-) 자동 생성
$files = $staged -split "`r?`n" | Where-Object { $_ -match '\S' }
$bodyLines = @()
$hasFeat = $false
$hasFix = $false
$hasDocs = $false
$hasTest = $false

foreach ($line in $files) {
    $parts = $line -split '\s+', 2
    $status = $parts[0]
    $path = $parts[1]

    # 파일별 스마트 한글 설명 추론
    $desc = ""
    if ($path -match "FighterStatusHUD\.cs") {
        $desc = "FighterStatusHUD: 범용 모의(Mock) 데이터 모드 및 단독 키보드 테스트 로직 구현"
        $hasFeat = $true
    }
    elseif ($path -match "PartGaugeWidget\.cs") {
        $desc = "PartGaugeWidget: 데이터 미초기화 시 안전 방어 및 부위별 동적 색상/라벨 갱신"
        $hasFeat = $true
    }
    elseif ($path -match "HUDInteractiveTester\.cs") {
        $desc = "HUDInteractiveTester: 게임 뷰 키 입력 및 GUI 기반 HUD 실시간 인터랙티브 테스터 추가"
        $hasTest = $true
    }
    elseif ($path -match "CombatDataHubTester\.cs") {
        $desc = "CombatDataHubTester: 단위 검증 완료 후 참가자 스탯 풀 복구(Clean-up) 로직 추가"
        $hasTest = $true
    }
    elseif ($path -match "CombatDataHub\.cs") {
        $desc = "CombatDataHub: 단일 참가자 등록 및 기본 모의(Mock) 스냅샷 자동 생성 지원"
        $hasFeat = $true
    }
    elseif ($path -match "CombatCalculator\.cs") {
        $desc = "CombatCalculator: 피격 부위(hitPart)에 따른 파츠 내구도 및 코어 체력 동반 감쇄 공식 적용"
        $hasFeat = $true
    }
    elseif ($path -match "Battle_Test_KKH\.unity") {
        $desc = "Battle_Test_KKH: 전투 테스트 씬 UI 계층 구조 및 위젯 바인딩 갱신"
        $hasFeat = $true
    }
    elseif ($path -match "PartGaugeWidget\.prefab" -or $path -match "HP_HUD") {
        $desc = "HUD 위젯 및 체력바 프리팹 에셋 설정 및 컴포넌트 바인딩 완료"
        $hasFeat = $true
    }
    elseif ($path -match "\.md$") {
        $desc = "$([System.IO.Path]::GetFileName($path)): 기획서 명세 및 실전 작업 TODO 체크리스트 갱신"
        $hasDocs = $true
    }
    else {
        # .meta 파일은 주 에셋 변경에 포함되므로 본문 라인 중복 방지를 위해 스킵
        if ($path -match "\.meta$") { continue }

        $fileName = [System.IO.Path]::GetFileName($path)
        if ($status -eq "A" -or $status -eq "??") {
            $desc = "신규 에셋/파일 추가: $fileName"
        } elseif ($status -eq "D") {
            $desc = "불필요한 파일 삭제: $fileName"
        } else {
            $desc = "에셋/스크립트 갱신: $fileName"
        }
    }

    if ($desc) {
        $bodyLines += "- $desc"
    }
}

# 중복 설명 제거 (Clean-up)
$bodyLines = $bodyLines | Select-Object -Unique

# 3. 타입 및 제목 자동 추론
$detectedType = "feat"
if ($hasFeat) { $detectedType = "feat" }
elseif ($hasTest) { $detectedType = "test" }
elseif ($hasDocs) { $detectedType = "docs" }
elseif ($hasFix) { $detectedType = "fix" }

if (-not $Type) { $Type = $detectedType }

if (-not $Title) {
    if ($bodyLines -match "HUD" -or $bodyLines -match "FighterStatusHUD") {
        $Title = "전투 체력(Core HP) 및 파츠 내구도 HUD 연동 구현"
    } elseif ($hasTest) {
        $Title = "전투 데이터 허브 및 HUD 실시간 인터랙티브 테스터 구현"
    } elseif ($hasDocs) {
        $Title = "전투 체력 및 파츠 내구도 HUD 설계 가이드라인 갱신"
    } else {
        $Title = "전투 시스템 및 UI 연동 변경사항 반영"
    }
}

$commitSubject = "[$Type] $Title"
$commitBody = $bodyLines -join "`n"
$fullCommitMsg = "$commitSubject`n`n$commitBody"

# 4. 미리보기 출력
Write-Host "`n=======================================================" -ForegroundColor Cyan
Write-Host "[한글 커밋 메시지 미리보기]" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host $commitSubject -ForegroundColor Green
Write-Host ""
Write-Host $commitBody -ForegroundColor White
Write-Host "=======================================================" -ForegroundColor Cyan

# DryRun 모드 시 미리보기만 출력 후 종료
if ($DryRun) {
    Write-Host "`n[DryRun 모드] 실제 커밋을 수행하지 않고 미리보기만 출력했습니다." -ForegroundColor Yellow
    exit 0
}

# 5. 사용자 확인 및 커밋 실행
if ($Yes) {
    git commit -m $fullCommitMsg
    Write-Host "`n[성공] 한글 커밋이 완료되었습니다!" -ForegroundColor Green
    exit 0
}

Write-Host "`n선택하세요:" -ForegroundColor Yellow
Write-Host "  [1] 위 메시지로 바로 커밋 (기본값 - Enter)" -ForegroundColor White
Write-Host "  [2] 커밋 제목 직접 입력 후 커밋" -ForegroundColor White
Write-Host "  [3] 취소" -ForegroundColor White
$choice = Read-Host "번호 선택 (1/2/3)"

if ($choice -eq "2") {
    $customTitle = Read-Host "한글 커밋 제목 입력 (예: [$Type] ...)"
    if ($customTitle) {
        if (-not ($customTitle -match '^\[.*\]')) {
            $customTitle = "[$Type] $customTitle"
        }
        $fullCommitMsg = "$customTitle`n`n$commitBody"
    }
    git commit -m $fullCommitMsg
    Write-Host "`n[성공] 입력한 제목으로 한글 커밋이 완료되었습니다!" -ForegroundColor Green
}
elseif ($choice -eq "3") {
    Write-Host "커밋을 취소했습니다." -ForegroundColor Yellow
}
else {
    git commit -m $fullCommitMsg
    Write-Host "`n[성공] 한글 커밋이 완료되었습니다!" -ForegroundColor Green
}
