using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Roles;
using AITownNPCs.Common.Superpower;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Host
{
	/// <summary>
	/// While following: PreAI skips vanilla town AI; PostAI runs
	/// EvaluateMode → Follow (skipped only while a boss owns locomotion) → Role → Combat.Tick.
	/// Role.IsBusy pauses combat (e.g. nurse throw / dryad ward).
	/// </summary>
	public class NpcBehaviorHost : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		/// <summary>
		/// Skip vanilla AIStyle 7 (home / idle) on every side while this mod hosts locomotion.
		/// PostAI still runs when PreAI returns false. Nurse emergency PreAI stays higher priority
		/// via its own GlobalNPC when the nurse is busy.
		/// </summary>
		public override bool PreAI(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return true;
			}

			if (NpcVisitState.IsStaying(npc.whoAmI)) {
				return true; // NpcVisitGlobal already returns false; do not fight it.
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (follow.IsFollowing) {
				return false;
			}

			return true;
		}

		public override void PostAI(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return;
			}

			// Visit stay: do not let follow/role/combat yank the NPC away.
			if (NpcVisitState.IsStaying(npc.whoAmI)) {
				return;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			NpcCombatBrain.EvaluateMode(npc);

			if (NpcCombatBrain.ShouldRunFollow(npc, combat)) {
				NpcFollowAI.Update(npc);
			}
			else {
				NpcFollowAI.UpdateCombatPaused(npc);
			}

			NpcRoleRegistry.Update(npc);
			if (!NpcRoleRegistry.IsBusy(npc)) {
				NpcCombatBrain.Tick(npc);
			}
		}

		public override void TownNPCAttackCooldown(NPC npc, ref int cooldown, ref int randExtraCooldown) {
			NpcCombatBrain.SuppressVanillaTownAttack(npc, ref cooldown, ref randExtraCooldown);
		}
	}
}
