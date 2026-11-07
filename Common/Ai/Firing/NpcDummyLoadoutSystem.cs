using AITownNPCs.Common.Equip;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>Subscribes to equip weapon changes and stops the firing dummy.</summary>
	public class NpcDummyLoadoutSystem : ModSystem
	{
		public override void Load() {
			EquippedTownNPC.WeaponChanged += OnWeaponChanged;
			EquippedTownNPC.SummonStaffChanged += OnSummonStaffChanged;
		}

		public override void Unload() {
			EquippedTownNPC.WeaponChanged -= OnWeaponChanged;
			EquippedTownNPC.SummonStaffChanged -= OnSummonStaffChanged;
		}

		private static void OnWeaponChanged(int npcWhoAmI) {
			NpcDummyActor.StopAndRelease(npcWhoAmI, preserveSummons: false);
		}

		private static void OnSummonStaffChanged(int npcWhoAmI) {
			if (npcWhoAmI >= 0 && npcWhoAmI < Terraria.Main.maxNPCs && Terraria.Main.npc[npcWhoAmI].active) {
				Terraria.Main.npc[npcWhoAmI].GetGlobalNPC<NpcFiringState>().TrackedSummonType = -1;
			}

			NpcDummyActor.ClearSummons(npcWhoAmI);
		}
	}
}
