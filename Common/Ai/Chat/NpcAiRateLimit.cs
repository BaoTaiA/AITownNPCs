using System.Collections.Generic;
using Terraria;

namespace AITownNPCs.Common.Ai.Chat
{
	/// <summary>
	/// Per-player sliding window for player-initiated AI chat (not proactive).
	/// </summary>
	public static class NpcAiRateLimit
	{
		public const int WindowTicks = 60 * 10;
		public const int MaxEvents = 3;

		private static readonly Dictionary<int, Queue<uint>> ByPlayer = new();

		/// <summary>Returns false when limited; does not record. Call Record after a request is accepted.</summary>
		public static bool TryAllow(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return false;
			}

			uint now = Main.GameUpdateCount;
			if (!ByPlayer.TryGetValue(playerWhoAmI, out Queue<uint> q)) {
				return true;
			}

			Prune(q, now);
			return q.Count < MaxEvents;
		}

		public static void Record(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			uint now = Main.GameUpdateCount;
			if (!ByPlayer.TryGetValue(playerWhoAmI, out Queue<uint> q)) {
				q = new Queue<uint>();
				ByPlayer[playerWhoAmI] = q;
			}

			Prune(q, now);
			if (q.Count < MaxEvents) {
				q.Enqueue(now);
			}
		}

		public static void Clear() => ByPlayer.Clear();

		public static void ClearPlayer(int playerWhoAmI) => ByPlayer.Remove(playerWhoAmI);

		private static void Prune(Queue<uint> q, uint now) {
			while (q.Count > 0) {
				uint t = q.Peek();
				if (now - t < WindowTicks) {
					break;
				}

				q.Dequeue();
			}
		}
	}
}
