using System;
using AITownNPCs.Common.Configs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Follow
{
	public static class FollowMovement
	{
		public const float TeleportDistance = 2000f;
		public const float MaxSpeedMph = 50f;
		public const float MphPerVelocity = 5f;
		public const float MaxSpeedX = MaxSpeedMph / MphPerVelocity;
		public const float MinFollowDist = 64f;
		public const float HorizontalDeadzone = 32f;
		public const float MaxJumpTiles = 100f;
		public const float TileSize = 16f;
		public const int IceMirrorDustType = 197;
		public const int IceMirrorDustCount = 70;
		public const int FlyGraceTicks = 12;
		public const int JumpCooldownTicks = 45;
		public const int WingLandTicks = 12;
		public const float BehindOffset = 80f;
		public const float ComfortGap = 48f;
		public const float ChaseGap = 160f;
		public const float FarGap = 400f;
		public const float TravelSpeed = 1f;
		public const float ComfortAccel = 0.08f;
		public const float ComfortSpeedScale = 0.9f;
		public const float SettleBrake = 0.88f;
		public const float FaceSpeed = 0.35f;

		public static void Tick(NPC npc, Player player, FollowingTownNPC follow, ref int stuckTimer, ref int farTimer, float speedMult = 1f) {
			// Clients predict locomotion only; server owns teleports.
			bool allowTeleport = Main.netMode != NetmodeID.MultiplayerClient;

			float distToPlayer = Vector2.Distance(npc.Center, player.Center);
			if (allowTeleport && distToPlayer > TeleportDistance) {
				TeleportNearPlayer(npc, player);
				farTimer = 0;
				stuckTimer = 0;
				return;
			}

			if (distToPlayer > AinpcServerConfig.Instance.FollowTriggerDistancePixels * 4f) {
				farTimer++;
				if (allowTeleport && farTimer > 300) {
					TeleportNearPlayer(npc, player);
					farTimer = 0;
					stuckTimer = 0;
					return;
				}
			}
			else {
				farTimer = 0;
			}

			if (follow.UseFlyingFollow) {
				ApplyFlyingFollow(npc, player, ref stuckTimer);
				return;
			}

			npc.noGravity = false;
			ApplyGroundFollow(npc, player, ref stuckTimer, follow, speedMult);
		}

		public static bool IsPlayerInFlight(Player player) {
			if (player == null || !player.active || player.dead) {
				return false;
			}

			if (player.pulley || player.grappling[0] >= 0) {
				return false;
			}

			if (IsPlayerOnFlyingMount(player)) {
				return true;
			}

			if (player.mount.Active) {
				return false;
			}

			return IsPlayerUsingWingFlight(player);
		}

		public static bool IsPlayerOnFlyingMount(Player player) {
			return player.mount.Active && !player.mount.Cart && player.mount.CanFly();
		}

		public static bool IsPlayerUsingWingFlight(Player player) {
			if (player.wingsLogic <= 0 || IsOnGroundOrPlatform(player) || player.jump > 0) {
				return false;
			}

			if (player.controlJump && player.wingTime > 0) {
				return true;
			}

			return player.wingTime < player.wingTimeMax;
		}

		public static bool IsOnGroundOrPlatform(Player player) {
			return IsOnGroundOrPlatform(player.position, player.width, player.height);
		}

		public static bool IsOnGroundOrPlatform(Vector2 position, int width, int height) {
			int xStart = (int)(position.X / 16f);
			int xEnd = (int)((position.X + width - 1f) / 16f);
			int y = (int)((position.Y + height + 2f) / 16f);
			for (int x = xStart; x <= xEnd; x++) {
				if (!WorldGen.InWorld(x, y)) {
					continue;
				}

				Tile tile = Framing.GetTileSafely(x, y);
				if (!tile.HasUnactuatedTile) {
					continue;
				}

				if (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]) {
					return true;
				}
			}

			return false;
		}

		public static void ApplyFlyingFollow(NPC npc, Player player, ref int stuckTimer) {
			npc.noGravity = true;
			npc.rotation = 0f;

			Vector2 target = player.Center + new Vector2(-player.direction * 48f, 8f);
			Vector2 to = target - npc.Center;
			float dist = to.Length();

			if (dist < 12f) {
				npc.velocity *= 0.9f;
				if (npc.velocity.LengthSquared() < 0.04f) {
					npc.velocity = Vector2.Zero;
				}
			}
			else {
				to /= dist;
				float speed = MathHelper.Clamp(dist / 80f * MaxSpeedX, 3f, MaxSpeedX);
				const float inertia = 12f;
				npc.velocity = (npc.velocity * inertia + to * speed) / (inertia + 1f);
			}

			if (Math.Abs(npc.velocity.X) > 0.15f) {
				npc.direction = npc.velocity.X > 0f ? 1 : -1;
				npc.spriteDirection = npc.direction;
			}

			bool blocked = Collision.SolidCollision(
				npc.position + npc.velocity,
				npc.width,
				npc.height);
			if (blocked) {
				stuckTimer++;
			}
			else {
				stuckTimer = 0;
			}
		}

		public static void ApplyGroundFollow(NPC npc, Player player, ref int stuckTimer, FollowingTownNPC follow, float speedMult) {
			npc.noGravity = false;
			npc.rotation = 0f;

			int travel = 0;
			if (Math.Abs(player.velocity.X) > TravelSpeed) {
				travel = player.velocity.X > 0f ? 1 : -1;
			}

			if (travel != 0) {
				follow.BehindSign = travel;
			}
			else if (follow.BehindSign == 0) {
				follow.BehindSign = player.direction >= 0 ? 1 : -1;
			}

			float goalX = player.Center.X - follow.BehindSign * BehindOffset;
			float gap = Math.Abs(goalX - npc.Center.X);
			float speedScale = MathHelper.Clamp(speedMult, 0.2f, 1f);
			follow.InCloseFollow = gap <= ComfortGap;

			if (gap <= ComfortGap) {
				stuckTimer = 0;
				SettleBehind(npc, player, speedScale);
			}
			else {
				float accel;
				float cap;
				if (gap < ChaseGap) {
					float t = (gap - ComfortGap) / (ChaseGap - ComfortGap);
					accel = MathHelper.Lerp(0.12f, 0.28f, t);
					float nearCap = Math.Max(Math.Abs(player.velocity.X), 2.2f);
					cap = MathHelper.Lerp(nearCap, 6f, t);
				}
				else {
					float t = MathHelper.Clamp((gap - ChaseGap) / (FarGap - ChaseGap), 0f, 1f);
					accel = MathHelper.Lerp(0.28f, 0.5f, t);
					cap = MathHelper.Lerp(6f, MaxSpeedX, t);
				}

				StepHorizontallyToward(npc, goalX, cap * speedScale, ref stuckTimer, accel);
			}

			FaceAlongTravel(npc, follow);
			TryJumpTowardPlayer(npc, player, follow);
		}

		private static void SettleBehind(NPC npc, Player player, float speedScale) {
			if (Math.Abs(player.velocity.X) > TravelSpeed) {
				float cap = MaxSpeedX * speedScale;
				float target = MathHelper.Clamp(player.velocity.X * ComfortSpeedScale, -cap, cap);
				float step = ComfortAccel;
				if (npc.velocity.X < target) {
					npc.velocity.X = Math.Min(npc.velocity.X + step, target);
				}
				else if (npc.velocity.X > target) {
					npc.velocity.X = Math.Max(npc.velocity.X - step, target);
				}
				return;
			}

			npc.velocity.X *= SettleBrake;
			if (Math.Abs(npc.velocity.X) < 0.1f) {
				npc.velocity.X = 0f;
			}
		}

		private static void FaceAlongTravel(NPC npc, FollowingTownNPC follow) {
			int dir;
			if (Math.Abs(npc.velocity.X) > FaceSpeed) {
				dir = npc.velocity.X > 0f ? 1 : -1;
			}
			else {
				dir = follow.BehindSign >= 0 ? 1 : -1;
			}

			npc.direction = dir;
			npc.spriteDirection = dir;
		}

		/// <summary>Shared ground step used by follow and combat kite movement.</summary>
		public static void StepHorizontallyToward(NPC npc, float goalX, float cap, ref int stuckTimer, float accel = 0.55f) {
			float dx = goalX - npc.Center.X;
			float absX = Math.Abs(dx);
			if (absX >= HorizontalDeadzone * 0.5f) {
				float dir = dx >= 0f ? 1f : -1f;
				npc.direction = dir >= 0f ? 1 : -1;
				npc.spriteDirection = npc.direction;
			}

			float dirX = npc.direction;
			npc.velocity.X += dirX * accel;
			if (Math.Abs(npc.velocity.X) > cap) {
				npc.velocity.X = dirX * cap;
			}

			bool blocked = Collision.SolidCollision(
				npc.position + new Vector2(npc.velocity.X, 0f),
				npc.width,
				npc.height);

			if (blocked || HitWallAhead(npc)) {
				stuckTimer++;
				Collision.StepUp(
					ref npc.position,
					ref npc.velocity,
					npc.width,
					npc.height,
					ref npc.stepSpeed,
					ref npc.gfxOffY);

				if (npc.velocity.Y == 0f && stuckTimer > 4) {
					float wallPx = MeasureWallHeightPixels(npc);
					ApplyJump(npc, Math.Max(wallPx + TileSize, TileSize * 3f));
					stuckTimer = 0;
				}
			}
			else {
				stuckTimer = 0;
			}
		}

		public static void KeepPaceOrBrake(NPC npc, Player player, float dist, float absX, float cap) {
			bool playerMoving = Math.Abs(player.velocity.X) > 2.5f;
			if (playerMoving) {
				float target = MathHelper.Clamp(player.velocity.X, -cap, cap);
				npc.velocity.X = MathHelper.Lerp(npc.velocity.X, target, 0.28f);
				return;
			}

			if (dist < MinFollowDist || absX < HorizontalDeadzone * 0.5f) {
				BrakeHorizontal(npc);
			}
		}

		public static void BrakeHorizontal(NPC npc) {
			npc.velocity.X *= 0.82f;
			if (Math.Abs(npc.velocity.X) < 0.1f) {
				npc.velocity.X = 0f;
			}
		}

		public static void TryJumpTowardPlayer(NPC npc, Player player, FollowingTownNPC follow) {
			if (follow.JumpCooldown > 0 || npc.velocity.Y != 0f) {
				return;
			}

			float rise = npc.Bottom.Y - player.Bottom.Y;
			if (rise <= 24f) {
				return;
			}

			ApplyJump(npc, rise + TileSize * 2f);
			follow.JumpCooldown = JumpCooldownTicks;
		}

		public static void ApplyJump(NPC npc, float heightPixels) {
			float gravity = npc.gravity > 0.01f ? npc.gravity : 0.3f;
			float capped = MathHelper.Clamp(heightPixels, TileSize, MaxJumpTiles * TileSize);
			float jump = (float)Math.Sqrt(2f * gravity * capped);
			npc.velocity.Y = -jump;
		}

		public static float MeasureWallHeightPixels(NPC npc) {
			int tileX = (int)((npc.Center.X + npc.direction * (npc.width / 2f + 4f)) / 16f);
			int tileY = (int)((npc.Bottom.Y - 8f) / 16f);
			int tiles = 0;
			for (int i = 0; i < (int)MaxJumpTiles; i++) {
				int y = tileY - i;
				if (!WorldGen.InWorld(tileX, y)) {
					break;
				}

				Tile tile = Framing.GetTileSafely(tileX, y);
				if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]) {
					break;
				}

				tiles++;
			}

			return tiles * TileSize;
		}

		public static bool HitWallAhead(NPC npc) {
			int tileX = (int)((npc.Center.X + npc.direction * (npc.width / 2f + 4f)) / 16f);
			int tileY = (int)((npc.Bottom.Y - 8f) / 16f);
			if (!WorldGen.InWorld(tileX, tileY)) {
				return false;
			}

			Tile tile = Framing.GetTileSafely(tileX, tileY);
			Tile above = Framing.GetTileSafely(tileX, tileY - 1);
			return tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]
				&& (!above.HasUnactuatedTile || !Main.tileSolid[above.TileType]);
		}

		public static void TeleportNearPlayer(NPC npc, Player player) {
			Vector2 oldPos = npc.position;
			Vector2 dest = player.Center + new Vector2(-player.direction * 48f, -8f);
			npc.position = dest - new Vector2(npc.width / 2f, npc.height);
			npc.velocity = Vector2.Zero;
			if (Collision.SolidCollision(npc.position, npc.width, npc.height)) {
				npc.position = player.position - new Vector2(npc.width + 8f, 0f);
				npc.velocity = Vector2.Zero;
			}

			npc.netUpdate = true;
			NpcFollowNetSystem.BroadcastTeleportFx(npc.whoAmI, oldPos, npc.position, npc.width, npc.height);
		}

		public static void PlayIceMirrorFx(Vector2 from, Vector2 to, int width, int height) {
			if (Main.dedServ) {
				return;
			}

			SoundEngine.PlaySound(SoundID.Item6, from);
			SoundEngine.PlaySound(SoundID.Item6, to);
			for (int i = 0; i < IceMirrorDustCount; i++) {
				Dust.NewDust(from, width, height, IceMirrorDustType, 0f, 0f, 150, default, 1.2f);
				Dust.NewDust(to, width, height, IceMirrorDustType, 0f, 0f, 150, default, 1.2f);
			}
		}
	}
}
