using AITownNPCs.Common.Ai.Chat;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Net
{
	/// <summary>
	/// Server → client presentation only. Never use ShowLocal on dedicated server as sync.
	/// </summary>
	public static class ServerFx
	{
		public static void NpcSpeechBubble(int npcWhoAmI, string text) {
			if (string.IsNullOrWhiteSpace(text) || npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				NpcSpeechBubbleSystem.Broadcast(npcWhoAmI, text);
				return;
			}

			// Singleplayer / listen-server host local draw.
			NpcSpeechBubbleSystem.ShowLocal(npcWhoAmI, text);
		}
	}
}
