using System;
using UnityEngine;

namespace FlyingChick
{
    // Owns run-wide shared state that terrain, physics, and scoring all
    // read/advance: how far the world has scrolled, which island we're on,
    // and the reference viewport height the ported formulas are tuned
    // against. Singleton per project convention (GameManager/ScoreManager/
    // SaveSystem only).
    //
    // Also owns the Start/Playing/DayOver state machine (M4). BeginRun()
    // resets scroll/island and generates a fresh terrain seed, then fires
    // OnRunStart -- every other system (bird, score, streak, fever, coins,
    // clouds) subscribes to that event to reset itself. GameManager doesn't
    // know any of their details; it just broadcasts.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private const float IslandLength = 2600f;

        [SerializeField] private float viewHeight = 720f;

        public float ViewHeight => viewHeight;
        public float ScrollX { get; private set; }
        public int Island { get; private set; } = 1;
        public int Multiplier => 10 + (Island - 1) * 2;
        public GameState State { get; private set; } = GameState.Start;

        // Single shared terrain instance -- TerrainGenerator and BirdPhysics
        // both query this so the rendered hill and the physics ground line
        // never disagree (see the earlier island-desync bug for why this
        // matters).
        public GroundSampler Ground { get; private set; }

        public event Action<int> OnIslandAdvanced;
        public event Action OnRunStart;

        private float islandDistance;

        private void Awake()
        {
            Instance = this;
        }

        public void Configure(float viewHeightValue, int terrainSeed)
        {
            viewHeight = viewHeightValue;
            Ground = new GroundSampler(terrainSeed, viewHeight);
        }

        // Called by StartScreen (first press) and DayOverScreen ("다시하기").
        public void BeginRun()
        {
            ScrollX = 0f;
            Island = 1;
            islandDistance = 0f;
            Ground = new GroundSampler(UnityEngine.Random.Range(1, int.MaxValue), viewHeight);
            State = GameState.Playing;
            OnRunStart?.Invoke();
        }

        // Called by DayCycle when the day-length timer runs out.
        public void EndRun()
        {
            State = GameState.DayOver;
        }

        // Called by DayOverScreen ("홈").
        public void ReturnToStart()
        {
            State = GameState.Start;
        }

        public void AdvanceScroll(float delta)
        {
            ScrollX += delta;
            islandDistance += delta;

            if (islandDistance >= IslandLength)
            {
                islandDistance -= IslandLength;
                Island++;
                OnIslandAdvanced?.Invoke(Island);
            }
        }
    }
}
