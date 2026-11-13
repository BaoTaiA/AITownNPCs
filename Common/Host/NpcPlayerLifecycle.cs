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

			// Do not StopFollow immediately — brief disconnect/telnet flicker used to clear
			// follow with announce:false. PostUpdateNPCs drops follow after OwnerInactiveGraceTicks.
			if (playerWhoAmI >= 0 && playerWhoAmI < Main.maxPlayers) {
				Player player = Main.player[playerWhoAmI];
				var followMp = player.GetModPlayer<NpcFollowPlayer>();
				if (followMp.HasFollower) {
					NPC npc = Main.npc[followMp.FollowingNpcWhoAmI];
					if (NpcHostRules.IsManagedTownNpc(npc)) {
						FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
						if (follow.OwnerInactiveGrace == 0) {
							ModContent.GetInstance<AITownNPCs>()?.Logger.Info(
								$"Follow disconnect grace start npc={npc.whoAmI} owner={playerWhoAmI}");
						}
					}
				}
			}

			NpcAiFacade.InvalidateForPlayer(playerWhoAmI);
		}
	}
}
