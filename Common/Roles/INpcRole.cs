using AITownNPCs.Common.Equip;
using Terraria;

namespace AITownNPCs.Common.Roles
{
	public interface INpcRole
	{
		int NpcType { get; }
		NpcEquipProfile EquipProfile { get; }
		void Update(NPC npc);
		bool IsBusy(NPC npc);
	}
}
