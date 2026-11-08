using AITownNPCs.Common.Ai.Firing;
using AITownNPCs.Common.Host;
using AITownNPCs.Common.UI;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Equip
{
	public class NpcEquipPlayer : ModPlayer
	{
		/// <summary>NPC whose equipment panel is open under the dialogue; -1 if none.</summary>
		public int EditingNpcWhoAmI = -1;

		private int _blockedTalkNpc = -1;

		public bool IsEditing => EditingNpcWhoAmI >= 0 && EditingNpcWhoAmI < Main.maxNPCs;

		public NPC EditingNpc {
			get {
				if (!IsEditing) {
					return null;
				}

				NPC npc = Main.npc[EditingNpcWhoAmI];
				return NpcHostRules.IsManagedTownNpc(npc) ? npc : null;
			}
		}

		public EquippedTownNPC EditingEquip {
			get {
				NPC npc = EditingNpc;
				return npc?.GetGlobalNPC<EquippedTownNPC>();
			}
		}

		/// <summary>Request server lock (MP) or open immediately (SP).</summary>
		public void RequestOpenEquip(int npcWhoAmI) {
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			if (!NpcHostRules.IsManagedTownNpc(npc) || !NpcEquipHelper.IsPlayerNearNpc(Player, npc)) {
				return;
			}

			if (NpcInteractGate.TryGetBlockMessage(Player, npc, out string blocked)) {
				Main.NewText(blocked, NpcInteractGate.NoticeColor);
				return;
			}

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NpcEquipNetSystem.RequestOpenEquip(npcWhoAmI);
				return;
			}

			// Singleplayer / listen-server host local open.
			if (!NpcOwnershipSystem.TryAcquireEquipLock(npcWhoAmI, Player.whoAmI)) {
				NotifyLocal("Mods.AITownNPCs.UI.EquipBusy");
				return;
			}

			OpenEquipLocal(npcWhoAmI);
		}

		public void OpenEquipLocal(int npcWhoAmI) {
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return;
			}

			Player.GetModPlayer<NpcStatsPlayer>().CloseStatsQuiet();
			EditingNpcWhoAmI = npcWhoAmI;
			Main.playerInventory = true;
			Player.chest = -1;
			if (Player.talkNPC != npcWhoAmI) {
				Player.SetTalkNPC(npcWhoAmI);
			}
		}

		public void CloseEquip() {
			int npcWho = EditingNpcWhoAmI;
			CloseEquipLocal();
			if (npcWho < 0) {
				return;
			}

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NpcEquipNetSystem.RequestReleaseEquip(npcWho);
			}
			else {
				NpcOwnershipSystem.ReleaseEquipLock(npcWho, Player.whoAmI);
			}
		}

		public void CloseEquipLocal() {
			EditingNpcWhoAmI = -1;
			ModContent.GetInstance<NpcEquipInventorySystem>()?.ClearSnapshots();
		}

		public override void OnEnterWorld() {
			EditingNpcWhoAmI = -1;
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NpcEquipNetSystem.RequestSyncAll();
			}
		}

		public override void PlayerDisconnect() {
			NpcPlayerLifecycle.CleanupPlayer(Player.whoAmI);
		}

		public override void PostUpdate() {
			if (Player.whoAmI == Main.myPlayer
				&& !NpcDummyPool.IsDummy(Player.whoAmI)
				&& !NpcDummyLocal.IsRunning) {
				int talk = Player.talkNPC;
				string blocked = null;
				bool blockedTalk = talk >= 0 && talk < Main.maxNPCs
					&& NpcInteractGate.TryGetBlockMessage(Player, Main.npc[talk], out blocked);
				if (blockedTalk) {
					if (IsEditing && EditingNpcWhoAmI == talk) {
						CloseEquip();
					}

					Player.SetTalkNPC(-1);
					Main.npcChatText = string.Empty;
					if (_blockedTalkNpc != talk) {
						_blockedTalkNpc = talk;
						Main.NewText(blocked, NpcInteractGate.NoticeColor);
					}
				}
				else {
					_blockedTalkNpc = -1;
				}
			}

			if (!IsEditing) {
				return;
			}

			NPC npc = Main.npc[EditingNpcWhoAmI];
			if (!npc.active
				|| !NpcHostRules.IsManagedTownNpc(npc)
				|| !NpcEquipHelper.IsPlayerNearNpc(Player, npc)
				|| Player.talkNPC != EditingNpcWhoAmI
				|| Player.dead) {
				CloseEquip();
			}
		}

		private static void NotifyLocal(string key) {
			Main.NewText(Language.GetTextValue(key), 255, 180, 100);
		}
	}
}
