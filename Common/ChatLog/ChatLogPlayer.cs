using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.ChatLog
{
	public class ChatLogPlayer : ModPlayer
	{
		/// <summary>Stable id for follow-channel history keys (survives rename).</summary>
		public string PlayerId = string.Empty;

		public string EnsurePlayerId() {
			if (string.IsNullOrWhiteSpace(PlayerId)) {
				PlayerId = Guid.NewGuid().ToString("N");
			}

			return PlayerId;
		}

		public override void SaveData(TagCompound tag) {
			tag["playerId"] = EnsurePlayerId();
		}

		public override void LoadData(TagCompound tag) {
			PlayerId = tag.ContainsKey("playerId") ? tag.GetString("playerId") : string.Empty;
			if (string.IsNullOrWhiteSpace(PlayerId)) {
				PlayerId = Guid.NewGuid().ToString("N");
			}
		}

		public override void OnEnterWorld() {
			EnsurePlayerId();

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				ChatLogWorldSystem.RequestHistoryFromServer(PlayerId);
				return;
			}

			// Singleplayer / listen-server local player: replay from world data.
			if (Player.whoAmI == Main.myPlayer) {
				ChatLogWorldSystem.ReplayLocalHistory(PlayerId);
			}
		}
	}
}
