using AITownNPCs.Common.Ai.Follow;
using Microsoft.Xna.Framework;
using Terraria;

namespace AITownNPCs.Common.Ai.Combat
{
	public static class NpcCombatLocomotion
	{
		public static void WalkToward(NPC npc, FollowingTownNPC follow, Vector2 anchor, bool wantJump, ref int stuck) {
			npc.noGravity = false;
			npc.rotation = 0f;

			if (follow.JumpCooldown > 0) {
				follow.JumpCooldown--;
			}

			float absX = System.Math.Abs(anchor.X - npc.Center.X);
			if (absX < 8f) {
				FollowMovement.BrakeHorizontal(npc);
			}
			else {
				FollowMovement.StepHorizontallyToward(npc, anchor.X, FollowMovement.MaxSpeedX, ref stuck);
			}

			bool grounded = npc.velocity.Y == 0f
				&& FollowMovement.IsOnGroundOrPlatform(npc.position, npc.width, npc.height);

			if (wantJump && grounded && follow.JumpCooldown <= 0) {
				float rise = System.Math.Max(0f, npc.Center.Y - anchor.Y);
				FollowMovement.ApplyJump(npc, System.Math.Max(rise + FollowMovement.TileSize * 2f, FollowMovement.TileSize * 3f));
				follow.JumpCooldown = FollowMovement.JumpCooldownTicks;
			}
			else if (grounded && FollowMovement.HitWallAhead(npc) && follow.JumpCooldown <= 0) {
				FollowMovement.ApplyJump(npc, FollowMovement.TileSize * 3f);
				follow.JumpCooldown = FollowMovement.JumpCooldownTicks;
			}
		}

		public static void FlyToward(NPC npc, Vector2 goal, ref int stuckTimer) {
			npc.noGravity = true;
			npc.rotation = 0f;

			Vector2 to = goal - npc.Center;
			float dist = to.Length();
			if (dist < 12f) {
				npc.velocity *= 0.9f;
				if (npc.velocity.LengthSquared() < 0.04f) {
					npc.velocity = Vector2.Zero;
				}
			}
			else {
				to /= dist;
				float speed = MathHelper.Clamp(dist / 80f * FollowMovement.MaxSpeedX, 3f, FollowMovement.MaxSpeedX);
				const float inertia = 12f;
				npc.velocity = (npc.velocity * inertia + to * speed) / (inertia + 1f);
			}

			if (System.Math.Abs(npc.velocity.X) > 0.15f) {
				npc.direction = npc.velocity.X > 0f ? 1 : -1;
				npc.spriteDirection = npc.direction;
			}

			bool blocked = Collision.SolidCollision(npc.position + npc.velocity, npc.width, npc.height);
			stuckTimer = blocked ? stuckTimer + 1 : 0;
		}

		public static void TeleportNear(NPC npc, Vector2 worldCenter) {
			Vector2 oldPos = npc.position;
			npc.position = worldCenter - new Vector2(npc.width / 2f, npc.height);
			npc.velocity = Vector2.Zero;
			if (Collision.SolidCollision(npc.position, npc.width, npc.height)) {
				npc.position = worldCenter - new Vector2(npc.width / 2f, npc.height + 32f);
				npc.velocity = Vector2.Zero;
			}

			npc.netUpdate = true;
			NpcFollowNetSystem.BroadcastTeleportFx(npc.whoAmI, oldPos, npc.position, npc.width, npc.height);
		}
	}
}
