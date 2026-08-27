using UnityEngine;

namespace HillyWings
{
    // Reference: const PALETTES = [...] (10 entries), currentPalette() =
    // PALETTES[(island-1) % PALETTES.length]. Hex values copied verbatim
    // from hilly-wings.html so this stays visually identical to the
    // validated reference.
    public struct IslandPalette
    {
        public Color SkyTop;
        public Color SkyBottom;
        public Color HillTop;
        public Color Hill0;
        public Color Hill1;
        public Color Hill2;
        public Color Grass;
    }

    public static class IslandPalettes
    {
        public static readonly IslandPalette[] All =
        {
            Make("#fbf7d8", "#eef0a8", "#f3c14a", "#f0a83a", "#e88b2a", "#d97220", "#7a9a2e"),
            Make("#dff3ff", "#a9dcf5", "#6fc7d8", "#49aecb", "#2f8fc0", "#2f6fb0", "#2e7a8f"),
            Make("#ffe9e0", "#ffc2b0", "#ff9a76", "#f4744f", "#e0553f", "#c23b3b", "#a83b4e"),
            Make("#efe0ff", "#c9a8f5", "#b98cf0", "#9d6fe0", "#7f52c8", "#6a3fb0", "#5c3f9a"),
            Make("#e2ffe8", "#a8f0c2", "#5fd89a", "#3fc487", "#2fa877", "#268f6a", "#1f7a5e"),
            Make("#fff3d8", "#ffd98a", "#ffb84a", "#f2963a", "#e0742a", "#c85a20", "#8a6a2a"),
            Make("#ffe0f0", "#ffb0d8", "#ff8ac0", "#f45fa8", "#e03f90", "#c23b78", "#a83b6e"),
            Make("#e0f4ff", "#a8d8f5", "#6fb8e0", "#4f98d0", "#3f78c0", "#3b5fb0", "#2e5a8f"),
            Make("#f0f0f0", "#c8c8d8", "#a8a8c0", "#8888a8", "#6a6a90", "#55557a", "#4a4a6a"),
            Make("#fff8e0", "#ffe0a8", "#ffcf5a", "#f2a83a", "#e0882a", "#c86a20", "#8a7a2a"),
        };

        public static IslandPalette ForIsland(int island)
        {
            int index = Mathf.Max(0, island - 1) % All.Length;
            return All[index];
        }

        private static IslandPalette Make(string skyTop, string skyBottom, string hillTop, string hill0, string hill1, string hill2, string grass)
        {
            return new IslandPalette
            {
                SkyTop = ColorUtil.Hex(skyTop),
                SkyBottom = ColorUtil.Hex(skyBottom),
                HillTop = ColorUtil.Hex(hillTop),
                Hill0 = ColorUtil.Hex(hill0),
                Hill1 = ColorUtil.Hex(hill1),
                Hill2 = ColorUtil.Hex(hill2),
                Grass = ColorUtil.Hex(grass),
            };
        }
    }
}
