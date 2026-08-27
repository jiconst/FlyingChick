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
        private static readonly (string path, int faceIndex)[] KoreanFontCandidates =
        {
            ("/System/Library/Fonts/AppleSDGothicNeo.ttc", 0),
            ("/System/Library/Fonts/Supplemental/AppleGothic.ttf", 0),
        };

        private static TMP_FontAsset cached;

        public static TMP_FontAsset Get()
        {
            if (cached != null) return cached;

            foreach (var (path, faceIndex) in KoreanFontCandidates)
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

            // Safety net so a missing/renamed font file degrades to
            // Latin-only text instead of an NRE crashing the whole UI --
            // this path uses a real bundled Font asset (not an OS dynamic
            // font), so LoadFontFace succeeds; Korean text will show as
            // tofu if this branch is ever hit.
            Debug.LogError("UIFontProvider: no Korean-capable font file found among candidates -- Hangul text will be missing/tofu. Check the paths in KoreanFontCandidates for this machine/OS.");
            var fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cached = TMP_FontAsset.CreateFontAsset(fallbackFont, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048);
            cached.name = "Runtime Fallback SDF (Latin only)";
            return cached;
        }
    }
}
