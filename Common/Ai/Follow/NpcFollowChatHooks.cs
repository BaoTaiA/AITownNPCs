using System;
using System.IO;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.ChatLog;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Net;
using AITownNPCs.Common.Superpower;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>
	/// Follow-owned chat hooks: start/stop keywords, hide @-to-follower from chat box,
	/// player overhead bubble. AI replies are raised via <see cref="NpcPlayerChatIngress"/> (Chat registers).
	/// </summary>
	public class NpcFollowChatHooks : ModSystem
	{
		/// <summary>Last handled game frame per player (avoids MP same-frame drop).</summary>
		private static readonly int[] HandledFrameByPlayer = CreateFrameArray();
		private static readonly int[] FollowLogFrameByPlayer = CreateFrameArray();

		private static int[] CreateFrameArray() {
			var arr = new int[Main.maxPlayers];
			for (int i = 0; i < arr.Length; i++) {
				arr[i] = -1;
			}

			return arr;
		}

		public override void Load() {
			On_ChatCommandProcessor.ProcessIncomingMessage += OnProcessIncomingMessage;
			On_ChatHelper.SendChatMessageFromClient += OnSendChatMessageFromClient;
			On_Main.DoUpdate_HandleChat += OnDoUpdateHandleChat;
		}

		public override void Unload() {
			On_ChatCommandProcessor.ProcessIncomingMessage -= OnProcessIncomingMessage;
			On_ChatHelper.SendChatMessageFromClient -= OnSendChatMessageFromClient;
			On_Main.DoUpdate_HandleChat -= OnDoUpdateHandleChat;
		}

		private static void OnProcessIncomingMessage(On_ChatCommandProcessor.orig_ProcessIncomingMessage orig, ChatCommandProcessor self, ChatMessage message, int clientId) {
			string text = SafeGetText(message);

			if (Main.netMode != NetmodeID.MultiplayerClient
			    && ShouldHideChatForFollower(clientId, text)) {
				ShowPlayerOverheadLocal(clientId, text);
				RecordHiddenFollowChat(clientId, text);
				if (Main.netMode == NetmodeID.Server) {
					BroadcastPlayerOverhead(clientId, text, ignoreClient: clientId);
				}

				TryHandleChatForPlayer(text, clientId);
				return;
			}

			orig(self, message, clientId);

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			// Persist normal public chat on dedicated (BroadcastChatMessage often skips player lines).
			if (Main.netMode == NetmodeID.Server && !string.IsNullOrWhiteSpace(text) && !IsFollowKeyword(text)) {
				ChatLogWorldSystem.AppendPublicPlayerChat(clientId, text);
			}

			// MP: follow keywords are handled solely by RequestStart/Stop from the client.
			// ProcessIncomingMessage must not also TryStartFollow (double pink announce).
			if (Main.netMode == NetmodeID.Server && IsFollowKeyword(text)) {
				return;
			}

			TryHandleChatForPlayer(text, clientId);
		}

		private static void OnSendChatMessageFromClient(On_ChatHelper.orig_SendChatMessageFromClient orig, ChatMessage message) {
			string text = SafeGetText(message);

			if (Main.netMode == NetmodeID.MultiplayerClient
			    && Main.LocalPlayer != null
			    && Main.LocalPlayer.active
			    && ShouldHideChatForFollower(Main.myPlayer, text)) {
				ShowPlayerOverheadLocal(Main.myPlayer, text);
				string trimmed = text?.Trim();
				ChatLogWorldSystem.AddFollowLocalOptimistic(FormatPlayerFollowLine(Main.myPlayer, trimmed), HiddenChatOverheadColor);
				SendHiddenChatPacket(trimmed);
				return;
			}

			orig(message);

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			if (string.IsNullOrWhiteSpace(text) || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			DispatchClientRequest(text.Trim());
		}

		private static void OnDoUpdateHandleChat(On_Main.orig_DoUpdate_HandleChat orig) {
			string pending = null;
			bool hide = false;
			try {
				if (Main.drawingPlayerChat
					&& !string.IsNullOrEmpty(Main.chatText)
					&& Main.chatRelease
					&& Main.hasFocus
					&& Main.keyState.IsKeyDown(Keys.Enter)
					&& !Main.oldKeyState.IsKeyDown(Keys.Enter)) {
					pending = Main.chatText;
					hide = Main.netMode != NetmodeID.MultiplayerClient
						&& ShouldHideChatForFollower(Main.myPlayer, pending);
					if (hide) {
						Main.chatText = string.Empty;
					}
				}
			}
			catch {
				pending = null;
				hide = false;
			}

			orig();

			if (pending == null || Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (hide) {
				ShowPlayerOverheadLocal(Main.myPlayer, pending);
				RecordHiddenFollowChat(Main.myPlayer, pending);
			}

			TryHandleChatForPlayer(pending, Main.myPlayer);
		}

		private static void RecordHiddenFollowChat(int playerWhoAmI, string text) {
			if (string.IsNullOrWhiteSpace(text)) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			int frame = (int)Main.GameUpdateCount;
			if (FollowLogFrameByPlayer[playerWhoAmI] == frame) {
				return;
			}

			FollowLogFrameByPlayer[playerWhoAmI] = frame;
			ChatLogWorldSystem.AppendFollowForPlayer(
				playerWhoAmI,
				FormatPlayerFollowLine(playerWhoAmI, text.Trim()),
				HiddenChatOverheadColor);
		}

		private static string FormatPlayerFollowLine(int playerWhoAmI, string text) {
			string name = "Player";
			if (playerWhoAmI >= 0 && playerWhoAmI < Main.maxPlayers) {
				Player player = Main.player[playerWhoAmI];
				if (player != null && player.active && !string.IsNullOrEmpty(player.name)) {
					name = player.name;
				}
			}

			return name + "；" + (text ?? string.Empty);
		}

		/// <summary>
		/// True when the message is @-addressed to the current follower (bare @ or @name).
		/// </summary>
		public static bool ShouldHideChatForFollower(int playerWhoAmI, string text) {
			if (string.IsNullOrWhiteSpace(text)) {
				return false;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return false;
			}

			Player player = Main.player[playerWhoAmI];
			if (player == null || !player.active) {
				return false;
			}

			NPC follower = NpcFollowQuery.FindFollower(player);
			if (follower == null || !follower.active) {
				return false;
			}

			string trimmed = text.Trim();
			if (!trimmed.StartsWith("@")) {
				return false;
			}

			return NpcPlayerChatIngress.CheckAddressedToFollower(trimmed, follower);
		}

		public static void HandleHiddenChatPacket(BinaryReader reader, int sender) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			string text = reader.ReadString();
			ulong favoritedMask = reader.ReadUInt64();
			byte selectedItem = reader.ReadByte();
			long copperOwned = reader.ReadInt64();
			int statLife = reader.ReadInt32();
			int statLifeMax2 = reader.ReadInt32();
			PlayerSuperpowerClientSnapshot.Set(sender, favoritedMask, selectedItem, copperOwned, statLife, statLifeMax2);
			PlayerSuperpowerClientSnapshot.Pin(sender);
			RecordHiddenFollowChat(sender, text);
			TryHandleChatForPlayer(text, sender);
			BroadcastPlayerOverhead(sender, text, ignoreClient: sender);
		}

		public static void HandlePlayerOverheadPacket(BinaryReader reader) {
			int playerWhoAmI = reader.ReadByte();
			string text = reader.ReadString();
			ShowPlayerOverheadLocal(playerWhoAmI, text);
		}

		public static readonly Color HiddenChatOverheadColor = new(80, 220, 100);

		public static void ShowPlayerOverheadLocal(int playerWhoAmI, string text) {
			if (Main.dedServ || string.IsNullOrWhiteSpace(text)) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			Player player = Main.player[playerWhoAmI];
			if (player == null || !player.active) {
				return;
			}

			string trimmed = text.Trim();
			player.chatOverhead.NewMessage(trimmed, Main.PlayerOverheadChatMessageDisplayTime);
			player.chatOverhead.color = HiddenChatOverheadColor;
			player.chatOverhead.snippets = ChatManager.ParseMessage(trimmed, HiddenChatOverheadColor).ToArray();
			player.chatOverhead.messageSize = ChatManager.GetStringSize(
				FontAssets.MouseText.Value,
				player.chatOverhead.snippets,
				Vector2.One);
		}

		private static void BroadcastPlayerOverhead(int playerWhoAmI, string text, int ignoreClient = -1) {
			if (Main.netMode != NetmodeID.Server || string.IsNullOrWhiteSpace(text)) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcChatMessageType.PlayerOverhead);
			packet.Write((byte)playerWhoAmI);
			packet.Write(text.Trim());
			packet.Send(ignoreClient: ignoreClient);
		}

		private static void SendHiddenChatPacket(string trimmed) {
			if (string.IsNullOrWhiteSpace(trimmed) || Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			Player local = Main.LocalPlayer;
			ulong favoritedMask = PlayerSuperpowerClientSnapshot.BuildFavoritedMaskLocal(local);
			byte selectedItem = (byte)Math.Clamp(local.selectedItem, 0, 255);
			long copperOwned = PlayerSuperpowerClientSnapshot.CountCopperLocal(local);

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcChatMessageType.PlayerChatHidden);
			packet.Write(trimmed);
			packet.Write(favoritedMask);
			packet.Write(selectedItem);
			packet.Write(copperOwned);
			packet.Write(local.statLife);
			packet.Write(local.statLifeMax2);
			packet.Send();
		}

		private static void DispatchClientRequest(string trimmed) {
			if (string.IsNullOrWhiteSpace(trimmed)) {
				return;
			}

			AinpcServerConfig config = AinpcServerConfig.Instance;
			if (config == null) {
				return;
			}

			string start = (config.FollowStartKeyword ?? string.Empty).Trim();
			string stop = (config.FollowStopKeyword ?? string.Empty).Trim();

			if (!string.IsNullOrEmpty(start) && trimmed.Equals(start, StringComparison.OrdinalIgnoreCase)) {
				NpcFollowNetSystem.RequestStart();
				return;
			}

			if (!string.IsNullOrEmpty(stop) && trimmed.Equals(stop, StringComparison.OrdinalIgnoreCase)) {
				NpcFollowNetSystem.RequestStop();
				return;
			}

			// Dedicated server: do not rely on ChatCommandProcessor alone for AI / superpowers.
			SendPlayerChatLinePacket(trimmed);
		}

		private static void SendPlayerChatLinePacket(string trimmed) {
			if (string.IsNullOrWhiteSpace(trimmed) || Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			Player local = Main.LocalPlayer;
			ulong favoritedMask = PlayerSuperpowerClientSnapshot.BuildFavoritedMaskLocal(local);
			byte selectedItem = (byte)Math.Clamp(local.selectedItem, 0, 255);
			long copperOwned = PlayerSuperpowerClientSnapshot.CountCopperLocal(local);

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcChatMessageType.PlayerChatLine);
			packet.Write(trimmed);
			packet.Write(favoritedMask);
			packet.Write(selectedItem);
			packet.Write(copperOwned);
			packet.Write(local.statLife);
			packet.Write(local.statLifeMax2);
			packet.Send();
		}

		/// <summary>Server: client sent a public chat line for follow / AI routing.</summary>
		public static void HandlePlayerChatLinePacket(BinaryReader reader, int sender) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			string text = reader.ReadString();
			ulong favoritedMask = reader.ReadUInt64();
			byte selectedItem = reader.ReadByte();
			long copperOwned = reader.ReadInt64();
			int statLife = reader.ReadInt32();
			int statLifeMax2 = reader.ReadInt32();
			PlayerSuperpowerClientSnapshot.Set(sender, favoritedMask, selectedItem, copperOwned, statLife, statLifeMax2);
			PlayerSuperpowerClientSnapshot.Pin(sender);
			TryHandleChatForPlayer(text, sender);
		}

		private static void TryHandleChatForPlayer(string text, int playerWhoAmI) {
			if (string.IsNullOrWhiteSpace(text)) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				playerWhoAmI = Main.myPlayer;
			}

			// SP / listen-server host: refresh snapshot from local inventory (no client packet).
			if (Main.netMode == NetmodeID.SinglePlayer
			    || (Main.netMode == NetmodeID.Server && !Main.dedServ && playerWhoAmI == Main.myPlayer)) {
				Player host = Main.player[playerWhoAmI];
				if (host != null && host.active) {
					PlayerSuperpowerClientSnapshot.Set(
						playerWhoAmI,
						PlayerSuperpowerClientSnapshot.BuildFavoritedMaskLocal(host),
						(byte)Math.Clamp(host.selectedItem, 0, 255),
						PlayerSuperpowerClientSnapshot.CountCopperLocal(host),
						host.statLife,
						host.statLifeMax2);
					PlayerSuperpowerClientSnapshot.Pin(playerWhoAmI);
				}
			}

			int frame = (int)Main.GameUpdateCount;
			if (HandledFrameByPlayer[playerWhoAmI] == frame) {
				PlayerSuperpowerClientSnapshot.Unpin(playerWhoAmI);
				return;
			}

			AinpcServerConfig config = AinpcServerConfig.Instance;
			if (config == null) {
				PlayerSuperpowerClientSnapshot.Unpin(playerWhoAmI);
				return;
			}

			string trimmed = text.Trim();
			string start = (config.FollowStartKeyword ?? string.Empty).Trim();
			string stop = (config.FollowStopKeyword ?? string.Empty).Trim();

			HandledFrameByPlayer[playerWhoAmI] = frame;

			if (!string.IsNullOrEmpty(start) && trimmed.Equals(start, StringComparison.OrdinalIgnoreCase)) {
				NpcFollowNetSystem.TryStartFollow(playerWhoAmI);
				PlayerSuperpowerClientSnapshot.Unpin(playerWhoAmI);
				return;
			}

			if (!string.IsNullOrEmpty(stop) && trimmed.Equals(stop, StringComparison.OrdinalIgnoreCase)) {
				NpcFollowNetSystem.TryStopFollow(playerWhoAmI);
				PlayerSuperpowerClientSnapshot.Unpin(playerWhoAmI);
				return;
			}

			// AI / superpower path keeps pin until SuperpowerOrchestrator finishes.
			NpcPlayerChatIngress.RaiseNonKeywordChat(playerWhoAmI, trimmed);
		}

		private static bool IsFollowKeyword(string text) {
			if (string.IsNullOrWhiteSpace(text)) {
				return false;
			}

			AinpcServerConfig config = AinpcServerConfig.Instance;
			if (config == null) {
				return false;
			}

			string trimmed = text.Trim();
			string start = (config.FollowStartKeyword ?? string.Empty).Trim();
			string stop = (config.FollowStopKeyword ?? string.Empty).Trim();
			if (!string.IsNullOrEmpty(start) && trimmed.Equals(start, StringComparison.OrdinalIgnoreCase)) {
				return true;
			}

			if (!string.IsNullOrEmpty(stop) && trimmed.Equals(stop, StringComparison.OrdinalIgnoreCase)) {
				return true;
			}

			return false;
		}

		private static string SafeGetText(ChatMessage message) {
			try {
				return message?.Text;
			}
			catch {
				return null;
			}
		}

		public override void OnWorldUnload() {
			for (int i = 0; i < HandledFrameByPlayer.Length; i++) {
				HandledFrameByPlayer[i] = -1;
				FollowLogFrameByPlayer[i] = -1;
			}
		}
	}
}
