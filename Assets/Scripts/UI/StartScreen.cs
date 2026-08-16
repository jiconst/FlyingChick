using UnityEngine;

namespace FlyingChick
{
    // Reference: title overlay shown in state 'start'; any click/tap/space
    // begins the run -- EXCEPT when the tap lands on one of this screen's
    // own buttons (bird icons, egg purchase, leaderboard toggle), which now
    // exist as of M6. OnGUI-based like the rest of the current UI --
    // Canvas/TMP is a later visual pass.
    public class StartScreen : MonoBehaviour
    {
        private CoinWallet wallet;
        private DailyMissions dailyMissions;
        private BirdCollection collection;
        private Leaderboard leaderboard;

        private bool showLeaderboard;
        private string hatchMessage;
        private float hatchMessageTimeLeft;
        private Texture2D[] birdIconTextures;
        private Rect[] birdIconRects = new Rect[0];
        private Rect eggButtonRect;
        private Rect leaderboardToggleRect;
        private Rect leaderboardPanelRect;

        public void Bind(CoinWallet walletRef, DailyMissions dailyMissionsRef, BirdCollection collectionRef, Leaderboard leaderboardRef)
        {
            wallet = walletRef;
            dailyMissions = dailyMissionsRef;
            collection = collectionRef;
            leaderboard = leaderboardRef;
        }

        private void Update()
        {
            if (hatchMessageTimeLeft > 0f)
            {
                hatchMessageTimeLeft -= Time.deltaTime;
            }

            var gm = GameManager.Instance;
            if (gm.State != GameState.Start) return;

            ComputeLayout();

            if (InputService.IsPointerDownThisFrame())
            {
                Vector2 pos = InputService.PointerPosition();
                Vector2 guiPos = new Vector2(pos.x, Screen.height - pos.y);
                if (!IsBlockingClick(guiPos))
                    gm.BeginRun();
            }
        }

        private void OnGUI()
        {
            if (GameManager.Instance.State != GameState.Start) return;

            ComputeLayout();
            DrawOverlay();

            if (showLeaderboard)
            {
                DrawLeaderboard();
                return;
            }

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.36f, 0.24f, 0.1f);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.42f, 0.29f, 0.12f);

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            GUI.Label(new Rect(cx - 300f, cy - 150f, 600f, 60f), "Flying Chick", titleStyle);
            GUI.Label(new Rect(cx - 300f, cy - 80f, 600f, 30f), "내리막에서 눌러 다이빙, 오르막에서 발사!", subStyle);
            GUI.Label(new Rect(cx - 300f, cy - 50f, 600f, 30f), "터치 / 클릭 / 스페이스바로 시작", subStyle);

            if (SaveSystem.Instance != null && SaveSystem.Instance.BestScore > 0)
                GUI.Label(new Rect(cx - 300f, cy - 10f, 600f, 26f), $"Best: {SaveSystem.Instance.BestScore:N0}", subStyle);

            if (wallet != null)
            {
                var coinStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                coinStyle.normal.textColor = new Color(0.85f, 0.6f, 0.1f);
                GUI.Label(new Rect(Screen.width - 160f, 16f, 140f, 26f), $"Coins: {wallet.Coins:N0}", coinStyle);
            }

            if (GUI.Button(leaderboardToggleRect, "기록 보기"))
                showLeaderboard = true;

            if (dailyMissions != null) DrawDailyMissions();
            if (collection != null) DrawBirdRow();
        }

        private void ComputeLayout()
        {
            leaderboardToggleRect = new Rect(Screen.width - 160f, 48f, 140f, 26f);
            eggButtonRect = new Rect(Screen.width * 0.5f - 100f, Screen.height - 56f, 200f, 40f);
            leaderboardPanelRect = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 240f, 440f, 480f);

            var birds = BirdPool.All;
            const float iconSize = 46f, spacing = 10f;
            float totalW = birds.Length * iconSize + (birds.Length - 1) * spacing;
            float startX = Screen.width * 0.5f - totalW * 0.5f;
            float y = Screen.height - 116f;

            birdIconRects = new Rect[birds.Length];
            for (int i = 0; i < birds.Length; i++)
                birdIconRects[i] = new Rect(startX + i * (iconSize + spacing), y, iconSize, iconSize);
        }

        private bool IsBlockingClick(Vector2 guiPos)
        {
            if (showLeaderboard) return true; // whole screen is the leaderboard while it's open

            if (leaderboardToggleRect.Contains(guiPos)) return true;
            if (eggButtonRect.Contains(guiPos)) return true;
            foreach (var r in birdIconRects)
                if (r.Contains(guiPos)) return true;
            return false;
        }

        private void DrawBirdRow()
        {
            var birds = BirdPool.All;
            for (int i = 0; i < birds.Length; i++)
            {
                var rect = birdIconRects[i];
                bool owned = collection.IsOwned(birds[i].Id);
                bool selected = collection.SelectedBirdId == birds[i].Id;

                if (selected)
                    DrawSolidRect(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), Color.white);

                var prevColor = GUI.color;
                GUI.color = owned ? Color.white : new Color(0.35f, 0.35f, 0.35f, 0.85f);
                bool clicked = GUI.Button(rect, GetBirdIconTexture(i));
                GUI.color = prevColor;

                if (!owned)
                {
                    var lockStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    lockStyle.normal.textColor = Color.white;
                    GUI.Label(rect, "?", lockStyle);
                }
                else if (clicked)
                {
                    collection.Select(birds[i].Id);
                }
            }

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            nameStyle.normal.textColor = new Color(0.42f, 0.29f, 0.12f);
            var selectedBird = collection.SelectedBird;
            string perkText = selectedBird.Perk == PerkType.None ? selectedBird.Name : $"{selectedBird.Name} · {selectedBird.PerkDescription}";
            GUI.Label(new Rect(Screen.width * 0.5f - 300f, birdIconRects[0].y - 22f, 600f, 20f), perkText, nameStyle);

            var eggStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            bool allOwned = collection.OwnedBirdIds.Count >= BirdPool.All.Length;
            GUI.enabled = !allOwned;
            if (GUI.Button(eggButtonRect, allOwned ? "새를 모두 모았어요" : $"알 구매 ({BirdPool.EggCostCoins} 코인)", eggStyle))
            {
                var hatched = collection.BuyEgg();
                if (hatched.HasValue)
                {
                    hatchMessage = $"부화! {hatched.Value.Name} 획득";
                    hatchMessageTimeLeft = 2.5f;
                }
                // null means funds were short -- button just stays available.
            }
            GUI.enabled = true;

            if (hatchMessageTimeLeft > 0f)
            {
                var hatchStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                hatchStyle.normal.textColor = new Color(1f, 0.6f, 0.15f);
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, eggButtonRect.y - 26f, 400f, 22f), hatchMessage, hatchStyle);
            }
        }

        private Texture2D GetBirdIconTexture(int index)
        {
            if (birdIconTextures == null) birdIconTextures = new Texture2D[BirdPool.All.Length];
            if (birdIconTextures[index] == null)
                birdIconTextures[index] = ProceduralSprite.CreateCircle(40, BirdPool.All[index].BodyColor).texture;
            return birdIconTextures[index];
        }

        private void DrawLeaderboard()
        {
            DrawSolidRect(leaderboardPanelRect, new Color(0.15f, 0.1f, 0.08f, 0.9f));

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            headerStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(leaderboardPanelRect.x, leaderboardPanelRect.y + 14f, leaderboardPanelRect.width, 34f), "기록", headerStyle);

            var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            lineStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);

            float y = leaderboardPanelRect.y + 56f;
            if (leaderboard != null)
            {
                var scores = leaderboard.TopScores;
                if (scores.Count == 0)
                {
                    GUI.Label(new Rect(leaderboardPanelRect.x + 24f, y, leaderboardPanelRect.width - 48f, 24f), "아직 기록이 없어요", lineStyle);
                    y += 26f;
                }
                for (int i = 0; i < scores.Count; i++)
                {
                    GUI.Label(new Rect(leaderboardPanelRect.x + 24f, y, leaderboardPanelRect.width - 48f, 22f), $"{i + 1}.  {scores[i]:N0}", lineStyle);
                    y += 24f;
                }

                y += 12f;
                GUI.Label(new Rect(leaderboardPanelRect.x + 24f, y, leaderboardPanelRect.width - 48f, 22f), $"총 슬라이드: {leaderboard.TotalSlidesAllTime:N0}", lineStyle);
                y += 24f;
                GUI.Label(new Rect(leaderboardPanelRect.x + 24f, y, leaderboardPanelRect.width - 48f, 22f), $"총 비행일 수: {leaderboard.TotalRuns:N0}", lineStyle);
            }

            var closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            if (GUI.Button(new Rect(leaderboardPanelRect.x + leaderboardPanelRect.width * 0.5f - 60f, leaderboardPanelRect.yMax - 50f, 120f, 36f), "닫기", closeStyle))
                showLeaderboard = false;
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prevColor;
        }

        private void DrawDailyMissions()
        {
            const float panelW = 260f;
            float panelH = 26f + dailyMissions.ActiveMissions.Length * 24f;
            var panelRect = new Rect(16f, Screen.height - panelH - 16f, panelW, panelH);
            DrawSolidRect(panelRect, new Color(0f, 0f, 0f, 0.3f));

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 4f, panelW - 20f, 20f), "오늘의 미션", headerStyle);

            var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            float y = panelRect.y + 26f;
            for (int i = 0; i < dailyMissions.ActiveMissions.Length; i++)
            {
                var mission = dailyMissions.ActiveMissions[i];
                bool done = dailyMissions.Completed[i];
                lineStyle.normal.textColor = done ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.85f);
                string mark = done ? "✓" : $"{dailyMissions.Progress[i]}/{mission.Target}";
                GUI.Label(new Rect(panelRect.x + 10f, y, panelW - 20f, 20f), $"{mission.Description} ({mark})", lineStyle);
                y += 22f;
            }
        }

        private void DrawOverlay()
        {
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 0.97f, 0.87f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }
    }
}
