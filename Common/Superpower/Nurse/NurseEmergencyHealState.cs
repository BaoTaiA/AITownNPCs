using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.Nurse
{
	public class NurseEmergencyHealState : ModSystem
	{
		public static bool IsBusy(int npcWhoAmI) => NpcVisitState.IsStaying(npcWhoAmI);

		public static void ClearAll() => NpcVisitState.ClearAll();

		public static bool Start(NPC npc, Player player) {
			if (!NpcVisitState.TryBegin(npc, player, NpcVisitState.StayTicks)) {
				return false;
			}

			HealPlayer(player);
			return true;
		}

		public static void HealPlayer(Player player) {
			ServerPlayerNet.FullHealSynced(player);
			ServerPlayerNet.AddBuffSynced(
				player,
				ModContent.BuffType<SuperHeartBuff>(),
				SuperHeartBuff.DurationTicks);
		}
	}
}
