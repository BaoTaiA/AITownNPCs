using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.GoblinTinkerer
{
	/// <summary>Permanently rewrites the held weapon's prefix (intent id remains goblin_temp_prefix).</summary>
	public sealed class GoblinPrefixPower : ISuperpower
	{
		public string Id => SuperpowerIds.GoblinTempPrefix;
		public int NpcType => NPCID.GoblinTinkerer;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "附魔词缀";
		public string DescriptionZh => "永久改写手持武器词缀";
		public string RefuseStyleZh => "贪婪、狡猾、胆小";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			int slot = ResolveHeldSlot(player);
			if (slot < 0 || slot >= player.inventory.Length) {
				return SuperpowerResult.Failed(Id, "no_weapon", "没有手持可附魔武器",
					"拒绝风格：" + RefuseStyleZh);
			}

			Item held = player.inventory[slot];
			if (held == null || held.IsAir || held.damage <= 0 || held.maxStack != 1 || held.accessory) {
				return SuperpowerResult.Failed(Id, "no_weapon", "没有手持可附魔武器",
					"拒绝风格：" + RefuseStyleZh);
			}

			int original = held.prefix;
			bool applied = false;
			for (int attempt = 0; attempt < 30; attempt++) {
				Item probe = held.Clone();
				if (!probe.Prefix(-1)) {
					continue;
				}

				if (probe.prefix != original || attempt >= 20) {
					held.Prefix(probe.prefix);
					applied = true;
					break;
				}
			}

			if (!applied && !held.Prefix(-1)) {
				return SuperpowerResult.Failed(Id, "prefix_fail", "未能附魔词缀");
			}

			int newPrefix = held.prefix;
			ServerPlayerNet.SetInventorySlotSynced(player, slot);

			string prefixName = newPrefix > 0 && newPrefix < Lang.prefix.Length
				? Lang.prefix[newPrefix].Value
				: "随机";
			return SuperpowerResult.Ok(Id, true, "已附魔词缀：" + prefixName);
		}

		private static int ResolveHeldSlot(Player player) {
			if (PlayerSuperpowerClientSnapshot.TryGet(player.whoAmI, out _, out byte selected, out _)) {
				return selected;
			}

			if (Main.netMode == NetmodeID.SinglePlayer
			    || (Main.netMode == NetmodeID.Server && !Main.dedServ && player.whoAmI == Main.myPlayer)) {
				return player.selectedItem;
			}

			// Dedicated without snapshot: last-resort server selectedItem (may be stale).
			return player.selectedItem;
		}
	}
}
