using Terraria;

namespace AITownNPCs.Common.Superpower
{
	public interface ISuperpower
	{
		string Id { get; }
		int NpcType { get; }
		int CooldownFrames { get; }
		string DisplayNameZh { get; }
		string DescriptionZh { get; }
		string RefuseStyleZh { get; }
		SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent);
	}
}
