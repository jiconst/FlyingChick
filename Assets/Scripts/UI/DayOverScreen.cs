using UnityEngine;

namespace FlyingChick
{
    // Reference: endScreen -- final stats (score/island/slides), submits to
    // SaveSystem, shows Best line, "다시하기"(restart)/"홈"(back to start)
    // buttons. Coin count-up animation is a later visual pass.
    //
    // M5 additions: coins earned this run (CoinWallet), and pass/fail for
    // this run's 3 Nest Multiplier objectives (NestMultiplier).
    public class DayOverScreen : MonoBehaviour
    {
        private ScoreManager score;
        private SlideJudge slideJudge;
        private CloudSpawner cloudSpawner;
        private FeverSystem fever;
        private GameManager gameManager;
        private CoinWallet wallet;
        private NestMultiplier nest;

        private bool submittedThisRun;
        private bool isNewBest;

        public void Bind(ScoreManager scoreRef, SlideJudge slideJudgeRef, CloudSpawner cloudSpawnerRef, FeverSystem feverRef, GameManager gameManagerRef, CoinWallet walletRef, NestMultiplier nestRef)
        {
            score = scoreRef;
            slideJudge = slideJudgeRef;
            cloudSpawner = cloudSpawnerRef;
            fever = feverRef;
            gameManager = gameManagerRef;
            wallet = walletRef;
            nest = nestRef;

            gameManager.OnRunStart += HandleRunStart;
        }

        private void OnDestroy()
        {
            if (gameManager != null)
                gameManager.OnRunStart -= HandleRunStart;
        }

        private void HandleRunStart() => submittedThisRun = false;

        private void Update()
        {
            if (gameManager.State != GameState.DayOver) return;

            if (!submittedThisRun)
            {
                isNewBest = SaveSystem.Instance != null && SaveSystem.Instance.SubmitScore(score.Score);
                submittedThisRun = true;
            }
        }

        private void OnGUI()
        {
            if (gameManager.State != GameState.DayOver) return;

            DrawOverlay();

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.32f, 0.2f, 0.36f);
            GUI.Label(new Rect(cx - 300f, cy - 210f, 600f, 46f), "해가 졌어요", titleStyle);

            if (isNewBest)
            {
                var bestStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bestStyle.normal.textColor = new Color(1f, 0.55f, 0.15f);
                GUI.Label(new Rect(cx - 300f, cy - 170f, 600f, 24f), "NEW HIGHSCORE!", bestStyle);
            }

            var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            statStyle.normal.textColor = new Color(0.32f, 0.2f, 0.2f);

            string[] lines =
            {
                $"Score: {score.Score:N0}",
                $"Island: {gameManager.Island}",
                $"Great Slides: {slideJudge.TotalSlides}",
                $"Cloud Touches: {(cloudSpawner != null ? cloudSpawner.TouchCount : 0)}",
                $"Longest Fever: {(fever != null ? fever.LongestDuration : 0f):0.0}s",
                $"Best: {(SaveSystem.Instance != null ? SaveSystem.Instance.BestScore : score.Score):N0}",
                $"Coins earned: +{(wallet != null ? wallet.LastRunCoinsAwarded : 0)}  (total {(wallet != null ? wallet.Coins : 0):N0})"
            };

            float y = cy - 140f;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(cx - 300f, y, 600f, 24f), line, statStyle);
                y += 24f;
            }

            if (nest != null) y = DrawNestObjectives(cx, y + 8f);

            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            float btnY = Mathf.Max(y + 16f, cy + 130f);
            if (GUI.Button(new Rect(cx - 170f, btnY, 150f, 46f), "다시하기", btnStyle))
            {
                gameManager.BeginRun();
            }
            if (GUI.Button(new Rect(cx + 20f, btnY, 150f, 46f), "홈", btnStyle))
            {
                gameManager.ReturnToStart();
            }
        }

        private float DrawNestObjectives(float cx, float y)
        {
            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            headerStyle.normal.textColor = new Color(0.42f, 0.29f, 0.12f);
            GUI.Label(new Rect(cx - 300f, y, 600f, 22f), $"Nest Multiplier (+{nest.Bonus})", headerStyle);
            y += 24f;

            var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            foreach (var mission in nest.ActiveMissions)
            {
                bool passed = nest.GetProgress(mission) >= mission.Target;
                lineStyle.normal.textColor = passed ? new Color(0.2f, 0.55f, 0.2f) : new Color(0.55f, 0.2f, 0.2f);
                string mark = passed ? "✓" : "✗";
                GUI.Label(new Rect(cx - 300f, y, 600f, 22f), $"{mark} {mission.Description}", lineStyle);
                y += 22f;
            }

            return y;
        }

        private void DrawOverlay()
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0.1f, 0.05f, 0.15f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }
    }
}
