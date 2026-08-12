using UnityEngine;

namespace FlyingChick
{
    // Temporary on-screen debug readout for validating the physics prototype
    // by feel. Not intended to ship -- replace with real UI once the core
    // mechanic is confirmed to be fun.
    public class SimpleHud : MonoBehaviour
    {
        private BirdController bird;
        private GameManager gameManager;

        public void Bind(BirdController birdRef, GameManager gameManagerRef)
        {
            bird = birdRef;
            gameManager = gameManagerRef;
        }

        private void OnGUI()
        {
            if (bird == null || gameManager == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            style.normal.textColor = Color.white;

            var diveStyle = new GUIStyle(style);
            diveStyle.normal.textColor = bird.IsDiving ? Color.red : Color.gray;

            GUI.Label(new Rect(20, 20, 400, 30), $"Speed: {bird.Speed:0.0}", style);
            GUI.Label(new Rect(20, 50, 400, 30), $"State: {(bird.IsGrounded ? "Grounded" : "Airborne")}{(bird.IsInFever ? " [FEVER]" : "")}", style);
            GUI.Label(new Rect(20, 80, 400, 30), $"Input held: {bird.IsHoldingInput}", style);
            GUI.Label(new Rect(20, 110, 400, 30), bird.IsDiving ? "DIVING!" : "not diving", diveStyle);
            GUI.Label(new Rect(20, 140, 400, 30), $"Score: {gameManager.Score}", style);
            GUI.Label(new Rect(20, 170, 400, 30), $"Day: {gameManager.TimeRemaining:0.0}s{(gameManager.IsDayOver ? " - DAY OVER" : "")}", style);
            GUI.Label(new Rect(20, 210, 500, 30), "Hold mouse/touch on a downhill to dive", style);
        }
    }
}
