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
	/// EvaluateMode → Follow → Role → Combat.Tick (Boss and Normal share follow locomotion).
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

			if (npc.GetGlobalNPC<NpcVisitGlobal>().IsVisitHolding(npc)) {
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

			// Visit stay: do not let follow/role/combat yank the NPC away (ClientStay on MP clients).
			if (npc.GetGlobalNPC<NpcVisitGlobal>().IsVisitHolding(npc)) {
				return;
			}

			NpcCombatBrain.EvaluateMode(npc);
			NpcFollowAI.Update(npc);

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
