using System.Collections.Generic;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Roles.Dryad;
using AITownNPCs.Common.Roles.Nurse;
using Terraria;

namespace AITownNPCs.Common.Roles
{
	public static class NpcRoleRegistry
	{
		private static readonly Dictionary<int, INpcRole> Roles = new();
		private static bool _initialized;

		public static void EnsureInitialized() {
			if (_initialized) {
				return;
			}

			Register(new NurseRole());
			Register(new DryadRole());
			_initialized = true;
		}

		public static void Register(INpcRole role) {
			Roles[role.NpcType] = role;
			NpcEquipProfileRegistry.Register(role.NpcType, role.EquipProfile);
		}

		public static INpcRole Get(int npcType) {
			EnsureInitialized();
			return Roles.TryGetValue(npcType, out INpcRole role) ? role : null;
		}

		public static void Update(NPC npc) {
			Get(npc.type)?.Update(npc);
		}

		public static bool IsBusy(NPC npc) {
			INpcRole role = Get(npc.type);
			return role != null && role.IsBusy(npc);
		}
	}
}
