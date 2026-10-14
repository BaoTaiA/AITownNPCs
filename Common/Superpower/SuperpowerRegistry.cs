using System.Collections.Generic;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerRegistry
	{
		private static readonly Dictionary<int, List<ISuperpower>> ByNpc = new();
		private static readonly Dictionary<string, ISuperpower> ById = new();
		private static bool _ready;

		public static void EnsureInitialized() {
			if (_ready) {
				return;
			}

			RegisterAll();
			_ready = true;
		}

		public static void Register(ISuperpower power) {
			if (power == null) {
				return;
			}

			ById[power.Id] = power;
			if (!ByNpc.TryGetValue(power.NpcType, out List<ISuperpower> list)) {
				list = new List<ISuperpower>();
				ByNpc[power.NpcType] = list;
			}

			list.Add(power);
		}

		public static bool HasPowers(int npcType) =>
			ByNpc.TryGetValue(npcType, out List<ISuperpower> list) && list.Count > 0;

		public static IReadOnlyList<ISuperpower> GetPowers(int npcType) {
			if (ByNpc.TryGetValue(npcType, out List<ISuperpower> list)) {
				return list;
			}

			return System.Array.Empty<ISuperpower>();
		}

		public static bool TryGet(string abilityId, out ISuperpower power) =>
			ById.TryGetValue(abilityId, out power);

		public static bool IsAbilityForNpc(int npcType, string abilityId) {
			if (!ByNpc.TryGetValue(npcType, out List<ISuperpower> list)) {
				return false;
			}

			foreach (ISuperpower p in list) {
				if (p.Id == abilityId) {
					return true;
				}
			}

			return false;
		}

		private static void RegisterAll() {
			ByNpc.Clear();
			ById.Clear();
			Register(new Guide.GuideWeatherPower());
			Register(new Guide.GuideClearItemsPower());
			Register(new Guide.GuideWorldQueryPower());
			Register(new Nurse.NurseEmergencyHealPower());
			Register(new ArmsDealer.ArmsDealerTempWeaponPower());
			Register(new Merchant.MerchantBuyTrashPower());
			Register(new Merchant.MerchantEmergencyDeliveryPower());
			Register(new Demolitionist.DemolitionistBlastPower());
			Register(new Demolitionist.DemolitionistFireSupportPower());
			Register(new GoblinTinkerer.GoblinPrefixPower());
			Register(new Cyborg.CyborgFireSupportPower());
			Register(new TaxCollector.TaxCollectorGrantGoldPower());
		}
	}
}
