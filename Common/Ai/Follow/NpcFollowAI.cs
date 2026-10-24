using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>
	/// Follow entry. Host skips full Update only while a boss owns locomotion;
	/// <see cref="UpdateCombatPaused"/> keeps lifecycle (dead player, wings, jump CD).
	/// </summary>
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

			FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
		}

		/// <summary>Lifecycle only while combat owns locomotion.</summary>
		public static void UpdateCombatPaused(NPC npc) {
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

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NpcWingFlight.TickAnimation(npc, follow, follow.UseFlyingFollow);
				if (follow.UseFlyingFollow) {
					NpcWingFlight.PlayVisuals(npc);
				}

				FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
				return;
			}

			npc.homeless = true;
			FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
		}
	}
}
