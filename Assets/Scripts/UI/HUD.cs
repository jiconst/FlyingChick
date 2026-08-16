using System.Collections.Generic;
using UnityEngine;

namespace FlyingChick
{
    // M2 scope: functional score/island/fever/streak readout via OnGUI --
    // no Canvas/TMP yet, that's a later visual pass (M6/M7). Also carries a
    // small physics debug line (was Debug/SimpleHud, now retired).
    //
    // Streak/fever feedback was reported hard to notice twice now. Boxes are
    // drawn with a solid 1x1 texture + GUI.color instead of GUI.Box's
    // backgroundColor tint (the default Unity skin's box texture doesn't
    // tint reliably), and the streak/fever elements are bigger with a solid
    // backing panel so they read clearly against the terrain.
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
        private Texture2D solidTex;
        private Texture2D sunTex;

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
        }

        public void BindDayCycle(DayCycle dayCycleRef)
        {
            dayCycle = dayCycleRef;
        }

        public void BindMeta(NestMultiplier nestRef)
        {
            nest = nestRef;
        }

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
            if (fever != null)
            {
                fever.OnFeverStart -= HandleFeverStart;
            }
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

        private void Update()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                t.TimeLeft -= Time.deltaTime;
                if (t.TimeLeft <= 0f) toasts.RemoveAt(i);
                else toasts[i] = t;
            }

            for (int i = pickupToasts.Count - 1; i >= 0; i--)
            {
                var t = pickupToasts[i];
                t.TimeLeft -= Time.deltaTime;
                if (t.TimeLeft <= 0f) pickupToasts.RemoveAt(i);
                else pickupToasts[i] = t;
            }
        }

        private void OnGUI()
        {
            if (score == null) return;
            if (gameManager.State != GameState.Playing) return;

            var scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
            scoreStyle.normal.textColor = new Color(0.42f, 0.29f, 0.12f);
            var midStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            midStyle.normal.textColor = new Color(0.42f, 0.29f, 0.12f);

            GUI.Label(new Rect(20, 14, 300, 44), score.Score.ToString("N0"), scoreStyle);
            GUI.Label(new Rect(Screen.width - 220, 14, 200, 30), $"Island {gameManager.Island} · {gameManager.Multiplier}x", midStyle);
            if (dayCycle != null) DrawDayClock();

            DrawStreakPanel();
            if (fever.IsActive) DrawFeverBadge();
            if (nest != null) DrawNestPanel();
            DrawToasts();
            DrawPickupToasts();

            var dbgStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            dbgStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            string state = bird.OnGround ? "Grounded" : bird.Airborne ? "Airborne" : "Falling";
            GUI.Label(new Rect(Screen.width - 220, Screen.height - 50, 220, 20), $"{state}  spd {bird.Speed:0}{(bird.IsDiving ? "  DIVE" : "")}", dbgStyle);
            if (cam != null)
                GUI.Label(new Rect(Screen.width - 220, Screen.height - 30, 220, 20), $"height {bird.HeightAboveGround:0}  zoom {cam.orthographicSize:0}", dbgStyle);
        }

        private void DrawDayClock()
        {
            const float trackW = 100f, trackH = 10f;
            var trackRect = new Rect(Screen.width - 120f, 48f, trackW, trackH);

            // Dark, sky-independent track so it stays visible whether the
            // background is the pale day sky or the dark night sky (the old
            // pale-yellow fill on a pale-yellow sky was nearly invisible).
            DrawRect(trackRect, new Color(0f, 0f, 0f, 0.45f));

            float t = dayCycle.DayTime;
            var fillRect = new Rect(trackRect.x, trackRect.y, trackW * t, trackH);
            DrawRect(fillRect, Color.Lerp(new Color(1f, 0.6f, 0.15f), new Color(0.55f, 0.3f, 0.85f), t));

            const float sunSize = 24f;
            float sunX = trackRect.x + trackW * t - sunSize * 0.5f;
            float sunY = trackRect.y + trackH * 0.5f - sunSize * 0.5f;
            GUI.DrawTexture(new Rect(sunX, sunY, sunSize, sunSize), SunTexture);
        }

        private Texture2D SunTexture => sunTex != null ? sunTex : (sunTex = BuildSunTexture(32));

        private void DrawStreakPanel()
        {
            const float panelW = 210f, panelH = 56f;
            var panelRect = new Rect(16f, Screen.height - panelH - 14f, panelW, panelH);
            DrawRect(panelRect, new Color(0f, 0f, 0f, 0.35f));

            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 4f, panelW - 20f, 24f), $"STREAK {slideJudge.SlideStreak}/3", labelStyle);

            for (int i = 0; i < 3; i++)
            {
                bool lit = slideJudge.SlideStreak > i || fever.IsActive;
                var dotRect = new Rect(panelRect.x + 10f + i * 34f, panelRect.y + 28f, 26f, 26f);
                DrawRect(dotRect, lit ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.25f));
            }
        }

        private void DrawNestPanel()
        {
            var missions = nest.ActiveMissions;
            if (missions.Length == 0) return;

            const float panelW = 230f;
            float panelH = 22f + missions.Length * 20f;
            var panelRect = new Rect(20f, 66f, panelW, panelH);
            DrawRect(panelRect, new Color(0f, 0f, 0f, 0.3f));

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 2f, panelW - 16f, 18f), $"Nest 목표 (+{nest.Bonus} 배수)", headerStyle);

            var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            float y = panelRect.y + 20f;
            foreach (var mission in missions)
            {
                float progress = nest.GetProgress(mission);
                bool done = progress >= mission.Target;
                lineStyle.normal.textColor = done ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.85f);
                string mark = done ? "✓" : $"{Mathf.Min(progress, mission.Target):0}/{mission.Target}";
                GUI.Label(new Rect(panelRect.x + 8f, y, panelW - 16f, 18f), $"{mission.Description} ({mark})", lineStyle);
                y += 20f;
            }
        }

        private void DrawFeverBadge()
        {
            var prevMatrix = GUI.matrix;

            var rect = new Rect(Screen.width * 0.5f - 130f, 16f, 260f, 44f);
            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.06f;
            GUIUtility.ScaleAroundPivot(new Vector2(pulse, pulse), new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f));

            DrawRect(rect, new Color(1f, 0.25f, 0.5f));

            var feverStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            feverStyle.normal.textColor = Color.white;
            GUI.Label(rect, $"FEVER x{fever.Multiplier:0}  {fever.TimeRemaining:0.0}s", feverStyle);

            GUI.matrix = prevMatrix;
        }

        private void DrawToasts()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            float y = Screen.height * 0.32f;
            foreach (var t in toasts)
            {
                float alpha = Mathf.Clamp01(t.TimeLeft / (t.Duration * 0.4f));
                float rise = (1f - t.TimeLeft / t.Duration) * 30f;
                var c = t.Color;
                c.a = alpha;
                style.normal.textColor = c;
                GUI.Label(new Rect(Screen.width * 0.5f - 260f, y - rise, 520f, 40f), t.Text, style);
                y += 34f;
            }
        }

        private void DrawPickupToasts()
        {
            if (cam == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (var t in pickupToasts)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(t.WorldPos);
                if (screenPos.z < 0f) continue; // behind camera, don't draw

                float alpha = Mathf.Clamp01(t.TimeLeft / (t.Duration * 0.5f));
                float rise = (1f - t.TimeLeft / t.Duration) * 24f;
                var c = t.Color;
                c.a = alpha;
                style.normal.textColor = c;

                float guiY = Screen.height - screenPos.y; // GUI space has y-down, screen space y-up
                GUI.Label(new Rect(screenPos.x - 80f, guiY - rise - 20f, 160f, 26f), t.Text, style);
            }
        }

        // Cute sun icon that rides the day-clock progress (round body, dotted
        // rays, tiny face) -- procedural, same technique as BirdVisual's chick.
        private Texture2D BuildSunTexture(int size)
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
            return tex;
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

        private void DrawRect(Rect rect, Color color)
        {
            if (solidTex == null)
            {
                solidTex = new Texture2D(1, 1);
                solidTex.SetPixel(0, 0, Color.white);
                solidTex.Apply();
            }
            var prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, solidTex);
            GUI.color = prevColor;
        }
    }
}
