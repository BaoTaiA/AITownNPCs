using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ID;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.TaxCollector
{
	public sealed class TaxCollectorGrantGoldPower : ISuperpower
	{
		public string Id => SuperpowerIds.TaxGrantGold;
		public int NpcType => NPCID.TaxCollector;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			ServerPlayerNet.GiveCopperSynced(player, 5L * 100L * 10000L);
			return SuperpowerResult.Ok(Id, true, ModText.Get("Runtime.Superpower.Fixed.tax_ok"));
		}
	}
}
