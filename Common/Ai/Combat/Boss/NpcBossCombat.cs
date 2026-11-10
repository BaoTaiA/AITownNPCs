using AITownNPCs.Common.Ai.Combat.Normal;
using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat.Boss
{
	public static class NpcBossCombat
	{
		/// <summary>Boss-part link search around the locked root (not the de-aggro leash).</summary>
		public const float PartLinkRadius = 4000f;

		/// <summary>Drop Boss lock when the follow player is farther than this from the boss (150 tiles).</summary>
		public const float PlayerKeepRadius = 2400f;

		/// <summary>Beyond this from the boss, force Stand approach (no ranged hold-in-place). 40 tiles.</summary>
		public const float ChaseForceDist = 640f;

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

		/// <summary>Worm-style multi-segment boss (Destroyer, EoW, most mod worms).</summary>
		public static bool IsWormBossEncounter(NPC locked) {
			if (locked == null || !locked.active) {
				return false;
			}

			if (locked.aiStyle == NPCAIStyleID.Worm) {
				return true;
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || other.whoAmI == locked.whoAmI || other.friendly) {
					continue;
				}

				if (other.realLife == locked.whoAmI) {
					return true;
				}

				if (locked.realLife >= 0
					&& locked.realLife < Main.maxNPCs
					&& other.realLife == locked.realLife) {
					return true;
				}
			}

			return false;
		}

		/// <summary>True while the follow player is still within keep range of the locked boss (any worm segment).</summary>
		public static bool PlayerWithinKeepRadius(Player player, NPC locked) {
			if (player == null || locked == null || !locked.active) {
				return false;
			}

			if (!IsWormBossEncounter(locked)) {
				return player.Distance(locked.Center) <= PlayerKeepRadius;
			}

			float keepSq = PlayerKeepRadius * PlayerKeepRadius;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || other.friendly || !IsPartOfLockedBoss(other, locked)) {
					continue;
				}

				if (Vector2.DistanceSquared(player.Center, other.Center) <= keepSq) {
					return true;
				}
			}

			return false;
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
		public static NPC ResolveFireTarget(NPC attacker, NPC locked, Item weapon = null) {
			if (locked == null || !locked.active) {
				return null;
			}

			if (IsWormBossEncounter(locked)) {
				return ResolveWormFireTarget(attacker, locked, weapon);
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

		/// <summary>
		/// Prefer a segment that can be shot now (range + LoS), then any LoS segment, then nearest.
		/// Avoids locking onto a buried body while an exposed tail sits in front.
		/// </summary>
		private static NPC ResolveWormFireTarget(NPC attacker, NPC locked, Item weapon) {
			Vector2 from = attacker != null ? attacker.Center : locked.Center;
			bool hasWeapon = weapon != null && !weapon.IsAir;
			Item ammo = attacker?.GetGlobalNPC<EquippedTownNPC>()?.Ammo;
			bool throughWalls = hasWeapon && NpcAttackPath.ThroughWalls(weapon, ammo);
			float range = hasWeapon ? NpcWeaponReach.EffectiveRange(weapon) : 0f;

			NPC bestShoot = null;
			float bestShootDist = float.MaxValue;
			NPC bestClear = null;
			float bestClearDist = float.MaxValue;
			NPC bestAny = null;
			float bestAnyDist = float.MaxValue;

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || other.friendly || !IsPartOfLockedBoss(other, locked)) {
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

				Vector2 hit = other.Hitbox.ClosestPointInRect(from);
				float dist = Vector2.Distance(from, hit);
				if (dist < bestAnyDist) {
					bestAnyDist = dist;
					bestAny = other;
				}

				if (!NpcWeaponReach.Clear(from, other.Center) && !throughWalls) {
					continue;
				}

				if (dist < bestClearDist) {
					bestClearDist = dist;
					bestClear = other;
				}

				if (hasWeapon && dist <= range && dist < bestShootDist) {
					bestShootDist = dist;
					bestShoot = other;
				}
			}

			return bestShoot ?? bestClear ?? bestAny
				?? FindNearestChaseableNear(attacker, locked.Center, NpcNormalCombat.LeashDist);
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
			return IsKnownBossPartType(part.type) && part.Distance(locked.Center) <= PartLinkRadius;
		}

		public static void TickFire(NPC npc, EquippedTownNPC equip, NpcCombatState combat) {
			NPC locked = GetLockedBoss(combat);
			Item weapon = equip != null && !equip.Weapon.IsAir ? equip.Weapon : null;
			NPC target = ResolveFireTarget(npc, locked, weapon);
			// CanUse/Clear run inside NpcCombatFire after AimAtTarget (facing-sensitive HandPoint).
			NpcCombatFire.Tick(npc, equip, target, allowFire: target != null);
		}
	}
}
