using System;
using System.Collections.Generic;
using System.IO;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Net;
using AITownNPCs.Common.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.UI.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.ChatLog
{
	public class ChatLogWorldSystem : ModSystem
	{
		public const int MaxPublic = 1000;
		public const int MaxFollowPerPlayer = 500;
		private const int ChunkSize = 24;

		private static readonly List<ChatLogEntry> PublicEntries = new();
		private static readonly Dictionary<string, List<ChatLogEntry>> FollowByPlayerId = new(StringComparer.Ordinal);
		private static long _nextId = 1;
		private static bool _replayingHistory;
		private static bool _hooksLoaded;

		public static bool IsReplayingHistory => _replayingHistory;

		public override void Load() {
			if (_hooksLoaded) {
				return;
			}

			On_RemadeChatMonitor.AddNewMessage += OnAddNewMessage;
			On_ChatHelper.BroadcastChatMessage += OnBroadcastChatMessage;
			_hooksLoaded = true;
		}

		public override void Unload() {
			if (!_hooksLoaded) {
				return;
			}

			On_RemadeChatMonitor.AddNewMessage -= OnAddNewMessage;
			On_ChatHelper.BroadcastChatMessage -= OnBroadcastChatMessage;
			_hooksLoaded = false;
			ClearRuntime();
		}

		public override void OnWorldUnload() {
			ClearRuntime();
			ChatLogClientBuffer.Clear();
		}

		private static void ClearRuntime() {
			PublicEntries.Clear();
			FollowByPlayerId.Clear();
			_nextId = 1;
			_replayingHistory = false;
		}

		public override void SaveWorldData(TagCompound tag) {
			tag["nextId"] = _nextId;
			var publicTags = new List<TagCompound>(PublicEntries.Count);
			foreach (ChatLogEntry entry in PublicEntries) {
				publicTags.Add(entry.ToTag());
			}

			tag["public"] = publicTags;

			var followTags = new List<TagCompound>();
			foreach (KeyValuePair<string, List<ChatLogEntry>> kv in FollowByPlayerId) {
				var lines = new List<TagCompound>(kv.Value.Count);
				foreach (ChatLogEntry entry in kv.Value) {
					lines.Add(entry.ToTag());
				}

				followTags.Add(new TagCompound {
					["pid"] = kv.Key,
					["lines"] = lines
				});
			}

			tag["follow"] = followTags;
		}

		public override void LoadWorldData(TagCompound tag) {
			ClearRuntime();
			_nextId = tag.ContainsKey("nextId") ? Math.Max(1L, tag.GetLong("nextId")) : 1L;

			if (tag.ContainsKey("public")) {
				foreach (TagCompound entryTag in tag.GetList<TagCompound>("public")) {
					PublicEntries.Add(ChatLogEntry.FromTag(entryTag));
				}
			}

			if (tag.ContainsKey("follow")) {
				foreach (TagCompound playerTag in tag.GetList<TagCompound>("follow")) {
					string pid = playerTag.GetString("pid");
					if (string.IsNullOrWhiteSpace(pid)) {
						continue;
					}

					var list = new List<ChatLogEntry>();
					if (playerTag.ContainsKey("lines")) {
						foreach (TagCompound lineTag in playerTag.GetList<TagCompound>("lines")) {
							list.Add(ChatLogEntry.FromTag(lineTag));
						}
					}

					FollowByPlayerId[pid] = list;
				}
			}

			BumpNextIdFromLoaded();
		}

		private static void BumpNextIdFromLoaded() {
			long max = _nextId - 1;
			foreach (ChatLogEntry entry in PublicEntries) {
				if (entry.Id > max) {
					max = entry.Id;
				}
			}

			foreach (List<ChatLogEntry> list in FollowByPlayerId.Values) {
				foreach (ChatLogEntry entry in list) {
					if (entry.Id > max) {
						max = entry.Id;
					}
				}
			}

			_nextId = max + 1;
		}

		private static void OnAddNewMessage(On_RemadeChatMonitor.orig_AddNewMessage orig, RemadeChatMonitor self, string text, Color color, int widthLimitInPixels) {
			orig(self, text, color, widthLimitInPixels);

			if (_replayingHistory || Main.netMode != NetmodeID.SinglePlayer) {
				return;
			}

			if (string.IsNullOrEmpty(text)) {
				return;
			}

			AppendPublicAuthority(text, color);
		}

		private static void OnBroadcastChatMessage(On_ChatHelper.orig_BroadcastChatMessage orig, NetworkText text, Color color, int excludedPlayer) {
			orig(text, color, excludedPlayer);

			if (_replayingHistory || Main.netMode != NetmodeID.Server) {
				return;
			}

			string plain = SafeNetworkText(text);
			if (string.IsNullOrEmpty(plain)) {
				return;
			}

			AppendPublicAuthority(plain, color);
		}

		private static string SafeNetworkText(NetworkText text) {
			try {
				return text?.ToString();
			}
			catch {
				return null;
			}
		}

		private static ChatLogEntry CreateEntry(string text, Color color) {
			return new ChatLogEntry {
				Id = _nextId++,
				TimestampUtc = DateTime.UtcNow.Ticks,
				R = color.R,
				G = color.G,
				B = color.B,
				Text = text ?? string.Empty
			};
		}

		private static void AppendPublicAuthority(string text, Color color) {
			if (string.IsNullOrWhiteSpace(text)) {
				return;
			}

			ChatLogEntry entry = CreateEntry(text.Trim(), color);
			PublicEntries.Add(entry);
			while (PublicEntries.Count > MaxPublic) {
				PublicEntries.RemoveAt(0);
			}
		}

		/// <summary>
		/// Dedicated-server player chat does not always hit BroadcastChatMessage;
		/// record from ProcessIncomingMessage so rejoin can restore the public tab.
		/// </summary>
		public static void AppendPublicPlayerChat(int playerWhoAmI, string text) {
			if (Main.netMode != NetmodeID.Server || _replayingHistory) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers || string.IsNullOrWhiteSpace(text)) {
				return;
			}

			Player player = Main.player[playerWhoAmI];
			if (player == null || !player.active) {
				return;
			}

			string name = string.IsNullOrEmpty(player.name) ? "Player" : player.name;
			string line = name + ": " + text.Trim();
			// Skip if BroadcastChatMessage already stored the same line this tick.
			if (PublicEntries.Count > 0) {
				ChatLogEntry last = PublicEntries[PublicEntries.Count - 1];
				if (last.Text == line && last.TimestampUtc >= DateTime.UtcNow.Ticks - TimeSpan.FromSeconds(2).Ticks) {
					return;
				}
			}

			AppendPublicAuthority(line, Color.White);
		}

		/// <summary>Authority (SP/server): append a follow-channel line for a player and sync to them.</summary>
		public static void AppendFollowForPlayer(int playerWhoAmI, string text, Color color) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			Player player = Main.player[playerWhoAmI];
			if (player == null || !player.active || string.IsNullOrWhiteSpace(text)) {
				return;
			}

			ChatLogPlayer logPlayer = player.GetModPlayer<ChatLogPlayer>();
			// Prefer already-bound client character id; only mint if history request has not run yet.
			string playerId = string.IsNullOrWhiteSpace(logPlayer.PlayerId)
				? logPlayer.EnsurePlayerId()
				: logPlayer.PlayerId;
			ChatLogEntry entry = CreateEntry(text.Trim(), color);

			if (!FollowByPlayerId.TryGetValue(playerId, out List<ChatLogEntry> list)) {
				list = new List<ChatLogEntry>();
				FollowByPlayerId[playerId] = list;
			}

			list.Add(entry);
			while (list.Count > MaxFollowPerPlayer) {
				list.RemoveAt(0);
			}

			if (Main.netMode == NetmodeID.SinglePlayer) {
				bool markUnread = !PlayerChatTabsSystem.IsFollowTabActive || !Main.drawingPlayerChat;
				ChatLogClientBuffer.AddFollowLine(entry, markUnread);
				return;
			}

			// Dedicated / listen server: send to that client (listen host is also a client index).
			SendFollowLineTo(playerWhoAmI, entry);
			if (Main.netMode == NetmodeID.Server && playerWhoAmI == Main.myPlayer && !Main.dedServ) {
				bool markUnread = !PlayerChatTabsSystem.IsFollowTabActive || !Main.drawingPlayerChat;
				ChatLogClientBuffer.AddFollowLine(entry, markUnread);
			}
		}

		/// <summary>MP client optimistic insert before server ack.</summary>
		public static void AddFollowLocalOptimistic(string text, Color color) {
			if (Main.netMode != NetmodeID.MultiplayerClient || string.IsNullOrWhiteSpace(text)) {
				return;
			}

			var entry = new ChatLogEntry {
				Id = 0,
				TimestampUtc = DateTime.UtcNow.Ticks,
				R = color.R,
				G = color.G,
				B = color.B,
				Text = text.Trim()
			};
			bool markUnread = !PlayerChatTabsSystem.IsFollowTabActive || !Main.drawingPlayerChat;
			ChatLogClientBuffer.AddFollowLine(entry, markUnread);
		}

		public static void ReplayLocalHistory(string playerId) {
			if (Main.dedServ) {
				return;
			}

			ReplayPublicToChatMonitor();
			List<ChatLogEntry> follow = GetFollowListCopy(playerId);
			ChatLogClientBuffer.ReplaceFollowHistory(follow);
		}

		private static void ReplayPublicToChatMonitor() {
			if (Main.chatMonitor == null || PublicEntries.Count == 0) {
				return;
			}

			_replayingHistory = true;
			try {
				Main.chatMonitor.Clear();
				foreach (ChatLogEntry entry in PublicEntries) {
					Main.chatMonitor.NewText(entry.Text, entry.R, entry.G, entry.B);
				}
			}
			finally {
				_replayingHistory = false;
			}
		}

		private static List<ChatLogEntry> GetFollowListCopy(string playerId) {
			if (string.IsNullOrWhiteSpace(playerId)
			    || !FollowByPlayerId.TryGetValue(playerId, out List<ChatLogEntry> list)) {
				return new List<ChatLogEntry>();
			}

			return new List<ChatLogEntry>(list);
		}

		public static void RequestHistoryFromServer(string playerId) {
			if (Main.netMode != NetmodeID.MultiplayerClient || string.IsNullOrWhiteSpace(playerId)) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcChatMessageType.ChatHistoryRequest);
			packet.Write(playerId);
			packet.Send();
		}

		public static void HandlePacket(byte type, BinaryReader reader, int whoAmI) {
			switch (type) {
				case (byte)NpcChatMessageType.ChatHistoryRequest:
					HandleHistoryRequest(reader, whoAmI);
					break;
				case (byte)NpcChatMessageType.ChatHistoryChunk:
					HandleHistoryChunk(reader);
					break;
				case (byte)NpcChatMessageType.FollowChatLine:
					HandleFollowChatLine(reader);
					break;
			}
		}

		private static void HandleHistoryRequest(BinaryReader reader, int whoAmI) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			string playerId = reader.ReadString();
			if (string.IsNullOrWhiteSpace(playerId) || whoAmI < 0 || whoAmI >= Main.maxPlayers) {
				return;
			}

			// Always trust the client character id (persisted on the player file).
			Player player = Main.player[whoAmI];
			if (player != null && player.active) {
				BindFollowPlayerId(player.GetModPlayer<ChatLogPlayer>(), playerId);
			}

			SendHistoryTo(whoAmI, playerId);
			// Client OnEnterWorld sends ChatHistoryRequest — treat as "joined world" for tips.
			NpcAiChatSystem.OnPlayerJoinedWorld(whoAmI);
		}

		/// <summary>
		/// Bind server ModPlayer to the client's stable id; merge any lists that were
		/// written under a previously minted server Guid.
		/// </summary>
		private static void BindFollowPlayerId(ChatLogPlayer logPlayer, string clientPlayerId) {
			if (logPlayer == null || string.IsNullOrWhiteSpace(clientPlayerId)) {
				return;
			}

			string oldId = logPlayer.PlayerId;
			logPlayer.PlayerId = clientPlayerId;
			if (string.IsNullOrWhiteSpace(oldId)
			    || string.Equals(oldId, clientPlayerId, StringComparison.Ordinal)) {
				return;
			}

			if (!FollowByPlayerId.TryGetValue(oldId, out List<ChatLogEntry> orphan) || orphan.Count == 0) {
				FollowByPlayerId.Remove(oldId);
				return;
			}

			if (!FollowByPlayerId.TryGetValue(clientPlayerId, out List<ChatLogEntry> dest)) {
				FollowByPlayerId[clientPlayerId] = orphan;
			}
			else {
				dest.AddRange(orphan);
				while (dest.Count > MaxFollowPerPlayer) {
					dest.RemoveAt(0);
				}
			}

			FollowByPlayerId.Remove(oldId);
		}

		private static void SendHistoryTo(int playerWhoAmI, string playerId) {
			List<ChatLogEntry> followList = GetFollowListCopy(playerId);

			if (PublicEntries.Count == 0) {
				SendChunk(
					playerWhoAmI,
					isFollow: false,
					isFirst: true,
					isLast: followList.Count == 0,
					PublicEntries,
					0,
					0);
			}
			else {
				int pubIndex = 0;
				while (pubIndex < PublicEntries.Count) {
					int count = Math.Min(ChunkSize, PublicEntries.Count - pubIndex);
					bool lastPublic = pubIndex + count >= PublicEntries.Count;
					SendChunk(
						playerWhoAmI,
						isFollow: false,
						isFirst: pubIndex == 0,
						isLast: lastPublic && followList.Count == 0,
						PublicEntries,
						pubIndex,
						count);
					pubIndex += count;
				}
			}

			if (followList.Count == 0) {
				return;
			}

			int followIndex = 0;
			while (followIndex < followList.Count) {
				int count = Math.Min(ChunkSize, followList.Count - followIndex);
				bool last = followIndex + count >= followList.Count;
				SendChunk(
					playerWhoAmI,
					isFollow: true,
					isFirst: followIndex == 0,
					isLast: last,
					followList,
					followIndex,
					count);
				followIndex += count;
			}
		}

		private static void SendChunk(int playerWhoAmI, bool isFollow, bool isFirst, bool isLast, List<ChatLogEntry> source, int start, int count) {
			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcChatMessageType.ChatHistoryChunk);
			byte flags = 0;
			if (isFollow) {
				flags |= 1;
			}

			if (isFirst) {
				flags |= 2;
			}

			if (isLast) {
				flags |= 4;
			}

			packet.Write(flags);
			packet.Write((ushort)count);
			for (int i = 0; i < count; i++) {
				ChatLogEntry entry = source[start + i];
				WriteEntry(packet, entry);
			}

			packet.Send(toClient: playerWhoAmI);
		}

		private static void SendFollowLineTo(int playerWhoAmI, ChatLogEntry entry) {
			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcChatMessageType.FollowChatLine);
			WriteEntry(packet, entry);
			packet.Send(toClient: playerWhoAmI);
		}

		private static void WriteEntry(ModPacket packet, ChatLogEntry entry) {
			packet.Write(entry.Id);
			packet.Write(entry.TimestampUtc);
			packet.Write(entry.R);
			packet.Write(entry.G);
			packet.Write(entry.B);
			packet.Write(entry.Text ?? string.Empty);
		}

		private static ChatLogEntry ReadEntry(BinaryReader reader) {
			return new ChatLogEntry {
				Id = reader.ReadInt64(),
				TimestampUtc = reader.ReadInt64(),
				R = reader.ReadByte(),
				G = reader.ReadByte(),
				B = reader.ReadByte(),
				Text = reader.ReadString()
			};
		}

		private static readonly List<ChatLogEntry> PendingPublic = new();
		private static readonly List<ChatLogEntry> PendingFollow = new();

		private static void HandleHistoryChunk(BinaryReader reader) {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			byte flags = reader.ReadByte();
			bool isFollow = (flags & 1) != 0;
			bool isFirst = (flags & 2) != 0;
			bool isLast = (flags & 4) != 0;
			ushort count = reader.ReadUInt16();

			if (isFirst && !isFollow) {
				PendingPublic.Clear();
				PendingFollow.Clear();
				ChatLogClientBuffer.Clear();
			}

			if (isFirst && isFollow) {
				PendingFollow.Clear();
			}

			for (int i = 0; i < count; i++) {
				ChatLogEntry entry = ReadEntry(reader);
				if (isFollow) {
					PendingFollow.Add(entry);
				}
				else {
					PendingPublic.Add(entry);
				}
			}

			if (!isLast) {
				return;
			}

			ApplyPendingHistory();
		}

		private static void ApplyPendingHistory() {
			_replayingHistory = true;
			try {
				if (Main.chatMonitor != null) {
					Main.chatMonitor.Clear();
					foreach (ChatLogEntry entry in PendingPublic) {
						Main.chatMonitor.NewText(entry.Text, entry.R, entry.G, entry.B);
					}
				}

				ChatLogClientBuffer.ReplaceFollowHistory(PendingFollow);
			}
			finally {
				_replayingHistory = false;
				PendingPublic.Clear();
				PendingFollow.Clear();
			}
		}

		private static void HandleFollowChatLine(BinaryReader reader) {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ChatLogEntry entry = ReadEntry(reader);
			bool markUnread = !PlayerChatTabsSystem.IsFollowTabActive || !Main.drawingPlayerChat;
			ChatLogClientBuffer.AddFollowLine(entry, markUnread);
		}
	}
}
