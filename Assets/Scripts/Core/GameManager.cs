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
    // Island progression (M2): ported from the reference's inline
    // islandDistance/ISLAND_LEN handling in update(). Day-cycle fields land
    // in M4, not here.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private const float IslandLength = 2600f;

        [SerializeField] private float viewHeight = 720f;

        public float ViewHeight => viewHeight;
        public float ScrollX { get; private set; }
        public int Island { get; private set; } = 1;
        public int Multiplier => 10 + (Island - 1) * 2;

        // Single shared terrain instance -- TerrainGenerator and BirdPhysics
        // both query this so the rendered hill and the physics ground line
        // never disagree (see the earlier island-desync bug for why this
        // matters).
        public GroundSampler Ground { get; private set; }

        public event Action<int> OnIslandAdvanced;

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
