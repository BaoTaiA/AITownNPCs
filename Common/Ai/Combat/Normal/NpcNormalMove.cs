using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat.Normal
{
	/// <summary>Normal locomotion while Follow is paused: approach, jump up, fall down, fly if needed.</summary>
	public static class NpcNormalMove
	{
		private const float JumpRiseTiles = 2f;
		private const float FlyRiseTiles = 5f;
		private const float FallBelowTiles = 1.5f;

		public static void Tick(NPC npc, Player player, NpcCombatState combat) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			int who = combat.CachedTargetWhoAmI;
			if (who < 0 || who >= Main.maxNPCs) {
				combat.UseCombatFlight = false;
				combat.CombatWantDrop = false;
				npc.noGravity = false;
				return;
			}

			NPC target = Main.npc[who];
			if (!target.active) {
				combat.UseCombatFlight = false;
				combat.CombatWantDrop = false;
				npc.noGravity = false;
				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			WeaponStance stance = NpcWeaponStance.Resolve(equip.Weapon);
			Vector2 anchor = ComputeAnchor(npc, target, stance);

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			int stuck = follow.StuckTimer;
			float dy = target.Center.Y - npc.Center.Y;
			float rise = npc.Center.Y - target.Center.Y;

			combat.CombatWantDrop = dy > FollowMovement.TileSize * FallBelowTiles
				&& target.Bottom.Y > npc.Bottom.Y + 24f;

			bool wantFly = combat.UseCombatFlight
				|| rise > FollowMovement.TileSize * FlyRiseTiles
				|| (rise > FollowMovement.TileSize * JumpRiseTiles && !CanJumpReach(rise));

			bool grounded = FollowMovement.IsOnGroundOrPlatform(npc.position, npc.width, npc.height);
			bool stillNeedFly = rise > FollowMovement.TileSize * FlyRiseTiles
				|| (rise > FollowMovement.TileSize * JumpRiseTiles && !CanJumpReach(rise));

			if ((wantFly || combat.UseCombatFlight)
				&& stillNeedFly
				&& npc.Distance(player.Center) < NpcNormalCombat.LeashDist * 0.95f) {
				combat.UseCombatFlight = true;
				combat.CombatWantDrop = false;
				follow.UseFlyingFollow = true;
				follow.TickWingsSelf();
				NpcCombatLocomotion.FlyToward(npc, anchor, ref stuck);

				float near = Vector2.Distance(npc.Center, anchor);
				if (grounded || (near < 24f && npc.velocity.Y >= 0f && rise <= FollowMovement.TileSize * JumpRiseTiles)) {
					combat.UseCombatFlight = false;
					follow.UseFlyingFollow = false;
					npc.noGravity = false;
				}
			}
			else {
				combat.UseCombatFlight = false;
				follow.UseFlyingFollow = false;
				npc.noGravity = false;
				bool wantJump = !combat.CombatWantDrop
					&& rise > FollowMovement.TileSize * JumpRiseTiles;
				NpcCombatLocomotion.WalkToward(npc, follow, anchor, wantJump, ref stuck);
			}

			follow.StuckTimer = stuck;
			FollowingTownNPC.ApplyFollowNetFlags(npc, following: true);
		}

		private static Vector2 ComputeAnchor(NPC npc, NPC target, WeaponStance stance) {
			float ideal = stance.IdealDistance;
			Vector2 away = npc.Center - target.Center;
			away.Y = 0f;
			if (away.LengthSquared() < 0.01f) {
				away = new Vector2(-npc.direction, 0f);
			}

			away.Normalize();
			float dist = npc.Distance(target.Center);
			if (dist < ideal * 0.7f) {
				return target.Center + away * ideal;
			}

			if (dist > ideal * 1.3f) {
				return target.Center + away * ideal * 0.85f;
			}

			return new Vector2(npc.Center.X, target.Center.Y);
		}

		private static bool CanJumpReach(float risePixels) {
			float maxJump = FollowMovement.MaxJumpTiles * FollowMovement.TileSize;
			return risePixels <= maxJump * 0.85f;
		}
	}
}
