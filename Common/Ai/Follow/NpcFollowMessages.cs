using AITownNPCs.Common.Host;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Follow
{
	public static class NpcFollowMessages
	{
		public static readonly Color Pink = new(255, 105, 180);

		public static void AnnounceStart(NPC npc, Player player) {
			Broadcast(BuildMessage(npc, player, start: true));
		}

		public static void AnnounceStop(NPC npc, Player player) {
			Broadcast(BuildMessage(npc, player, start: false));
		}

		private static string BuildMessage(NPC npc, Player player, bool start) {
			string head = NpcPresentation.GetNpcHeadTag(npc);
			string displayName = NpcPresentation.FormatDisplayName(npc);
			string playerName = player.name;
			string key = start ? "Mods.AITownNPCs.Follow.Start" : "Mods.AITownNPCs.Follow.Stop";
			string body = Language.GetTextValue(key, displayName, playerName);
			return head + body;
		}

		private static void Broadcast(string message) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				Main.NewText(message, Pink);
				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), Pink);
			}
		}
	}
}
