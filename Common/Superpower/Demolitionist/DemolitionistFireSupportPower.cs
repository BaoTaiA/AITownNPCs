using Terraria;
using Terraria.ID;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Demolitionist
{
	public sealed class DemolitionistFireSupportPower : ISuperpower
	{
		public string Id => SuperpowerIds.DemoFireSupport;
		public int NpcType => NPCID.Demolitionist;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (player.dead) {
				return SuperpowerResult.Failed(Id, "dead", ModText.Get("Runtime.Superpower.Fixed.player_dead"));
			}

			DemolitionistBarrageSystem.Start(player.whoAmI);
			return SuperpowerResult.Ok(Id, true, ModText.Get("Runtime.Superpower.Fixed.demo_support_ok"), ModText.Get("Runtime.Superpower.Fixed.support_duration"));
		}
	}
}
