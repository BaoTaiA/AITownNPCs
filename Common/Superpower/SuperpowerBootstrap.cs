using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower
{
	public class SuperpowerBootstrap : ModSystem
	{
		public override void Load() {
			SuperpowerRegistry.EnsureInitialized();
		}

		public override void OnWorldUnload() {
			SuperpowerCooldownStore.Clear();
			Merchant.MerchantPendingQuoteStore.ClearAll();
			Nurse.NurseEmergencyHealState.ClearAll();
			Cyborg.CyborgBarrageSystem.ClearAll();
			Demolitionist.DemolitionistBarrageSystem.ClearAll();
		}
	}
}
