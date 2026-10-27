using System;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Combat.Normal;
using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Equip;
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
		public const float HorizontalDeadzone = 32f;
		public const float MaxJumpTiles = 100f;
		public const float TileSize = 16f;
		public const int IceMirrorDustType = 197;
		public const int IceMirrorDustCount = 70;
		public const int FlyGraceTicks = 12;
		public const int JumpCooldownTicks = 45;
		public const int WingLandTicks = 12;
		public const float FaceSpeed = 0.35f;
		public const float SlotMin = 32f;
		public const float SlotMax = 80f;
		public const float FollowAccel = 0.32f;
		public const float PlatformGap = 32f;
		public const float DodgeRange = 32f;
		public const float DodgeAccel = 1.5f;
		public const float DodgeJumpTiles = 6f;
		public const int TeleportSettleTicks = 45;

		public static void Tick(NPC npc, Player player, FollowingTownNPC follow, ref int stuckTimer, ref int farTimer) {
			// Clients predict locomotion only; server owns teleports.
			bool allowTeleport = Main.netMode != NetmodeID.MultiplayerClient;

			float distToPlayer = Vector2.Distance(npc.Center, player.Center);
			if (allowTeleport && distToPlayer > TeleportDistance) {
				TeleportNearPlayer(npc, player);
				farTimer = 0;
				stuckTimer = 0;
				return;
			}

			// Soft recall: own config (not chat/follow-start range × 4).
			if (distToPlayer > AinpcServerConfig.Instance.FollowLostTeleportDistancePixels) {
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
				ApplyFlyingFollow(npc, player, follow, ref stuckTimer);
				return;
			}

			npc.noGravity = false;
			ApplyGroundFollow(npc, player, ref stuckTimer, follow);
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

		public static void ApplyFlyingFollow(NPC npc, Player player, FollowingTownNPC follow, ref int stuckTimer) {
			RememberTravelSign(player, follow);
			if (TryGetChaseTarget(npc, out NPC foe)) {
				if (npc.Distance(foe.Center) <= DodgeRange) {
					float dir = npc.Center.X >= foe.Center.X ? 1f : -1f;
					if (Math.Abs(npc.Center.X - foe.Center.X) < 0.5f) {
						dir = npc.direction != 0 ? -npc.direction : -1f;
					}

					Vector2 retreat = foe.Center + new Vector2(dir * SlotMax, 0f);
					NpcWingFlight.Apply(npc, retreat, ref stuckTimer);
					return;
				}

				EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
				WeaponStance stance = NpcWeaponStance.Resolve(equip?.Weapon);
				Vector2 anchor = ComputeAnchor(npc, foe, stance);
				bool mustHug = stance.PreferMeleeHug || stance.Band == StanceBand.MeleeHug;
				bool canShoot = !mustHug
					&& npc.Distance(foe.Center) <= stance.MaxFireDistance
					&& NpcCombatFire.HasLineOfSight(npc, foe);
				if (canShoot) {
					Vector2 hold = new Vector2(anchor.X, npc.Center.Y);
					if (Math.Abs(npc.Center.X - hold.X) <= TileSize) {
						hold = npc.Center;
					}

					NpcWingFlight.Apply(npc, hold, ref stuckTimer);
				}
				else {
					NpcWingFlight.Apply(npc, anchor, ref stuckTimer);
				}

				return;
			}

			int travel = follow.BehindSign == 0
				? (player.direction >= 0 ? 1 : -1)
				: follow.BehindSign;
			float gap = SlotMin + (npc.whoAmI % 4) * ((SlotMax - SlotMin) / 3f);
			Vector2 slot = new Vector2(player.Center.X - travel * gap, player.Center.Y + 8f);
			NpcWingFlight.Apply(npc, slot, ref stuckTimer);
		}

		public static void ApplyGroundFollow(NPC npc, Player player, ref int stuckTimer, FollowingTownNPC follow) {
			npc.noGravity = false;
			npc.rotation = 0f;

			RememberTravelSign(player, follow);
			if (TryGetChaseTarget(npc, out NPC target)) {
				if (npc.Distance(target.Center) <= DodgeRange) {
					stuckTimer = 0;
					RetreatFrom(npc, target, follow);
				}
				else {
					EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
					WeaponStance stance = NpcWeaponStance.Resolve(equip?.Weapon);
					Vector2 anchor = ComputeAnchor(npc, target, stance);
					StepToward(npc, follow, anchor.X, MaxSpeedX, ref stuckTimer);
					bool mustHug = stance.PreferMeleeHug || stance.Band == StanceBand.MeleeHug;
					bool canShoot = !mustHug
						&& npc.Distance(target.Center) <= stance.MaxFireDistance
						&& NpcCombatFire.HasLineOfSight(npc, target);
					if (!canShoot) {
						TryPlatformStep(npc, follow, anchor);
					}
				}
			}
			else {
				ApplyPlayerSlot(npc, player, follow, ref stuckTimer);
			}

			FaceAlongTravel(npc, follow);
		}

		private static void ApplyPlayerSlot(NPC npc, Player player, FollowingTownNPC follow, ref int stuckTimer) {
			int travel = follow.BehindSign == 0 ? 1 : follow.BehindSign;
			float gap = SlotMin + (npc.whoAmI % 4) * ((SlotMax - SlotMin) / 3f);
			bool playerBelow = player.Bottom.Y > npc.Bottom.Y + PlatformGap;
			bool shaftFollow = playerBelow
				&& Math.Abs(npc.Center.X - player.Center.X) <= SlotMax + TileSize;
			float slotX = follow.TeleportSettle > 0 || shaftFollow
				? player.Center.X
				: player.Center.X - travel * gap;
			float dx = slotX - npc.Center.X;
			if (Math.Abs(dx) <= TileSize) {
				stuckTimer = 0;
				EaseHorizontal(npc, shaftFollow ? 0f : player.velocity.X);
			}
			else {
				StepToward(npc, follow, slotX, MaxSpeedX, ref stuckTimer);
			}

			Vector2 goal = new Vector2(slotX, player.Center.Y);
			if (TryDropShaft(npc, player, follow)) {
				return;
			}

			if (!TryPlatformStep(npc, follow, goal)) {
				MatchPlayerJump(npc, player);
			}
		}

		/// <summary>Walk to the dig column and drop in, solid lip or platform.</summary>
		private static bool TryDropShaft(NPC npc, Player player, FollowingTownNPC follow) {
			if (follow.JumpCooldown > 0 || npc.velocity.Y != 0f) {
				return false;
			}

			if (player.Bottom.Y <= npc.Bottom.Y + PlatformGap) {
				return false;
			}

			if (Math.Abs(npc.Center.X - player.Center.X) > SlotMax + TileSize) {
				return false;
			}

			if (!HoleOpenBelow(npc, player.Center.X)) {
				return false;
			}

			if (Math.Abs(npc.Center.X - player.Center.X) > TileSize) {
				return false;
			}

			npc.position.X = player.Center.X - npc.width * 0.5f;
			npc.velocity.X = 0f;
			if (StandingOnPlatform(npc)) {
				npc.position.Y += 8f;
			}
			else {
				npc.position.Y += 2f;
			}

			npc.velocity.Y = 2f;
			return true;
		}

		private static bool HoleOpenBelow(NPC npc, float worldX) {
			int tileX = (int)(worldX / TileSize);
			int startY = (int)((npc.Bottom.Y + 4f) / TileSize);
			for (int i = 0; i < 4; i++) {
				int y = startY + i;
				if (!WorldGen.InWorld(tileX, y)) {
					return false;
				}

				Tile tile = Framing.GetTileSafely(tileX, y);
				if (!tile.HasUnactuatedTile) {
					continue;
				}

				if (Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
					return false;
				}
			}

			return true;
		}

		private static void EaseHorizontal(NPC npc, float desired) {
			if (npc.velocity.X < desired) {
				npc.velocity.X = Math.Min(npc.velocity.X + FollowAccel, desired);
			}
			else if (npc.velocity.X > desired) {
				npc.velocity.X = Math.Max(npc.velocity.X - FollowAccel, desired);
			}
		}

		private static void RetreatFrom(NPC npc, NPC target, FollowingTownNPC follow) {
			float dir = npc.Center.X >= target.Center.X ? 1f : -1f;
			if (Math.Abs(npc.Center.X - target.Center.X) < 0.5f) {
				dir = npc.direction != 0 ? -npc.direction : -1f;
			}

			npc.direction = dir > 0f ? 1 : -1;
			npc.spriteDirection = npc.direction;
			float desired = dir * MaxSpeedX;
			float step = DodgeAccel;
			if (npc.velocity.X < desired) {
				npc.velocity.X = Math.Min(npc.velocity.X + step, desired);
			}
			else if (npc.velocity.X > desired) {
				npc.velocity.X = Math.Max(npc.velocity.X - step, desired);
			}

			if (follow.JumpCooldown > 0 || npc.velocity.Y != 0f) {
				return;
			}

			if (!IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return;
			}

			TryWallJump(npc, follow, Math.Max(DodgeJumpTiles * TileSize, TileSize * 3f));
		}

		private static bool TryPlatformStep(NPC npc, FollowingTownNPC follow, Vector2 goal) {
			if (follow.JumpCooldown > 0
				|| npc.velocity.Y != 0f
				|| !IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return false;
			}

			float rise = npc.Center.Y - goal.Y;
			if (rise > PlatformGap) {
				if (!TryMeasurePlatformAbove(npc, goal, out float height)) {
					height = Math.Min(rise + TileSize * 2f, MaxJumpTiles * TileSize);
				}

				ApplyJump(npc, height);
				follow.JumpCooldown = JumpCooldownTicks;
				return true;
			}

			return false;
		}

		private static bool StandingOnPlatform(NPC npc) {
			int xStart = (int)(npc.position.X / TileSize);
			int xEnd = (int)((npc.Right.X - 1f) / TileSize);
			int y = (int)((npc.Bottom.Y + 2f) / TileSize);
			for (int x = xStart; x <= xEnd; x++) {
				if (IsPlatformTile(x, y)) {
					return true;
				}
			}

			return false;
		}

		private static bool TryMeasurePlatformAbove(NPC npc, Vector2 goal, out float heightPixels) {
			heightPixels = 0f;
			int originX = (int)(npc.Center.X / TileSize);
			int goalX = (int)(goal.X / TileSize);
			int toward = goalX >= originX ? 1 : -1;
			int span = Math.Min(6, Math.Abs(goalX - originX));
			int startY = (int)((npc.Top.Y - 2f) / TileSize);
			for (int c = 0; c <= span; c++) {
				for (int i = 1; i <= (int)MaxJumpTiles; i++) {
					int y = startY - i;
					int x = originX + toward * c;
					if (!WorldGen.InWorld(x, y)) {
						break;
					}

					if (!IsPlatformTile(x, y)) {
						continue;
					}

					float surface = y * TileSize;
					if (surface < goal.Y - SlotMax) {
						break;
					}

					float rise = npc.Bottom.Y - surface;
					if (rise <= TileSize) {
						continue;
					}

					heightPixels = Math.Min(rise + TileSize, MaxJumpTiles * TileSize);
					return true;
				}
			}

			return false;
		}

		private static bool IsPlatformTile(int x, int y) {
			if (!WorldGen.InWorld(x, y)) {
				return false;
			}

			Tile tile = Framing.GetTileSafely(x, y);
			return tile.HasUnactuatedTile
				&& Main.tileSolidTop[tile.TileType]
				&& !Main.tileSolid[tile.TileType];
		}

		private static void StepToward(NPC npc, FollowingTownNPC follow, float goalX, float speed, ref int stuckTimer) {
			float dx = goalX - npc.Center.X;
			float abs = Math.Abs(dx);
			if (abs < 8f) {
				stuckTimer = 0;
				EaseHorizontal(npc, 0f);
				return;
			}

			float dir = dx >= 0f ? 1f : -1f;
			npc.direction = dir >= 0f ? 1 : -1;
			npc.spriteDirection = npc.direction;
			float scaled = Math.Min(speed, Math.Max(1.8f, abs * 0.05f));
			EaseHorizontal(npc, dir * scaled);

			bool blocked = Collision.SolidCollision(
				npc.position + new Vector2(npc.velocity.X, 0f),
				npc.width,
				npc.height);
			if (!blocked && !HitWallAhead(npc)) {
				stuckTimer = 0;
				return;
			}

			stuckTimer++;
			Collision.StepUp(
				ref npc.position,
				ref npc.velocity,
				npc.width,
				npc.height,
				ref npc.stepSpeed,
				ref npc.gfxOffY);

			if (stuckTimer > 2 && follow.TeleportSettle <= 0) {
				TryWallJump(npc, follow, TileSize * 3f);
				stuckTimer = 0;
			}
		}

		private static void TryWallJump(NPC npc, FollowingTownNPC follow, float minHeight) {
			if (follow.TeleportSettle > 0 || follow.JumpCooldown > 0 || npc.velocity.Y != 0f) {
				return;
			}

			if (!IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return;
			}

			if (follow.IsFollowing) {
				Player owner = Main.player[follow.FollowPlayerWhoAmI];
				if (owner != null
					&& owner.active
					&& !owner.dead
					&& npc.Distance(owner.Center) <= TileSize * 40f) {
					npc.velocity.X = 0f;
					return;
				}
			}

			float wall = MeasureWallHeightPixels(npc);
			float height = Math.Max(minHeight, wall + TileSize);
			ApplyJump(npc, height);
			follow.JumpCooldown = JumpCooldownTicks;
		}

		private static void MatchPlayerJump(NPC npc, Player player) {
			if (player.velocity.Y >= 0f || npc.velocity.Y != 0f) {
				return;
			}

			if (!IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return;
			}

			npc.velocity.Y = player.velocity.Y;
		}

		private static void RememberTravelSign(Player player, FollowingTownNPC follow) {
			if (Math.Abs(player.velocity.X) > 1f) {
				follow.BehindSign = player.velocity.X > 0f ? 1 : -1;
			}
			else if (follow.BehindSign == 0) {
				follow.BehindSign = player.direction >= 0 ? 1 : -1;
			}
		}

		private static bool TryGetChaseTarget(NPC npc, out NPC target) {
			target = null;
			NpcCombatState state = npc.GetGlobalNPC<NpcCombatState>();
			if (state == null || state.Mode == NpcCombatMode.Boss || !state.HasEngagedTarget) {
				return false;
			}

			target = NpcNormalCombat.GetCachedIfValid(npc, state);
			return target != null;
		}

		/// <summary>Stop distance from the equipped weapon. Same bands the old normal move used.</summary>
		public static Vector2 ComputeAnchor(NPC npc, NPC target, WeaponStance stance) {
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

		public static void BrakeHorizontal(NPC npc) {
			npc.velocity.X *= 0.82f;
			if (Math.Abs(npc.velocity.X) < 0.1f) {
				npc.velocity.X = 0f;
			}
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
			if (!TryFindClearSpot(npc, player, out Vector2 spot)) {
				return;
			}

			npc.position = spot;
			npc.velocity = Vector2.Zero;
			npc.netUpdate = true;
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			follow.TeleportSettle = TeleportSettleTicks;
			follow.JumpCooldown = TeleportSettleTicks;
			NpcFollowNetSystem.BroadcastTeleportFx(npc.whoAmI, oldPos, npc.position, npc.width, npc.height);
		}

		private static bool TryFindClearSpot(NPC npc, Player player, out Vector2 spot) {
			float originX = player.Center.X - npc.width * 0.5f;
			float feetY = player.Bottom.Y - npc.height;
			Vector2 fallback = default;
			bool haveFallback = false;
			for (int dy = 0; dy <= 12; dy++) {
				for (int dx = 0; dx <= 4; dx++) {
					int signed = dx == 0 ? 0 : (dx % 2 == 0 ? dx / 2 : -(dx / 2 + 1));
					Vector2 candidate = new Vector2(originX + signed * TileSize, feetY - dy * TileSize);
					if (Collision.SolidCollision(candidate, npc.width, npc.height)) {
						continue;
					}

					if (!haveFallback) {
						fallback = candidate;
						haveFallback = true;
					}

					if (IsOnGroundOrPlatform(candidate, npc.width, npc.height)
						|| OpenAirColumn(candidate, npc.width, npc.height, depth: 3)) {
						spot = candidate;
						return true;
					}
				}
			}

			if (haveFallback) {
				spot = fallback;
				return true;
			}

			spot = default;
			return false;
		}

		private static bool OpenAirColumn(Vector2 position, int width, int height, int depth) {
			int xStart = (int)(position.X / TileSize);
			int xEnd = (int)((position.X + width - 1f) / TileSize);
			int y0 = (int)((position.Y + height + 2f) / TileSize);
			for (int i = 0; i < depth; i++) {
				int y = y0 + i;
				for (int x = xStart; x <= xEnd; x++) {
					if (!WorldGen.InWorld(x, y)) {
						return false;
					}

					Tile tile = Framing.GetTileSafely(x, y);
					if (tile.HasUnactuatedTile
						&& Main.tileSolid[tile.TileType]
						&& !Main.tileSolidTop[tile.TileType]) {
						return false;
					}
				}
			}

			return true;
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
