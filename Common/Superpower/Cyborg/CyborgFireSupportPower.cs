using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Cyborg
{
	public sealed class CyborgFireSupportPower : ISuperpower
	{
		public string Id => SuperpowerIds.CyborgFireSupport;
		public int NpcType => NPCID.Cyborg;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "火力支援";
		public string DescriptionZh => "在玩家周围 400 像素用火箭密集覆盖约 1 分钟";
		public string RefuseStyleZh => "机械、直接、冷酷";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (player.dead) {
				return SuperpowerResult.Failed(Id, "dead", "玩家已死亡");
			}

			CyborgBarrageSystem.Start(player.whoAmI);
			return SuperpowerResult.Ok(Id, true, "已启动火箭火力支援", "约持续1分钟");
		}
	}
}
