using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Nurse
{
	public sealed class NurseEmergencyHealPower : ISuperpower
	{
		public string Id => SuperpowerIds.NurseEmergencyHeal;
		public int NpcType => NPCID.Nurse;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayNameZh => "紧急治疗";
		public string DescriptionZh => "传送到玩家身边回满血并清除 debuff，稍后回家";
		public string RefuseStyleZh => "傲娇、毒舌、嘴硬心软";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (player.dead) {
				return SuperpowerResult.Failed(Id, "dead", "玩家已死亡");
			}

			// Do not gate on server statLife (!SSC copy often looks full while client is hurt).
			if (NurseEmergencyHealState.IsBusy(npc.whoAmI)) {
				return SuperpowerResult.Failed(Id, "busy", "护士正忙");
			}

			if (!NurseEmergencyHealState.Start(npc, player)) {
				return SuperpowerResult.Failed(Id, "start_failed", "紧急治疗启动失败");
			}

			return SuperpowerResult.Ok(Id, true, "已紧急治疗至满血");
		}
	}
}
