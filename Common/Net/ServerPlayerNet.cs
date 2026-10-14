using System.Collections.Generic;
using System.IO;
using AITownNPCs.Common.Superpower;
using AITownNPCs.Common.Superpower.ArmsDealer;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Net
{
	/// <summary>
	/// Server → owning-client player-visible state via ModPacket.
	/// Do not use MessageID.PlayerMana / PlayerBuffs / SyncEquipment as authority under !SSC.
	/// Life/mana: owner ModPacket only (no SpiritHeal / ManaEffect as authority).
	/// </summary>
	public static class ServerPlayerNet
	{
		public static void AddBuffSynced(Player player, int buffType, int time) {
			if (player == null || !player.active || buffType <= 0 || time <= 0) {
				return;
			}

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				player.AddBuff(buffType, time, quiet: true);
				return;
			}

			player.AddBuff(buffType, time, quiet: true);
			if (Main.netMode == NetmodeID.Server) {
				Send(player.whoAmI, ServerPlayerMessageType.ApplyBuff, p => {
					p.Write(buffType);
					p.Write(time);
				});
			}
		}

		public static void ClearBuffSynced(Player player, int buffType) {
			if (player == null || !player.active || buffType <= 0) {
				return;
			}

			int idx = player.FindBuffIndex(buffType);
			if (idx >= 0) {
				player.DelBuff(idx);
			}

			if (Main.netMode == NetmodeID.Server) {
				Send(player.whoAmI, ServerPlayerMessageType.ClearBuff, p => p.Write(buffType));
			}
		}

		/// <summary>
		/// Apply life heal. Always notifies owning client with <paramref name="amount"/>
		/// (client clamps); does not skip when server copy already looks full.
		/// </summary>
		public static void HealLifeSynced(Player player, int amount) {
			if (player == null || !player.active || amount <= 0) {
				return;
			}

			int before = player.statLife;
			player.statLife = System.Math.Min(player.statLife + amount, player.statLifeMax2);
			int applied = player.statLife - before;

			if (Main.netMode == NetmodeID.SinglePlayer) {
				if (applied > 0) {
					player.HealEffect(applied, broadcast: true);
				}

				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				// Owner applies via HealLife packet (local HealEffect). No SpiritHeal (would double).
				Send(player.whoAmI, ServerPlayerMessageType.HealLife, p => p.Write(amount));
			}
		}

		/// <summary>
		/// Apply mana heal. Always notifies owning client with <paramref name="amount"/>
		/// (client clamps); does not skip when server copy already looks full.
		/// </summary>
		public static void HealManaSynced(Player player, int amount) {
			if (player == null || !player.active || amount <= 0) {
				return;
			}

			player.statMana = System.Math.Min(player.statMana + amount, player.statManaMax2);

			if (Main.netMode == NetmodeID.SinglePlayer) {
				player.ManaEffect(amount);
				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				// Owner packet applies mana + local ManaEffect visual.
				// Others: blue numbers only (ManaEffect message is visual).
				NetMessage.SendData(
					MessageID.ManaEffect,
					-1,
					player.whoAmI,
					null,
					player.whoAmI,
					amount);
				Send(player.whoAmI, ServerPlayerMessageType.HealMana, p => p.Write(amount));
			}
		}

		public static void FullHealSynced(Player player) {
			if (player == null || !player.active) {
				return;
			}

			int healTo = player.statLifeMax2;
			int applied = healTo - player.statLife;
			player.statLife = healTo;
			ClearNurseDebuffsLocal(player);

			if (Main.netMode == NetmodeID.SinglePlayer) {
				if (applied > 0) {
					player.HealEffect(applied, broadcast: true);
				}

				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				Send(player.whoAmI, ServerPlayerMessageType.ClearNurseDebuffs, null);
				// Enough to fill any client copy from empty; client clamps + HealEffect(shown).
				Send(player.whoAmI, ServerPlayerMessageType.HealLife, p => p.Write(healTo));
			}
		}

		/// <summary>Push current inventory[slot] to owning client (after server already mutated).</summary>
		public static void SetInventorySlotSynced(Player player, int slot) {
			if (player == null || !player.active || slot < 0 || slot >= player.inventory.Length) {
				return;
			}

			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			Item item = player.inventory[slot] ?? new Item();
			bool armsLoan = item.TryGetGlobalItem(out ArmsLoanGlobalItem g) && g.IsArmsLoan;
			Send(player.whoAmI, ServerPlayerMessageType.SetInventorySlot, p => {
				p.Write((byte)slot);
				p.Write(item.type);
				p.Write(item.stack);
				p.Write((byte)item.prefix);
				p.Write(armsLoan);
			});
		}

		public static void ClearInventorySlotSynced(Player player, int slot) {
			if (player == null || !player.active || slot < 0 || slot >= player.inventory.Length) {
				return;
			}

			player.inventory[slot]?.TurnToAir();
			if (Main.netMode == NetmodeID.Server) {
				Send(player.whoAmI, ServerPlayerMessageType.ClearInventorySlot, p => p.Write((byte)slot));
			}
		}

		/// <summary>
		/// Spend copper. Afford gate prefers client snapshot under !SSC.
		/// SSC: BuyItem on server only, then mirror coin-bearing slots.
		/// !SSC: SpendCopper to owning client only (server wallet often empty).
		/// </summary>
		public static bool TrySpendCopperSynced(Player player, long copper) {
			if (player == null || !player.active) {
				return false;
			}

			if (copper <= 0) {
				return true;
			}

			int price = (int)System.Math.Min(int.MaxValue, copper);
			long owned = Utils.CoinsCount(out bool _, player.inventory);
			if (Main.netMode == NetmodeID.Server
			    && !Main.ServerSideCharacter
			    && PlayerSuperpowerClientSnapshot.TryGet(player.whoAmI, out _, out _, out long clientCopper)) {
				owned = clientCopper;
			}

			if (owned < price) {
				return false;
			}

			if (Main.netMode == NetmodeID.Server) {
				if (Main.ServerSideCharacter) {
					CaptureInventoryFingerprint(player, out int[] beforeType, out int[] beforeStack);
					if (!player.BuyItem(price)) {
						return false;
					}

					SyncChangedInventorySlots(player, beforeType, beforeStack);
					return true;
				}

				Send(player.whoAmI, ServerPlayerMessageType.SpendCopper, p => p.Write(price));
				return true;
			}

			return player.BuyItem(price);
		}

		/// <summary>Give coin stacks into coin slots 50–53 (no QuickSpawnItem authority on dedicated).</summary>
		public static void GiveCopperSynced(Player player, long copper) {
			if (player == null || !player.active || copper <= 0) {
				return;
			}

			int[] stacks = Utils.CoinsSplit(copper);
			if (stacks[3] > 0) {
				GiveItemSynced(player, ItemID.PlatinumCoin, stacks[3]);
			}

			if (stacks[2] > 0) {
				GiveItemSynced(player, ItemID.GoldCoin, stacks[2]);
			}

			if (stacks[1] > 0) {
				GiveItemSynced(player, ItemID.SilverCoin, stacks[1]);
			}

			if (stacks[0] > 0) {
				GiveItemSynced(player, ItemID.CopperCoin, stacks[0]);
			}
		}

		/// <summary>
		/// Place items on server then mirror changed slots to owning client.
		/// Returns leftover stack that did not fit (0 = all placed).
		/// </summary>
		public static int GiveItemSynced(Player player, int itemType, int stack) {
			if (player == null || !player.active || itemType <= 0 || stack <= 0) {
				return stack > 0 ? stack : 0;
			}

			if (Main.netMode == NetmodeID.SinglePlayer) {
				player.QuickSpawnItem(player.GetSource_GiftOrReward(), itemType, stack);
				return 0;
			}

			if (Main.netMode != NetmodeID.Server) {
				return stack;
			}

			var dirty = new List<int>();
			int remain = PlaceItemOnServer(player, itemType, stack, armsLoan: false, dirty);
			for (int i = 0; i < dirty.Count; i++) {
				SetInventorySlotSynced(player, dirty[i]);
			}

			return remain;
		}

		/// <summary>Arms-loan marked item place + sync (server already wrote the slot).</summary>
		public static void SyncInventorySlotAfterServerWrite(Player player, int slot) {
			SetInventorySlotSynced(player, slot);
		}

		public static void HandlePacket(byte type, BinaryReader reader) {
			if (Main.netMode == NetmodeID.Server) {
				return;
			}

			Player player = Main.LocalPlayer;
			if (player == null || !player.active) {
				return;
			}

			switch ((ServerPlayerMessageType)type) {
				case ServerPlayerMessageType.ApplyBuff: {
					int buffType = reader.ReadInt32();
					int time = reader.ReadInt32();
					if (buffType > 0 && time > 0) {
						player.AddBuff(buffType, time, quiet: true);
					}

					break;
				}
				case ServerPlayerMessageType.ClearBuff: {
					int buffType = reader.ReadInt32();
					if (buffType > 0) {
						int idx = player.FindBuffIndex(buffType);
						if (idx >= 0) {
							player.DelBuff(idx);
						}
					}

					break;
				}
				case ServerPlayerMessageType.ClearNurseDebuffs: {
					ClearNurseDebuffsLocal(player);
					break;
				}
				case ServerPlayerMessageType.HealMana: {
					int amount = reader.ReadInt32();
					if (amount > 0) {
						player.statMana = System.Math.Min(player.statMana + amount, player.statManaMax2);
						player.ManaEffect(amount);
					}

					break;
				}
				case ServerPlayerMessageType.HealLife: {
					int amount = reader.ReadInt32();
					if (amount > 0) {
						int before = player.statLife;
						player.statLife = System.Math.Min(player.statLife + amount, player.statLifeMax2);
						int shown = player.statLife - before;
						if (shown > 0) {
							player.HealEffect(shown, broadcast: false);
						}
					}

					break;
				}
				case ServerPlayerMessageType.SetInventorySlot: {
					int slot = reader.ReadByte();
					int itemType = reader.ReadInt32();
					int stack = reader.ReadInt32();
					byte prefix = reader.ReadByte();
					bool armsLoan = reader.ReadBoolean();
					ApplySlotLocal(player, slot, itemType, stack, prefix, armsLoan);
					break;
				}
				case ServerPlayerMessageType.ClearInventorySlot: {
					int slot = reader.ReadByte();
					if (slot >= 0 && slot < player.inventory.Length) {
						player.inventory[slot]?.TurnToAir();
					}

					break;
				}
				case ServerPlayerMessageType.SpendCopper: {
					int price = reader.ReadInt32();
					if (price > 0) {
						player.BuyItem(price);
					}

					break;
				}
				case ServerPlayerMessageType.GiveItem: {
					// Legacy: older servers sent Place-on-both-ends. Drain payload; ignore place.
					reader.ReadInt32();
					reader.ReadInt32();
					reader.ReadBoolean();
					break;
				}
			}
		}

		private static void ClearNurseDebuffsLocal(Player player) {
			for (int i = Player.MaxBuffs - 1; i >= 0; i--) {
				int type = player.buffType[i];
				if (type > 0 && Main.debuff[type] && !BuffID.Sets.NurseCannotRemoveDebuff[type]) {
					player.DelBuff(i);
				}
			}
		}

		private static void ApplySlotLocal(Player player, int slot, int itemType, int stack, byte prefix, bool armsLoan) {
			if (slot < 0 || slot >= player.inventory.Length) {
				return;
			}

			Item dest = player.inventory[slot] ??= new Item();
			if (itemType <= 0 || stack <= 0) {
				dest.TurnToAir();
				return;
			}

			dest.SetDefaults(itemType);
			dest.stack = stack;
			if (prefix > 0) {
				dest.Prefix(prefix);
			}

			if (armsLoan && dest.TryGetGlobalItem(out ArmsLoanGlobalItem g)) {
				g.IsArmsLoan = true;
			}

			dest.favorited = false;
		}

		private static void CaptureInventoryFingerprint(Player player, out int[] types, out int[] stacks) {
			int len = System.Math.Min(54, player.inventory.Length);
			types = new int[len];
			stacks = new int[len];
			for (int i = 0; i < len; i++) {
				Item item = player.inventory[i];
				types[i] = item == null || item.IsAir ? 0 : item.type;
				stacks[i] = item == null || item.IsAir ? 0 : item.stack;
			}
		}

		private static void SyncChangedInventorySlots(Player player, int[] beforeType, int[] beforeStack) {
			int len = System.Math.Min(beforeType.Length, player.inventory.Length);
			for (int i = 0; i < len; i++) {
				Item item = player.inventory[i];
				int type = item == null || item.IsAir ? 0 : item.type;
				int stack = item == null || item.IsAir ? 0 : item.stack;
				if (type != beforeType[i] || stack != beforeStack[i]) {
					SetInventorySlotSynced(player, i);
				}
			}
		}

		/// <summary>Copper/silver/gold/platinum coin slots in player.inventory.</summary>
		private static int CoinSlotFor(int itemType) {
			return itemType switch {
				ItemID.CopperCoin => 50,
				ItemID.SilverCoin => 51,
				ItemID.GoldCoin => 52,
				ItemID.PlatinumCoin => 53,
				_ => -1
			};
		}

		private static int PlaceItemOnServer(
			Player player,
			int itemType,
			int stack,
			bool armsLoan,
			List<int> dirtySlots) {
			int coinSlot = CoinSlotFor(itemType);
			if (coinSlot >= 0 && !armsLoan) {
				int remain = PlaceIntoCoinSlot(player, coinSlot, itemType, stack, dirtySlots);
				if (remain > 0) {
					remain = PlaceIntoMainInventory(player, itemType, remain, armsLoan: false, dirtySlots);
				}

				return remain;
			}

			return PlaceIntoMainInventory(player, itemType, stack, armsLoan, dirtySlots);
		}

		private static void MarkDirty(List<int> dirtySlots, int slot) {
			if (dirtySlots == null || dirtySlots.Contains(slot)) {
				return;
			}

			dirtySlots.Add(slot);
		}

		private static int PlaceIntoCoinSlot(
			Player player,
			int slot,
			int itemType,
			int stack,
			List<int> dirtySlots) {
			Item probe = new Item();
			probe.SetDefaults(itemType);
			int maxStack = System.Math.Max(1, probe.maxStack);
			int remain = stack;

			Item dest = player.inventory[slot] ??= new Item();
			if (dest.IsAir || dest.type != itemType) {
				int put = System.Math.Min(maxStack, remain);
				dest.SetDefaults(itemType);
				dest.stack = put;
				dest.favorited = false;
				remain -= put;
				MarkDirty(dirtySlots, slot);
			}
			else {
				int room = maxStack - dest.stack;
				if (room > 0) {
					int add = System.Math.Min(room, remain);
					dest.stack += add;
					remain -= add;
					MarkDirty(dirtySlots, slot);
				}
			}

			return remain;
		}

		private static int PlaceIntoMainInventory(
			Player player,
			int itemType,
			int stack,
			bool armsLoan,
			List<int> dirtySlots) {
			int remain = stack;
			Item probe = new Item();
			probe.SetDefaults(itemType);
			int maxStack = System.Math.Max(1, probe.maxStack);

			for (int i = 0; i < 50 && remain > 0; i++) {
				Item slot = player.inventory[i];
				if (slot == null || slot.IsAir || slot.type != itemType || slot.stack >= maxStack) {
					continue;
				}

				if (armsLoan) {
					if (!slot.TryGetGlobalItem(out ArmsLoanGlobalItem g) || !g.IsArmsLoan) {
						continue;
					}
				}

				int room = maxStack - slot.stack;
				int add = System.Math.Min(room, remain);
				slot.stack += add;
				remain -= add;
				MarkDirty(dirtySlots, i);
			}

			while (remain > 0) {
				int empty = -1;
				for (int i = 0; i < 50; i++) {
					if (player.inventory[i] == null || player.inventory[i].IsAir) {
						empty = i;
						break;
					}
				}

				if (empty < 0) {
					break;
				}

				int put = System.Math.Min(maxStack, remain);
				Item dest = player.inventory[empty] ??= new Item();
				dest.SetDefaults(itemType);
				dest.stack = put;
				if (armsLoan && dest.TryGetGlobalItem(out ArmsLoanGlobalItem g)) {
					g.IsArmsLoan = true;
				}

				dest.favorited = false;
				remain -= put;
				MarkDirty(dirtySlots, empty);
			}

			return remain;
		}

		private static void Send(int playerWhoAmI, ServerPlayerMessageType type, System.Action<ModPacket> writeBody) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)type);
			writeBody?.Invoke(packet);
			packet.Send(toClient: playerWhoAmI);
		}
	}
}
