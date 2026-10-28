using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Roles.Dryad;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Outgoing damage for NPC firing: Wrath via <see cref="NpcCombatHooks"/>;
	/// Magic Power / Archery via <see cref="TownNpcAuraBuffs"/> gated by equipped weapon class.
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
			mult *= ClassPotionMult(npc);

			if (mult > 1f) {
				damage = System.Math.Max(1, (int)(damage * mult));
			}

			float crit = GetCritChanceAdd(npc);
			if (crit > 0f && Main.rand.NextFloat(100f) < crit) {
				damage = System.Math.Max(1, (int)(damage * 2f));
			}

			return damage;
		}

		/// <summary>Archery potion arrow/projectile speed scale (1 = none).</summary>
		public static float GetArcherySpeedMult(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 1f;
			}

			Item weapon = npc.GetGlobalNPC<EquippedTownNPC>().Weapon;
			if (weapon == null || weapon.IsAir || !weapon.CountsAsClass(DamageClass.Ranged)) {
				return 1f;
			}

			float m = npc.GetGlobalNPC<TownNpcAuraBuffs>().ArcheryMult;
			return m > 1f ? m : 1f;
		}

		private static float ClassPotionMult(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 1f;
			}

			Item weapon = npc.GetGlobalNPC<EquippedTownNPC>().Weapon;
			if (weapon == null || weapon.IsAir) {
				return 1f;
			}

			TownNpcAuraBuffs aura = npc.GetGlobalNPC<TownNpcAuraBuffs>();
			float mult = 1f;
			if (weapon.CountsAsClass(DamageClass.Magic) && aura.MagicPowerMult > 1f) {
				mult *= aura.MagicPowerMult;
			}

			if (weapon.CountsAsClass(DamageClass.Ranged) && aura.ArcheryMult > 1f) {
				mult *= aura.ArcheryMult;
			}

			return mult;
		}
	}
}
