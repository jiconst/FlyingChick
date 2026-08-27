#!/usr/bin/env bash
# HillyWings CLI 빌드 스크립트 (macOS / Linux)
# 사용법: ./build.sh <ios|android|windows>
# UNITY_PATH 환경변수를 설정하면 Unity 실행 경로를 직접 지정할 수 있음

set -euo pipefail

# ─── 도움말 ───────────────────────────────────────────────────────────────────
usage() {
    cat <<EOF
사용법: $(basename "$0") [-h] <플랫폼>

플랫폼:
  ios       iOS Xcode 프로젝트 빌드
  android   Android APK 빌드
  windows   Windows x64 실행 파일(.exe) 빌드

옵션:
  -h, --help   이 도움말을 표시하고 종료

환경변수:
  UNITY_PATH   Unity 실행 파일 경로를 직접 지정 (생략 시 아래 순서로 자동 탐색)
               1순위: /Applications/Unity/<버전>/Unity.app/Contents/MacOS/Unity
               2순위: /Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents/MacOS/Unity

출력:
  build/<버전>/<플랫폼>/             빌드 결과물
  build/<버전>/<플랫폼>_build.log   빌드 로그

예시:
  ./build.sh ios
  ./build.sh android
  UNITY_PATH=/opt/unity/Unity ./build.sh windows
EOF
}

# ─── 인자 확인 ────────────────────────────────────────────────────────────────
if [[ $# -lt 1 ]]; then
    usage
    exit 1
fi

case "$1" in
    -h|--help)
        usage
        exit 0
        ;;
esac

PLATFORM_ARG=$(echo "$1" | tr '[:upper:]' '[:lower:]')

case "$PLATFORM_ARG" in
    ios)
        METHOD="HillyWings.Editor.BuildMenu.BuildIOS"
        PLATFORM_NAME="iOS"
        ;;
    android)
        METHOD="HillyWings.Editor.BuildMenu.BuildAndroid"
        PLATFORM_NAME="Android"
        ;;
    windows)
        METHOD="HillyWings.Editor.BuildMenu.BuildWindows"
        PLATFORM_NAME="Windows"
        ;;
    *)
        echo "오류: 알 수 없는 플랫폼 '$1'. ios, android, windows 중 하나를 입력하세요."
        echo ""
        usage
        exit 1
        ;;
esac

# ─── 경로 설정 ────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_SETTINGS="$SCRIPT_DIR/ProjectSettings/ProjectSettings.asset"
PROJECT_VERSION_FILE="$SCRIPT_DIR/ProjectSettings/ProjectVersion.txt"

# bundleVersion 파싱 (예: "  bundleVersion: 1.0" → "1.0")
VERSION=$(grep -m1 'bundleVersion:' "$PROJECT_SETTINGS" | awk '{print $2}')
if [[ -z "$VERSION" ]]; then
    echo "오류: ProjectSettings.asset에서 bundleVersion을 읽지 못했습니다."
    exit 1
fi

# Unity 에디터 버전 파싱 (예: "m_EditorVersion: 6000.5.7f1" → "6000.5.7f1")
UNITY_VERSION=$(grep -m1 'm_EditorVersion:' "$PROJECT_VERSION_FILE" | awk '{print $2}')
if [[ -z "$UNITY_VERSION" ]]; then
    echo "오류: ProjectVersion.txt에서 Unity 에디터 버전을 읽지 못했습니다."
    exit 1
fi

# Unity 실행 파일 경로 (환경변수로 오버라이드 가능)
# UNITY_PATH가 지정되지 않은 경우 후보 경로를 순서대로 탐색
if [[ -z "${UNITY_PATH:-}" ]]; then
    CANDIDATES=(
        "/Applications/Unity/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
        "/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
    )
    for CANDIDATE in "${CANDIDATES[@]}"; do
        if [[ -x "$CANDIDATE" ]]; then
            UNITY_PATH="$CANDIDATE"
            break
        fi
    done
fi

if [[ -z "${UNITY_PATH:-}" || ! -x "$UNITY_PATH" ]]; then
    echo "오류: Unity $UNITY_VERSION 실행 파일을 찾을 수 없습니다."
    echo "  탐색한 경로:"
    for CANDIDATE in "${CANDIDATES[@]}"; do
        echo "    $CANDIDATE"
    done
    echo "  Unity Hub에서 버전 $UNITY_VERSION 이 설치되어 있는지 확인하거나,"
    echo "  환경변수 UNITY_PATH에 직접 경로를 지정하세요."
    echo "  예: UNITY_PATH=/path/to/Unity ./build.sh ios"
    exit 1
fi

# 경로 설정
LOG_DIR="$SCRIPT_DIR/build/$VERSION"
LOG_FILE="$LOG_DIR/${PLATFORM_NAME}_build.log"
OUTPUT_DIR="$SCRIPT_DIR/build/$VERSION/$PLATFORM_NAME"
mkdir -p "$LOG_DIR"

# ─── 빌드 실행 ────────────────────────────────────────────────────────────────
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HillyWings $PLATFORM_NAME 빌드"
echo "  버전    : $VERSION"
echo "  Unity   : $UNITY_VERSION"
echo "  출력    : $OUTPUT_DIR"
echo "  로그    : $LOG_FILE"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

START_TIME=$(date +%s)

# set -e를 잠시 해제해 Unity 종료 코드를 직접 받음
set +e
"$UNITY_PATH" \
    -batchmode \
    -quit \
    -projectPath "$SCRIPT_DIR" \
    -executeMethod "$METHOD" \
    -logFile "$LOG_FILE"
EXIT_CODE=$?
set -e

END_TIME=$(date +%s)
ELAPSED=$((END_TIME - START_TIME))

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [[ $EXIT_CODE -eq 0 ]]; then
    echo "  결과    : ✅ SUCCESS"
    echo "  소요    : ${ELAPSED}초"
    echo "  출력    : $OUTPUT_DIR"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
else
    echo "  결과    : ❌ FAIL  (종료 코드: $EXIT_CODE)"
    echo "  소요    : ${ELAPSED}초"
    echo "  로그    : $LOG_FILE"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    # 로그에서 에러/경고 원인 줄만 추출해 출력
    echo "[ 오류 원인 ]"
    grep -E '(error|Error|ERROR|FAILED|exception|Exception|\[BuildMenu\])' "$LOG_FILE" 2>/dev/null \
        | grep -v '^#' \
        | tail -n 20 \
        || echo "  (로그에서 원인을 추출하지 못했습니다 — 전체 로그를 확인하세요)"
    echo ""
    echo "[ 로그 끝 부분 ]"
    tail -n 20 "$LOG_FILE" 2>/dev/null || true
    exit $EXIT_CODE
fi
