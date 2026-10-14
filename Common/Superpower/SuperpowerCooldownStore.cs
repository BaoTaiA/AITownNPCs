using System.Collections.Generic;
using Terraria;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerCooldownStore
	{
		private static readonly Dictionary<string, uint> UntilTick = new();

		private static string Key(int playerWho, int npcType, string abilityId) =>
			playerWho + "|" + npcType + "|" + abilityId;

		public static bool IsOnCooldown(int playerWho, int npcType, string abilityId, out int remainFrames) {
			remainFrames = 0;
			string key = Key(playerWho, npcType, abilityId);
			if (!UntilTick.TryGetValue(key, out uint until)) {
				return false;
			}

			uint now = Main.GameUpdateCount;
			if (now >= until) {
				UntilTick.Remove(key);
				return false;
			}

			remainFrames = (int)(until - now);
			return true;
		}

		public static void Start(int playerWho, int npcType, string abilityId, int frames) {
			if (frames <= 0) {
				return;
			}

			UntilTick[Key(playerWho, npcType, abilityId)] = Main.GameUpdateCount + (uint)frames;
		}

		public static void Clear() => UntilTick.Clear();

		public static void ClearPlayer(int playerWho) {
			var remove = new List<string>();
			string prefix = playerWho + "|";
			foreach (string key in UntilTick.Keys) {
				if (key.StartsWith(prefix)) {
					remove.Add(key);
				}
			}

			foreach (string key in remove) {
				UntilTick.Remove(key);
			}
		}
	}
}
