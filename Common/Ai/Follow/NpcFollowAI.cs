using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>Follow entry for all combat modes (Normal and Boss share locomotion).</summary>
	public static class NpcFollowAI
	{
		public static void Update(NPC npc) {
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing) {
				follow.ResetTimers();
				follow.ResetFlightState(npc);
				FollowingTownNPC.ApplyFollowNetFlags(npc, following: false);
				return;
			}

			Player player = Main.player[follow.FollowPlayerWhoAmI];
			if (!player.active || player.dead) {
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					NpcFollowNetSystem.StopFollow(npc, announce: true);
				}

				return;
			}

			npc.rotation = 0f;

			bool playerFlying = FollowMovement.IsPlayerInFlight(player);
			follow.UpdateFlightState(npc, player, playerFlying);
			NpcWingFlight.TickAnimation(npc, follow, follow.UseFlyingFollow);

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				npc.homeless = true;
			}

			// Clients predict locomotion; server still owns teleports inside Tick.
			int stuck = follow.StuckTimer;
			int far = follow.FarTimer;
			FollowMovement.Tick(npc, player, follow, ref stuck, ref far);
			follow.StuckTimer = stuck;
			follow.FarTimer = far;

			// Vanilla AI (and its FindFrame) is skipped while following — clear sit leftovers
			// and drive walk / idle frames from velocity so feet animate.
			FollowingTownNPC.ClearVanillaTownIdleForFollow(npc);
			RefreshFollowFrames(npc);

			FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
		}

		private static void RefreshFollowFrames(NPC npc) {
			Main.instance.LoadNPC(npc.type);
			int frameCount = Main.npcFrameCount[npc.type];
			if (frameCount <= 0) {
				return;
			}

			int frameHeight = TextureAssets.Npc[npc.type].Height() / frameCount;
			NPCLoader.FindFrame(npc, frameHeight);
		}
	}
}
