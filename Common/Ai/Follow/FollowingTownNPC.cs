using System;
using System.IO;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Host;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>
	/// Follow state only. Movement runs in <see cref="Ai.Follow.NpcFollowAI"/>.
	/// Ownership syncs via ModPacket only — ExtraAI must never clear FollowPlayerWhoAmI.
	/// Scoped to managed town NPCs only (never enemies).
	/// </summary>
	public class FollowingTownNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
			NpcHostRules.AppliesToManagedTownNpc(entity);

		public int FollowPlayerWhoAmI = -1;

		public int StuckTimer;
		public int FarTimer;
		public int WingFrame;
		public int WingFrameCounter;
		public int FlyGraceTimer;
		public int JumpCooldown;
		public int WingLandTimer;
		public int TeleportSettle;
		public bool UseFlyingFollow;
		/// <summary>Last travel direction used for the behind slot. +1 right, -1 left. 0 until set.</summary>
		public int BehindSign;

		/// <summary>Server: ticks the follow owner has been inactive while FollowPlayerWhoAmI is still set.</summary>
		public int OwnerInactiveGrace;

		public bool IsFollowing =>
			FollowPlayerWhoAmI >= 0
			&& FollowPlayerWhoAmI < Main.maxPlayers
			&& Main.player[FollowPlayerWhoAmI].active;

		/// <summary>True when this NPC has a follow owner index (even if owner is briefly inactive).</summary>
		public bool HasFollowOwner =>
			FollowPlayerWhoAmI >= 0 && FollowPlayerWhoAmI < Main.maxPlayers;

		public void ResetTimers() {
			StuckTimer = 0;
			FarTimer = 0;
			TeleportSettle = 0;
			BehindSign = 0;
		}

		public void ResetFlightState(NPC npc = null) {
			FlyGraceTimer = 0;
			JumpCooldown = 0;
			WingLandTimer = 0;
			TeleportSettle = 0;
			UseFlyingFollow = false;
			ResetWings();
			if (npc != null) {
				npc.noGravity = false;
				npc.rotation = 0f;
			}
		}

		public void ResetWings() {
			WingFrame = 0;
			WingFrameCounter = 0;
		}

		public void UpdateFlightState(NPC npc, Player player, bool playerFlying) {
			if (JumpCooldown > 0) {
				JumpCooldown--;
			}

			if (TeleportSettle > 0) {
				TeleportSettle--;
			}

			if (!NpcWingFlight.HasWings(npc)) {
				if (UseFlyingFollow) {
					UseFlyingFollow = false;
					WingLandTimer = 0;
					npc.noGravity = false;
				}

				return;
			}

			if (playerFlying || FollowMovement.NeedsWingCatchUp(npc, player, this)) {
				UseFlyingFollow = true;
				FlyGraceTimer = FollowMovement.FlyGraceTicks;
				WingLandTimer = 0;
				return;
			}

			if (!UseFlyingFollow) {
				return;
			}

			if (FlyGraceTimer > 0) {
				FlyGraceTimer--;
			}

			// Catch-up already returned. Land once the player is walking and this NPC is close enough.
			bool playerGrounded = FollowMovement.IsOnGroundOrPlatform(player);
			if (playerGrounded && FlyGraceTimer <= 0) {
				UseFlyingFollow = false;
				JumpCooldown = FollowMovement.JumpCooldownTicks;
				WingLandTimer = FollowMovement.WingLandTicks;
				npc.noGravity = false;
			}
		}

		/// <summary>
		/// Flight visuals only. Ownership is ModPacket SyncFollow / SyncAll — never clear FollowPlayerWhoAmI here.
		/// </summary>
		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bitWriter.WriteBit(UseFlyingFollow);
			binaryWriter.Write((byte)Math.Clamp(WingLandTimer, 0, 255));
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			UseFlyingFollow = bitReader.ReadBit();
			WingLandTimer = binaryReader.ReadByte();
			// Only mirror flight gravity while following. Never force gravity on idle town NPCs
			// (and this global no longer applies to enemies at all).
			if (HasFollowOwner) {
				npc.noGravity = UseFlyingFollow;
			}
		}

		public static void ClearReverseFollowLink(NPC npc, int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			NpcFollowPlayer mp = Main.player[playerWhoAmI].GetModPlayer<NpcFollowPlayer>();
			if (mp.FollowingNpcWhoAmI == npc.whoAmI) {
				mp.FollowingNpcWhoAmI = -1;
			}
		}

		public static void MaintainReverseFollowLink(NPC npc, int previousPlayer, int newPlayer) {
			if (previousPlayer != newPlayer) {
				ClearReverseFollowLink(npc, previousPlayer);
			}

			if (newPlayer < 0 || newPlayer >= Main.maxPlayers) {
				return;
			}

			Main.player[newPlayer].GetModPlayer<NpcFollowPlayer>().FollowingNpcWhoAmI = npc.whoAmI;
		}

		/// <summary>
		/// Follow ownership uses ModPacket. Position uses throttled <see cref="TickFollowPositionSync"/> —
		/// never netAlways (SyncNPC+ExtraAI flood + ExtraAI ownership clear caused silent unfollow on connect).
		/// </summary>
		public const int FollowSyncInterval = 4;

		/// <summary>Server: wait this many ticks of owner inactive before dropping follow.</summary>
		public const int OwnerInactiveGraceTicks = 60;

		public static void ApplyFollowNetFlags(NPC npc, bool following) {
			if (npc == null) {
				return;
			}

			// Never enable netAlways; following only documents call-site intent.
			npc.netAlways = false;
			if (!following) {
				return;
			}
		}

		/// <summary>Server: one immediate SyncNPC (start / teleport). Clears netSpam so it is not dropped.</summary>
		public static void RequestFollowPositionSync(NPC npc) {
			if (npc == null || Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			npc.netAlways = false;
			npc.netSpam = 0;
			npc.netUpdate = true;
		}

		/// <summary>Server: capped-rate position sync while following (staggered by npc index).</summary>
		public static void TickFollowPositionSync(NPC npc) {
			if (npc == null || Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if ((Main.GameUpdateCount + (uint)npc.whoAmI) % FollowSyncInterval != 0) {
				return;
			}

			RequestFollowPositionSync(npc);
		}

		public static void LogFollowClear(string reason, NPC npc, int ownerWhoAmI) {
			int npcWho = npc?.whoAmI ?? -1;
			ModContent.GetInstance<AITownNPCs>()?.Logger.Info(
				$"Follow cleared reason={reason} npc={npcWho} owner={ownerWhoAmI} netMode={Main.netMode}");
		}

		public override bool? CanFallThroughPlatforms(NPC npc) {
			if (!IsFollowing) {
				return null;
			}

			Player player = Main.player[FollowPlayerWhoAmI];
			if (!player.active || player.dead) {
				return null;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			if (combat.Mode == NpcCombatMode.Normal && combat.HasEngagedTarget) {
				int who = combat.CachedTargetWhoAmI;
				if (who < 0 || who >= Main.maxNPCs) {
					return false;
				}

				NPC target = Main.npc[who];
				return target.active && target.Bottom.Y > npc.Bottom.Y + 24f;
			}

			if (combat.Mode == NpcCombatMode.Boss) {
				int bossWho = combat.LockedBossWhoAmI;
				if (bossWho < 0 || bossWho >= Main.maxNPCs) {
					return false;
				}

				NPC boss = Main.npc[bossWho];
				return boss.active && boss.Bottom.Y > npc.Bottom.Y + 24f;
			}

			if (FollowMovement.IsPlayerInFlight(player)) {
				return true;
			}

			if (UseFlyingFollow) {
				if (player.Bottom.Y > npc.Bottom.Y + 32f) {
					return true;
				}

				return false;
			}

			if (player.Bottom.Y > npc.Bottom.Y + 24f) {
				return true;
			}

			return false;
		}

		/// <summary>
		/// Drop sit / sleep / talk / party state so FindFrame uses the walk cycle.
		/// Dryad ward writes ai[0] = 14 again from the circle projectile after NPC update.
		/// </summary>
		public void ClearVanillaPose(NPC npc) {
			if (npc == null) {
				return;
			}

			int aiCount = Math.Min(4, npc.ai.Length);
			for (int i = 0; i < aiCount; i++) {
				npc.ai[i] = 0f;
			}

			int localCount = Math.Min(4, npc.localAI.Length);
			for (int i = 0; i < localCount; i++) {
				npc.localAI[i] = 0f;
			}
		}

		public override void FindFrame(NPC npc, int frameHeight) {
			if (!IsFollowing || frameHeight <= 0) {
				return;
			}

			int extra = NPCID.Sets.ExtraFramesCount[npc.type];
			int attack = NPCID.Sets.AttackFrameCount[npc.type];
			int walk = Main.npcFrameCount[npc.type] - extra - attack;
			if (walk <= 1) {
				return;
			}

			int index = npc.frame.Y / frameHeight;
			if (index < walk) {
				return;
			}

			bool walking = Math.Abs(npc.velocity.X) > FollowMovement.FaceSpeed
				&& Math.Abs(npc.velocity.Y) < 0.4f
				&& FollowMovement.IsOnGroundOrPlatform(npc.position, npc.width, npc.height);
			if (!walking) {
				npc.frame.Y = 0;
				npc.frameCounter = 0;
				return;
			}

			npc.frameCounter += Math.Abs(npc.velocity.X);
			int step = (int)(npc.frameCounter / 8.0);
			npc.frame.Y = (step % walk) * frameHeight;
		}

		public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
			if (IsFollowing) {
				NpcWingFlight.Draw(npc, spriteBatch, screenPos, drawColor, WingFrame);
			}

			return true;
		}

		public void ClearFollowLocal(NPC npc = null, string reason = "local") {
			int owner = FollowPlayerWhoAmI;
			if (owner >= 0) {
				LogFollowClear(reason, npc, owner);
				if (npc != null) {
					ClearReverseFollowLink(npc, owner);
				}
			}

			FollowPlayerWhoAmI = -1;
			OwnerInactiveGrace = 0;
			ResetTimers();
			ResetFlightState(npc);
			ApplyFollowNetFlags(npc, following: false);
		}
	}
}
