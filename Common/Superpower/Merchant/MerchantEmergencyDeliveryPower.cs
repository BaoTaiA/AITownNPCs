using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public sealed class MerchantEmergencyDeliveryPower : ISuperpower
	{
		public string Id => SuperpowerIds.MerchantDelivery;
		public int NpcType => NPCID.Merchant;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "紧急送货";
		public string DescriptionZh => "高价远程送基础物资（火把、绳子、药水、弹药、炸弹、雷管等）";
		public string RefuseStyleZh => "市侩、精明、客气但算计";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			string hint = intent.GetParam("itemHint", intent.GetParam("item", ""));
			if (!MerchantCatalog.TryResolve(hint, out MerchantCatalogEntry entry)) {
				return SuperpowerResult.Failed(Id, "unknown_item", "无法识别要送的货物",
					"可送：火把、绳子、治疗药水、箭、子弹、木头、土、回忆药水、炸弹、雷管、手榴弹");
			}

			float mult = Main.rand.NextFloat(2.0f, 3.0f);
			long price = (long)System.Math.Ceiling(entry.ShopUnitPriceCopper * (double)entry.Stack * mult);
			if (price > int.MaxValue) {
				price = int.MaxValue;
			}

			if (!SuperpowerCoinUtil.TrySpendCopper(player, price)) {
				return SuperpowerResult.Failed(Id, "broke",
					"你的金币不足，需要" + SuperpowerCoinUtil.FormatCopper(price));
			}

			int remain = ServerPlayerNet.GiveItemSynced(player, entry.ItemType, entry.Stack);
			if (remain > 0) {
				SuperpowerCoinUtil.GiveCopper(player, price);
				return SuperpowerResult.Failed(Id, "inventory_full", "背包已满，无法送货，已退款");
			}

			return SuperpowerResult.Ok(Id, true,
				"已送货：" + Lang.GetItemNameValue(entry.ItemType) + " x" + entry.Stack,
				"收费=" + SuperpowerCoinUtil.FormatCopper(price));
		}
	}
}
