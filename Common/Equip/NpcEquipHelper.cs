using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Host;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Equip
{
	public static class NpcEquipHelper
	{
		public const float InteractDistance = 200f;
		public const float AttackRange = 700f;
		public const float MeleeEngageRange = 112f;
		public const float MeleeProjectileRange = 400f;
		public const float FacingDeadzone = 16f;

		public static bool IsAllowedWeapon(Item item) {
			return item != null && !item.IsAir && item.damage > 0;
		}

		public static float GetEngageRange(Item weapon) {
			WeaponStance stance = NpcWeaponStance.Resolve(weapon);
			return stance.Band switch {
				StanceBand.MeleeHug => MeleeEngageRange,
				StanceBand.MeleeReach => MeleeProjectileRange,
				_ => System.Math.Max(stance.IdealDistance * 1.15f, AttackRange * 0.8f)
			};
		}

		public static string GetWeaponRejectedMessage(Item item) {
			return Language.GetTextValue("Mods.AITownNPCs.UI.WeaponNotSupported");
		}

		public static bool IsAllowedAmmo(Item item, Item weapon) {
			if (item == null || item.IsAir) {
				return false;
			}

			if (item.ammo == AmmoID.None) {
				return false;
			}

			if (weapon == null || weapon.IsAir || weapon.useAmmo == AmmoID.None) {
				return true;
			}

			return item.ammo == weapon.useAmmo;
		}

		public static string EquipButtonText => Language.GetTextValue("Mods.AITownNPCs.UI.Equip");

		public static bool IsPlayerNearNpc(Player player, NPC npc) {
			return player != null && NpcHostRules.IsManagedTownNpc(npc)
				&& player.Distance(npc.Center) <= InteractDistance * 1.5f;
		}
	}
}
