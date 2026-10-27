using AITownNPCs.Common.Ai.Firing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Host
{
	/// <summary>Block opening town-NPC chat while the NPC is fighting or following someone else.</summary>
	public class NpcTownTalkGate : ModSystem
	{
		public override void Load() {
			On_Player.SetTalkNPC += OnSetTalkNPC;
		}

		public override void Unload() {
			On_Player.SetTalkNPC -= OnSetTalkNPC;
		}

		private static void OnSetTalkNPC(On_Player.orig_SetTalkNPC orig, Player player, int npcIndex, bool fromNet) {
			// Dummy briefly is Main.myPlayer. Swallow the call so vanilla does not
			// open chat or clear Main.npcChatText for the real player.
			if (player != null && NpcDummyPool.IsDummy(player.whoAmI)) {
				return;
			}

			if (!fromNet
				&& player != null
				&& player.whoAmI == Main.myPlayer
				&& npcIndex >= 0
				&& npcIndex < Main.maxNPCs) {
				NPC npc = Main.npc[npcIndex];
				// Smart cursor can snap interact aim to tiles beside a close follower.
				// Only open talk when the real mouse is on the NPC hitbox (not SmartCursorX/Y).
				if (npc == null || !npc.active || !npc.Hitbox.Contains(Main.MouseWorld.ToPoint())) {
					return;
				}

				if (NpcInteractGate.TryGetBlockMessage(player, npc, out string message)) {
					Main.NewText(message, NpcInteractGate.NoticeColor);
					return;
				}
			}

			orig(player, npcIndex, fromNet);
		}
	}
}
