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
		public string DescriptionZh => "有概率给 5 个铂金币";
		public string RefuseStyleZh => "贪婪、阴险、算计";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			ServerPlayerNet.GiveCopperSynced(player, 5L * 100L * 10000L);
			return SuperpowerResult.Ok(Id, true, "已经给5个铂金币");
		}
	}
}
