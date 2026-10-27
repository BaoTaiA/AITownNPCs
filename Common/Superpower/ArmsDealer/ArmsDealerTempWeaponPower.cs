using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.ArmsDealer
{
	public sealed class ArmsDealerTempWeaponPower : ISuperpower
	{
		public const int BuffSeconds = 60 * 10;
		public string Id => SuperpowerIds.ArmsTempWeapon;
		public int NpcType => NPCID.ArmsDealer;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "临时配枪";
		public string DescriptionZh => "借一把临时远程武器，10 分钟后收回";
		public string RefuseStyleZh => "粗鲁、暴躁、讲义气";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			ReclaimAll(player, notify: false);
			int weaponId = PickWeapon();
			(int ammoId, int ammoStack) = PickAmmo(weaponId);
			int empty = FindEmptySlot(player);
			if (empty < 0) {
				return SuperpowerResult.Failed(Id, "inventory_full", "背包已满无法给枪");
			}

			Item item = player.inventory[empty];
			item.SetDefaults(weaponId);
			ArmsLoanGlobalItem.MarkLoan(item, player.whoAmI);
			item.favorited = false;
			SyncSlot(player, empty);

			string ammoFact = TryGiveLoanAmmo(player, ammoId, ammoStack)
				? "附带弹药：" + Lang.GetItemNameValue(ammoId) + "x" + ammoStack
				: "背包没空位，只给了枪没给弹药";

			ServerPlayerNet.AddBuffSynced(
				player,
				ModContent.BuffType<ArmsDealerWeaponBuff>(),
				BuffSeconds * 60);
			return SuperpowerResult.Ok(Id, true,
				"已借出临时配枪：" + Lang.GetItemNameValue(weaponId),
				ammoFact,
				"10分钟后收回");
		}

		public static void ReclaimAll(Player player, bool notify) {
			if (player == null) {
				return;
			}

			bool removed = false;
			for (int i = 0; i < player.inventory.Length; i++) {
				Item item = player.inventory[i];
				if (ArmsLoanGlobalItem.IsLoan(item)) {
					item.TurnToAir();
					SyncSlot(player, i);
					removed = true;
				}
			}

			removed |= StripLoanSlots(player.bank?.item);
			removed |= StripLoanSlots(player.bank2?.item);
			removed |= StripLoanSlots(player.bank3?.item);
			removed |= StripLoanSlots(player.bank4?.item);

			if (ArmsLoanGlobalItem.IsLoan(player.trashItem)) {
				player.trashItem.TurnToAir();
				removed = true;
			}

			// Mouse + sticky state only exist on the owning client / SP.
			if (player.whoAmI == Main.myPlayer || Main.netMode == NetmodeID.SinglePlayer) {
				if (ArmsLoanGlobalItem.IsLoan(Main.mouseItem)) {
					Main.mouseItem.TurnToAir();
					removed = true;
				}

				player.GetModPlayer<ArmsLoanPlayerHooks>().ClearStickyMouseState();
			}

			int buffType = ModContent.BuffType<ArmsDealerWeaponBuff>();
			ServerPlayerNet.ClearBuffSynced(player, buffType);

			if (removed && notify) {
				NpcAiSpeech.NotifyPlayerLiteral(player.whoAmI, "枪我收回了。", Color.Orange);
				TryRememberReclaim(player);
			}
		}

		/// <summary>Client-local wipe before asking the server to reclaim inventory (buff ended).</summary>
		public static void ClearLocalLoanExtras(Player player) {
			if (player == null) {
				return;
			}

			if (ArmsLoanGlobalItem.IsLoan(Main.mouseItem)) {
				Main.mouseItem.TurnToAir();
			}

			if (ArmsLoanGlobalItem.IsLoan(player.trashItem)) {
				player.trashItem.TurnToAir();
			}

			player.GetModPlayer<ArmsLoanPlayerHooks>().ClearStickyMouseState();
		}

		private static bool StripLoanSlots(Item[] slots) {
			if (slots == null) {
				return false;
			}

			bool any = false;
			for (int i = 0; i < slots.Length; i++) {
				if (ArmsLoanGlobalItem.IsLoan(slots[i])) {
					slots[i].TurnToAir();
					any = true;
				}
			}

			return any;
		}

		private static void TryRememberReclaim(Player player) {
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.type == NPCID.ArmsDealer) {
					AiConversationMemory.AppendEvent(player, npc, "已收回借出的枪和弹药");
					return;
				}
			}
		}

		private static int FindEmptySlot(Player player) {
			for (int i = 0; i < 50; i++) {
				if (player.inventory[i] == null || player.inventory[i].IsAir) {
					return i;
				}
			}

			return -1;
		}

		/// <summary>Stack into existing loan ammo of same type, else empty slot. Marks IsArmsLoan.</summary>
		private static bool TryGiveLoanAmmo(Player player, int ammoId, int stack) {
			if (ammoId <= 0 || stack <= 0) {
				return false;
			}

			int remain = stack;
			for (int i = 0; i < 50 && remain > 0; i++) {
				Item slot = player.inventory[i];
				if (slot == null || slot.IsAir || slot.type != ammoId || slot.stack >= slot.maxStack) {
					continue;
				}

				if (!ArmsLoanGlobalItem.IsLoan(slot)) {
					continue;
				}

				int room = slot.maxStack - slot.stack;
				int add = System.Math.Min(room, remain);
				slot.stack += add;
				remain -= add;
				SyncSlot(player, i);
			}

			while (remain > 0) {
				int empty = FindEmptySlot(player);
				if (empty < 0) {
					return stack > remain;
				}

				Item probe = new Item();
				probe.SetDefaults(ammoId);
				int put = System.Math.Min(probe.maxStack, remain);
				Item dest = player.inventory[empty];
				dest.SetDefaults(ammoId);
				dest.stack = put;
				ArmsLoanGlobalItem.MarkLoan(dest, player.whoAmI);
				dest.favorited = false;
				remain -= put;
				SyncSlot(player, empty);
			}

			return true;
		}

		private static void SyncSlot(Player player, int slot) {
			ServerPlayerNet.SetInventorySlotSynced(player, slot);
		}

		private static int PickWeapon() {
			return Main.hardMode ? ItemID.Uzi : ItemID.Minishark;
		}

		/// <summary>All current loan guns use bullets; ammo tier follows world progress.</summary>
		private static (int ammoId, int stack) PickAmmo(int weaponId) {
			_ = weaponId;
			if (NPC.downedPlantBoss) {
				return (ItemID.ChlorophyteBullet, 1000);
			}

			if (Main.hardMode) {
				return (ItemID.CrystalBullet, 1000);
			}

			if (NPC.downedBoss2) {
				return (ItemID.MeteorShot, 1000);
			}

			return (ItemID.MusketBall, 1000);
		}
	}
}
