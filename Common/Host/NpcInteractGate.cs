using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;

namespace AITownNPCs.Common.Host
{
	/// <summary>
	/// Right-click and equip open: combat blocks everyone; follow blocks everyone except the owner.
	/// Chat uses <see cref="TryGetFollowBlockMessage"/> only, so the owner can still @ during a fight.
	/// </summary>
	public static class NpcInteractGate
	{
		public static readonly Color NoticeColor = new(255, 180, 100);

		public static bool IsInCombat(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return false;
			}

			return npc.GetGlobalNPC<NpcCombatState>().Mode != NpcCombatMode.FollowOnly;
		}

		public static bool TryGetBlockMessage(Player player, NPC npc, out string message) {
			message = null;
			if (player == null || !NpcHostRules.IsManagedTownNpc(npc)) {
				return false;
			}

			if (IsInCombat(npc)) {
				message = Language.GetTextValue("Mods.AITownNPCs.UI.CombatBusy");
				return true;
			}

			return TryGetFollowBlockMessage(player, npc, out message);
		}

		public static bool TryGetFollowBlockMessage(Player player, NPC npc, out string message) {
			message = null;
			if (player == null || !NpcHostRules.IsManagedTownNpc(npc)) {
				return false;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing || follow.FollowPlayerWhoAmI == player.whoAmI) {
				return false;
			}

			string name = Main.player[follow.FollowPlayerWhoAmI].name;
			if (string.IsNullOrWhiteSpace(name)) {
				name = "Player";
			}

			message = Language.GetTextValue("Mods.AITownNPCs.UI.FollowInteractBlocked", name);
			return true;
		}

		/// <summary>Drop an open equip panel. Combat passes no allowed editor; follow start passes the new owner.</summary>
		public static void DismissOpenEquip(int npcWhoAmI, int allowedPlayer = -1) {
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) {
				return;
			}

			if (!NpcOwnershipSystem.TryGetEquipLockOwner(npcWhoAmI, out int editor)) {
				return;
			}

			if (allowedPlayer >= 0 && editor == allowedPlayer) {
				return;
			}

			NpcOwnershipSystem.ReleaseEquipLock(npcWhoAmI, editor);
			if (Main.netMode == Terraria.ID.NetmodeID.Server) {
				NpcEquipNetSystem.SendForceCloseEquip(editor, npcWhoAmI);
				return;
			}

			Player player = Main.player[editor];
			if (player != null && player.active) {
				player.GetModPlayer<NpcEquipPlayer>().CloseEquipLocal();
			}
		}
	}
}
