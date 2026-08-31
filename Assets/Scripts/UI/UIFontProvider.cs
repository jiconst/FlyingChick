using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HillyWings
{
    // Runtime UI text needs Korean glyph coverage ("병아리", "해가 졌어요",
    // "다시하기", ...), but TextMeshPro's default font (LiberationSans SDF)
    // is Latin-only. This builds a Korean-capable TMP_FontAsset at runtime
    // with a dynamic atlas instead, and every UI text component gets it
    // assigned explicitly (UIFactory.CreateText), so
    // TMP_Settings.defaultFontAsset (and therefore the Latin-only font) is
    // never actually rendered.
    //
    // IMPORTANT -- "Import TMP Essential Resources" IS still required once,
    // despite the above: this was the original plan (avoid the manual
    // Editor step entirely), but TMP_FontAsset.CreateFontAssetInstance()
    // unconditionally reads TMP_Settings.instance for unrelated bookkeeping
    // (e.g. clearDynamicDataOnBuild) no matter which font-creation overload
    // is used or whether defaultFontAsset itself is ever touched.
    // TMP_Settings.instance is a Resources.Load<TMP_Settings>("TMP
    // Settings") lookup with no code-side fallback if that asset doesn't
    // exist -- it stays null forever and every unguarded
    // TMP_Settings.instance.xxx access throughout TMP's internals NREs. The
    // only fix is Window > TextMeshPro > Import TMP Essential Resources
    // (creates Assets/TextMesh Pro/Resources/TMP Settings.asset), a
    // one-time step documented here as an explicit exception to this
    // project's normal "no manual Editor setup" rule -- see CLAUDE.md. The
    // Liberation Sans font asset that import also adds is simply never
    // referenced by any of our code (we always pass our own font
    // explicitly), so it doesn't affect Korean rendering either way.
    //
    // BUG FIXED: the first version of this file built the source Font via
    // Font.CreateDynamicFontFromOSFont(...) and fed that into
    // TMP_FontAsset.CreateFontAsset(Font, ...). That looked reasonable but
    // doesn't actually work -- CreateFontAsset(Font) calls
    // FontEngine.LoadFontFace(font, ...), which needs a font with real
    // embedded glyph outline data ("Include Font Data"). A Font built from
    // CreateDynamicFontFromOSFont only carries an OS-level name reference
    // for legacy dynamic-font text rendering; it has no such data, so
    // LoadFontFace silently fails (no exception, just a Debug.LogWarning)
    // and CreateFontAsset returns null -- which then NRE'd one line later.
    // Fix: use the FILE PATH overload (TMP_FontAsset.CreateFontAsset(string
    // path, ...)), which hands the path straight to FontEngine instead of
    // going through a Font object at all.
    //
    // RISK/TODO: the paths below are macOS system font locations (this
    // project's dev OS) -- confirmed present via `ls` on the dev machine
    // when this was written, but that's still one specific machine, not a
    // guarantee for every Mac/OS version, and definitely not for Android/iOS
    // device builds later. If Korean text is missing/tofu on a different
    // machine, add that OS's font path here first.
    public static class UIFontProvider
    {
        // StreamingAssets에 번들된 폰트 파일명 목록 (한글 지원 TTF/TTC)
        // Assets/StreamingAssets/Fonts/ 에 파일을 추가하면 자동으로 탐색됨
        private static readonly string[] BundledFontNames =
        {
            "NanumGothic-Bold.ttf",
        };

        // macOS 시스템 폰트 후보 (에디터 및 macOS 빌드용)
        private static readonly (string path, int faceIndex)[] MacFontCandidates =
        {
            ("/System/Library/Fonts/AppleSDGothicNeo.ttc", 0),
            ("/System/Library/Fonts/Supplemental/AppleGothic.ttf", 0),
        };

        // 프로젝트에 번들된 LiberationSans 경로 (최후 폴백 — 한글 불가, NRE 방지용)
        private const string LiberationSansPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        private static TMP_FontAsset cached;

        public static TMP_FontAsset Get()
        {
            if (cached != null) return cached;

            // 1순위: StreamingAssets 번들 폰트 (iOS/Android/모든 플랫폼 공통)
            var streamingFontsDir = Path.Combine(Application.streamingAssetsPath, "Fonts");
            foreach (var name in BundledFontNames)
            {
                var path = Path.Combine(streamingFontsDir, name);
                if (!File.Exists(path)) continue;
                var asset = TMP_FontAsset.CreateFontAsset(path, 0, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048);
                if (asset != null)
                {
                    asset.name = "Runtime Korean SDF";
                    cached = asset;
                    return cached;
                }
            }

            // 2순위: macOS 시스템 폰트 (에디터 / macOS 빌드)
            foreach (var (path, faceIndex) in MacFontCandidates)
            {
                if (!File.Exists(path)) continue;
                var asset = TMP_FontAsset.CreateFontAsset(path, faceIndex, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048);
                if (asset != null)
                {
                    asset.name = "Runtime Korean SDF";
                    cached = asset;
                    return cached;
                }
            }

            // 3순위: 프로젝트 번들 LiberationSans (한글 불가, 최후 NRE 방지 폴백)
            Debug.LogError(
                "UIFontProvider: 한글 폰트를 찾지 못했습니다.\n" +
                "Assets/StreamingAssets/Fonts/NanumGothic.ttf 를 추가하면 한글이 정상 표시됩니다.\n" +
                "현재는 Latin 전용 폰트(LiberationSans)로 폴백합니다 — 한글이 □로 표시됩니다.");

            if (File.Exists(LiberationSansPath))
            {
                var asset = TMP_FontAsset.CreateFontAsset(LiberationSansPath, 0, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048);
                if (asset != null)
                {
                    asset.name = "Runtime Fallback SDF (Latin only)";
                    cached = asset;
                    return cached;
                }
            }

            // 최후의 최후: TMP 기본 폰트 에셋 사용 (NRE 방지)
            cached = TMP_Settings.defaultFontAsset;
            Debug.LogError("UIFontProvider: 모든 폰트 후보 실패. TMP 기본 폰트를 사용합니다.");
            return cached;
        }
    }
}
