using AITownNPCs.Common.Ai.Combat.Normal;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat.Boss
{
	public static class NpcBossCombat
	{
		/// <summary>Drop the lock when the followed player is farther than this from the boss.</summary>
		public const float PlayerKeepRadius = 4000f;

		/// <summary>Parts without a reliable realLife link to the boss root (vanilla).</summary>
		private static bool IsKnownBossPartType(int type) =>
			type == NPCID.GolemHead
			|| type == NPCID.GolemHeadFree
			|| type == NPCID.GolemFistLeft
			|| type == NPCID.GolemFistRight
			|| type == NPCID.MoonLordHead
			|| type == NPCID.MoonLordHand
			|| type == NPCID.SkeletronHand
			|| type == NPCID.PrimeCannon
			|| type == NPCID.PrimeSaw
			|| type == NPCID.PrimeVice
			|| type == NPCID.PrimeLaser
			|| type == NPCID.WallofFleshEye;

		/// <summary>
		/// Boss root or a part of a boss encounter — Normal mode must not treat these as trash mobs.
		/// Independent adds (creepers, probes) are not included.
		/// </summary>
		public static bool IsBossEncounterNpc(NPC other) {
			if (other == null || !other.active || other.friendly) {
				return false;
			}

			if (other.boss) {
				return true;
			}

			if (other.realLife >= 0 && other.realLife < Main.maxNPCs) {
				NPC root = Main.npc[other.realLife];
				if (root.active && root.boss && !root.friendly) {
					return true;
				}
			}

			return IsKnownBossPartType(other.type);
		}

		public static NPC FindNearestBoss(Player player, NPC attacker = null, float maxPlayerDistance = float.MaxValue) {
			NPC bestHurtable = null;
			float bestHurtableDist = float.MaxValue;
			NPC bestAny = null;
			float bestAnyDist = float.MaxValue;

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || !other.boss || other.friendly) {
					continue;
				}

				float dist = player.Distance(other.Center);
				if (dist > maxPlayerDistance) {
					continue;
				}

				if (dist < bestAnyDist) {
					bestAnyDist = dist;
					bestAny = other;
				}

				bool chaseable = attacker != null ? other.CanBeChasedBy(attacker) : other.CanBeChasedBy();
				if (chaseable && dist < bestHurtableDist) {
					bestHurtableDist = dist;
					bestHurtable = other;
				}
			}

			// Prefer a damageable boss (Twins, Storm Weaver tail). Fall back to an invulnerable root for positioning (Golem, Moon Lord, Slime God core).
			return bestHurtable ?? bestAny;
		}

		public static NPC GetLockedBoss(NpcCombatState combat) {
			int who = combat.LockedBossWhoAmI;
			if (who < 0 || who >= Main.maxNPCs) {
				return null;
			}

			NPC boss = Main.npc[who];
			if (!boss.active || !boss.boss || boss.friendly) {
				return null;
			}

			return boss;
		}

		/// <summary>Damageable aim point for the locked boss (root or a part).</summary>
		public static NPC ResolveFireTarget(NPC attacker, NPC locked) {
			if (locked == null || !locked.active) {
				return null;
			}

			if (locked.CanBeChasedBy(attacker)) {
				return locked;
			}

			NPC best = null;
			float bestDist = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || other.friendly || !IsPartOfLockedBoss(other, locked)) {
					continue;
				}

				if (!other.CanBeChasedBy(attacker)) {
					continue;
				}

				float dist = locked.Distance(other.Center);
				if (dist >= bestDist) {
					continue;
				}

				bestDist = dist;
				best = other;
			}

			if (best != null) {
				return best;
			}

			// Invulnerable root with no linked parts (Slime God core): shoot the nearest chaseable foe beside it.
			return FindNearestChaseableNear(attacker, locked.Center, NpcNormalCombat.LeashDist);
		}

		private static NPC FindNearestChaseableNear(NPC attacker, Vector2 origin, float radius) {
			NPC best = null;
			float bestDist = radius;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || other.friendly || other.townNPC || NPCID.Sets.CountsAsCritter[other.type]) {
					continue;
				}

				if (attacker != null) {
					if (!other.CanBeChasedBy(attacker)) {
						continue;
					}
				}
				else if (!other.CanBeChasedBy()) {
					continue;
				}

				float dist = Vector2.Distance(origin, other.Center);
				if (dist >= bestDist) {
					continue;
				}

				bestDist = dist;
				best = other;
			}

			return best;
		}

		private static bool IsPartOfLockedBoss(NPC part, NPC locked) {
			if (part.whoAmI == locked.whoAmI) {
				return true;
			}

			if (part.realLife == locked.whoAmI) {
				return true;
			}

			if (locked.realLife >= 0 && (part.whoAmI == locked.realLife || part.realLife == locked.realLife)) {
				return true;
			}

			// Known vanilla parts that may not set realLife (Golem fists/head, Moon Lord eyes, …).
			return IsKnownBossPartType(part.type) && part.Distance(locked.Center) <= PlayerKeepRadius;
		}

		public static void TickFire(NPC npc, EquippedTownNPC equip, NpcCombatState combat) {
			NPC locked = GetLockedBoss(combat);
			NPC target = ResolveFireTarget(npc, locked);
			NpcCombatFire.Tick(npc, equip, target, allowFire: target != null);
		}
	}
}
