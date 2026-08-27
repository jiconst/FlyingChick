using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HillyWings
{
    // Owned birds + which one is selected, persisted through
    // SaveSystem.Data.ownedBirdIds/selectedBirdId. Everyone starts owning
    // just the default (free) bird. Not a singleton -- other systems
    // (BirdVisual, BirdController, SlideJudge, FeverSystem, CoinSpawner) get
    // it via Configure so they can read the selected bird's perk.
    public class BirdCollection : MonoBehaviour
    {
        public IReadOnlyList<string> OwnedBirdIds { get; private set; } = new List<string>();
        public string SelectedBirdId { get; private set; } = BirdPool.DefaultBirdId;
        public BirdDefinition SelectedBird => BirdPool.Find(SelectedBirdId);

        public event Action OnSelectionChanged;
        public event Action OnCollectionChanged;

        private CoinWallet wallet;

        public void Configure(CoinWallet walletRef)
        {
            wallet = walletRef;

            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (data == null)
            {
                OwnedBirdIds = new List<string> { BirdPool.DefaultBirdId };
                SelectedBirdId = BirdPool.DefaultBirdId;
                return;
            }

            if (data.ownedBirdIds == null || data.ownedBirdIds.Length == 0)
            {
                data.ownedBirdIds = new[] { BirdPool.DefaultBirdId };
                data.selectedBirdId = BirdPool.DefaultBirdId;
                SaveSystem.Instance.Save();
            }

            OwnedBirdIds = new List<string>(data.ownedBirdIds);
            SelectedBirdId = string.IsNullOrEmpty(data.selectedBirdId) ? BirdPool.DefaultBirdId : data.selectedBirdId;
        }

        public bool IsOwned(string id) => OwnedBirdIds.Contains(id);

        public void Select(string id)
        {
            if (!IsOwned(id) || id == SelectedBirdId) return;
            SelectedBirdId = id;
            Persist();
            OnSelectionChanged?.Invoke();
        }

        // Returns the hatched bird, or null if funds are short or every bird
        // is already owned (egg cost isn't spent in that case).
        public BirdDefinition? BuyEgg()
        {
            var unowned = BirdPool.All.Where(b => !IsOwned(b.Id)).ToArray();
            if (unowned.Length == 0) return null;
            if (!wallet.SpendCoins(BirdPool.EggCostCoins)) return null;

            var hatched = unowned[UnityEngine.Random.Range(0, unowned.Length)];
            var list = new List<string>(OwnedBirdIds) { hatched.Id };
            OwnedBirdIds = list;
            Persist();
            OnCollectionChanged?.Invoke();
            return hatched;
        }

        private void Persist()
        {
            if (SaveSystem.Instance == null) return;
            var data = SaveSystem.Instance.Data;
            data.ownedBirdIds = OwnedBirdIds.ToArray();
            data.selectedBirdId = SelectedBirdId;
            SaveSystem.Instance.Save();
        }
    }
}
