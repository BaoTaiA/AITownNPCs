using System;
using System.IO;
using AITownNPCs.Common.Ai.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
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
		/// <summary>Hysteresis: once in close-follow deadzone, stay until farther out.</summary>
		public bool InCloseFollow;

		public bool IsFollowing =>
			FollowPlayerWhoAmI >= 0
			&& FollowPlayerWhoAmI < Main.maxPlayers
			&& Main.player[FollowPlayerWhoAmI].active;

		public void ResetTimers() {
			StuckTimer = 0;
			FarTimer = 0;
			InCloseFollow = false;
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

			bool npcGrounded = FollowMovement.IsOnGroundOrPlatform(npc.position, npc.width, npc.height);
			bool nearPlayerStand = FollowMovement.IsOnGroundOrPlatform(player)
				&& Math.Abs(npc.Bottom.Y - player.Bottom.Y) < 20f
				&& Math.Abs(npc.Center.X - player.Center.X) < 96f;
			if ((npcGrounded || nearPlayerStand) && FlyGraceTimer <= 0) {
				UseFlyingFollow = false;
				JumpCooldown = FollowMovement.JumpCooldownTicks;
				WingLandTimer = FollowMovement.WingLandTicks;
				npc.noGravity = false;
			}
		}

		public void TickWingsSelf() {
			if (!UseFlyingFollow) {
				ResetWings();
				return;
			}

			WingFrameCounter++;
			if (WingFrameCounter >= 4) {
				WingFrameCounter = 0;
				WingFrame++;
				if (WingFrame >= 4) {
					WingFrame = 0;
				}
			}
		}

		public void TickWings(Player player) {
			if (FollowMovement.IsPlayerUsingWingFlight(player)) {
				WingFrame = player.wingFrame;
				WingFrameCounter = player.wingFrameCounter;
				return;
			}

			if (UseFlyingFollow && FollowMovement.IsPlayerOnFlyingMount(player)) {
				WingFrameCounter++;
				if (WingFrameCounter >= 4) {
					WingFrameCounter = 0;
					WingFrame++;
					if (WingFrame >= 4) {
						WingFrame = 0;
					}
				}

				return;
			}

			if (UseFlyingFollow || WingLandTimer > 0) {
				WingFrame = 2;
				if (!UseFlyingFollow && WingLandTimer > 0) {
					WingLandTimer--;
					if (WingLandTimer == 0) {
						ResetWings();
					}
				}

				return;
			}

			ResetWings();
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
			WingFrame = binaryReader.ReadByte();
			WingFrameCounter = binaryReader.ReadByte();
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
			DrawButterflyWings(npc, spriteBatch, screenPos, drawColor);
			return true;
		}

		private void DrawButterflyWings(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
			if (!IsFollowing) {
				return;
			}

			Player player = Main.player[FollowPlayerWhoAmI];
			if (!player.active || player.dead || (!UseFlyingFollow && WingLandTimer <= 0)) {
				return;
			}

			if (!ContentSamples.ItemsByType.TryGetValue(ItemID.ButterflyWings, out Item wingItem)) {
				return;
			}

			int slot = wingItem.wingSlot;
			if (slot <= 0) {
				return;
			}

			Main.instance.LoadWings(slot);
			if (TextureAssets.Wings[slot] == null || !TextureAssets.Wings[slot].IsLoaded) {
				return;
			}

			Texture2D texture = TextureAssets.Wings[slot].Value;
			const int frames = 4;
			int frameHeight = texture.Height / frames;
			int frame = WingFrame % frames;
			Rectangle source = new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);
			Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
			SpriteEffects effects = npc.spriteDirection == 1
				? SpriteEffects.FlipHorizontally
				: SpriteEffects.None;
			Vector2 drawPos = npc.Center - screenPos + new Vector2(-9f * npc.spriteDirection, 2f + npc.gfxOffY);
			spriteBatch.Draw(texture, drawPos, source, npc.GetAlpha(drawColor), 0f, origin, 1f, effects, 0f);
		}

		public void ClearFollowLocal(NPC npc = null) {
			FollowPlayerWhoAmI = -1;
			ResetTimers();
			ResetFlightState(npc);
			ApplyFollowNetFlags(npc, following: false);
		}
	}
}
