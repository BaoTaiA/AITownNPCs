using AITownNPCs.Common.Ai.Combat.Boss;
using AITownNPCs.Common.Equip;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat.Normal
{
	/// <summary>Normal mode acquire (player 500) + sticky keep (player 800).</summary>
	public static class NpcNormalCombat
	{
		public const float AcquireRadius = 500f;
		public const float StickyRadius = 800f;
		public const float LeashDist = 1200f;

		public static NPC ResolveTarget(NPC npc, Player player, NpcCombatState combat) {
			if (TryKeepSticky(npc, player, combat, out NPC sticky)) {
				return sticky;
			}

			return FindNearestAroundPlayer(npc, player, AcquireRadius);
		}

		public static void TickFire(NPC npc, EquippedTownNPC equip, NpcCombatState combat) {
			NPC target = GetCachedIfValid(npc, combat);
			bool allowFire = target != null && NpcCombatFire.HasLineOfSight(npc, target);
			NpcCombatFire.Tick(npc, equip, target, allowFire);
		}

		private static bool TryKeepSticky(NPC npc, Player player, NpcCombatState combat, out NPC sticky) {
			sticky = null;
			int who = combat.CachedTargetWhoAmI;
			if (who < 0 || who >= Main.maxNPCs) {
				return false;
			}

			NPC other = Main.npc[who];
			if (!IsChaseable(npc, other)) {
				return false;
			}

			if (player.Distance(other.Center) > StickyRadius) {
				return false;
			}

			if (npc.Distance(player.Center) > LeashDist) {
				return false;
			}

			// Wall blocks shots: drop sticky so we can pick a visible foe.
			if (!NpcCombatFire.HasLineOfSight(npc, other)) {
				return false;
			}

			sticky = other;
			return true;
		}

		private static NPC FindNearestAroundPlayer(NPC npc, Player player, float radius) {
			NPC best = null;
			float bestDist = radius;

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!IsChaseable(npc, other) || NpcBossCombat.IsBossEncounterNpc(other)) {
					continue;
				}

				float dist = player.Distance(other.Center);
				if (dist >= bestDist) {
					continue;
				}

				// Never lock a foe we cannot shoot — matches Palico Valid/Clear.
				if (!NpcCombatFire.HasLineOfSight(npc, other)) {
					continue;
				}

				bestDist = dist;
				best = other;
			}

			return best;
		}

		public static bool IsChaseable(NPC npc, NPC other) {
			if (other == null || !other.active || other.friendly || other.dontTakeDamage) {
				return false;
			}

			if (NpcBossCombat.IsBossEncounterNpc(other)) {
				return false;
			}

			if (NPCID.Sets.CountsAsCritter[other.type]) {
				return false;
			}

			return other.CanBeChasedBy(npc);
		}

		public static NPC GetCachedIfValid(NPC npc, NpcCombatState combat) {
			int who = combat.CachedTargetWhoAmI;
			if (who < 0 || who >= Main.maxNPCs) {
				return null;
			}

			NPC other = Main.npc[who];
			return IsChaseable(npc, other) ? other : null;
		}
	}
}
