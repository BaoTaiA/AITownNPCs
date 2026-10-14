using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Follow
{
	public class NpcFollowPlayer : ModPlayer
	{
		/// <summary>NPC currently following this player; -1 if none.</summary>
		public int FollowingNpcWhoAmI = -1;

		public bool HasFollower =>
			FollowingNpcWhoAmI >= 0
			&& FollowingNpcWhoAmI < Main.maxNPCs
			&& Main.npc[FollowingNpcWhoAmI].active
			&& Main.npc[FollowingNpcWhoAmI].GetGlobalNPC<FollowingTownNPC>().FollowPlayerWhoAmI == Player.whoAmI;

		public override void OnEnterWorld() {
			FollowingNpcWhoAmI = -1;
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) {
				NpcFollowNetSystem.RequestSyncAll();
			}
		}
	}
}
