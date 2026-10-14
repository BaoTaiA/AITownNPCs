using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Host;
using Microsoft.Xna.Framework;
using Terraria;

namespace AITownNPCs.Common.Ai.Chat
{
	/// <summary>
	/// Shared dialogue rules: ranges, cooldowns, who can be addressed by distance.
	/// </summary>
	public static class NpcDialoguePolicy
	{
		public const float WalkByRange = 50f;
		public const int WalkByStayTicks = 60 * 10;
		public const int WalkByCooldownTicks = 60 * 600;
		public const int FollowerProactiveMinTicks = 60 * 7;
		public const int FollowerProactiveMaxTicks = 60 * 15;

		public static float ChatRange() =>
			AinpcServerConfig.Instance?.FollowTriggerDistancePixels ?? 200f;

		public static bool IsFollowingAnyone(NPC npc) {
			if (npc == null || !npc.active) {
				return false;
			}

			return npc.GetGlobalNPC<FollowingTownNPC>().IsFollowing;
		}

		public static bool IsDistanceChatCandidate(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return false;
			}

			return !IsFollowingAnyone(npc);
		}

		public static bool ShouldMentionPlayer(NPC npc, float range = -1f) {
			if (npc == null || !npc.active) {
				return false;
			}

			if (range < 0f) {
				range = ChatRange();
			}

			int count = 0;
			for (int p = 0; p < Main.maxPlayers; p++) {
				Player other = Main.player[p];
				if (other == null || !other.active || other.dead) {
					continue;
				}

				if (Vector2.Distance(other.Center, npc.Center) < range) {
					count++;
					if (count >= 2) {
						return true;
					}
				}
			}

			return false;
		}

		public static int RollFollowerProactiveDelayTicks() {
			int min = FollowerProactiveMinTicks;
			int max = FollowerProactiveMaxTicks;
			if (max < min) {
				max = min;
			}

			return Main.rand.Next(min, max + 1);
		}
	}
}
