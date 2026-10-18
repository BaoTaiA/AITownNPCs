using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Host
{
	/// <summary>Block opening town-NPC chat while the NPC is fighting or following someone else.</summary>
	public class NpcTownTalkGate : ModSystem
	{
		public override void Load() {
			On_Player.SetTalkNPC += OnSetTalkNPC;
		}

		public override void Unload() {
			On_Player.SetTalkNPC -= OnSetTalkNPC;
		}

		private static void OnSetTalkNPC(On_Player.orig_SetTalkNPC orig, Player player, int npcIndex, bool fromNet) {
			if (!fromNet
				&& player != null
				&& player.whoAmI == Main.myPlayer
				&& npcIndex >= 0
				&& npcIndex < Main.maxNPCs
				&& NpcInteractGate.TryGetBlockMessage(player, Main.npc[npcIndex], out string message)) {
				Main.NewText(message, NpcInteractGate.NoticeColor);
				return;
			}

			orig(player, npcIndex, fromNet);
		}
	}
}
