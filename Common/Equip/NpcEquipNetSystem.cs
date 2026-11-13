using System.IO;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Host;
using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Equip
{
	public class NpcEquipNetSystem : ModSystem
	{
		public static void SendSlotSync(int npcWhoAmI, NpcEquipSlotId slot, Item item, int toClient = -1, int ignoreClient = -1) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcEquipMessageType.SyncSlot);
			packet.Write(npcWhoAmI);
			packet.Write((byte)slot);
			ItemIO.Send(item, packet, writeStack: true);
			packet.Send(toClient, ignoreClient);
		}

		/// <summary>Broadcast authoritative slot to every client (including editor).</summary>
		public static void BroadcastSlot(int npcWhoAmI, NpcEquipSlotId slot, Item item) {
			SendSlotSync(npcWhoAmI, slot, item, toClient: -1, ignoreClient: -1);
		}

		public static void SendForceCloseEquip(int toClient, int npcWhoAmI) {
			if (Main.netMode != NetmodeID.Server || toClient < 0) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcEquipMessageType.ForceCloseEquip);
			packet.Write(npcWhoAmI);
			packet.Send(toClient);
		}

		public static void RequestSyncAll() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcEquipMessageType.RequestSyncAll);
			packet.Send();
		}

		public static void RequestOpenEquip(int npcWhoAmI) {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcEquipMessageType.RequestOpenEquip);
			packet.Write(npcWhoAmI);
			packet.Send();
		}

		public static void RequestReleaseEquip(int npcWhoAmI) {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcEquipMessageType.ReleaseEquip);
			packet.Write(npcWhoAmI);
			packet.Send();
		}

		public static void SendAllEquipment(int toClient) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcEquipMessageType.SyncAll);

			int countPos = (int)packet.BaseStream.Position;
			packet.Write(0);

			int count = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!NpcHostRules.IsManagedTownNpc(npc)) {
					continue;
				}

				EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
				if (!equip.HasAnyEquipment()) {
					continue;
				}

				NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
				int slotCount = System.Math.Clamp(profile.Slots.Length, 0, EquippedTownNPC.MaxSlots);
				packet.Write(i);
				packet.Write((byte)slotCount);
				for (int s = 0; s < slotCount; s++) {
					ItemIO.Send(equip.Slots[s], packet, writeStack: true);
				}

				count++;
			}

			long end = packet.BaseStream.Position;
			packet.BaseStream.Position = countPos;
			packet.Write(count);
			packet.BaseStream.Position = end;
			packet.Send(toClient);
		}

		public static void HandlePacket(NpcEquipMessageType type, BinaryReader reader, int sender) {
			switch (type) {
				case NpcEquipMessageType.SyncSlot:
					HandleSyncSlot(reader, sender);
					break;
				case NpcEquipMessageType.RequestSyncAll:
					if (Main.netMode == NetmodeID.Server) {
						SendAllEquipment(sender);
					}
					break;
				case NpcEquipMessageType.SyncAll:
					HandleSyncAll(reader);
					break;
				case NpcEquipMessageType.RequestOpenEquip:
					if (Main.netMode == NetmodeID.Server) {
						HandleRequestOpenEquip(reader, sender);
					}
					break;
				case NpcEquipMessageType.OpenEquipResult:
					HandleOpenEquipResult(reader);
					break;
				case NpcEquipMessageType.ReleaseEquip:
					if (Main.netMode == NetmodeID.Server) {
						HandleReleaseEquip(reader, sender);
					}
					break;
				case NpcEquipMessageType.ForceCloseEquip:
					HandleForceCloseEquip(reader);
					break;
			}
		}

		private static void HandleRequestOpenEquip(BinaryReader reader, int sender) {
			int npcWhoAmI = reader.ReadInt32();
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				SendOpenResult(sender, npcWhoAmI, false);
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			Player player = Main.player[sender];
			if (player == null || !player.active
			    || !NpcHostRules.IsManagedTownNpc(npc)
			    || !NpcEquipHelper.IsPlayerNearNpc(player, npc)) {
				SendOpenResult(sender, npcWhoAmI, false);
				return;
			}

			if (NpcInteractGate.TryGetBlockMessage(player, npc, out string blocked)) {
				NpcAiSpeech.NotifyPlayerLiteral(sender, blocked, NpcInteractGate.NoticeColor);
				SendOpenResult(sender, npcWhoAmI, false);
				return;
			}

			if (NpcOwnershipSystem.IsEquipLockedByOther(npcWhoAmI, sender)) {
				SendOpenResult(sender, npcWhoAmI, false, busy: true);
				return;
			}

			if (!NpcOwnershipSystem.TryAcquireEquipLock(npcWhoAmI, sender)) {
				SendOpenResult(sender, npcWhoAmI, false, busy: true);
				return;
			}

			SendOpenResult(sender, npcWhoAmI, true);
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			int slotCount = System.Math.Clamp(profile.Slots.Length, 0, EquippedTownNPC.MaxSlots);
			for (int i = 0; i < slotCount; i++) {
				SendSlotSync(npcWhoAmI, (NpcEquipSlotId)i, equip.Slots[i], toClient: sender);
			}
		}

		private static void SendOpenResult(int toClient, int npcWhoAmI, bool ok, bool busy = false) {
			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcEquipMessageType.OpenEquipResult);
			packet.Write(npcWhoAmI);
			packet.Write(ok);
			packet.Write(busy);
			packet.Send(toClient);
		}

		private static void HandleOpenEquipResult(BinaryReader reader) {
			int npcWhoAmI = reader.ReadInt32();
			bool ok = reader.ReadBoolean();
			bool busy = reader.ReadBoolean();
			if (!ok) {
				if (busy) {
					Main.NewText(Language.GetTextValue("Mods.AITownNPCs.UI.EquipBusy"), 255, 180, 100);
				}

				return;
			}

			Main.LocalPlayer.GetModPlayer<NpcEquipPlayer>().OpenEquipLocal(npcWhoAmI);
			ModContent.GetInstance<NpcEquipInventorySystem>()?.ResetSnapshotsFromEquip(npcWhoAmI);
		}

		private static void HandleReleaseEquip(BinaryReader reader, int sender) {
			int npcWhoAmI = reader.ReadInt32();
			NpcOwnershipSystem.ReleaseEquipLock(npcWhoAmI, sender);
		}

		private static void HandleForceCloseEquip(BinaryReader reader) {
			int npcWhoAmI = reader.ReadInt32();
			NpcEquipPlayer mp = Main.LocalPlayer.GetModPlayer<NpcEquipPlayer>();
			if (mp.EditingNpcWhoAmI == npcWhoAmI) {
				mp.CloseEquipLocal();
				ModContent.GetInstance<NpcEquipInventorySystem>()?.ClearSnapshots();
			}
		}

		private static void HandleSyncSlot(BinaryReader reader, int sender) {
			int npcWhoAmI = reader.ReadInt32();
			NpcEquipSlotId slot = (NpcEquipSlotId)reader.ReadByte();
			Item item = ItemIO.Receive(reader, readStack: true);

			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return;
			}

			int slotIndex = (int)slot;
			if (slotIndex < 0 || slotIndex >= EquippedTownNPC.MaxSlots) {
				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
				Player player = Main.player[sender];
				if (player == null || !player.active || !NpcEquipHelper.IsPlayerNearNpc(player, npc)) {
					SendSlotSync(npcWhoAmI, slot, equip.GetSlot(slot), toClient: sender);
					return;
				}

				if (!NpcOwnershipSystem.TryGetEquipLockOwner(npcWhoAmI, out int owner) || owner != sender) {
					SendSlotSync(npcWhoAmI, slot, equip.GetSlot(slot), toClient: sender);
					return;
				}

				NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
				if (slotIndex >= profile.Slots.Length) {
					SendSlotSync(npcWhoAmI, slot, equip.GetSlot(slot), toClient: sender);
					return;
				}

				Item[] probe = (Item[])equip.Slots.Clone();
				probe[slotIndex] = item;
				if (!item.IsAir && !profile.IsItemAllowed(slot, item, probe)) {
					SendSlotSync(npcWhoAmI, slot, equip.GetSlot(slot), toClient: sender);
					return;
				}

				equip.SetSlot(slot, item, npcWhoAmI);
				// Ack to editor + everyone else (no ignoreClient).
				BroadcastSlot(npcWhoAmI, slot, item);
			}
			else {
				npc.GetGlobalNPC<EquippedTownNPC>().SetSlot(slot, item, npcWhoAmI);
				ModContent.GetInstance<NpcEquipInventorySystem>()
					?.ApplyAuthoritativeSlot(npcWhoAmI, slot, item);
			}
		}

		private static void HandleSyncAll(BinaryReader reader) {
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++) {
				int npcWhoAmI = reader.ReadInt32();
				int slotCount = reader.ReadByte();
				if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
					for (int s = 0; s < slotCount; s++) {
						ItemIO.Receive(reader, readStack: true);
					}

					continue;
				}

				NPC npc = Main.npc[npcWhoAmI];
				EquippedTownNPC equip = NpcHostRules.IsManagedTownNpc(npc)
					? npc.GetGlobalNPC<EquippedTownNPC>()
					: null;
				for (int s = 0; s < slotCount; s++) {
					Item item = ItemIO.Receive(reader, readStack: true);
					if (equip != null && s < EquippedTownNPC.MaxSlots) {
						equip.Slots[s] = item;
					}
				}

				if (equip != null) {
					for (int s = slotCount; s < EquippedTownNPC.MaxSlots; s++) {
						equip.Slots[s] = new Item();
					}

					ModContent.GetInstance<NpcEquipInventorySystem>()
						?.ResetSnapshotsFromEquip(npcWhoAmI);
				}
			}
		}
	}
}
