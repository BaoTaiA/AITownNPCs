using System.Collections.Generic;
using AITownNPCs.Common.Loc;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public sealed class MerchantCatalogEntry
	{
		public int ItemType;
		public int Stack;
		/// <summary>English hint ids kept in code; bilingual extras load from Aliases.MerchantHints.*</summary>
		public string[] Hints;
		public string HintAliasKey;
		public int ShopUnitPriceCopper;
	}

	public static class MerchantCatalog
	{
		private static readonly List<MerchantCatalogEntry> Entries = new() {
			new() { ItemType = ItemID.Torch, Stack = 50, ShopUnitPriceCopper = 50, HintAliasKey = "Torch", Hints = new[] { "torch" } },
			new() { ItemType = ItemID.Rope, Stack = 50, ShopUnitPriceCopper = 10, HintAliasKey = "Rope", Hints = new[] { "rope" } },
			new() { ItemType = ItemID.LesserHealingPotion, Stack = 10, ShopUnitPriceCopper = 300, HintAliasKey = "LesserHealingPotion", Hints = new[] { "healing potion", "potion" } },
			new() { ItemType = ItemID.WoodenArrow, Stack = 100, ShopUnitPriceCopper = 5, HintAliasKey = "WoodenArrow", Hints = new[] { "arrow", "wooden arrow" } },
			new() { ItemType = ItemID.MusketBall, Stack = 100, ShopUnitPriceCopper = 7, HintAliasKey = "MusketBall", Hints = new[] { "bullet", "musket ball" } },
			new() { ItemType = ItemID.Wood, Stack = 50, ShopUnitPriceCopper = 20, HintAliasKey = "Wood", Hints = new[] { "wood" } },
			new() { ItemType = ItemID.DirtBlock, Stack = 100, ShopUnitPriceCopper = 1, HintAliasKey = "DirtBlock", Hints = new[] { "dirt" } },
			new() { ItemType = ItemID.RecallPotion, Stack = 3, ShopUnitPriceCopper = 2000, HintAliasKey = "RecallPotion", Hints = new[] { "recall" } },
			new() { ItemType = ItemID.Bomb, Stack = 50, ShopUnitPriceCopper = 300, HintAliasKey = "Bomb", Hints = new[] { "bomb" } },
			new() { ItemType = ItemID.Dynamite, Stack = 30, ShopUnitPriceCopper = 2000, HintAliasKey = "Dynamite", Hints = new[] { "dynamite" } },
			new() { ItemType = ItemID.Grenade, Stack = 50, ShopUnitPriceCopper = 75, HintAliasKey = "Grenade", Hints = new[] { "grenade" } }
		};

		public static bool TryResolve(string hint, out MerchantCatalogEntry entry) {
			entry = null;
			if (string.IsNullOrWhiteSpace(hint)) {
				return false;
			}

			string h = hint.Trim().ToLowerInvariant();
			MerchantCatalogEntry best = null;
			int bestScore = 0;
			foreach (MerchantCatalogEntry e in Entries) {
				foreach (string key in EnumerateHints(e)) {
					string k = key.ToLowerInvariant();
					if (h.Contains(k) || k.Contains(h)) {
						int score = k.Length;
						if (score > bestScore) {
							bestScore = score;
							best = e;
						}
					}
				}
			}

			entry = best;
			return entry != null;
		}

		private static IEnumerable<string> EnumerateHints(MerchantCatalogEntry e) {
			if (e.Hints != null) {
				foreach (string h in e.Hints) {
					yield return h;
				}
			}

			if (!string.IsNullOrEmpty(e.HintAliasKey)) {
				foreach (string h in AliasLists.Get("MerchantHints." + e.HintAliasKey)) {
					yield return h;
				}
			}
		}
	}
}
