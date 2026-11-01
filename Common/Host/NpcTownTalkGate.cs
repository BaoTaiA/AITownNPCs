using System;
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
		/// <summary>Screen-space pad so high-res / zoom / DPI still counts as "on the NPC".</summary>
		private const int ScreenHitPad = 32;

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
				// Do not re-check mouse here: vanilla already chose this NPC (SI or hover).
				// A second MouseWorld hit test breaks talk at some resolutions while SI clear
				// already prevents dig→talk misclicks when the cursor is away.

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
			if (!IsFollowingNpc(npc) || MouseNearNpc(npc)) {
				return;
			}

			Main.ClearSmartInteract();
		}

		private static bool IsFollowingNpc(NPC npc) {
			return npc != null && npc.active && npc.GetGlobalNPC<FollowingTownNPC>().IsFollowing;
		}

		/// <summary>
		/// Cursor on the follower: prefer zoom-correct screen space (matches drawn sprite),
		/// fall back to generous world hitbox. Avoids MouseWorld-only misses at high res.
		/// </summary>
		private static bool MouseNearNpc(NPC npc) {
			if (MouseHitsNpcScreen(npc)) {
				return true;
			}

			Rectangle worldBox = npc.Hitbox;
			worldBox.Inflate(32, 32);
			return worldBox.Contains(Main.MouseWorld.ToPoint());
		}

		private static bool MouseHitsNpcScreen(NPC npc) {
			Matrix matrix = Main.GameViewMatrix.TransformationMatrix;
			Vector2 topLeft = Vector2.Transform(npc.position - Main.screenPosition, matrix);
			Vector2 bottomRight = Vector2.Transform(npc.BottomRight - Main.screenPosition, matrix);

			int left = (int)Math.Floor(Math.Min(topLeft.X, bottomRight.X));
			int top = (int)Math.Floor(Math.Min(topLeft.Y, bottomRight.Y));
			int right = (int)Math.Ceiling(Math.Max(topLeft.X, bottomRight.X));
			int bottom = (int)Math.Ceiling(Math.Max(topLeft.Y, bottomRight.Y));

			var screenBox = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
			screenBox.Inflate(ScreenHitPad, ScreenHitPad);
			return screenBox.Contains(Main.mouseX, Main.mouseY);
		}
	}
}
