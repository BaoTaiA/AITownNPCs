using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Combat.Stance
{
	public enum StanceBand : byte
	{
		MeleeHug = 0,
		MeleeReach = 1,
		RangedLine = 2,
		MagicArc = 3,
	}

	public readonly struct WeaponStance
	{
		public StanceBand Band { get; }
		public float IdealDistance { get; }
		public float AimElevation { get; }
		public bool PreferMeleeHug { get; }

		/// <summary>Soft max fire/search distance (≈1.35× ideal).</summary>
		public float MaxFireDistance => IdealDistance * 1.35f;

		public WeaponStance(StanceBand band, float idealDistance, float aimElevation, bool preferMeleeHug) {
			Band = band;
			IdealDistance = idealDistance;
			AimElevation = aimElevation;
			PreferMeleeHug = preferMeleeHug;
		}
	}

	/// <summary>Weapon class distance band + aim elevation (no per-weapon table).</summary>
	public static class NpcWeaponStance
	{
		public static WeaponStance Resolve(Item weapon) {
			if (weapon == null || weapon.IsAir) {
				return new WeaponStance(StanceBand.RangedLine, 480f, 0f, false);
			}

			if (weapon.CountsAsClass(DamageClass.SummonMeleeSpeed)) {
				return new WeaponStance(StanceBand.MeleeReach, 220f, 0f, false);
			}

			bool melee = weapon.CountsAsClass(DamageClass.Melee);
			if (melee && weapon.shoot <= ProjectileID.None && !weapon.noMelee) {
				return new WeaponStance(StanceBand.MeleeHug, 80f, 0f, true);
			}

			if (melee && weapon.shoot > ProjectileID.None) {
				return new WeaponStance(StanceBand.MeleeReach, 230f, 0f, false);
			}

			if (weapon.CountsAsClass(DamageClass.Magic) && weapon.shootSpeed > 0f && weapon.shootSpeed <= 12f) {
				return new WeaponStance(StanceBand.MagicArc, 400f, 0.25f, false);
			}

			if (weapon.CountsAsClass(DamageClass.Magic)) {
				return new WeaponStance(StanceBand.RangedLine, 420f, 0f, false);
			}

			return new WeaponStance(StanceBand.RangedLine, 480f, 0f, false);
		}
	}
}
