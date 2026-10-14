using System.Collections.Generic;
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
		public const int BuffSeconds = 60 * 5;
		public string Id => SuperpowerIds.ArmsTempWeapon;
		public int NpcType => NPCID.ArmsDealer;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "临时配枪";
		public string DescriptionZh => "借一把临时远程武器，5 分钟后收回";
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
			item.GetGlobalItem<ArmsLoanGlobalItem>().IsArmsLoan = true;
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
				"5分钟后收回");
		}

		public static void ReclaimAll(Player player, bool notify) {
			if (player == null) {
				return;
			}

			bool removed = false;
			for (int i = 0; i < player.inventory.Length; i++) {
				Item item = player.inventory[i];
				if (item != null && !item.IsAir && item.TryGetGlobalItem(out ArmsLoanGlobalItem g) && g.IsArmsLoan) {
					item.TurnToAir();
					SyncSlot(player, i);
					removed = true;
				}
			}

			int buffType = ModContent.BuffType<ArmsDealerWeaponBuff>();
			ServerPlayerNet.ClearBuffSynced(player, buffType);

			if (removed && notify) {
				NpcAiSpeech.NotifyPlayerLiteral(player.whoAmI, "枪我收回了。", Color.Orange);
				TryRememberReclaim(player);
			}
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

				if (!slot.TryGetGlobalItem(out ArmsLoanGlobalItem g) || !g.IsArmsLoan) {
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
				dest.GetGlobalItem<ArmsLoanGlobalItem>().IsArmsLoan = true;
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
			var pool = new List<int> {
				ItemID.FlintlockPistol, ItemID.Musket, ItemID.TheUndertaker, ItemID.Boomstick, ItemID.Handgun
			};
			if (NPC.downedBoss2) {
				pool.Add(ItemID.PhoenixBlaster);
				pool.Add(ItemID.Minishark);
			}

			if (Main.hardMode) {
				pool.Add(ItemID.ClockworkAssaultRifle);
				pool.Add(ItemID.Uzi);
			}

			if (NPC.downedMechBossAny) {
				pool.Add(ItemID.Megashark);
			}

			if (NPC.downedPlantBoss) {
				pool.Add(ItemID.VenusMagnum);
			}

			return pool[Main.rand.Next(pool.Count)];
		}

		/// <summary>All current loan guns use bullets; ammo tier follows world progress.</summary>
		private static (int ammoId, int stack) PickAmmo(int weaponId) {
			_ = weaponId;
			if (NPC.downedPlantBoss) {
				return (ItemID.ChlorophyteBullet, 100);
			}

			if (Main.hardMode) {
				return (ItemID.CrystalBullet, 150);
			}

			if (NPC.downedBoss2) {
				return (ItemID.MeteorShot, 150);
			}

			return (ItemID.MusketBall, 200);
		}
	}
}

