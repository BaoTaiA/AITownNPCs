using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>Follow entry for all combat modes (Normal and Boss share locomotion).</summary>
	public static class NpcFollowAI
	{
		public static void Update(NPC npc) {
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.HasFollowOwner) {
				follow.ResetTimers();
				follow.ResetFlightState(npc);
				FollowingTownNPC.ApplyFollowNetFlags(npc, following: false);
				return;
			}

			Player player = Main.player[follow.FollowPlayerWhoAmI];
			if (!player.active) {
				// Brief inactive: PostUpdateNPCs grace drops follow. Do not StopFollow here.
				FollowingTownNPC.ApplyFollowNetFlags(npc, following: false);
				return;
			}

			follow.OwnerInactiveGrace = 0;

			if (player.dead) {
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					NpcFollowNetSystem.StopFollow(npc, announce: true, reason: "owner_dead");
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
			FollowingTownNPC.TickFollowPositionSync(npc);
		}
	}
}
