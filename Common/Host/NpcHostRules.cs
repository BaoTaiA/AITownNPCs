using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Host
{
	/// <summary>Shared gate for town NPCs this mod may host (equip / follow / combat / chat).</summary>
	public static class NpcHostRules
	{
		/// <summary>
		/// For <see cref="Terraria.ModLoader.GlobalNPC.AppliesToEntity"/> — no <c>active</c> check
		/// so the global can attach at entity creation. Enemies and town pets are excluded.
		/// </summary>
		public static bool AppliesToManagedTownNpc(NPC entity) {
			return entity != null
				&& entity.townNPC
				&& !NPCID.Sets.IsTownPet[entity.type];
		}

		/// <summary>
		/// Town NPCs only. Town pets and enemies are not managed hosts.
		/// </summary>
		public static bool IsManagedTownNpc(NPC npc) {
			if (npc == null || !npc.active) {
				return false;
			}

			return AppliesToManagedTownNpc(npc);
		}
	}
}
