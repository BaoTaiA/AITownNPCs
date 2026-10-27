using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public sealed class MerchantEmergencyDeliveryPower : ISuperpower
	{
		public string Id => SuperpowerIds.MerchantDelivery;
		public int NpcType => NPCID.Merchant;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			string hint = intent.GetParam("itemHint", intent.GetParam("item", ""));
			if (!MerchantCatalog.TryResolve(hint, out MerchantCatalogEntry entry)) {
				return SuperpowerResult.Failed(Id, "unknown_item", ModText.Get("Runtime.Superpower.Fixed.unknown_item"),
					ModText.Get("Runtime.Superpower.DeliveryCatalog"));
			}

			float mult = Main.rand.NextFloat(2.0f, 3.0f);
			long price = (long)System.Math.Ceiling(entry.ShopUnitPriceCopper * (double)entry.Stack * mult);
			if (price > int.MaxValue) {
				price = int.MaxValue;
			}

			if (!SuperpowerCoinUtil.TrySpendCopper(player, price)) {
				return SuperpowerResult.Failed(Id, "broke",
					ModText.Get("Runtime.Superpower.NeedGold", SuperpowerCoinUtil.FormatCopper(price)));
			}

			int remain = ServerPlayerNet.GiveItemSynced(player, entry.ItemType, entry.Stack);
			if (remain > 0) {
				SuperpowerCoinUtil.GiveCopper(player, price);
				return SuperpowerResult.Failed(Id, "inventory_full", ModText.Get("Runtime.Superpower.Fixed.inventory_full_refund"));
			}

			return SuperpowerResult.Ok(Id, true,
				ModText.Get("Runtime.Superpower.Delivered", Lang.GetItemNameValue(entry.ItemType), entry.Stack),
				ModText.Get("Runtime.Superpower.Charge", SuperpowerCoinUtil.FormatCopper(price)));
		}
	}
}
