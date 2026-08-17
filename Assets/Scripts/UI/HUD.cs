using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingChick
{
    // M2 scope was a functional score/island/fever/streak readout via
    // OnGUI. M7: converted to a runtime-built UGUI/TextMeshPro hierarchy
    // (Canvas + TMP_Text + Image), per explicit choice among OnGUI
    // replacement options (see CLAUDE.md). Still fully code-driven -- built
    // once in Bind(), never touched in the Editor. Toasts and pickup toasts
    // use small fixed-size pools of pre-built UI elements (enabled/disabled
    // + text/color/position updated per frame) instead of
    // Instantiate/Destroy, matching this project's existing pooling
    // convention (see Collectibles/CoinSpawner etc).
    //
    // Korean text ("STREAK"/미션 설명 등) needs a font asset with Hangul
    // coverage -- see UIFontProvider for how that's built at runtime
    // without a manual Editor import step. If Korean glyphs show as tofu
    // boxes on first playtest, that file is the place to look.
    public class HUD : MonoBehaviour
    {
        private struct Toast
        {
            public string Text;
            public Color Color;
            public float Duration;
            public float TimeLeft;
        }

        private struct PositionedToast
        {
            public string Text;
            public Color Color;
            public Vector3 WorldPos;
            public float Duration;
            public float TimeLeft;
        }

        private const int ToastPoolSize = 6;
        private const int PickupToastPoolSize = 10;
        private const int NestLinePoolSize = 5;
        private const int StreakDotCount = 3;

        [SerializeField] private float toastDuration = 1.1f;
        [SerializeField] private float feverToastDuration = 1.6f;
        [SerializeField] private float pickupToastDuration = 0.8f;

        private BirdController bird;
        private ScoreManager score;
        private SlideJudge slideJudge;
        private FeverSystem fever;
        private GameManager gameManager;
        private DayCycle dayCycle;
        private Camera cam;
        private CoinSpawner coinSpawner;
        private CloudSpawner cloudSpawner;
        private NestMultiplier nest;

        private readonly List<Toast> toasts = new List<Toast>();
        private readonly List<PositionedToast> pickupToasts = new List<PositionedToast>();

        private GameObject root;

        private RectTransform scorePanelRect;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI midText;
        private RectTransform midRect;

        private RectTransform dayTrackRect;
        private Image dayTrackFill;
        private RectTransform sunIconRect;

        private RectTransform streakPanelRect;
        private TextMeshProUGUI streakLabel;
        private readonly Image[] streakDots = new Image[StreakDotCount];

        private RectTransform nestPanelRect;
        private Image nestPanelBg;
        private TextMeshProUGUI nestHeaderText;
        private readonly TextMeshProUGUI[] nestLines = new TextMeshProUGUI[NestLinePoolSize];

        private RectTransform feverRect;
        private TextMeshProUGUI feverText;

        private readonly TextMeshProUGUI[] toastPool = new TextMeshProUGUI[ToastPoolSize];
        private readonly TextMeshProUGUI[] pickupToastPool = new TextMeshProUGUI[PickupToastPoolSize];

        private RectTransform debugPanelRect;
        private TextMeshProUGUI dbgStateText;
        private TextMeshProUGUI dbgHeightText;

        public void Bind(BirdController birdRef, ScoreManager scoreRef, SlideJudge slideJudgeRef, FeverSystem feverRef, GameManager gameManagerRef)
        {
            bird = birdRef;
            score = scoreRef;
            slideJudge = slideJudgeRef;
            fever = feverRef;
            gameManager = gameManagerRef;

            slideJudge.OnGreatSlide += HandleGreatSlide;
            slideJudge.OnStreakBroken += HandleStreakBroken;
            fever.OnFeverStart += HandleFeverStart;

            BuildUI();
        }

        public void BindDayCycle(DayCycle dayCycleRef) => dayCycle = dayCycleRef;
        public void BindMeta(NestMultiplier nestRef) => nest = nestRef;

        public void BindCollectibles(CoinSpawner coinSpawnerRef, CloudSpawner cloudSpawnerRef, Camera camera)
        {
            cam = camera;
            coinSpawner = coinSpawnerRef;
            cloudSpawner = cloudSpawnerRef;
            coinSpawner.OnPickupPopup += HandlePickupPopup;
            cloudSpawner.OnPickupPopup += HandlePickupPopup;
        }

        private void HandlePickupPopup(Vector3 worldPos, string text, Color color)
        {
            pickupToasts.Add(new PositionedToast { Text = text, Color = color, WorldPos = worldPos, Duration = pickupToastDuration, TimeLeft = pickupToastDuration });
        }

        private void OnDestroy()
        {
            if (slideJudge != null)
            {
                slideJudge.OnGreatSlide -= HandleGreatSlide;
                slideJudge.OnStreakBroken -= HandleStreakBroken;
            }
            if (fever != null) fever.OnFeverStart -= HandleFeverStart;
            if (coinSpawner != null) coinSpawner.OnPickupPopup -= HandlePickupPopup;
            if (cloudSpawner != null) cloudSpawner.OnPickupPopup -= HandlePickupPopup;
        }

        private void HandleGreatSlide(int streak, int gained)
        {
            string text = streak >= 3 ? $"GREAT SLIDE x{streak}! +{gained}" : $"SLIDE! +{gained}";
            AddToast(text, new Color(1f, 0.6f, 0.2f), toastDuration);
        }

        private void HandleStreakBroken() => AddToast("STREAK RESET", new Color(0.85f, 0.35f, 0.35f), toastDuration);
        private void HandleFeverStart() => AddToast($"FEVER TRIGGERED!  SCORE x{fever.Multiplier:0}", new Color(1f, 0.3f, 0.55f), feverToastDuration);

        private void AddToast(string text, Color color, float duration)
        {
            toasts.Add(new Toast { Text = text, Color = color, Duration = duration, TimeLeft = duration });
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("HUD Canvas", 0);
            root = canvas.gameObject;
            var t = canvas.transform;

            var brown = new Color(0.42f, 0.29f, 0.12f);

            var scorePanel = UIFactory.CreatePanel(t, "ScorePanel", new Color(0f, 0f, 0f, 0.4f));
            scorePanelRect = (RectTransform)scorePanel.transform;
            UIFactory.SetTopLeft(scorePanelRect, 12, 8, 260, 56);

            scoreText = UIFactory.CreateText(t, "Score", 38, new Color(1f, 0.95f, 0.75f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)scoreText.transform, 24, 14, 300, 48);

            midText = UIFactory.CreateText(t, "IslandMult", 18, brown);
            midRect = (RectTransform)midText.transform;

            BuildDayClock(t);
            BuildStreakPanel(t);
            BuildNestPanel(t);
            BuildFeverBadge(t);
            BuildToastPool(t);
            BuildPickupToastPool(t);
            BuildDebugText(t);
        }

        private void BuildDayClock(Transform parent)
        {
            var track = UIFactory.CreatePanel(parent, "DayTrack", new Color(0f, 0f, 0f, 0.45f));
            dayTrackRect = (RectTransform)track.transform;

            dayTrackFill = UIFactory.CreatePanel(track.transform, "DayFill", new Color(1f, 0.6f, 0.15f));
            var fillRect = (RectTransform)dayTrackFill.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            var sunSprite = BuildSunSprite(32);
            var sun = UIFactory.CreateImage(parent, "SunIcon", sunSprite);
            sunIconRect = (RectTransform)sun.transform;
        }

        private void BuildStreakPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "StreakPanel", new Color(0f, 0f, 0f, 0.35f));
            streakPanelRect = (RectTransform)panel.transform;

            streakLabel = UIFactory.CreateText(panel.transform, "StreakLabel", 18, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)streakLabel.transform, 10f, 4f, 190f, 24f);

            for (int i = 0; i < StreakDotCount; i++)
            {
                var dot = UIFactory.CreatePanel(panel.transform, $"Dot{i}", Color.white);
                streakDots[i] = dot;
                UIFactory.SetTopLeft((RectTransform)dot.transform, 10f + i * 34f, 28f, 26f, 26f);
            }
        }

        private void BuildNestPanel(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "NestPanel", new Color(0f, 0f, 0f, 0.3f));
            nestPanelRect = (RectTransform)panel.transform;
            nestPanelBg = panel;

            nestHeaderText = UIFactory.CreateText(panel.transform, "NestHeader", 13, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.SetTopLeft((RectTransform)nestHeaderText.transform, 8f, 2f, 214f, 18f);

            for (int i = 0; i < NestLinePoolSize; i++)
            {
                var line = UIFactory.CreateText(panel.transform, $"NestLine{i}", 12, Color.white);
                nestLines[i] = line;
                UIFactory.SetTopLeft((RectTransform)line.transform, 8f, 20f + i * 20f, 214f, 18f);
            }
        }

        private void BuildFeverBadge(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "FeverBadge", new Color(1f, 0.25f, 0.5f));
            feverRect = (RectTransform)panel.transform;

            feverText = UIFactory.CreateText(panel.transform, "FeverText", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.StretchFull((RectTransform)feverText.transform);

            panel.gameObject.SetActive(false);
        }

        private void BuildToastPool(Transform parent)
        {
            for (int i = 0; i < ToastPoolSize; i++)
            {
                var text = UIFactory.CreateText(parent, $"Toast{i}", 26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                toastPool[i] = text;
                text.gameObject.SetActive(false);
            }
        }

        private void BuildPickupToastPool(Transform parent)
        {
            for (int i = 0; i < PickupToastPoolSize; i++)
            {
                var text = UIFactory.CreateText(parent, $"PickupToast{i}", 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                pickupToastPool[i] = text;
                text.gameObject.SetActive(false);
            }
        }

        private void BuildDebugText(Transform parent)
        {
            var panel = UIFactory.CreatePanel(parent, "DebugPanel", new Color(0f, 0f, 0f, 0.45f));
            debugPanelRect = (RectTransform)panel.transform;

            var brightWhite = new Color(1f, 0.95f, 0.75f);
            dbgStateText = UIFactory.CreateText(parent, "DbgState", 16, brightWhite, TextAlignmentOptions.TopRight, FontStyles.Bold);
            dbgHeightText = UIFactory.CreateText(parent, "DbgHeight", 16, brightWhite, TextAlignmentOptions.TopRight, FontStyles.Bold);
        }

        private void Update()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var tst = toasts[i];
                tst.TimeLeft -= Time.deltaTime;
                if (tst.TimeLeft <= 0f) toasts.RemoveAt(i);
                else toasts[i] = tst;
            }

            for (int i = pickupToasts.Count - 1; i >= 0; i--)
            {
                var tst = pickupToasts[i];
                tst.TimeLeft -= Time.deltaTime;
                if (tst.TimeLeft <= 0f) pickupToasts.RemoveAt(i);
                else pickupToasts[i] = tst;
            }

            if (score == null) return;

            bool playing = gameManager.State == GameState.Playing;
            if (root.activeSelf != playing) root.SetActive(playing);
            if (!playing) return;

            scoreText.text = score.Score.ToString("N0");

            midText.text = $"Island {gameManager.Island} · {gameManager.Multiplier}x";
            UIFactory.SetTopLeft(midRect, Screen.width - 220, 14, 200, 30);

            if (dayCycle != null) UpdateDayClock();
            UpdateStreakPanel();

            bool feverActive = fever.IsActive;
            if (feverRect.gameObject.activeSelf != feverActive) feverRect.gameObject.SetActive(feverActive);
            if (feverActive) UpdateFeverBadge();

            if (nest != null) UpdateNestPanel();
            else nestPanelBg.gameObject.SetActive(false);

            UpdateToasts();
            UpdatePickupToasts();

            bool showHeight = cam != null;
            UIFactory.SetTopLeft(debugPanelRect, Screen.width - 260, Screen.height - (showHeight ? 66 : 44), 248, showHeight ? 60 : 38);

            string state = bird.OnGround ? "Grounded" : bird.Airborne ? "Airborne" : "Falling";
            dbgStateText.text = $"{state}  spd {bird.Speed:0}{(bird.IsDiving ? "  DIVE" : "")}";
            UIFactory.SetTopLeft((RectTransform)dbgStateText.transform, Screen.width - 250, Screen.height - 58, 230, 24);

            if (showHeight)
            {
                dbgHeightText.gameObject.SetActive(true);
                dbgHeightText.text = $"height {bird.HeightAboveGround:0}  zoom {cam.orthographicSize:0}";
                UIFactory.SetTopLeft((RectTransform)dbgHeightText.transform, Screen.width - 250, Screen.height - 34, 230, 24);
            }
            else
            {
                dbgHeightText.gameObject.SetActive(false);
            }
        }

        private void UpdateDayClock()
        {
            const float trackW = 100f, trackH = 10f;
            UIFactory.SetTopLeft(dayTrackRect, Screen.width - 120f, 48f, trackW, trackH);

            float t = dayCycle.DayTime;
            ((RectTransform)dayTrackFill.transform).sizeDelta = new Vector2(trackW * t, 0f);
            dayTrackFill.color = Color.Lerp(new Color(1f, 0.6f, 0.15f), new Color(0.55f, 0.3f, 0.85f), t);

            const float sunSize = 24f;
            float sunX = Screen.width - 120f + trackW * t - sunSize * 0.5f;
            float sunY = 48f + trackH * 0.5f - sunSize * 0.5f;
            UIFactory.SetTopLeft(sunIconRect, sunX, sunY, sunSize, sunSize);
        }

        private void UpdateStreakPanel()
        {
            const float panelW = 210f, panelH = 56f;
            UIFactory.SetTopLeft(streakPanelRect, 16f, Screen.height - panelH - 14f, panelW, panelH);

            streakLabel.text = $"STREAK {slideJudge.SlideStreak}/3";

            for (int i = 0; i < StreakDotCount; i++)
            {
                bool lit = slideJudge.SlideStreak > i || fever.IsActive;
                streakDots[i].color = lit ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        private void UpdateNestPanel()
        {
            var missions = nest.ActiveMissions;
            if (missions.Length == 0)
            {
                nestPanelBg.gameObject.SetActive(false);
                return;
            }

            nestPanelBg.gameObject.SetActive(true);
            const float panelW = 230f;
            float panelH = 22f + missions.Length * 20f;
            UIFactory.SetTopLeft(nestPanelRect, 20f, 66f, panelW, panelH);

            nestHeaderText.text = string.Format(Localization.Get("hud.nestHeader"), nest.Bonus);

            for (int i = 0; i < NestLinePoolSize; i++)
            {
                if (i >= missions.Length) { nestLines[i].gameObject.SetActive(false); continue; }

                nestLines[i].gameObject.SetActive(true);
                var mission = missions[i];
                float progress = nest.GetProgress(mission);
                bool done = progress >= mission.Target;
                nestLines[i].color = done ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.85f);
                string mark = done ? "O" : $"{Mathf.Min(progress, mission.Target):0}/{mission.Target}";
                nestLines[i].text = $"{mission.Description} ({mark})";
            }
        }

        private void UpdateFeverBadge()
        {
            UIFactory.SetTopLeftCentered(feverRect, Screen.width * 0.5f - 130f, 16f, 260f, 44f);
            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.06f;
            feverRect.localScale = new Vector3(pulse, pulse, 1f);
            feverText.text = $"FEVER x{fever.Multiplier:0}  {fever.TimeRemaining:0.0}s";
        }

        private void UpdateToasts()
        {
            float y = Screen.height * 0.32f;
            for (int i = 0; i < ToastPoolSize; i++)
            {
                if (i >= toasts.Count) { toastPool[i].gameObject.SetActive(false); continue; }

                var tst = toasts[i];
                toastPool[i].gameObject.SetActive(true);
                float alpha = Mathf.Clamp01(tst.TimeLeft / (tst.Duration * 0.4f));
                float rise = (1f - tst.TimeLeft / tst.Duration) * 30f;
                var c = tst.Color;
                c.a = alpha;
                toastPool[i].color = c;
                toastPool[i].text = tst.Text;
                UIFactory.SetTopLeftCentered((RectTransform)toastPool[i].transform, Screen.width * 0.5f - 260f, y - rise, 520f, 40f);
                y += 34f;
            }
        }

        private void UpdatePickupToasts()
        {
            if (cam == null)
            {
                for (int i = 0; i < PickupToastPoolSize; i++) pickupToastPool[i].gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < PickupToastPoolSize; i++)
            {
                if (i >= pickupToasts.Count) { pickupToastPool[i].gameObject.SetActive(false); continue; }

                var tst = pickupToasts[i];
                Vector3 screenPos = cam.WorldToScreenPoint(tst.WorldPos);
                if (screenPos.z < 0f) { pickupToastPool[i].gameObject.SetActive(false); continue; }

                pickupToastPool[i].gameObject.SetActive(true);
                float alpha = Mathf.Clamp01(tst.TimeLeft / (tst.Duration * 0.5f));
                float rise = (1f - tst.TimeLeft / tst.Duration) * 24f;
                var c = tst.Color;
                c.a = alpha;
                pickupToastPool[i].color = c;
                pickupToastPool[i].text = tst.Text;

                float guiY = Screen.height - screenPos.y;
                UIFactory.SetTopLeftCentered((RectTransform)pickupToastPool[i].transform, screenPos.x - 80f, guiY - rise - 20f, 160f, 26f);
            }
        }

        // Cute sun icon that rides the day-clock progress -- same
        // procedural pixel-drawing technique as BirdVisual's chick, now
        // baked into a Sprite once (instead of a raw Texture2D handed to
        // GUI.DrawTexture every OnGUI call).
        private Sprite BuildSunSprite(int size)
        {
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            float cx = size * 0.5f, cy = size * 0.5f;
            float bodyR = size * 0.30f;
            var core = new Color(1f, 0.82f, 0.25f);
            var rayColor = new Color(1f, 0.62f, 0.15f);
            var faceColor = new Color(0.35f, 0.22f, 0.05f);

            const int rayCount = 8;
            for (int r = 0; r < rayCount; r++)
            {
                float angle = r * Mathf.PI * 2f / rayCount + Mathf.PI / 8f;
                float dirX = Mathf.Cos(angle), dirY = Mathf.Sin(angle);
                for (int k = 0; k < 3; k++)
                {
                    float dist = bodyR * 0.95f + k * size * 0.085f;
                    float px = cx + dirX * dist;
                    float py = cy + dirY * dist;
                    float rr = size * (0.05f - k * 0.009f);
                    FillDot(pixels, size, px, py, rr, rayColor);
                }
            }

            FillDot(pixels, size, cx, cy, bodyR, core);
            FillDot(pixels, size, cx - bodyR * 0.4f, cy - bodyR * 0.1f, size * 0.035f, faceColor);
            FillDot(pixels, size, cx + bodyR * 0.4f, cy - bodyR * 0.1f, size * 0.035f, faceColor);
            for (int i = -2; i <= 2; i++)
            {
                float sx = cx + i * size * 0.035f;
                float sy = cy + bodyR * 0.25f + (2 - Mathf.Abs(i)) * size * 0.02f;
                FillDot(pixels, size, sx, sy, size * 0.025f, faceColor);
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static void FillDot(Color[] pixels, int size, float cx, float cy, float r, Color color)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r)
                        pixels[y * size + x] = color;
                }
            }
        }
    }
}
