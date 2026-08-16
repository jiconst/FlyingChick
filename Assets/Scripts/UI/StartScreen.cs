using UnityEngine;

namespace FlyingChick
{
    // Reference: title overlay shown in state 'start'; any click/tap/space
    // begins the run (not just a button hit-test). OnGUI-based like the rest
    // of the current UI -- Canvas/TMP is a later visual pass.
    public class StartScreen : MonoBehaviour
    {
        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm.State != GameState.Start) return;

            if (InputService.IsPointerDownThisFrame())
                gm.BeginRun();
        }

        private void OnGUI()
        {
            if (GameManager.Instance.State != GameState.Start) return;

            var overlayStyle = GUI.skin.box;
            DrawOverlay();

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

            GUI.Label(new Rect(cx - 300f, cy - 120f, 600f, 60f), "Flying Chick", titleStyle);
            GUI.Label(new Rect(cx - 300f, cy - 50f, 600f, 30f), "내리막에서 눌러 다이빙, 오르막에서 발사!", subStyle);
            GUI.Label(new Rect(cx - 300f, cy - 20f, 600f, 30f), "터치 / 클릭 / 스페이스바로 시작", subStyle);

            if (SaveSystem.Instance != null && SaveSystem.Instance.BestScore > 0)
            {
                GUI.Label(new Rect(cx - 300f, cy + 20f, 600f, 26f), $"Best: {SaveSystem.Instance.BestScore:N0}", subStyle);
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
