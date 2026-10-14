using AITownNPCs.Common.Equip;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>Subscribes to equip weapon changes and stops the firing dummy.</summary>
	public class NpcDummyLoadoutSystem : ModSystem
	{
		public override void Load() {
			EquippedTownNPC.WeaponChanged += OnWeaponChanged;
		}

		public override void Unload() {
			EquippedTownNPC.WeaponChanged -= OnWeaponChanged;
		}

		private static void OnWeaponChanged(int npcWhoAmI) {
			NpcDummyActor.StopAndRelease(npcWhoAmI);
		}
	}
}
