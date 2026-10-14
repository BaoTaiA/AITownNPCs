using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Roles.Nurse
{
	public static class NurseHealAI
	{
		public const float HealRange = 480f;
		public const int ThrowCooldown = 90;
		public const int ThrowAnim = 24;

		public static void Update(NPC npc, NurseRoleState state) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				if (state.ThrowTimer > 0) {
					state.ThrowTimer--;
					FaceTarget(npc, state);
				}

				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			if (state.ThrowTimer > 0) {
				state.ThrowTimer--;
				FaceTarget(npc, state);
				if (state.ThrowTimer == ThrowAnim / 2) {
					TryThrow(npc, equip, state);
				}

				if (state.ThrowTimer <= 0) {
					state.PendingPotionType = 0;
					npc.netUpdate = true;
				}

				return;
			}

			if (state.Cooldown > 0) {
				state.Cooldown--;
				return;
			}

			if (!TryFindTarget(npc, equip, out Entity target, out Item potion, out NpcEquipSlotId slot)) {
				return;
			}

			state.TargetIsPlayer = target is Player;
			state.TargetIndex = target is Player p ? p.whoAmI : ((NPC)target).whoAmI;
			state.ThrowTimer = ThrowAnim;
			state.PendingSlot = slot;
			state.PendingPotionType = potion.type;
			FaceTarget(npc, state);
			npc.netUpdate = true;
		}

		public static bool IsBusy(NurseRoleState state) => state.ThrowTimer > 0;

		private static void FaceTarget(NPC npc, NurseRoleState state) {
			if (npc.GetGlobalNPC<FollowingTownNPC>().IsFollowing) {
				return;
			}

			Vector2? center = GetTargetCenter(state);
			if (!center.HasValue) {
				return;
			}

			npc.direction = center.Value.X >= npc.Center.X ? 1 : -1;
			npc.spriteDirection = npc.direction;
		}

		public static Vector2? GetTargetCenterPublic(NurseRoleState state) => GetTargetCenter(state);

		private static Vector2? GetTargetCenter(NurseRoleState state) {
			if (state.TargetIndex < 0) {
				return null;
			}

			if (state.TargetIsPlayer) {
				if (state.TargetIndex >= Main.maxPlayers) {
					return null;
				}

				Player player = Main.player[state.TargetIndex];
				return player.active ? player.Center : null;
			}

			if (state.TargetIndex >= Main.maxNPCs) {
				return null;
			}

			NPC other = Main.npc[state.TargetIndex];
			return other.active ? other.Center : null;
		}

		private static bool TryFindTarget(NPC npc, EquippedTownNPC equip, out Entity target, out Item potion, out NpcEquipSlotId slot) {
			target = null;
			potion = null;
			slot = NpcEquipSlotId.HealLife;
			float bestDist = HealRange;

			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (!player.active || player.dead) {
					continue;
				}

				float dist = npc.Distance(player.Center);
				if (dist >= bestDist
				    || !Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height)) {
					continue;
				}

				if (TryPickPotionForPlayer(equip, player, out Item pick, out NpcEquipSlotId pickSlot)) {
					bestDist = dist;
					target = player;
					potion = pick;
					slot = pickSlot;
				}
			}

			if (target != null) {
				return true;
			}

			bestDist = HealRange;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || other.whoAmI == npc.whoAmI || !other.townNPC || other.life >= other.lifeMax) {
					continue;
				}

				float dist = npc.Distance(other.Center);
				if (dist >= bestDist
				    || !Collision.CanHit(npc.position, npc.width, npc.height, other.position, other.width, other.height)) {
					continue;
				}

				if (TryPick(equip, item => item.healLife > 0, out Item pick, out NpcEquipSlotId pickSlot)) {
					bestDist = dist;
					target = other;
					potion = pick;
					slot = pickSlot;
				}
			}

			return target != null;
		}

		private static bool TryPickPotionForPlayer(EquippedTownNPC equip, Player player, out Item potion, out NpcEquipSlotId slot) {
			potion = null;
			slot = NpcEquipSlotId.HealLife;
			// Coarse server gate: better occasional miss under !SSC than infinite throws when full.
			if (player.statLife < player.statLifeMax2 && TryPick(equip, item => item.healLife > 0, out potion, out slot)) {
				return true;
			}

			if (player.statMana < player.statManaMax2 && TryPick(equip, item => item.healMana > 0, out potion, out slot)) {
				return true;
			}

			return false;
		}

		private static bool TryPick(EquippedTownNPC equip, System.Func<Item, bool> pred, out Item potion, out NpcEquipSlotId slot) {
			potion = null;
			slot = NpcEquipSlotId.HealLife;
			Item lifeSlot = equip.GetSlot(NpcEquipSlotId.HealLife);
			if (!lifeSlot.IsAir && pred(lifeSlot)) {
				potion = lifeSlot;
				slot = NpcEquipSlotId.HealLife;
				return true;
			}

			Item manaSlot = equip.GetSlot(NpcEquipSlotId.HealMana);
			if (!manaSlot.IsAir && pred(manaSlot)) {
				potion = manaSlot;
				slot = NpcEquipSlotId.HealMana;
				return true;
			}

			return false;
		}

		private static void TryThrow(NPC npc, EquippedTownNPC equip, NurseRoleState state) {
			Item potion = equip.GetSlot(state.PendingSlot);
			if (potion.IsAir || potion.type != state.PendingPotionType) {
				state.Cooldown = 20;
				return;
			}

			Vector2? aim = GetTargetCenter(state);
			if (!aim.HasValue) {
				state.Cooldown = 20;
				return;
			}

			Vector2 start = npc.Center + new Vector2(npc.direction * 12f, -8f);
			Vector2 velocity = aim.Value - start;
			float dist = velocity.Length();
			if (dist < 1f) {
				velocity = new Vector2(npc.direction, -0.5f);
				dist = 1f;
			}

			velocity = velocity / dist * 14f;

			IEntitySource source = npc.GetSource_FromAI();
			int pendingPotionType = potion.type;
			bool targetIsPlayer = state.TargetIsPlayer;
			int targetIndex = state.TargetIndex;
			int healLife = potion.healLife;
			int healMana = potion.healMana;

			int proj = NpcProjectileSpawn.SpawnServerOwned(
				source,
				start,
				velocity,
				ModContent.ProjectileType<NurseHealProjectile>(),
				0,
				0f,
				p => {
					p.ai[0] = healLife;
					p.ai[1] = healMana;
					p.localAI[0] = pendingPotionType;
					p.localAI[1] = targetIsPlayer ? 1f : 0f;
					p.ai[2] = targetIndex;
				});

			if (proj >= 0) {
				SoundEngine.PlaySound(SoundID.Item1, npc.Center);
			}

			ConsumePotion(equip, state.PendingSlot);
			NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, state.PendingSlot, equip.GetSlot(state.PendingSlot));
			state.Cooldown = ThrowCooldown;
			npc.netUpdate = true;
		}

		private static void ConsumePotion(EquippedTownNPC equip, NpcEquipSlotId slot) {
			Item item = equip.GetSlot(slot);
			if (item.IsAir) {
				return;
			}

			item.stack--;
			if (item.stack <= 0) {
				item.TurnToAir();
			}

			equip.SetSlot(slot, item, -1);
		}
	}

	public class NurseRoleState
	{
		public int ThrowTimer;
		public int Cooldown;
		public bool TargetIsPlayer;
		public int TargetIndex = -1;
		public NpcEquipSlotId PendingSlot;
		public int PendingPotionType;
	}
}
