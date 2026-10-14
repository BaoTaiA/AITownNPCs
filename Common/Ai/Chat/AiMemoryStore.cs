using System.IO;
using Terraria;

namespace AITownNPCs.Common.Ai.Chat
{
	/// <summary>Stable keys for short-term conversation memory (no disk persistence).</summary>
	public static class AiMemoryStore
	{
		public static string PlayerKey(Player player) {
			return Sanitize(player?.name ?? "unknown");
		}

		public static string NpcKey(NPC npc) {
			string name = string.IsNullOrWhiteSpace(npc.GivenName) ? "noname" : npc.GivenName;
			return Sanitize(npc.type + "_" + name);
		}

		private static string Sanitize(string s) {
			foreach (char c in Path.GetInvalidFileNameChars()) {
				s = s.Replace(c, '_');
			}

			return string.IsNullOrWhiteSpace(s) ? "unknown" : s.Trim();
		}
	}
}
