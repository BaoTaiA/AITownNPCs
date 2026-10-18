using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower
{
	/// <summary>
	/// Client-reported player fields for dedicated-server superpower gates (!SSC).
	/// Filled from the owning client's chat packet; short TTL unless pinned for an in-flight Execute.
	/// </summary>
	public static class PlayerSuperpowerClientSnapshot
	{
		private const uint TtlTicks = 60 * 5; // ~5 seconds

		private static readonly ulong[] FavoritedMask = new ulong[Main.maxPlayers];
		private static readonly byte[] SelectedItem = new byte[Main.maxPlayers];
		private static readonly long[] CopperOwned = new long[Main.maxPlayers];
		private static readonly int[] StatLife = new int[Main.maxPlayers];
		private static readonly int[] StatLifeMax2 = new int[Main.maxPlayers];
		private static readonly uint[] ExpireTick = new uint[Main.maxPlayers];
		private static readonly bool[] HasSnapshot = new bool[Main.maxPlayers];
		private static readonly bool[] Pinned = new bool[Main.maxPlayers];

		public static void Set(int playerWhoAmI, ulong favoritedMask, byte selectedItem, long copperOwned, int statLife, int statLifeMax2) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			FavoritedMask[playerWhoAmI] = favoritedMask;
			SelectedItem[playerWhoAmI] = selectedItem;
			CopperOwned[playerWhoAmI] = copperOwned < 0 ? 0 : copperOwned;
			StatLife[playerWhoAmI] = statLife < 0 ? 0 : statLife;
			StatLifeMax2[playerWhoAmI] = statLifeMax2 < 0 ? 0 : statLifeMax2;
			ExpireTick[playerWhoAmI] = Main.GameUpdateCount + TtlTicks;
			HasSnapshot[playerWhoAmI] = true;
		}

		/// <summary>Keep snapshot alive across slow LLM classify → Execute.</summary>
		public static void Pin(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers || !HasSnapshot[playerWhoAmI]) {
				return;
			}

			Pinned[playerWhoAmI] = true;
		}

		public static void Unpin(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			Pinned[playerWhoAmI] = false;
			if (HasSnapshot[playerWhoAmI]) {
				ExpireTick[playerWhoAmI] = Main.GameUpdateCount + TtlTicks;
			}
		}

		public static bool TryGet(int playerWhoAmI, out ulong favoritedMask, out byte selectedItem, out long copperOwned) {
			favoritedMask = 0;
			selectedItem = 0;
			copperOwned = 0;
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers || !HasSnapshot[playerWhoAmI]) {
				return false;
			}

			if (!Pinned[playerWhoAmI] && Main.GameUpdateCount >= ExpireTick[playerWhoAmI]) {
				HasSnapshot[playerWhoAmI] = false;
				return false;
			}

			favoritedMask = FavoritedMask[playerWhoAmI];
			selectedItem = SelectedItem[playerWhoAmI];
			copperOwned = CopperOwned[playerWhoAmI];
			return true;
		}

		public static bool TryGetLife(int playerWhoAmI, out int statLife, out int statLifeMax2) {
			statLife = 0;
			statLifeMax2 = 0;
			if (!TryGet(playerWhoAmI, out _, out _, out _)) {
				return false;
			}

			statLife = StatLife[playerWhoAmI];
			statLifeMax2 = StatLifeMax2[playerWhoAmI];
			return true;
		}

		public static bool IsSlotFavorited(int playerWhoAmI, int slot, ulong? quoteMask = null) {
			if (slot < 0 || slot >= 50) {
				return false;
			}

			ulong mask;
			if (quoteMask.HasValue) {
				mask = quoteMask.Value;
			}
			else if (TryGet(playerWhoAmI, out ulong live, out _, out _)) {
				mask = live;
			}
			else if (Main.netMode == NetmodeID.SinglePlayer) {
				Player player = Main.player[playerWhoAmI];
				Item item = player?.inventory[slot];
				return item != null && !item.IsAir && item.favorited;
			}
			else {
				return false;
			}

			return (mask & (1UL << slot)) != 0;
		}

		public static ulong BuildFavoritedMaskLocal(Player player) {
			ulong mask = 0;
			if (player?.inventory == null) {
				return mask;
			}

			for (int i = 0; i < 50; i++) {
				Item item = player.inventory[i];
				if (item != null && !item.IsAir && item.favorited) {
					mask |= 1UL << i;
				}
			}

			return mask;
		}

		public static long CountCopperLocal(Player player) {
			if (player?.inventory == null) {
				return 0;
			}

			return Utils.CoinsCount(out bool _, player.inventory);
		}

		public static void Clear(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			HasSnapshot[playerWhoAmI] = false;
			Pinned[playerWhoAmI] = false;
		}
	}
}
