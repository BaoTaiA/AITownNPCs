using Terraria.ModLoader;

namespace AITownNPCs.Common.Roles
{
	/// <summary>Registers role equip profiles into Equip registry before UI/net touch slots.</summary>
	public class NpcRoleBootstrap : ModSystem
	{
		public override void Load() {
			NpcRoleRegistry.EnsureInitialized();
		}
	}
}
