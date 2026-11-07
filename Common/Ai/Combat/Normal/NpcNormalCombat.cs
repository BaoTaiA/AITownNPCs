using AITownNPCs.Common.Ai.Combat.Boss;
using AITownNPCs.Common.Equip;
using Terraria;

namespace AITownNPCs.Common.Ai.Combat.Normal
{
	/// <summary>Normal combat: Palico Select / CanUse / Clear / Stand / AimPoint.</summary>
	public static class NpcNormalCombat
	{
		/// <summary>Leash matches SearchRange (40 tiles).</summary>
		public const float LeashDist = 640f;

		public static NPC ResolveTarget(NPC npc, Player player, NpcCombatState combat) {
			NPC selected = combat.Targeting.Select(player, npc.Center);
			combat.CachedTargetWhoAmI = selected?.whoAmI ?? -1;
			return selected;
		}

		public static void TickFire(NPC npc, EquippedTownNPC equip, NpcCombatState combat) {
			NPC visible = combat.Targeting.VisibleTarget;
			// CanUse/Clear run inside NpcCombatFire after AimAtTarget (facing-sensitive HandPoint).
			// VisibleTarget only — Obscured SightGrace retains engage, not fire/aim through walls.
			NpcCombatFire.Tick(npc, equip, visible, allowFire: visible != null);
		}

		public static bool IsChaseable(NPC npc, NPC other) {
			if (other == null || !other.active || other.friendly || other.dontTakeDamage) {
				return false;
			}

			if (NpcBossCombat.IsBossEncounterNpc(other)) {
				return false;
			}

			if (Terraria.ID.NPCID.Sets.CountsAsCritter[other.type]) {
				return false;
			}

			if (other.CanBeChasedBy(npc)) {
				return true;
			}

			return NpcCombatTargeting.Destructible(other);
		}

		public static NPC GetCachedIfValid(NPC npc, NpcCombatState combat) {
			NPC current = combat.Targeting.Current;
			if (current != null) {
				return IsChaseable(npc, current) ? current : null;
			}

			int who = combat.CachedTargetWhoAmI;
			if (who < 0 || who >= Main.maxNPCs) {
				return null;
			}

			NPC other = Main.npc[who];
			return IsChaseable(npc, other) ? other : null;
		}
	}
}
