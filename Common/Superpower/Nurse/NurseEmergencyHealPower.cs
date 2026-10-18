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

			if (!TryGetTrustedLife(player, out int life, out int lifeMax)) {
				return SuperpowerResult.Failed(Id, "no_client_state", "请再说一次（客户端状态未同步）");
			}

			if (lifeMax > 0 && life >= lifeMax) {
				return SuperpowerResult.Failed(Id, "full_hp", "玩家满血无需治疗",
					"拒绝风格：" + RefuseStyleZh);
			}

			if (NurseEmergencyHealState.IsBusy(npc.whoAmI)) {
				return SuperpowerResult.Failed(Id, "busy", "护士正忙");
			}

			if (!NurseEmergencyHealState.Start(npc, player)) {
				return SuperpowerResult.Failed(Id, "start_failed", "紧急治疗启动失败");
			}

			return SuperpowerResult.Ok(Id, true, "已紧急治疗至满血");
		}

		/// <summary>
		/// Single-player and the listen host use the local life total.
		/// Other clients report life with the chat packet; the server copy is not trustworthy without SSC.
		/// </summary>
		private static bool TryGetTrustedLife(Player player, out int life, out int lifeMax) {
			bool localCopy = Main.netMode == NetmodeID.SinglePlayer
				|| (Main.netMode == NetmodeID.Server && !Main.dedServ && player.whoAmI == Main.myPlayer);
			if (localCopy) {
				life = player.statLife;
				lifeMax = player.statLifeMax2;
				return true;
			}

			return PlayerSuperpowerClientSnapshot.TryGetLife(player.whoAmI, out life, out lifeMax);
		}
	}
}
