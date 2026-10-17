using System.Collections.Generic;
using System.Text;
using Terraria;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public sealed class MerchantQuoteLine
	{
		public int Slot;
		public int ItemType;
		public int Stack;
		public long LinePrice;
	}

	public sealed class MerchantPendingQuote
	{
		public int PlayerWhoAmI;
		public float Rate;
		public long TotalPrice;
		public uint ExpireTick;
		public List<MerchantQuoteLine> Lines = new();
		/// <summary>Client favorited bitmask at quote time (slots 0–49).</summary>
		public ulong FavoritedMaskAtQuote;
		public bool HasFavoritedMask;

		public bool IsExpired() => Main.GameUpdateCount >= ExpireTick;

		public string BuildIconChatLine() {
			var sb = new StringBuilder();
			for (int i = 0; i < Lines.Count; i++) {
				MerchantQuoteLine line = Lines[i];
				if (i > 0) {
					sb.Append(' ');
				}

				sb.Append("[i/s").Append(line.Stack).Append(':').Append(line.ItemType).Append(']');
			}

			return sb.ToString();
		}

		public string BuildQuoteAnnounce() {
			string body = "这些我收：" + BuildIconChatLine()
			              + " 总共" + SuperpowerCoinUtil.FormatCopper(TotalPrice)
			              + "，确定吗？回复「可以」或「不要」。";
			if (PlayerWhoAmI < 0 || PlayerWhoAmI >= Main.maxPlayers) {
				return body;
			}

			Player player = Main.player[PlayerWhoAmI];
			if (player == null || !player.active || string.IsNullOrEmpty(player.name)) {
				return body;
			}

			return "@" + player.name + " " + body;
		}
	}

	public static class MerchantPendingQuoteStore
	{
		private static readonly Dictionary<int, MerchantPendingQuote> ByPlayer = new();

		public static void Set(MerchantPendingQuote quote) {
			if (quote != null) {
				ByPlayer[quote.PlayerWhoAmI] = quote;
			}
		}

		public static bool TryGet(int playerWho, out MerchantPendingQuote quote) {
			if (ByPlayer.TryGetValue(playerWho, out quote) && quote != null && !quote.IsExpired()) {
				return true;
			}

			ByPlayer.Remove(playerWho);
			quote = null;
			return false;
		}

		public static void Clear(int playerWho) => ByPlayer.Remove(playerWho);
		public static void ClearAll() => ByPlayer.Clear();
	}
}
