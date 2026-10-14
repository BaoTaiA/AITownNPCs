using System.IO;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Roles.Nurse
{
	public class NurseHealProjectile : ModProjectile
	{
		public override string Texture => "Terraria/Images/Item_28";

		public int PotionItemType {
			get => (int)Projectile.localAI[0];
			set => Projectile.localAI[0] = value;
		}

		public bool TargetIsPlayer {
			get => Projectile.localAI[1] > 0.5f;
			set => Projectile.localAI[1] = value ? 1f : 0f;
		}

		public int TargetIndex {
			get => (int)Projectile.ai[2];
			set => Projectile.ai[2] = value;
		}

		public override void SetDefaults() {
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 180;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
		}

		public override void SendExtraAI(BinaryWriter writer) {
			writer.Write(PotionItemType);
			writer.Write(TargetIsPlayer);
			writer.Write(TargetIndex);
		}

		public override void ReceiveExtraAI(BinaryReader reader) {
			PotionItemType = reader.ReadInt32();
			TargetIsPlayer = reader.ReadBoolean();
			TargetIndex = reader.ReadInt32();
		}

		public override bool PreDraw(ref Color lightColor) {
			int itemType = PotionItemType;
			if (itemType <= 0) {
				itemType = ItemID.HealingPotion;
			}

			Main.instance.LoadItem(itemType);
			Texture2D texture = TextureAssets.Item[itemType].Value;
			Main.EntitySpriteDraw(
				texture,
				Projectile.Center - Main.screenPosition,
				null,
				Projectile.GetAlpha(lightColor),
				Projectile.rotation,
				texture.Size() * 0.5f,
				1f,
				SpriteEffects.None);
			return false;
		}

		public override void AI() {
			Projectile.rotation += 0.3f * Projectile.direction;
			HomeOnTarget();

			if (Main.rand.NextBool(4)) {
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.HealingPlus, 0f, 0f, 150, default, 0.8f);
			}

			TryHealNearby();
		}

		private void HomeOnTarget() {
			Vector2? dest = GetTrackedCenter();
			if (!dest.HasValue) {
				return;
			}

			Vector2 to = dest.Value - Projectile.Center;
			float dist = to.Length();
			if (dist < 1f) {
				return;
			}

			const float speed = 14f;
			Projectile.velocity = to / dist * speed;
		}

		private Vector2? GetTrackedCenter() {
			if (TargetIndex < 0) {
				return null;
			}

			if (TargetIsPlayer) {
				if (TargetIndex >= Main.maxPlayers) {
					return null;
				}

				Player player = Main.player[TargetIndex];
				return player.active && !player.dead ? player.Center : null;
			}

			if (TargetIndex >= Main.maxNPCs) {
				return null;
			}

			NPC npc = Main.npc[TargetIndex];
			return npc.active ? npc.Center : null;
		}

		public override bool OnTileCollide(Vector2 oldVelocity) {
			ApplySplashHeal();
			Projectile.Kill();
			return false;
		}

		public override void OnKill(int timeLeft) {
			SoundEngine.PlaySound(SoundID.Item107, Projectile.Center);
			for (int i = 0; i < 8; i++) {
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.HealingPlus);
			}
		}

		private void TryHealNearby() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			const float hitRadius = 36f;
			int healLife = (int)Projectile.ai[0];
			int healMana = (int)Projectile.ai[1];

			Vector2? tracked = GetTrackedCenter();
			if (tracked.HasValue && Projectile.Distance(tracked.Value) <= hitRadius) {
				if (TargetIsPlayer && TargetIndex >= 0 && TargetIndex < Main.maxPlayers) {
					Player player = Main.player[TargetIndex];
					if (player.active && !player.dead && ApplyHealToPlayer(player, healLife, healMana)) {
						Projectile.Kill();
						return;
					}
				}
				else if (!TargetIsPlayer && TargetIndex >= 0 && TargetIndex < Main.maxNPCs && healLife > 0) {
					NPC npc = Main.npc[TargetIndex];
					if (npc.active && npc.townNPC && npc.life < npc.lifeMax) {
						HealNpc(npc, healLife);
						Projectile.Kill();
						return;
					}
				}
			}

			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (!player.active || player.dead || Projectile.Distance(player.Center) > hitRadius) {
					continue;
				}

				if (ApplyHealToPlayer(player, healLife, healMana)) {
					Projectile.Kill();
					return;
				}
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || !npc.townNPC || npc.life >= npc.lifeMax) {
					continue;
				}

				if (healLife > 0 && Projectile.Distance(npc.Center) <= hitRadius) {
					HealNpc(npc, healLife);
					Projectile.Kill();
					return;
				}
			}
		}

		private void ApplySplashHeal() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			int healLife = (int)Projectile.ai[0];
			int healMana = (int)Projectile.ai[1];
			const float splash = 48f;

			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player.active && !player.dead && Projectile.Distance(player.Center) <= splash) {
					ApplyHealToPlayer(player, healLife, healMana);
				}
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.townNPC && npc.life < npc.lifeMax && healLife > 0
				    && Projectile.Distance(npc.Center) <= splash) {
					HealNpc(npc, healLife);
				}
			}
		}

		private static bool ApplyHealToPlayer(Player player, int healLife, int healMana) {
			bool healed = false;
			// Always push potion amounts; client clamps. Do not gate on server life/mana (!SSC).
			if (healLife > 0) {
				ServerPlayerNet.HealLifeSynced(player, healLife);
				healed = true;
			}

			if (healMana > 0) {
				ServerPlayerNet.HealManaSynced(player, healMana);
				healed = true;
			}

			return healed;
		}

		private static void HealNpc(NPC npc, int healLife) {
			int amount = System.Math.Min(healLife, npc.lifeMax - npc.life);
			if (amount <= 0) {
				return;
			}

			npc.life += amount;
			if (npc.life > npc.lifeMax) {
				npc.life = npc.lifeMax;
			}

			npc.netUpdate = true;
		}
	}
}
