using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Host
{
	/// <summary>Player leave / disconnect cleanup across follow, AI, and equip locks.</summary>
	public class NpcPlayerLifecycle : ModSystem
	{
		public static void CleanupPlayer(int playerWhoAmI) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			NpcOwnershipSystem.ReleaseAllEquipLocksForPlayer(playerWhoAmI);

			if (playerWhoAmI >= 0 && playerWhoAmI < Main.maxPlayers) {
				Player player = Main.player[playerWhoAmI];
				var followMp = player.GetModPlayer<NpcFollowPlayer>();
				if (followMp.HasFollower) {
					NPC npc = Main.npc[followMp.FollowingNpcWhoAmI];
					if (npc.active) {
						NpcFollowNetSystem.StopFollow(npc, announce: false);
					}
					else {
						followMp.FollowingNpcWhoAmI = -1;
					}
				}
			}

			NpcAiFacade.InvalidateForPlayer(playerWhoAmI);
		}
	}
}
