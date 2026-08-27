# HillyWings CLI 빌드 스크립트 (PowerShell — Windows 및 macOS/Linux의 PowerShell Core 사용 가능)
# 사용법: ./build.ps1 -Platform <ios|android|windows>
# UNITY_PATH 환경변수를 설정하면 Unity 실행 경로를 직접 지정할 수 있음

[CmdletBinding()]
param(
    [Parameter(Position = 0, HelpMessage = "빌드 플랫폼: ios, android, windows")]
    [ValidateSet('ios', 'android', 'windows', IgnoreCase = $true)]
    [string]$Platform,

    [Alias('h')]
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── 도움말 ───────────────────────────────────────────────────────────────────
function Show-Usage {
    Write-Host @"
사용법: ./build.ps1 [-Help] -Platform <플랫폼>
        ./build.ps1 <플랫폼>

플랫폼:
  ios       iOS Xcode 프로젝트 빌드
  android   Android APK 빌드
  windows   Windows x64 실행 파일(.exe) 빌드

옵션:
  -Help, -h   이 도움말을 표시하고 종료

환경변수:
  UNITY_PATH   Unity 실행 파일 경로를 직접 지정 (기본값: Unity Hub 설치 경로)
               Windows: C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe
               macOS  : /Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents/MacOS/Unity

출력:
  build\<버전>\<플랫폼>\             빌드 결과물
  build\<버전>\<플랫폼>_build.log   빌드 로그

예시:
  ./build.ps1 ios
  ./build.ps1 -Platform android
  $env:UNITY_PATH = "C:\Unity\Unity.exe"; ./build.ps1 windows
"@
}

if ($Help) {
    Show-Usage
    exit 0
}

if (-not $Platform) {
    Show-Usage
    exit 1
}

# ─── 인자 → 메서드/폴더명 매핑 ──────────────────────────────────────────────
switch ($Platform.ToLower()) {
    'ios'     { $Method = 'HillyWings.Editor.BuildMenu.BuildIOS';     $PlatformName = 'iOS'     }
    'android' { $Method = 'HillyWings.Editor.BuildMenu.BuildAndroid'; $PlatformName = 'Android' }
    'windows' { $Method = 'HillyWings.Editor.BuildMenu.BuildWindows'; $PlatformName = 'Windows' }
}

# ─── 경로 설정 ────────────────────────────────────────────────────────────────
$ScriptDir        = $PSScriptRoot
$ProjectSettings  = Join-Path $ScriptDir 'ProjectSettings/ProjectSettings.asset'
$ProjectVersion   = Join-Path $ScriptDir 'ProjectSettings/ProjectVersion.txt'

# bundleVersion 파싱
$VersionLine = (Get-Content $ProjectSettings | Select-String -Pattern 'bundleVersion:' | Select-Object -First 1).Line
if (-not $VersionLine) {
    Write-Error "ProjectSettings.asset에서 bundleVersion을 읽지 못했습니다."
    exit 1
}
$Version = ($VersionLine -split '\s+')[2]

# Unity 에디터 버전 파싱
$EditorLine = (Get-Content $ProjectVersion | Select-String -Pattern 'm_EditorVersion:' | Select-Object -First 1).Line
if (-not $EditorLine) {
    Write-Error "ProjectVersion.txt에서 Unity 에디터 버전을 읽지 못했습니다."
    exit 1
}
$UnityVersion = ($EditorLine -split '\s+')[1]

# Unity 실행 파일 경로 (환경변수로 오버라이드 가능)
if ($env:UNITY_PATH) {
    $UnityExe = $env:UNITY_PATH
} elseif ($IsWindows -or $env:OS -eq 'Windows_NT') {
    $UnityExe = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
} else {
    # macOS / Linux (PowerShell Core)
    $UnityExe = "/Applications/Unity/Hub/Editor/$UnityVersion/Unity.app/Contents/MacOS/Unity"
}

if (-not (Test-Path $UnityExe)) {
    Write-Error @"
Unity 실행 파일을 찾을 수 없습니다: $UnityExe
  Unity Hub에서 버전 $UnityVersion 이 설치되어 있는지 확인하거나,
  환경변수 UNITY_PATH에 직접 경로를 지정하세요.
"@
    exit 1
}

# 경로 설정
$LogDir    = Join-Path $ScriptDir "build/$Version"
$LogFile   = Join-Path $LogDir "${PlatformName}_build.log"
$OutputDir = Join-Path $ScriptDir "build/$Version/$PlatformName"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null

# ─── 빌드 실행 ────────────────────────────────────────────────────────────────
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host "  HillyWings $PlatformName 빌드"
Write-Host "  버전    : $Version"
Write-Host "  Unity   : $UnityVersion"
Write-Host "  출력    : $OutputDir"
Write-Host "  로그    : $LogFile"
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

$StartTime = Get-Date
$ErrorActionPreference = 'Continue'   # Unity 종료 코드를 직접 받기 위해 임시 완화

$Process = Start-Process -FilePath $UnityExe -ArgumentList @(
    '-batchmode',
    '-quit',
    '-projectPath', "`"$ScriptDir`"",
    '-executeMethod', $Method,
    '-logFile', "`"$LogFile`""
) -NoNewWindow -Wait -PassThru

$ErrorActionPreference = 'Stop'
$ExitCode = $Process.ExitCode
$Elapsed  = [int]((Get-Date) - $StartTime).TotalSeconds

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if ($ExitCode -eq 0) {
    Write-Host "  결과    : [SUCCESS]"
    Write-Host "  소요    : ${Elapsed}초"
    Write-Host "  출력    : $OutputDir"
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
} else {
    Write-Host "  결과    : [FAIL]  (종료 코드: $ExitCode)"
    Write-Host "  소요    : ${Elapsed}초"
    Write-Host "  로그    : $LogFile"
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    Write-Host ""

    if (Test-Path $LogFile) {
        Write-Host "[ 오류 원인 ]"
        $ErrorLines = Get-Content $LogFile |
            Select-String -Pattern 'error|Error|ERROR|FAILED|exception|Exception|\[BuildMenu\]' |
            Where-Object { $_ -notmatch '^#' } |
            Select-Object -Last 20
        if ($ErrorLines) {
            $ErrorLines | ForEach-Object { Write-Host "  $_" }
        } else {
            Write-Host "  (로그에서 원인을 추출하지 못했습니다 — 전체 로그를 확인하세요)"
        }
        Write-Host ""
        Write-Host "[ 로그 끝 부분 ]"
        Get-Content $LogFile -Tail 20
    }
    exit $ExitCode
}
