using System;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Combat.Boss;
using AITownNPCs.Common.Ai.Combat.Normal;
using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Roles.Dryad;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Follow
{
	public static class FollowMovement
	{
		public const float TeleportDistance = 2000f;
		public const float MaxSpeedMph = 50f;
		public const float MphPerVelocity = 5f;
		public const float MaxSpeedX = MaxSpeedMph / MphPerVelocity;
		public const float CombatSpeedMph = 75f;
		public const float CombatSpeedX = CombatSpeedMph / MphPerVelocity;
		public const float HorizontalDeadzone = 32f;
		public const float MaxJumpTiles = 100f;
		public const float TileSize = 16f;
		public const int IceMirrorDustType = 197;
		public const int IceMirrorDustCount = 70;
		public const int FlyGraceTicks = 12;
		public const int JumpCooldownTicks = 45;
		public const int WingLandTicks = 12;
		public const float FaceSpeed = 0.35f;
		public const float SlotMin = 16f;
		public const float SlotMax = 32f;
		public const float FollowAccel = 0.32f;
		public const float CombatAccel = 0.7f;
		public const float PlatformGap = 32f;
		/// <summary>Legacy name kept for call sites; emergency retreat uses hitbox edge, not Center.</summary>
		public const float DodgeRange = 32f;
		/// <summary>Retreat only when this close to the target hitbox edge (not Center — bosses break Center checks).</summary>
		public const float CombatEdgeDodge = 12f;
		/// <summary>Boss ground self fall-through: fire target may be at most this far below the follow player.</summary>
		public const float CombatVerticalChaseTiles = 10f;
		public const float CombatVerticalChasePixels = CombatVerticalChaseTiles * TileSize;
		public const float DodgeAccel = 1.5f;
		public const float DodgeJumpTiles = 6f;
		public const int TeleportSettleTicks = 45;
		/// <summary>Soft recall wait after exceeding FollowLostTeleportDistance (~2s at 60 tick).</summary>
		public const int FollowLostTeleportTicks = 120;
		public const float WingCatchUpExtraTiles = 6f;
		public const float WingCatchUpHeightTiles = 4f;
		public const int WingCatchUpStuckTicks = 15;
		/// <summary>Skip door I/O only when clearly airborne; StepUp micro-vy still opens doors.</summary>
		public const float DoorMaxVerticalSpeed = 1.2f;
		public const int DoorProbeExtraTiles = 2;

		public static void Tick(NPC npc, Player player, FollowingTownNPC follow, ref int stuckTimer, ref int farTimer) {
			// Clients predict locomotion only; server owns teleports.
			// Boss fight: stay with the encounter (no soft recall while near the boss). Hard teleport
			// and soft rescue still fire when stranded far from both player and boss.
			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			bool bossFight = combat != null && combat.Mode == NpcCombatMode.Boss && combat.HasEngagedTarget;
			bool serverTeleport = Main.netMode != NetmodeID.MultiplayerClient;

			float distToPlayer = Vector2.Distance(npc.Center, player.Center);
			float lostDist = AinpcServerConfig.Instance.FollowLostTeleportDistancePixels;

			// Hard safety net — even during boss fights (ocean / out-of-world stranding).
			if (serverTeleport && distToPlayer > TeleportDistance) {
				TeleportNearPlayer(npc, player);
				farTimer = 0;
				stuckTimer = 0;
				return;
			}

			bool softRecall = distToPlayer > lostDist;
			if (bossFight && softRecall) {
				NPC locked = NpcBossCombat.GetLockedBoss(combat);
				float toBoss = NpcBossCombat.NearestEncounterDistance(npc.Center, locked);
				// Still in the fight: do not pull back to the player.
				if (toBoss <= lostDist) {
					softRecall = false;
				}
			}

			// Soft recall / boss rescue. Wings close the gap in the air — skip mirror teleport.
			if (softRecall) {
				if (follow.UseFlyingFollow && NpcWingFlight.HasWings(npc)) {
					farTimer = 0;
				}
				else {
					farTimer++;
					if (serverTeleport && farTimer > FollowLostTeleportTicks) {
						TeleportNearPlayer(npc, player);
						farTimer = 0;
						stuckTimer = 0;
						return;
					}
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

		/// <summary>
		/// Wings should chase the player instead of jump-spam and the soft teleport.
		/// While a combat target is engaged, use <see cref="NeedsCombatWingFlight"/> instead.
		/// </summary>
		public static bool NeedsWingCatchUp(NPC npc, Player player, FollowingTownNPC follow) {
			if (npc == null || player == null || follow == null || !player.active || player.dead) {
				return false;
			}

			if (!NpcWingFlight.HasWings(npc) || TryGetChaseTarget(npc, out _)) {
				return false;
			}

			float dx = Math.Abs(player.Center.X - npc.Center.X);
			if (dx > SlotMax + TileSize * WingCatchUpExtraTiles) {
				return true;
			}

			if (npc.Center.Y - player.Center.Y > TileSize * WingCatchUpHeightTiles) {
				return true;
			}

			if (follow.StuckTimer >= WingCatchUpStuckTicks && dx > SlotMax) {
				return true;
			}

			return Vector2.Distance(npc.Center, player.Center) > AinpcServerConfig.Instance.FollowLostTeleportDistancePixels;
		}

		/// <summary>
		/// Normal + Boss: take off to chase Stand / a high target instead of staying grounded.
		/// </summary>
		public static bool NeedsCombatWingFlight(NPC npc) {
			if (npc == null || !NpcWingFlight.HasWings(npc)) {
				return false;
			}

			if (!TryGetChaseTarget(npc, out NPC target)) {
				return false;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			Item stance = StanceWeapon(equip);
			Vector2 anchor = CombatStandAnchor(combat, stance, npc, target);

			// Target or stand clearly above.
			if (npc.Center.Y - target.Center.Y > TileSize * WingCatchUpHeightTiles) {
				return true;
			}

			if (npc.Center.Y - anchor.Y > TileSize * WingCatchUpHeightTiles) {
				return true;
			}

			// Ground path stuck against terrain while fighting.
			if (follow != null && follow.StuckTimer >= WingCatchUpStuckTicks) {
				return true;
			}

			return false;
		}

		static bool IsEngagedBossFight(NPC npc) {
			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			return combat != null && combat.Mode == NpcCombatMode.Boss && combat.HasEngagedTarget;
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
				ApplyFlyingCombat(npc, follow, foe, ref stuckTimer);
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
				ApplyGroundCombat(npc, follow, target, ref stuckTimer);
				return;
			}

			ApplyPlayerSlot(npc, player, follow, ref stuckTimer);
			FaceAlongTravel(npc, follow);
		}

		/// <summary>Emergency edge retreat, else soft StepToward Stand (boss + trash).</summary>
		static void ApplyGroundCombat(NPC npc, FollowingTownNPC follow, NPC target, ref int stuckTimer) {
			if (NeedsEmergencyRetreat(npc, target)) {
				stuckTimer = 0;
				RetreatFrom(npc, target, follow);
				return;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			if (combat.Targeting.Holding) {
				stuckTimer = 0;
				BrakeHorizontal(npc);
				FaceTowardX(npc, target.Center.X);
				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			Item stance = StanceWeapon(equip);
			Vector2 anchor = CombatStandAnchor(combat, stance, npc, target);
			bool forceChase = MustForceBossChase(combat, npc, target);
			bool canShoot = !forceChase
				&& NpcWeaponReach.CanUse(npc, stance, target)
				&& NpcAttackPath.Allows(stance, equip.Ammo, npc.Center, target.Center);

			StepToward(npc, follow, anchor.X, CombatSpeedX, ref stuckTimer, climb: true, fullSpeed: false, accel: CombatAccel);
			if (!canShoot) {
				TryPlatformStep(npc, follow, anchor);
				FaceTowardX(npc, anchor.X);
			}
			else {
				FaceTowardX(npc, target.Center.X);
			}
		}

		static void ApplyFlyingCombat(NPC npc, FollowingTownNPC follow, NPC foe, ref int stuckTimer) {
			if (NeedsEmergencyRetreat(npc, foe)) {
				float dir = npc.Center.X >= foe.Center.X ? 1f : -1f;
				if (Math.Abs(npc.Center.X - foe.Center.X) < 0.5f) {
					dir = npc.direction != 0 ? -npc.direction : -1f;
				}

				Vector2 retreat = foe.Center + new Vector2(dir * Math.Max(SlotMax, DodgeRange), 0f);
				NpcWingFlight.Apply(npc, retreat, ref stuckTimer);
				return;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			if (combat.Targeting.Holding) {
				NpcWingFlight.Apply(npc, npc.Center, ref stuckTimer);
				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			Item stance = StanceWeapon(equip);
			Vector2 anchor = CombatStandAnchor(combat, stance, npc, foe);
			NpcWingFlight.Apply(npc, anchor, ref stuckTimer);
		}

		/// <summary>True when overlapping / scraping the target hitbox — works for bosses and trash.</summary>
		static bool NeedsEmergencyRetreat(NPC npc, NPC foe) {
			if (npc == null || foe == null || !foe.active) {
				return false;
			}

			float edge = Vector2.Distance(npc.Center, foe.Hitbox.ClosestPointInRect(npc.Center));
			float limit = Math.Max(CombatEdgeDodge, npc.width * 0.35f);
			return edge <= limit;
		}

		/// <summary>Palico combat navigation: face Stand goal when |dx| &gt; 8.</summary>
		private static void FaceTowardX(NPC npc, float goalX) {
			float dx = goalX - npc.Center.X;
			if (Math.Abs(dx) <= 8f) {
				return;
			}

			npc.direction = dx > 0f ? 1 : -1;
			npc.spriteDirection = npc.direction;
		}

		private static void ApplyPlayerSlot(NPC npc, Player player, FollowingTownNPC follow, ref int stuckTimer) {
			int travel = follow.BehindSign == 0 ? 1 : follow.BehindSign;
			float gap = SlotMin + (npc.whoAmI % 4) * ((SlotMax - SlotMin) / 3f);
			bool playerBelow = player.Bottom.Y > npc.Bottom.Y + PlatformGap;
			bool playerAbove = player.Bottom.Y < npc.Bottom.Y - PlatformGap;
			// Dig-down / settle: share player column. Otherwise keep the behind-slot gap (not stacked on the player).
			float slotX = follow.TeleportSettle > 0 || playerBelow
				? player.Center.X
				: player.Center.X - travel * gap;
			float dx = slotX - npc.Center.X;
			if (Math.Abs(dx) <= TileSize) {
				stuckTimer = 0;
				EaseHorizontal(npc, playerBelow ? 0f : player.velocity.X);
				TryOpenDoor(npc);
			}
			else {
				// Climb stays on: jump only when a real wall blocks the path to slotX.
				StepToward(npc, follow, slotX, MaxSpeedX, ref stuckTimer, climb: true);
			}

			Vector2 goal = new Vector2(slotX, player.Center.Y);
			if (TryDropShaft(npc, player, follow)) {
				return;
			}

			if (playerAbove) {
				TryPlatformStep(npc, follow, goal);
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

			float drop = StandingOnPlatform(npc) ? 8f : 2f;
			Vector2 next = new Vector2(player.Center.X - npc.width * 0.5f, npc.position.Y + drop);
			if (Collision.SolidCollision(next, npc.width, npc.height)) {
				return false;
			}

			npc.position = next;
			npc.velocity.X = 0f;
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

		/// <summary>Dryad Swiftness (and similar) scale for chase caps — never multiply residual velocity.</summary>
		public static float MoveSpeedScale(NPC npc) {
			if (npc == null || !npc.townNPC) {
				return 1f;
			}

			float m = npc.GetGlobalNPC<TownNpcAuraBuffs>().MoveSpeedMult;
			return m > 0.01f ? m : 1f;
		}

		private static void EaseHorizontal(NPC npc, float desired, float accel = FollowAccel) {
			// Scale accel only when chasing a non-zero target; braking to 0 stays snappy.
			if (Math.Abs(desired) > 0.05f) {
				accel *= MoveSpeedScale(npc);
			}

			if (npc.velocity.X < desired) {
				npc.velocity.X = Math.Min(npc.velocity.X + accel, desired);
			}
			else if (npc.velocity.X > desired) {
				npc.velocity.X = Math.Max(npc.velocity.X - accel, desired);
			}
		}

		private static void RetreatFrom(NPC npc, NPC target, FollowingTownNPC follow) {
			float dir = npc.Center.X >= target.Center.X ? 1f : -1f;
			if (Math.Abs(npc.Center.X - target.Center.X) < 0.5f) {
				dir = npc.direction != 0 ? -npc.direction : -1f;
			}

			npc.direction = dir > 0f ? 1 : -1;
			npc.spriteDirection = npc.direction;
			float scale = MoveSpeedScale(npc);
			float desired = dir * CombatSpeedX * scale;
			float step = DodgeAccel * scale;
			if (npc.velocity.X < desired) {
				npc.velocity.X = Math.Min(npc.velocity.X + step, desired);
			}
			else if (npc.velocity.X > desired) {
				npc.velocity.X = Math.Max(npc.velocity.X - step, desired);
			}

			TryOpenDoor(npc);
			if (follow.JumpCooldown > 0 || npc.velocity.Y != 0f) {
				return;
			}

			if (!IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return;
			}

			if (!HitWallAhead(npc)) {
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

		private static void StepToward(
			NPC npc,
			FollowingTownNPC follow,
			float goalX,
			float speed,
			ref int stuckTimer,
			bool climb = true,
			bool fullSpeed = false,
			float accel = FollowAccel) {
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
			float cap = speed * MoveSpeedScale(npc);
			float scaled = fullSpeed ? cap : Math.Min(cap, Math.Max(1.8f, abs * 0.05f));
			EaseHorizontal(npc, dir * scaled, accel);

			if (!climb) {
				stuckTimer = 0;
				TryOpenDoor(npc);
				return;
			}

			StepUpIfGrounded(npc);
			TryOpenDoor(npc);
			if (!HitWallAhead(npc)) {
				stuckTimer = 0;
				return;
			}

			stuckTimer++;
			if (stuckTimer > 2 && follow.TeleportSettle <= 0 && !ShouldDeferWallJump(npc, follow)) {
				TryWallJump(npc, follow, TileSize * 3f);
				stuckTimer = 0;
			}
		}

		/// <summary>
		/// Let stuck time climb into wing catch-up instead of hopping the same wall.
		/// </summary>
		static bool ShouldDeferWallJump(NPC npc, FollowingTownNPC follow) {
			if (!follow.IsFollowing || !NpcWingFlight.HasWings(npc)) {
				return false;
			}

			if (IsEngagedBossFight(npc) || TryGetChaseTarget(npc, out _)) {
				return false;
			}

			Player player = Main.player[follow.FollowPlayerWhoAmI];
			if (!player.active || player.dead) {
				return false;
			}

			if (NeedsWingCatchUp(npc, player, follow)) {
				return true;
			}

			float dx = Math.Abs(player.Center.X - npc.Center.X);
			return dx > SlotMax && npc.velocity.Y == 0f && HitWallAhead(npc);
		}

		static void StepUpIfGrounded(NPC npc) {
			if (Math.Abs(npc.velocity.X) < 0.05f || npc.velocity.Y != 0f) {
				return;
			}

			if (!IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return;
			}

			Collision.StepUp(
				ref npc.position,
				ref npc.velocity,
				npc.width,
				npc.height,
				ref npc.stepSpeed,
				ref npc.gfxOffY);
		}

		/// <summary>Vanilla town walk: open a closed door ahead, close an open door just behind.</summary>
		static void TryOpenDoor(NPC npc) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (Math.Abs(npc.velocity.X) < FaceSpeed || Math.Abs(npc.velocity.Y) > DoorMaxVerticalSpeed) {
				return;
			}

			if (!IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return;
			}

			int dir = npc.velocity.X > 0f ? 1 : -1;
			float ahead = dir > 0 ? npc.Right.X + 4f : npc.Left.X - 4f;
			float behind = dir > 0 ? npc.Left.X - 8f : npc.Right.X + 8f;
			TryOpenDoorAt(npc, ahead, dir);
			TryCloseDoorAt(npc, behind, dir);
		}

		static void TryOpenDoorAt(NPC npc, float worldX, int dir) {
			if (!TryFindDoor(npc, worldX, open: false, dir, out int x, out int top, out bool gate)) {
				return;
			}

			bool opened = gate
				? WorldGen.ShiftTallGate(x, top, false)
				: WorldGen.OpenDoor(x, top, dir);
			if (!opened && !gate) {
				dir = -dir;
				opened = WorldGen.OpenDoor(x, top, dir);
			}

			if (!opened) {
				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, gate ? 4 : 0, x, top, dir);
			}
		}

		static void TryCloseDoorAt(NPC npc, float worldX, int travelDir) {
			if (!TryFindDoor(npc, worldX, open: true, -travelDir, out int x, out int top, out bool gate)) {
				return;
			}

			bool closed = gate
				? WorldGen.ShiftTallGate(x, top, true)
				: WorldGen.CloseDoor(x, top);
			if (!closed || Main.netMode != NetmodeID.Server) {
				return;
			}

			NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, gate ? 5 : 1, x, top, 1f);
		}

		/// <summary>
		/// Scan the probe column and up to <see cref="DoorProbeExtraTiles"/> further in <paramref name="scanDir"/>
		/// so a one-tile lip in front of the door still finds it. Vertical fit allows ~1 tile slack.
		/// </summary>
		static bool TryFindDoor(NPC npc, float worldX, bool open, int scanDir, out int x, out int top, out bool gate) {
			x = 0;
			top = 0;
			gate = false;
			int baseX = (int)(worldX / TileSize);
			int step = scanDir >= 0 ? 1 : -1;
			for (int col = 0; col <= DoorProbeExtraTiles; col++) {
				int tileX = baseX + step * col;
				if (TryFindDoorInColumn(npc, tileX, open, out x, out top, out gate)) {
					return true;
				}
			}

			return false;
		}

		static bool TryFindDoorInColumn(NPC npc, int tileX, bool open, out int x, out int top, out bool gate) {
			x = tileX;
			top = 0;
			gate = false;
			int yTop = (int)(npc.Top.Y / TileSize) - 1;
			int yBot = (int)((npc.Bottom.Y - 1f) / TileSize) + 1;
			for (int y = yTop; y <= yBot; y++) {
				if (!WorldGen.InWorld(tileX, y, 2)) {
					continue;
				}

				Tile tile = Main.tile[tileX, y];
				if (!tile.HasUnactuatedTile) {
					continue;
				}

				bool tileGate = open
					? tile.TileType == TileID.TallGateOpen
					: tile.TileType == TileID.TallGateClosed;
				bool tileDoor = tileGate || (open
					? TileLoader.CloseDoorID(tile) >= 0
					: TileLoader.IsClosedDoor(tile));
				if (!tileDoor) {
					continue;
				}

				int frameSpan = tileGate ? 90 : 54;
				int doorTop = y - tile.TileFrameY % frameSpan / 18;
				int doorTiles = tileGate ? 5 : 3;
				float doorTopPx = doorTop * TileSize;
				float doorBotPx = (doorTop + doorTiles) * TileSize + TileSize;
				// One-tile slack for half-blocks / small steps beside the frame.
				if (npc.Top.Y < doorTopPx - TileSize || npc.Bottom.Y > doorBotPx + TileSize) {
					continue;
				}

				gate = tileGate;
				top = doorTop;
				return true;
			}

			return false;
		}

		private static void TryWallJump(NPC npc, FollowingTownNPC follow, float minHeight) {
			if (follow.TeleportSettle > 0 || follow.JumpCooldown > 0 || npc.velocity.Y != 0f) {
				return;
			}

			if (!IsOnGroundOrPlatform(npc.position, npc.width, npc.height)) {
				return;
			}

			// Follow uses correct slotX (player column when near) + TryDropShaft; combat clears walls here.
			float wall = MeasureWallHeightPixels(npc);
			float height = Math.Max(minHeight, wall + TileSize);
			ApplyJump(npc, height);
			follow.JumpCooldown = JumpCooldownTicks;
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
			if (state == null || !state.HasEngagedTarget) {
				return false;
			}

			if (state.Mode == NpcCombatMode.Boss) {
				EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
				Item stance = StanceWeapon(equip);
				target = NpcBossCombat.ResolveFireTarget(npc, NpcBossCombat.GetLockedBoss(state), stance);
				return target != null;
			}

			if (state.Mode != NpcCombatMode.Normal) {
				return false;
			}

			target = NpcNormalCombat.GetCachedIfValid(npc, state);
			return target != null;
		}

		static bool MustForceBossChase(NpcCombatState combat, NPC npc, NPC foe) =>
			combat != null
			&& combat.Mode == NpcCombatMode.Boss
			&& foe != null
			&& npc.Distance(foe.Center) > NpcBossCombat.ChaseForceDist;

		static Item StanceWeapon(EquippedTownNPC equip) {
			if (equip == null) {
				return null;
			}

			if (!equip.Weapon.IsAir && NpcEquipHelper.IsAllowedWeapon(equip.Weapon)) {
				return equip.Weapon;
			}

			if (NpcEquipHelper.IsSummonOrSentryStaff(equip.SummonStaff)) {
				return equip.SummonStaff;
			}

			return equip.Weapon;
		}

		static Vector2 CombatStandAnchor(NpcCombatState combat, Item weapon, NPC npc, NPC foe) =>
			NpcWeaponReach.Stand(weapon, npc, foe, holdInRange: !MustForceBossChase(combat, npc, foe));

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
			float scale = MoveSpeedScale(npc);
			cap *= scale;
			accel *= scale;

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

			StepUpIfGrounded(npc);
			if (HitWallAhead(npc)) {
				stuckTimer++;
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
				if (!WorldGen.InWorld(tileX, y) || !IsFullSolid(Framing.GetTileSafely(tileX, y))) {
					break;
				}

				tiles++;
			}

			return tiles * TileSize;
		}

		/// <summary>A real wall is two full solid tiles. Slopes, half-blocks, and one-tile steps are walked.</summary>
		public static bool HitWallAhead(NPC npc) {
			int tileX = (int)((npc.Center.X + npc.direction * (npc.width / 2f + 4f)) / 16f);
			int tileY = (int)((npc.Bottom.Y - 8f) / 16f);
			if (!WorldGen.InWorld(tileX, tileY) || !WorldGen.InWorld(tileX, tileY - 1)) {
				return false;
			}

			return IsFullSolid(Framing.GetTileSafely(tileX, tileY))
				&& IsFullSolid(Framing.GetTileSafely(tileX, tileY - 1));
		}

		static bool IsFullSolid(Tile tile) {
			return tile.HasUnactuatedTile
				&& Main.tileSolid[tile.TileType]
				&& !Main.tileSolidTop[tile.TileType]
				&& !tile.IsHalfBlock
				&& tile.Slope == SlopeType.Solid;
		}

		public static void TeleportNearPlayer(NPC npc, Player player) {
			Vector2 oldPos = npc.position;
			if (!TryFindClearSpot(npc, player, out Vector2 spot)) {
				return;
			}

			npc.position = spot;
			npc.velocity = Vector2.Zero;
			FollowingTownNPC.RequestFollowPositionSync(npc);
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
