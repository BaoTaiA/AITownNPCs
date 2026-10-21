using System;
using System.IO;
using AITownNPCs.Common.Ai.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>
	/// Follow state only. Movement runs in <see cref="Ai.Follow.NpcFollowAI"/>.
	/// </summary>
	public class FollowingTownNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public int FollowPlayerWhoAmI = -1;

		public int StuckTimer;
		public int FarTimer;
		public int WingFrame;
		public int WingFrameCounter;
		public int FlyGraceTimer;
		public int JumpCooldown;
		public int WingLandTimer;
		public bool UseFlyingFollow;
		/// <summary>True while the NPC is inside the ground-follow comfort gap.</summary>
		public bool InCloseFollow;
		/// <summary>Last travel direction used for the behind slot. +1 right, -1 left. 0 until set.</summary>
		public int BehindSign;

		public bool IsFollowing =>
			FollowPlayerWhoAmI >= 0
			&& FollowPlayerWhoAmI < Main.maxPlayers
			&& Main.player[FollowPlayerWhoAmI].active;

		public void ResetTimers() {
			StuckTimer = 0;
			FarTimer = 0;
			InCloseFollow = false;
			BehindSign = 0;
		}

		public void ResetFlightState(NPC npc = null) {
			FlyGraceTimer = 0;
			JumpCooldown = 0;
			WingLandTimer = 0;
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

			if (!NpcWingFlight.HasWings(npc)) {
				if (UseFlyingFollow) {
					UseFlyingFollow = false;
					WingLandTimer = 0;
					npc.noGravity = false;
				}

				return;
			}

			if (playerFlying) {
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

			// Player is already walking. Do not wait for the NPC to reach their feet.
			bool playerGrounded = FollowMovement.IsOnGroundOrPlatform(player);
			if (playerGrounded && FlyGraceTimer <= 0) {
				UseFlyingFollow = false;
				JumpCooldown = FollowMovement.JumpCooldownTicks;
				WingLandTimer = FollowMovement.WingLandTicks;
				npc.noGravity = false;
			}
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bitWriter.WriteBit(IsFollowing);
			if (IsFollowing) {
				binaryWriter.Write((byte)FollowPlayerWhoAmI);
				bitWriter.WriteBit(UseFlyingFollow);
				binaryWriter.Write((byte)(WingFrame & 0xFF));
				binaryWriter.Write((byte)(WingFrameCounter & 0xFF));
				binaryWriter.Write((byte)Math.Clamp(WingLandTimer, 0, 255));
			}
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			int previousFollow = FollowPlayerWhoAmI;
			if (!bitReader.ReadBit()) {
				ClearReverseFollowLink(npc, previousFollow);
				FollowPlayerWhoAmI = -1;
				UseFlyingFollow = false;
				WingLandTimer = 0;
				ResetWings();
				ApplyFollowNetFlags(npc, following: false);
				return;
			}

			FollowPlayerWhoAmI = binaryReader.ReadByte();
			UseFlyingFollow = bitReader.ReadBit();
			binaryReader.ReadByte();
			binaryReader.ReadByte();
			WingLandTimer = binaryReader.ReadByte();
			npc.noGravity = UseFlyingFollow;
			ApplyFollowNetFlags(npc, following: true);
			MaintainReverseFollowLink(npc, previousFollow, FollowPlayerWhoAmI);
		}

		private static void ClearReverseFollowLink(NPC npc, int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			NpcFollowPlayer mp = Main.player[playerWhoAmI].GetModPlayer<NpcFollowPlayer>();
			if (mp.FollowingNpcWhoAmI == npc.whoAmI) {
				mp.FollowingNpcWhoAmI = -1;
			}
		}

		private static void MaintainReverseFollowLink(NPC npc, int previousPlayer, int newPlayer) {
			if (previousPlayer != newPlayer) {
				ClearReverseFollowLink(npc, previousPlayer);
			}

			if (newPlayer < 0 || newPlayer >= Main.maxPlayers) {
				return;
			}

			Main.player[newPlayer].GetModPlayer<NpcFollowPlayer>().FollowingNpcWhoAmI = npc.whoAmI;
		}

		/// <summary>
		/// Force high-rate NPC sync while following. netUpdate alone is dropped by netSpam.
		/// </summary>
		public static void ApplyFollowNetFlags(NPC npc, bool following) {
			if (npc == null) {
				return;
			}

			npc.netAlways = following;
			if (following) {
				npc.netUpdate = true;
			}
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
			if (combat.Mode == NpcCombatMode.Normal) {
				if (!combat.CombatWantDrop || !combat.HasEngagedTarget) {
					return false;
				}

				if (combat.CachedTargetWhoAmI < 0 || combat.CachedTargetWhoAmI >= Main.maxNPCs) {
					return false;
				}

				NPC target = Main.npc[combat.CachedTargetWhoAmI];
				if (!target.active) {
					return false;
				}

				return target.Bottom.Y > npc.Bottom.Y + 24f;
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

		public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
			if (IsFollowing) {
				NpcWingFlight.Draw(npc, spriteBatch, screenPos, drawColor, WingFrame);
			}

			return true;
		}

		public void ClearFollowLocal(NPC npc = null) {
			FollowPlayerWhoAmI = -1;
			ResetTimers();
			ResetFlightState(npc);
			ApplyFollowNetFlags(npc, following: false);
		}
	}
}
