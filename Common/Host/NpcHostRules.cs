using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Host
{
	/// <summary>Shared gate for town NPCs this mod may host (equip / follow / combat / chat).</summary>
	public static class NpcHostRules
	{
		/// <summary>
		/// Town NPCs only. Town pets and enemies are not managed hosts.
		/// </summary>
		public static bool IsManagedTownNpc(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return false;
			}

			return !NPCID.Sets.IsTownPet[npc.type];
		}
	}
}
