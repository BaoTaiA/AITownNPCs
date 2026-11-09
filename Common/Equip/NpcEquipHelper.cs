using AITownNPCs.Common.Ai.Firing;
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
			if (item == null || item.IsAir || IsSummonOrSentryStaff(item)) {
				return false;
			}

			if (item.damage > 0) {
				// Consumable throwers: vanilla list or Thorium SafeThrow (Palico).
				if (item.consumable && item.shoot > ProjectileID.None) {
					return IsVanillaSafeThrow(item) || NpcThoriumWeaponBridge.IsSafeThrow(item);
				}

				return true;
			}

			return NpcHealerSupport.IsTool(item);
		}

		private static bool IsVanillaSafeThrow(Item i) =>
			i.type is ItemID.Shuriken or ItemID.ThrowingKnife or ItemID.PoisonedKnife or ItemID.Snowball
				or ItemID.SpikyBall or ItemID.Grenade or ItemID.StickyGrenade or ItemID.BouncyGrenade
				or ItemID.Beenade or ItemID.FrostDaggerfish or ItemID.Javelin or ItemID.BoneJavelin
				or ItemID.MolotovCocktail or ItemID.RottenEgg or ItemID.Bone;

		/// <summary>Summon staff (minion projectile) or sentry staff. Whips stay main-hand weapons.</summary>
		public static bool IsSummonOrSentryStaff(Item item) {
			return IsMinionStaff(item) || IsSentryStaff(item);
		}

		public static bool IsMinionStaff(Item item) {
			if (item == null || item.IsAir) {
				return false;
			}

			// SOTS void summons (Nature Spirit etc.) may use void capacity instead of
			// (or in addition to) minion slots — still belong in the summon slot.
			if (NpcDummyModResources.IsVoidSummon(item)) {
				return true;
			}

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

		public static string StatsButtonText => Language.GetTextValue("Mods.AITownNPCs.UI.Stats");

		public static bool IsPlayerNearNpc(Player player, NPC npc) {
			return player != null && NpcHostRules.IsManagedTownNpc(npc)
				&& player.Distance(npc.Center) <= InteractDistance * 1.5f;
		}
	}
}
