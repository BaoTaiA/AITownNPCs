using AITownNPCs.Common.Loc;
using AITownNPCs.Common.Net;
using Terraria;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerCoinUtil
	{
		public static string FormatCopper(long copper) {
			if (copper < 0) {
				copper = 0;
			}

			long plat = copper / 1000000;
			copper %= 1000000;
			long gold = copper / 10000;
			copper %= 10000;
			long silver = copper / 100;
			long copperLeft = copper % 100;
			return ModText.Get("Runtime.Coin.Format", plat, gold, silver, copperLeft);
		}

		public static void GiveCopper(Player player, long copper) {
			ServerPlayerNet.GiveCopperSynced(player, copper);
		}

		public static bool TrySpendCopper(Player player, long copper) {
			return ServerPlayerNet.TrySpendCopperSynced(player, copper);
		}
	}
}
