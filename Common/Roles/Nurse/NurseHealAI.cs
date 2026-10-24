using System.Collections.Generic;
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
		public const int MaxHealTargets = 5;

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

			if (!TryCollectTargets(npc, equip, state)) {
				return;
			}

			state.ThrowTimer = ThrowAnim;
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

		private static bool TryCollectTargets(NPC npc, EquippedTownNPC equip, NurseRoleState state) {
			state.TossCount = 0;
			Item lifeItem = equip.GetSlot(NpcEquipSlotId.HealLife);
			Item manaItem = equip.GetSlot(NpcEquipSlotId.HealMana);
			int lifeLeft = !lifeItem.IsAir && lifeItem.healLife > 0 ? lifeItem.stack : 0;
			int manaLeft = !manaItem.IsAir && manaItem.healMana > 0 ? manaItem.stack : 0;
			if (lifeLeft <= 0 && manaLeft <= 0) {
				return false;
			}

			var candidates = new List<HealCandidate>();
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (!player.active || player.dead) {
					continue;
				}

				bool needsLife = player.statLife < player.statLifeMax2;
				bool needsMana = player.statMana < player.statManaMax2;
				if ((!needsLife || lifeLeft <= 0) && (!needsMana || manaLeft <= 0)) {
					continue;
				}

				float d = npc.Distance(player.Center);
				if (d >= HealRange || !CanHit(npc, player.position, player.width, player.height)) {
					continue;
				}

				candidates.Add(new HealCandidate(d, isPlayer: true, i));
			}

			if (lifeLeft > 0) {
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC other = Main.npc[i];
					if (!other.active || other.whoAmI == npc.whoAmI || !other.townNPC || other.life >= other.lifeMax) {
						continue;
					}

					float d = npc.Distance(other.Center);
					if (d >= HealRange || !CanHit(npc, other.position, other.width, other.height)) {
						continue;
					}

					candidates.Add(new HealCandidate(d, isPlayer: false, i));
				}
			}

			candidates.Sort((a, b) => a.Dist.CompareTo(b.Dist));
			for (int n = 0; n < candidates.Count && state.TossCount < MaxHealTargets; n++) {
				HealCandidate candidate = candidates[n];
				bool life;
				if (candidate.IsPlayer) {
					Player player = Main.player[candidate.Index];
					if (player.statLife < player.statLifeMax2 && lifeLeft > 0) {
						life = true;
					}
					else if (player.statMana < player.statManaMax2 && manaLeft > 0) {
						life = false;
					}
					else {
						continue;
					}
				}
				else if (lifeLeft > 0) {
					life = true;
				}
				else {
					continue;
				}

				int slot = state.TossCount;
				state.TossIsPlayer[slot] = candidate.IsPlayer;
				state.TossIndex[slot] = candidate.Index;
				state.TossUseLife[slot] = life;
				state.TossCount++;
				if (life) {
					lifeLeft--;
				}
				else {
					manaLeft--;
				}
			}

			if (state.TossCount <= 0) {
				return false;
			}

			state.TargetIsPlayer = state.TossIsPlayer[0];
			state.TargetIndex = state.TossIndex[0];
			state.PendingSlot = state.TossUseLife[0] ? NpcEquipSlotId.HealLife : NpcEquipSlotId.HealMana;
			Item shown = equip.GetSlot(state.PendingSlot);
			state.PendingPotionType = shown.IsAir ? 0 : shown.type;
			return state.PendingPotionType > 0;
		}

		private readonly struct HealCandidate
		{
			public readonly float Dist;
			public readonly bool IsPlayer;
			public readonly int Index;

			public HealCandidate(float dist, bool isPlayer, int index) {
				Dist = dist;
				IsPlayer = isPlayer;
				Index = index;
			}
		}

		private static bool CanHit(NPC npc, Vector2 position, int width, int height) {
			return Collision.CanHit(npc.position, npc.width, npc.height, position, width, height);
		}

		private static void TryThrow(NPC npc, EquippedTownNPC equip, NurseRoleState state) {
			bool threw = false;
			bool usedLife = false;
			bool usedMana = false;
			for (int i = 0; i < state.TossCount; i++) {
				NpcEquipSlotId slot = state.TossUseLife[i] ? NpcEquipSlotId.HealLife : NpcEquipSlotId.HealMana;
				Item potion = equip.GetSlot(slot);
				if (potion.IsAir) {
					continue;
				}

				if (state.TossUseLife[i] && potion.healLife <= 0) {
					continue;
				}

				if (!state.TossUseLife[i] && potion.healMana <= 0) {
					continue;
				}

				Vector2? aim = TargetCenter(state.TossIsPlayer[i], state.TossIndex[i]);
				if (!aim.HasValue) {
					continue;
				}

			if (SpawnToss(npc, aim.Value, potion, state.TossIsPlayer[i], state.TossIndex[i])) {
				if (!threw) {
					SoundEngine.PlaySound(SoundID.Item1, npc.Center);
				}

				threw = true;
					ConsumePotion(equip, slot);
					if (slot == NpcEquipSlotId.HealLife) {
						usedLife = true;
					}
					else {
						usedMana = true;
					}
				}
			}

			state.TossCount = 0;
			if (usedLife) {
				NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, NpcEquipSlotId.HealLife, equip.GetSlot(NpcEquipSlotId.HealLife));
			}

			if (usedMana) {
				NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, NpcEquipSlotId.HealMana, equip.GetSlot(NpcEquipSlotId.HealMana));
			}

			state.Cooldown = threw ? ThrowCooldown : 20;
			npc.netUpdate = true;
		}

		private static Vector2? TargetCenter(bool isPlayer, int index) {
			if (index < 0) {
				return null;
			}

			if (isPlayer) {
				if (index >= Main.maxPlayers) {
					return null;
				}

				Player player = Main.player[index];
				return player.active && !player.dead ? player.Center : null;
			}

			if (index >= Main.maxNPCs) {
				return null;
			}

			NPC other = Main.npc[index];
			return other.active ? other.Center : null;
		}

		private static bool SpawnToss(NPC npc, Vector2 aim, Item potion, bool targetIsPlayer, int targetIndex) {
			Vector2 start = npc.Center + new Vector2(npc.direction * 12f, -8f);
			Vector2 velocity = aim - start;
			float dist = velocity.Length();
			if (dist < 1f) {
				velocity = new Vector2(npc.direction, -0.5f);
				dist = 1f;
			}

			velocity = velocity / dist * 14f;
			int healLife = potion.healLife;
			int healMana = potion.healMana;
			int potionType = potion.type;
			int proj = NpcProjectileSpawn.SpawnServerOwned(
				npc.GetSource_FromAI(),
				start,
				velocity,
				ModContent.ProjectileType<NurseHealProjectile>(),
				0,
				0f,
				p => {
					p.ai[0] = healLife;
					p.ai[1] = healMana;
					p.localAI[0] = potionType;
					p.localAI[1] = targetIsPlayer ? 1f : 0f;
					p.ai[2] = targetIndex;
				});

			return proj >= 0;
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
		public int TossCount;
		public readonly bool[] TossIsPlayer = new bool[NurseHealAI.MaxHealTargets];
		public readonly int[] TossIndex = new int[NurseHealAI.MaxHealTargets];
		public readonly bool[] TossUseLife = new bool[NurseHealAI.MaxHealTargets];
	}
}
