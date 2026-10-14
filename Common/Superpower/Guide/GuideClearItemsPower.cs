using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower.Guide
{
	public sealed class GuideClearItemsPower : ISuperpower
	{
		public string Id => SuperpowerIds.GuideClearItems;
		public int NpcType => NPCID.Guide;
		public int CooldownFrames => 60 * 60;
		public string DisplayNameZh => "清除掉落物";
		public string DescriptionZh => "清除全世界地面掉落物品";
		public string RefuseStyleZh => "温和";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			int cleared = 0;
			for (int i = 0; i < Main.maxItems; i++) {
				Item item = Main.item[i];
				if (item != null && item.active) {
					item.TurnToAir();
					cleared++;
					if (Main.netMode == NetmodeID.Server) {
						NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
					}
				}
			}

			return SuperpowerResult.Ok(Id, true, "已清除掉落物", "数量=" + cleared);
		}
	}
}
