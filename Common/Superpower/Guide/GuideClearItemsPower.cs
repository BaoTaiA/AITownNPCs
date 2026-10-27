using Terraria;
using Terraria.ID;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Guide
{
	public sealed class GuideClearItemsPower : ISuperpower
	{
		public string Id => SuperpowerIds.GuideClearItems;
		public int NpcType => NPCID.Guide;
		public int CooldownFrames => 60 * 60;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

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

			return SuperpowerResult.Ok(Id, true, ModText.Get("Runtime.Superpower.Fixed.clear_items_ok"), ModText.Get("Runtime.Superpower.Qty", cleared));
		}
	}
}
