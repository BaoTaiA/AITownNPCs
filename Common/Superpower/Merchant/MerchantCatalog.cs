using System.Collections.Generic;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public sealed class MerchantCatalogEntry
	{
		public int ItemType;
		public int Stack;
		public string[] Hints;
		public int ShopUnitPriceCopper;
	}

	public static class MerchantCatalog
	{
		private static readonly List<MerchantCatalogEntry> Entries = new() {
			new() { ItemType = ItemID.Torch, Stack = 50, ShopUnitPriceCopper = 50, Hints = new[] { "火把", "torch" } },
			new() { ItemType = ItemID.Rope, Stack = 50, ShopUnitPriceCopper = 10, Hints = new[] { "绳子", "rope" } },
			new() { ItemType = ItemID.LesserHealingPotion, Stack = 10, ShopUnitPriceCopper = 300, Hints = new[] { "治疗药水", "回血", "药水" } },
			new() { ItemType = ItemID.WoodenArrow, Stack = 100, ShopUnitPriceCopper = 5, Hints = new[] { "箭", "木箭" } },
			new() { ItemType = ItemID.MusketBall, Stack = 100, ShopUnitPriceCopper = 7, Hints = new[] { "子弹", "子弹" } },
			new() { ItemType = ItemID.Wood, Stack = 50, ShopUnitPriceCopper = 20, Hints = new[] { "木头", "木板", "木材" } },
			new() { ItemType = ItemID.DirtBlock, Stack = 100, ShopUnitPriceCopper = 1, Hints = new[] { "土", "土块" } },
			new() { ItemType = ItemID.RecallPotion, Stack = 3, ShopUnitPriceCopper = 2000, Hints = new[] { "回忆药水", "回城" } }
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
				foreach (string key in e.Hints) {
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
	}
}
