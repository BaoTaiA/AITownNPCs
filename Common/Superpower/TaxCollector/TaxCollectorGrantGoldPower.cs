using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.TaxCollector
{
	public sealed class TaxCollectorGrantGoldPower : ISuperpower
	{
		public string Id => SuperpowerIds.TaxGrantGold;
		public int NpcType => NPCID.TaxCollector;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "施舍金币";
		public string DescriptionZh => "有概率给 50 金币";
		public string RefuseStyleZh => "贪婪、阴险、算计";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			ServerPlayerNet.GiveCopperSynced(player, 50L * 10000L);
			return SuperpowerResult.Ok(Id, true, "已经给50金币");
		}
	}
}
