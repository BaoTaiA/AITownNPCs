using AITownNPCs.Common.Host;
using Terraria;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>Read-only follow lookups for Chat and other layers.</summary>
	public static class NpcFollowQuery
	{
		public static NPC FindFollower(Player player) {
			if (player == null || !player.active) {
				return null;
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!NpcHostRules.IsManagedTownNpc(npc)) {
					continue;
				}

				var follow = npc.GetGlobalNPC<FollowingTownNPC>();
				if (follow.IsFollowing && follow.FollowPlayerWhoAmI == player.whoAmI) {
					return npc;
				}
			}

			return null;
		}
	}
}
