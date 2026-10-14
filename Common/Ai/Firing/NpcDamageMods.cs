using AITownNPCs.Common.Ai.Combat;
using Terraria;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Neutral damage modifiers for firing. Reads <see cref="NpcCombatHooks"/> only — no role types.
	/// </summary>
	public static class NpcDamageMods
	{
		public static float GetOutgoingMult(NPC npc) => NpcCombatHooks.GetOutgoingMult(npc);

		/// <summary>0–100 style chance (Rage = 10 → 10%).</summary>
		public static float GetCritChanceAdd(NPC npc) => NpcCombatHooks.GetCritChanceAdd(npc);

		public static int ApplyOutgoing(NPC npc, int damage) {
			if (damage <= 0) {
				return damage;
			}

			float mult = GetOutgoingMult(npc);
			if (mult > 1f) {
				damage = System.Math.Max(1, (int)(damage * mult));
			}

			float crit = GetCritChanceAdd(npc);
			if (crit > 0f && Main.rand.NextFloat(100f) < crit) {
				damage = System.Math.Max(1, (int)(damage * 2f));
			}

			return damage;
		}
	}
}
