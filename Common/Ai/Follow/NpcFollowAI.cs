using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>
	/// Follow entry. Host skips full Update when Boss / Normal-with-target;
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
			follow.TickWings(player);
			npc.noGravity = follow.UseFlyingFollow;

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				npc.homeless = true;
			}

			// Clients predict locomotion; server still owns teleports inside Tick.
			int stuck = follow.StuckTimer;
			int far = follow.FarTimer;
			FollowMovement.Tick(npc, player, follow, ref stuck, ref far, speedMult: 1f);
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

			// JumpCooldown / wing frames are owned by combat move while follow is paused.
			// Do not TickWingsSelf here — NpcBossMove / NpcNormalMove already tick once per frame.

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
				return;
			}

			npc.homeless = true;
			FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
		}
	}
}
