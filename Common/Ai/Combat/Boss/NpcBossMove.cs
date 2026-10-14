using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat.Boss
{
	public static class NpcBossMove
	{
		private const float TeleportBossDist = 2000f;
		private const int PredictTicks = 8;
		private const float FlyRiseTiles = 5f;
		private const float LandNearAnchor = 48f;
		private const int StuckFlyTicks = 24;

		public static void Tick(NPC npc, NpcCombatState combat) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			NPC boss = NpcBossCombat.GetLockedBoss(combat);
			if (boss == null) {
				EndFlight(npc, combat, follow);
				FollowMovement.BrakeHorizontal(npc);
				return;
			}

			float dist = npc.Distance(boss.Center);
			if (dist > TeleportBossDist) {
				NpcCombatLocomotion.TeleportNear(npc, boss.Center + new Vector2(-npc.direction * 96f, -32f));
				follow.StuckTimer = 0;
				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			WeaponStance stance = NpcWeaponStance.Resolve(equip.Weapon);
			Vector2 predicted = boss.Center + boss.velocity * PredictTicks;
			Vector2 away = npc.Center - predicted;
			away.Y = 0f;
			if (away.LengthSquared() < 0.01f) {
				away = new Vector2(-npc.direction, 0f);
			}

			away.Normalize();
			Vector2 anchor = predicted + away * stance.IdealDistance;

			int stuck = follow.StuckTimer;
			bool grounded = FollowMovement.IsOnGroundOrPlatform(npc.position, npc.width, npc.height);
			float rise = npc.Center.Y - boss.Center.Y;
			bool bossHigh = rise > FollowMovement.TileSize * FlyRiseTiles;
			bool nearAnchor = Vector2.Distance(npc.Center, anchor) < LandNearAnchor;

			// Prefer ground: fly only when Boss is clearly above, or stuck trying to reach height.
			bool startFly = bossHigh || (stuck >= StuckFlyTicks && rise > FollowMovement.TileSize * 2f);
			bool keepFlying = combat.UseCombatFlight && !grounded && (bossHigh || !nearAnchor);
			bool wantFly = startFly || keepFlying;

			if (wantFly && combat.UseCombatFlight && nearAnchor && !bossHigh && npc.velocity.Y >= 0f) {
				wantFly = false;
			}

			if (wantFly && grounded && !bossHigh && stuck < StuckFlyTicks) {
				wantFly = false;
			}

			if (wantFly) {
				combat.UseCombatFlight = true;
				follow.UseFlyingFollow = true;
				follow.TickWingsSelf();
				NpcCombatLocomotion.FlyToward(npc, anchor, ref stuck);
			}
			else {
				EndFlight(npc, combat, follow);
				bool wantJump = grounded && rise > FollowMovement.TileSize * 2f;
				NpcCombatLocomotion.WalkToward(npc, follow, anchor, wantJump, ref stuck);
			}

			follow.StuckTimer = stuck;
			FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
		}

		private static void EndFlight(NPC npc, NpcCombatState combat, FollowingTownNPC follow) {
			combat.UseCombatFlight = false;
			follow.UseFlyingFollow = false;
			npc.noGravity = false;
		}
	}
}
