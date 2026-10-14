using System.Collections.Generic;

namespace AITownNPCs.Common.Equip
{
	/// <summary>Equip-owned profile lookup. Roles register profiles at init. Slot rules may use RoleItemRules.</summary>
	public static class NpcEquipProfileRegistry
	{
		private static readonly Dictionary<int, NpcEquipProfile> Profiles = new();
		private static readonly NpcEquipProfile DefaultProfile = NpcEquipProfile.CreateDefaultWeaponAmmo();

		public static void Register(int npcType, NpcEquipProfile profile) {
			if (profile == null) {
				return;
			}

			Profiles[npcType] = profile;
		}

		public static NpcEquipProfile Get(int npcType) {
			return Profiles.TryGetValue(npcType, out NpcEquipProfile profile) ? profile : DefaultProfile;
		}
	}
}
