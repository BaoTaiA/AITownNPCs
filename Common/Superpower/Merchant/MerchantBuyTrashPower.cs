using System.Collections.Generic;
using AITownNPCs.Common.Net;
using AITownNPCs.Common.Superpower.ArmsDealer;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public sealed class MerchantBuyTrashPower : ISuperpower
	{
		public const int QuoteSeconds = 60;
		public string Id => SuperpowerIds.MerchantBuyTrash;
		public int NpcType => NPCID.Merchant;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "收购垃圾";
		public string DescriptionZh => "扫描背包报价收购垃圾，确认后成交（约半价到八折）";
		public string RefuseStyleZh => "市侩、精明、客气但算计";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			ulong favoritedMask = 0;
			bool haveMask = PlayerSuperpowerClientSnapshot.TryGet(player.whoAmI, out favoritedMask, out _, out _);
			if (Main.netMode == NetmodeID.Server && Main.dedServ && !haveMask) {
				return SuperpowerResult.Failed(Id, "no_client_state", "请再说一次（客户端状态未同步）");
			}

			if (!haveMask && Main.netMode == NetmodeID.SinglePlayer) {
				favoritedMask = PlayerSuperpowerClientSnapshot.BuildFavoritedMaskLocal(player);
				haveMask = true;
			}

			float rate = Main.rand.NextFloat(0.50f, 0.80f);
			var lines = new List<MerchantQuoteLine>();
			long total = 0;
			for (int i = 0; i < 50; i++) {
				if (haveMask && (favoritedMask & (1UL << i)) != 0) {
					continue;
				}

				Item item = player.inventory[i];
				if (!IsBuyableTrash(item, skipFavoritedCheck: true)) {
					continue;
				}

				if (!haveMask && item.favorited) {
					continue;
				}

				long unitSell = item.value / 5;
				if (unitSell <= 0) {
					continue;
				}

				long line = (long)System.Math.Floor(unitSell * item.stack * rate);
				if (line <= 0) {
					line = 1;
				}

				lines.Add(new MerchantQuoteLine {
					Slot = i, ItemType = item.type, Stack = item.stack, LinePrice = line
				});
				total += line;
			}

			if (lines.Count == 0) {
				return SuperpowerResult.Failed(Id, "empty", "背包没有可收购的垃圾");
			}

			var quote = new MerchantPendingQuote {
				PlayerWhoAmI = player.whoAmI,
				Rate = rate,
				TotalPrice = total,
				ExpireTick = Main.GameUpdateCount + (uint)(QuoteSeconds * 60),
				Lines = lines,
				FavoritedMaskAtQuote = favoritedMask,
				HasFavoritedMask = haveMask
			};
			MerchantPendingQuoteStore.Set(quote);
			return SuperpowerResult.Quoted(Id, "已生成收购垃圾报价",
				"总价=" + SuperpowerCoinUtil.FormatCopper(total));
		}

		public static SuperpowerResult CommitQuote(Player player, MerchantPendingQuote quote) {
			if (quote == null || quote.IsExpired()) {
				MerchantPendingQuoteStore.Clear(player.whoAmI);
				return SuperpowerResult.Failed(SuperpowerIds.MerchantBuyTrash, "expired", "报价已过期");
			}

			ulong liveMask = quote.FavoritedMaskAtQuote;
			bool haveLive = PlayerSuperpowerClientSnapshot.TryGet(player.whoAmI, out ulong fresh, out _, out _);
			if (haveLive) {
				liveMask = fresh;
			}
			else if (Main.netMode == NetmodeID.SinglePlayer) {
				liveMask = PlayerSuperpowerClientSnapshot.BuildFavoritedMaskLocal(player);
				haveLive = true;
			}

			foreach (MerchantQuoteLine line in quote.Lines) {
				if (line.Slot < 0 || line.Slot >= 50) {
					MerchantPendingQuoteStore.Clear(player.whoAmI);
					return SuperpowerResult.Failed(SuperpowerIds.MerchantBuyTrash, "stale", "背包已变化");
				}

				if ((quote.HasFavoritedMask || haveLive) && (liveMask & (1UL << line.Slot)) != 0) {
					MerchantPendingQuoteStore.Clear(player.whoAmI);
					return SuperpowerResult.Failed(SuperpowerIds.MerchantBuyTrash, "stale", "背包已变化");
				}

				Item item = player.inventory[line.Slot];
				if (item == null || item.type != line.ItemType || item.stack != line.Stack) {
					MerchantPendingQuoteStore.Clear(player.whoAmI);
					return SuperpowerResult.Failed(SuperpowerIds.MerchantBuyTrash, "stale", "背包已变化");
				}
			}

			foreach (MerchantQuoteLine line in quote.Lines) {
				ServerPlayerNet.ClearInventorySlotSynced(player, line.Slot);
			}

			SuperpowerCoinUtil.GiveCopper(player, quote.TotalPrice);
			MerchantPendingQuoteStore.Clear(player.whoAmI);
			if (SuperpowerRegistry.TryGet(SuperpowerIds.MerchantBuyTrash, out ISuperpower power)) {
				SuperpowerCooldownStore.Start(player.whoAmI, NPCID.Merchant, power.Id, power.CooldownFrames);
			}

			return SuperpowerResult.Ok(SuperpowerIds.MerchantBuyTrash, false,
				"已收购垃圾并付款",
				"总价=" + SuperpowerCoinUtil.FormatCopper(quote.TotalPrice));
		}

		private static bool IsBuyableTrash(Item item, bool skipFavoritedCheck) {
			if (item == null || item.IsAir || item.stack <= 0 || item.value <= 0) {
				return false;
			}

			if (!skipFavoritedCheck && item.favorited) {
				return false;
			}

			if (item.IsACoin || item.questItem) {
				return false;
			}

			if (item.TryGetGlobalItem(out ArmsLoanGlobalItem loan) && loan.IsArmsLoan) {
				return false;
			}

			if (item.pick > 0 || item.axe > 0 || item.hammer > 0) {
				return false;
			}

			if (item.mountType != -1 || item.fishingPole > 0) {
				return false;
			}

			return true;
		}
	}
}
