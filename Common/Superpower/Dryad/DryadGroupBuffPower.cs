using AITownNPCs.Common.Roles.Dryad;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Dryad
{
	public sealed class DryadGroupBuffPower : ISuperpower
	{
		public string Id => SuperpowerIds.DryadGroupBuff;
		public int NpcType => NPCID.Dryad;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "群体增益";
		public string DescriptionZh => "传送到玩家身边放原版结界，结界结束后再等 8 秒回去";
		public string RefuseStyleZh => "温柔、甜美";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (player.dead) {
				return SuperpowerResult.Failed(Id, "dead", "玩家已死亡");
			}

			if (!DryadAuraAI.HasBuffPotion(npc)) {
				return SuperpowerResult.Failed(Id, "no_potion", "身上没有增益药水");
			}

			if (!NpcVisitState.TryBegin(npc, player, NpcVisitState.StayTicks)) {
				return SuperpowerResult.Failed(Id, "start_failed", "结界施放失败");
			}

			if (!DryadAuraAI.TryCastWard(npc)) {
				NpcVisitState.Cancel(npc.whoAmI);
				return SuperpowerResult.Failed(Id, "start_failed", "结界施放失败");
			}

			return SuperpowerResult.Ok(Id, true, "已施放群体增益");
		}
	}
}
