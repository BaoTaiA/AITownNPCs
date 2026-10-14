using System.Collections.Generic;
using AITownNPCs.Common.Configs;

namespace AITownNPCs.Common.Ai.Chat
{
	public readonly struct ChatTurn
	{
		public readonly string Role;
		public readonly string Content;

		public ChatTurn(string role, string content) {
			Role = role;
			Content = content;
		}

		public bool IsEvent => Role == "event";
	}

	/// <summary>
	/// In-memory sliding-window short-term memory per (playerKey, npcKey).
	/// Dialogue turns and event notes share one cap; trim dialogue before events.
	/// </summary>
	public static class AiConversationMemory
	{
		private static readonly Dictionary<string, List<ChatTurn>> Sessions = new();

		public static string MakeKey(string playerKey, string npcKey) => playerKey + "|" + npcKey;

		public static IReadOnlyList<ChatTurn> GetTurns(string sessionKey) {
			if (!Sessions.TryGetValue(sessionKey, out List<ChatTurn> list)) {
				return System.Array.Empty<ChatTurn>();
			}

			return list;
		}

		public static void Append(string sessionKey, string role, string content) {
			if (string.IsNullOrWhiteSpace(content)) {
				return;
			}

			if (!Sessions.TryGetValue(sessionKey, out List<ChatTurn> list)) {
				list = new List<ChatTurn>();
				Sessions[sessionKey] = list;
			}

			list.Add(new ChatTurn(role, content.Trim()));
			TrimToCap(list);
		}

		public static void AppendEvent(string sessionKey, string note) => Append(sessionKey, "event", note);

		public static void AppendEvent(Terraria.Player player, Terraria.NPC npc, string note) {
			if (player == null || npc == null || !npc.active) {
				return;
			}

			AppendEvent(MakeKey(AiMemoryStore.PlayerKey(player), AiMemoryStore.NpcKey(npc)), note);
		}

		public static void ClearAll() {
			Sessions.Clear();
		}

		public static int ConfiguredMaxEntries() {
			AinpcServerConfig config = AinpcServerConfig.Instance;
			return config?.AiShortTermTurns ?? 20;
		}

		/// <summary>Drop oldest dialogue first; only then drop oldest events.</summary>
		private static void TrimToCap(List<ChatTurn> list) {
			int max = System.Math.Max(2, ConfiguredMaxEntries());
			while (list.Count > max) {
				int drop = IndexOfOldestDialogue(list);
				if (drop < 0) {
					drop = 0;
				}

				list.RemoveAt(drop);
			}
		}

		private static int IndexOfOldestDialogue(List<ChatTurn> list) {
			for (int i = 0; i < list.Count; i++) {
				if (!list[i].IsEvent) {
					return i;
				}
			}

			return -1;
		}
	}
}
