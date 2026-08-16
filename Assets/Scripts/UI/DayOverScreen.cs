using UnityEngine;

namespace FlyingChick
{
    // Reference: endScreen -- final stats (score/island/slides), submits to
    // SaveSystem, shows Best line, "다시하기"(restart)/"홈"(back to start)
    // buttons. Coin count-up animation is a later visual pass.
    public class DayOverScreen : MonoBehaviour
    {
        private ScoreManager score;
        private SlideJudge slideJudge;
        private CloudSpawner cloudSpawner;
        private FeverSystem fever;
        private GameManager gameManager;

        private bool submittedThisRun;
        private bool isNewBest;

        public void Bind(ScoreManager scoreRef, SlideJudge slideJudgeRef, CloudSpawner cloudSpawnerRef, FeverSystem feverRef, GameManager gameManagerRef)
        {
            score = scoreRef;
            slideJudge = slideJudgeRef;
            cloudSpawner = cloudSpawnerRef;
            fever = feverRef;
            gameManager = gameManagerRef;

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

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.32f, 0.2f, 0.36f);
            GUI.Label(new Rect(cx - 300f, cy - 170f, 600f, 50f), "해가 졌어요", titleStyle);

            if (isNewBest)
            {
                var bestStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bestStyle.normal.textColor = new Color(1f, 0.55f, 0.15f);
                GUI.Label(new Rect(cx - 300f, cy - 120f, 600f, 26f), "NEW HIGHSCORE!", bestStyle);
            }

            var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            statStyle.normal.textColor = new Color(0.32f, 0.2f, 0.2f);

            string[] lines =
            {
                $"Score: {score.Score:N0}",
                $"Island: {gameManager.Island}",
                $"Great Slides: {slideJudge.TotalSlides}",
                $"Cloud Touches: {(cloudSpawner != null ? cloudSpawner.TouchCount : 0)}",
                $"Longest Fever: {(fever != null ? fever.LongestDuration : 0f):0.0}s",
                $"Best: {(SaveSystem.Instance != null ? SaveSystem.Instance.BestScore : score.Score):N0}"
            };

            float y = cy - 80f;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(cx - 300f, y, 600f, 28f), line, statStyle);
                y += 30f;
            }

            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(cx - 170f, cy + 130f, 150f, 46f), "다시하기", btnStyle))
            {
                gameManager.BeginRun();
            }
            if (GUI.Button(new Rect(cx + 20f, cy + 130f, 150f, 46f), "홈", btnStyle))
            {
                gameManager.ReturnToStart();
            }
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
