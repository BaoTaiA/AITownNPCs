using System;
using Terraria;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>Follow lifecycle signals for Chat memory and other subscribers.</summary>
	public static class NpcFollowEvents
	{
		public static event Action<Player, NPC> FollowStarted;
		public static event Action<Player, NPC> FollowStopped;

		public static void RaiseFollowStarted(Player player, NPC npc) {
			FollowStarted?.Invoke(player, npc);
		}

		public static void RaiseFollowStopped(Player player, NPC npc) {
			FollowStopped?.Invoke(player, npc);
		}
	}
}
