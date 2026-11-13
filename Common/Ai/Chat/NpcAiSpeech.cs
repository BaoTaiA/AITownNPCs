using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.ChatLog;
using AITownNPCs.Common.Host;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;

namespace AITownNPCs.Common.Ai.Chat
{
	public static class NpcAiSpeech
	{
		public static readonly Color SpeechColor = new(180, 220, 255);
		public static readonly Color WarningColor = new(255, 80, 80);

		/// <summary>True when the server may safely send a directed chat packet to this slot.</summary>
		public static bool CanSendToClient(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return false;
			}

			Player player = Main.player[playerWhoAmI];
			if (player == null || !player.active) {
				return false;
			}

			if (Main.netMode != NetmodeID.Server) {
				return true;
			}

			RemoteClient client = Netplay.Clients[playerWhoAmI];
			return client != null && client.IsConnected();
		}

		public static string FormatDisplayName(NPC npc) => NpcPresentation.FormatDisplayName(npc);

		public static string GetNpcHeadTag(NPC npc) => NpcPresentation.GetNpcHeadTag(npc);

		public static string BuildChatLine(NPC npc, string reply) {
			return GetNpcHeadTag(npc) + FormatDisplayName(npc) + "；" + reply;
		}

		/// <summary>
		/// Following NPCs: follow channel + overhead bubble (not public chat).
		/// Others: public chat + bubble.
		/// </summary>
		public static void Announce(NPC npc, string reply) {
			if (!NpcHostRules.IsManagedTownNpc(npc) || string.IsNullOrWhiteSpace(reply)) {
				return;
			}

			string trimmed = reply.Trim();
			var follow = npc.GetGlobalNPC<FollowingTownNPC>();
			bool following = follow.IsFollowing;

			if (following) {
				string line = BuildChatLine(npc, trimmed);
				ChatLogWorldSystem.AppendFollowForPlayer(follow.FollowPlayerWhoAmI, line, SpeechColor);
			}
			else {
				string line = BuildChatLine(npc, trimmed);
				if (Main.netMode == NetmodeID.SinglePlayer) {
					Main.NewText(line, SpeechColor);
				}
				else if (Main.netMode == NetmodeID.Server) {
					ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(line), SpeechColor);
				}
			}

			// Dedicated server: only Broadcast path draws on clients.
			ServerFx.NpcSpeechBubble(npc.whoAmI, trimmed);
		}

		public static bool NotifyPlayer(int playerWhoAmI, string localizationKey, Color? color = null, params object[] args) {
			string text = args == null || args.Length == 0
				? Language.GetTextValue(localizationKey)
				: Language.GetTextValue(localizationKey, args);
			return NotifyPlayerLiteral(playerWhoAmI, text, color);
		}

		/// <returns>False when text empty or the client is not connected (caller may retry).</returns>
		public static bool NotifyPlayerLiteral(int playerWhoAmI, string text, Color? color = null) {
			if (string.IsNullOrWhiteSpace(text)) {
				return false;
			}

			Color c = color ?? SpeechColor;
			// SP or local client: show immediately (client-side gates like arms-loan use this path).
			if (Main.netMode == NetmodeID.SinglePlayer
			    || (Main.netMode == NetmodeID.MultiplayerClient && playerWhoAmI == Main.myPlayer)) {
				Main.NewText(text, c);
				return true;
			}

			if (Main.netMode == NetmodeID.Server && CanSendToClient(playerWhoAmI)) {
				ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(text), c, playerWhoAmI);
				return true;
			}

			return false;
		}
	}
}
