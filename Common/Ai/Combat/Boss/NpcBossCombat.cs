using AITownNPCs.Common.Equip;
using Terraria;

namespace AITownNPCs.Common.Ai.Combat.Boss
{
	public static class NpcBossCombat
	{
		public static NPC FindNearestBoss(Player player, NPC attacker = null) {
			NPC best = null;
			float bestDist = float.MaxValue;

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || !other.boss || other.friendly || other.dontTakeDamage) {
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

				float dist = player.Distance(other.Center);
				if (dist >= bestDist) {
					continue;
				}

				bestDist = dist;
				best = other;
			}

			return best;
		}

		public static NPC GetLockedBoss(NpcCombatState combat) {
			int who = combat.LockedBossWhoAmI;
			if (who < 0 || who >= Main.maxNPCs) {
				return null;
			}

			NPC boss = Main.npc[who];
			if (!boss.active || !boss.boss || boss.friendly || boss.dontTakeDamage) {
				return null;
			}

			return boss;
		}

		public static void TickFire(NPC npc, EquippedTownNPC equip, NpcCombatState combat) {
			NPC boss = GetLockedBoss(combat);
			NpcCombatFire.Tick(npc, equip, boss, allowFire: boss != null);
		}
	}
}
