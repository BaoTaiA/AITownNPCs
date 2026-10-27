using AITownNPCs.Common.Roles.Dryad;
using Terraria;
using Terraria.ID;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Dryad
{
	public sealed class DryadGroupBuffPower : ISuperpower
	{
		public string Id => SuperpowerIds.DryadGroupBuff;
		public int NpcType => NPCID.Dryad;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (player.dead) {
				return SuperpowerResult.Failed(Id, "dead", ModText.Get("Runtime.Superpower.Fixed.player_dead"));
			}

			if (!DryadAuraAI.HasBuffPotion(npc)) {
				return SuperpowerResult.Failed(Id, "no_potion", ModText.Get("Runtime.Superpower.Fixed.no_potion"));
			}

			if (!NpcVisitState.TryBegin(npc, player, NpcVisitState.StayTicks)) {
				return SuperpowerResult.Failed(Id, "start_failed", ModText.Get("Runtime.Superpower.Fixed.aura_fail"));
			}

			if (!DryadAuraAI.TryCastWard(npc)) {
				NpcVisitState.Cancel(npc.whoAmI);
				return SuperpowerResult.Failed(Id, "start_failed", ModText.Get("Runtime.Superpower.Fixed.aura_fail"));
			}

			return SuperpowerResult.Ok(Id, true, ModText.Get("Runtime.Superpower.Fixed.group_buff_ok"));
		}
	}
}
