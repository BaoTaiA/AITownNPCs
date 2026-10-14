using System;
using Terraria;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>
	/// Follow owns chat hooks/keywords; Chat registers AI routing and @-hide policy here.
	/// </summary>
	public static class NpcPlayerChatIngress
	{
		/// <summary>Non-follow-keyword chat after Follow handled start/stop. Args: playerWhoAmI, trimmed text.</summary>
		public static Action<int, string> OnNonKeywordChat;

		/// <summary>
		/// Optional: whether @-addressed text targets the given follower (for hide-from-chat-box).
		/// Args: trimmed text, follower NPC. Null = only bare @ hides.
		/// </summary>
		public static Func<string, NPC, bool> IsAddressedToFollower;

		public static void RaiseNonKeywordChat(int playerWhoAmI, string trimmed) {
			OnNonKeywordChat?.Invoke(playerWhoAmI, trimmed);
		}

		public static bool CheckAddressedToFollower(string trimmed, NPC follower) {
			if (follower == null || !follower.active) {
				return false;
			}

			if (IsAddressedToFollower != null) {
				return IsAddressedToFollower(trimmed, follower);
			}

			return trimmed.StartsWith("@", StringComparison.Ordinal);
		}
	}
}
