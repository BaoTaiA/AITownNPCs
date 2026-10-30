using AITownNPCs.Common.Ai.Firing;
using AITownNPCs.Common.Ai.Follow;
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
			On_Player.SmartInteractLookup += OnSmartInteractLookup;
		}

		public override void Unload() {
			On_Player.SetTalkNPC -= OnSetTalkNPC;
			On_Player.SmartInteractLookup -= OnSmartInteractLookup;
		}

		private static void OnSmartInteractLookup(On_Player.orig_SmartInteractLookup orig, Player self) {
			orig(self);
			if (self == null || self.whoAmI != Main.myPlayer) {
				return;
			}

			// Followers sit in smart-interact range. If the mouse is not on them,
			// drop that target so right-click goes to tiles instead of a swallowed talk.
			ReleaseFollowerSmartInteract();
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
				// Followers stay in smart-cursor reach. World right-click only talks
				// when the mouse tile hits them — same as smart cursor off. Equip reopen skips this.
				if ((Main.mouseRight || player.controlUseTile) && IsFollowingNpc(npc) && !MouseTileHits(npc)) {
					return;
				}

				if (NpcInteractGate.TryGetBlockMessage(player, npc, out string message)) {
					Main.NewText(message, NpcInteractGate.NoticeColor);
					return;
				}
			}

			orig(player, npcIndex, fromNet);
		}

		private static void ReleaseFollowerSmartInteract() {
			int npcIndex = Main.SmartInteractNPC;
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcIndex];
			if (!IsFollowingNpc(npc) || MouseTileHits(npc)) {
				return;
			}

			Main.ClearSmartInteract();
		}

		private static bool IsFollowingNpc(NPC npc) {
			return npc != null && npc.active && npc.GetGlobalNPC<FollowingTownNPC>().IsFollowing;
		}

		/// <summary>16×16 under the real mouse, not the smart-cursor aim tile.</summary>
		private static bool MouseTileHits(NPC npc) {
			Vector2 world = Main.MouseWorld;
			int x = (int)(world.X / 16f);
			int y = (int)(world.Y / 16f);
			return new Rectangle(x * 16, y * 16, 16, 16).Intersects(npc.Hitbox);
		}
	}
}
