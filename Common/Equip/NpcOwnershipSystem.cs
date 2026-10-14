using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Equip
{
	/// <summary>
	/// Server-authoritative equip UI locks only. Player leave cleanup lives in NpcPlayerLifecycle.
	/// </summary>
	public class NpcOwnershipSystem : ModSystem
	{
		/// <summary>npcWhoAmI -> editor playerWhoAmI</summary>
		private static readonly Dictionary<int, int> EquipLocks = new();

		public static bool TryGetEquipLockOwner(int npcWhoAmI, out int playerWhoAmI) {
			return EquipLocks.TryGetValue(npcWhoAmI, out playerWhoAmI);
		}

		public static bool IsEquipLockedByOther(int npcWhoAmI, int playerWhoAmI) {
			return EquipLocks.TryGetValue(npcWhoAmI, out int owner) && owner != playerWhoAmI;
		}

		public static bool TryAcquireEquipLock(int npcWhoAmI, int playerWhoAmI) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return false;
			}

			if (EquipLocks.TryGetValue(npcWhoAmI, out int owner)) {
				return owner == playerWhoAmI;
			}

			EquipLocks[npcWhoAmI] = playerWhoAmI;
			return true;
		}

		public static void ReleaseEquipLock(int npcWhoAmI, int playerWhoAmI) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (EquipLocks.TryGetValue(npcWhoAmI, out int owner) && owner == playerWhoAmI) {
				EquipLocks.Remove(npcWhoAmI);
			}
		}

		public static void ReleaseAllEquipLocksForPlayer(int playerWhoAmI) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			List<int> remove = null;
			foreach (var kv in EquipLocks) {
				if (kv.Value == playerWhoAmI) {
					remove ??= new List<int>();
					remove.Add(kv.Key);
				}
			}

			if (remove == null) {
				return;
			}

			foreach (int npcWho in remove) {
				EquipLocks.Remove(npcWho);
			}
		}

		public override void PostUpdatePlayers() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			// Drop locks when owner goes inactive or too far from the NPC.
			List<int> staleLocks = null;
			foreach (var kv in EquipLocks) {
				int npcWho = kv.Key;
				int owner = kv.Value;
				if (owner < 0 || owner >= Main.maxPlayers || !Main.player[owner].active) {
					staleLocks ??= new List<int>();
					staleLocks.Add(npcWho);
					continue;
				}

				if (npcWho < 0 || npcWho >= Main.maxNPCs || !Main.npc[npcWho].active) {
					staleLocks ??= new List<int>();
					staleLocks.Add(npcWho);
					continue;
				}

				Player p = Main.player[owner];
				NPC npc = Main.npc[npcWho];
				if (!NpcEquipHelper.IsPlayerNearNpc(p, npc)) {
					staleLocks ??= new List<int>();
					staleLocks.Add(npcWho);
				}
			}

			if (staleLocks != null) {
				foreach (int npcWho in staleLocks) {
					if (EquipLocks.TryGetValue(npcWho, out int staleOwner)) {
						if (Main.netMode == NetmodeID.SinglePlayer
						    || (Main.netMode != NetmodeID.Server && staleOwner == Main.myPlayer)) {
							Main.player[staleOwner].GetModPlayer<NpcEquipPlayer>().CloseEquipLocal();
						}
						else if (Main.netMode == NetmodeID.Server) {
							NpcEquipNetSystem.SendForceCloseEquip(staleOwner, npcWho);
						}
					}

					EquipLocks.Remove(npcWho);
				}
			}

			for (int i = 0; i < Main.maxPlayers; i++) {
				Player p = Main.player[i];
				if (p == null) {
					continue;
				}

				if (!p.active) {
					ReleaseAllEquipLocksForPlayer(i);
				}
			}
		}

		public override void OnWorldUnload() {
			EquipLocks.Clear();
		}
	}
}
