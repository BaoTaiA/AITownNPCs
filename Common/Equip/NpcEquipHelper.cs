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
		public const float FacingDeadzone = 16f;

		public static bool IsAllowedWeapon(Item item) {
			return item != null && !item.IsAir && item.damage > 0 && !IsSummonOrSentryStaff(item);
		}

		/// <summary>Summon staff (minion projectile) or sentry staff. Whips stay main-hand weapons.</summary>
		public static bool IsSummonOrSentryStaff(Item item) {
			return IsMinionStaff(item) || IsSentryStaff(item);
		}

		public static bool IsMinionStaff(Item item) {
			Projectile shot = SampleShot(item);
			return shot != null && shot.minion && !shot.sentry;
		}

		public static bool IsSentryStaff(Item item) {
			if (item == null || item.IsAir) {
				return false;
			}

			if (item.sentry) {
				return true;
			}

			Projectile shot = SampleShot(item);
			return shot != null && shot.sentry;
		}

		public static bool IsAllowedSummonOrSentry(Item item) {
			return IsSummonOrSentryStaff(item);
		}

		public static bool IsAllowedWing(Item item) {
			return item != null && !item.IsAir && item.wingSlot > 0;
		}

		public static string GetWeaponRejectedMessage(Item item) {
			if (IsSummonOrSentryStaff(item)) {
				return Language.GetTextValue("Mods.AITownNPCs.UI.SummonNotMain");
			}

			return Language.GetTextValue("Mods.AITownNPCs.UI.WeaponNotSupported");
		}

		private static Projectile SampleShot(Item item) {
			if (item == null || item.IsAir || item.shoot <= ProjectileID.None) {
				return null;
			}

			return ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile shot) ? shot : null;
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
