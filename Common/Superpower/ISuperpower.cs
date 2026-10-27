using Terraria;

namespace AITownNPCs.Common.Superpower
{
	public interface ISuperpower
	{
		string Id { get; }
		int NpcType { get; }
		int CooldownFrames { get; }
		string DisplayName { get; }
		string Description { get; }
		string RefuseStyle { get; }
		SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent);
	}
}
