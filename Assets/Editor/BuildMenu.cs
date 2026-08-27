// Unity 에디터 전용 — 빌드 결과물에 포함되지 않음.
// 메뉴 HillyWings > Build > * 로 각 플랫폼 빌드를 실행함.
// 출력 경로: {프로젝트루트}/build/{버전}/{플랫폼}/
// 빌드 완료 시 해당 폴더를 Finder에서 자동으로 엶.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HillyWings.Editor
{
    public static class BuildMenu
    {
        // ─────────────────────────────────────────────
        // 메뉴 항목
        // ─────────────────────────────────────────────

        [MenuItem("HillyWings/Build/iPhone (iOS)", priority = 200)]
        public static void BuildIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("HillyWings/Build/Android", priority = 201)]
        public static void BuildAndroid() => Build(BuildTarget.Android, "Android");

        [MenuItem("HillyWings/Build/Windows (x64)", priority = 202)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows");

        // ─────────────────────────────────────────────
        // 메뉴 활성화 조건 (해당 모듈 미설치 시 회색 처리)
        // ─────────────────────────────────────────────

        [MenuItem("HillyWings/Build/iPhone (iOS)", validate = true)]
        private static bool ValidateIOS() =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);

        [MenuItem("HillyWings/Build/Android", validate = true)]
        private static bool ValidateAndroid() =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

        [MenuItem("HillyWings/Build/Windows (x64)", validate = true)]
        private static bool ValidateWindows() =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        // ─────────────────────────────────────────────
        // 공통 빌드 로직
        // ─────────────────────────────────────────────

        private static void Build(BuildTarget target, string platformName)
        {
            // Build Settings에 활성화된 씬 목록
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildMenu] 빌드할 씬이 없습니다. File > Build Settings에서 씬을 추가하세요.");
                return;
            }

            // 출력 경로: {프로젝트루트}/build/{버전}/{플랫폼}/
            string version = PlayerSettings.bundleVersion;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDir = Path.Combine(projectRoot, "build", version, platformName);
            Directory.CreateDirectory(outputDir);

            // 플랫폼별 실제 빌드 결과물 경로
            string outputPath = target switch
            {
                BuildTarget.iOS                  => outputDir,                              // Xcode 프로젝트 폴더
                BuildTarget.Android              => Path.Combine(outputDir, "HillyWings.apk"),
                BuildTarget.StandaloneWindows64  => Path.Combine(outputDir, "HillyWings.exe"),
                _                               => Path.Combine(outputDir, "HillyWings"),
            };

            Debug.Log($"[BuildMenu] {platformName} 빌드 시작 → {outputPath}");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes            = scenes,
                locationPathName  = outputPath,
                target            = target,
                options           = BuildOptions.None,
            });

            BuildSummary summary = report.summary;

            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    long mb = (long)(summary.totalSize / (1024 * 1024));
                    double sec = summary.totalTime.TotalSeconds;
                    Debug.Log($"[BuildMenu] {platformName} 빌드 완료 — {outputDir} ({mb} MB, {sec:F1}초)");
                    // 배치 모드(CLI 빌드)에서는 창이 없으므로 RevealInFinder를 건너뜀
                    if (!Application.isBatchMode)
                        EditorUtility.RevealInFinder(outputDir);
                    break;

                case BuildResult.Failed:
                    Debug.LogError($"[BuildMenu] {platformName} 빌드 실패 (에러 {summary.totalErrors}개) — " +
                                   "Window > Build > Build Report 에서 상세 내용을 확인하세요.");
                    // 배치 모드에서는 비정상 종료 코드로 CI가 실패를 감지하게 함
                    if (Application.isBatchMode)
                        EditorApplication.Exit(1);
                    break;

                case BuildResult.Cancelled:
                    Debug.LogWarning($"[BuildMenu] {platformName} 빌드가 취소되었습니다.");
                    if (Application.isBatchMode)
                        EditorApplication.Exit(2);
                    break;
            }
        }
    }
}
