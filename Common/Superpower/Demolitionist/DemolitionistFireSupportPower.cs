using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Demolitionist
{
	public sealed class DemolitionistFireSupportPower : ISuperpower
	{
		public string Id => SuperpowerIds.DemoFireSupport;
		public int NpcType => NPCID.Demolitionist;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "火力支援";
		public string DescriptionZh => "在玩家周围 400 像素用手榴弹密集覆盖约 1 分钟";
		public string RefuseStyleZh => "疯狂、兴奋、不可控";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (player.dead) {
				return SuperpowerResult.Failed(Id, "dead", "玩家已死亡");
			}

			DemolitionistBarrageSystem.Start(player.whoAmI);
			return SuperpowerResult.Ok(Id, true, "已启动手榴弹火力支援", "约持续1分钟");
		}
	}
}
