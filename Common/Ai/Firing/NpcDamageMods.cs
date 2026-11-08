using AITownNPCs.Common.Equip;
using Terraria;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Mod-only outgoing bonuses for NPC firing (lifeMax growth table).
	/// Potion wrath/rage/magic/archery/flask run on the combat dummy via vanilla UpdateBuffs.
	/// </summary>
	public static class NpcDamageMods
	{
		public static int ApplyOutgoing(NPC npc, int damage) {
			if (damage <= 0) {
				return damage;
			}

			int flat = NpcLifeStatScale.GetAttackBonus(npc);
			if (flat > 0) {
				damage += flat;
			}

			return damage;
		}

		/// <summary>Growth-table flat for dummy melee HitModifiers (authority client).</summary>
		public static void ApplyToHitModifiers(NPC npc, ref NPC.HitModifiers modifiers) {
			if (npc == null || !npc.active) {
				return;
			}

			int flat = NpcLifeStatScale.GetAttackBonus(npc);
			if (flat != 0) {
				modifiers.SourceDamage.Flat += flat;
			}
		}
	}
}
