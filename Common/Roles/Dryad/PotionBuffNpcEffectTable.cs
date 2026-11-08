using Microsoft.Xna.Framework;
using Terraria.ID;

namespace AITownNPCs.Common.Roles.Dryad
{
	/// <summary>
	/// NPC-body effects from Dryad aura only (defense/regen/move/lifeMax/light/thorns/lava/minion slots).
	/// Outgoing damage/crit/flask go on the combat dummy via <see cref="DummySyncedBuffTypes"/>.
	/// </summary>
	public struct PotionBuffNpcEffect
	{
		public int DefenseAdd;
		public int LifeRegenAdd;
		public float DamageTakenMult; // 1 = none; 0.9 = Endurance
		public float MoveSpeedMult;   // 1 = none; 1.25 = Swiftness
		public bool Lifeforce;
		public Vector3 LightRgb;
		public bool LavaImmune;
		public float ThornsFraction;
		public int MinionSlotAdd;

		public static PotionBuffNpcEffect Empty => new() {
			DamageTakenMult = 1f,
			MoveSpeedMult = 1f,
			LightRgb = Vector3.Zero
		};
	}

	public static class PotionBuffNpcEffectTable
	{
		/// <summary>
		/// Buffs mirrored onto the combat dummy so vanilla Player.UpdateBuffs applies them.
		/// Cleared then re-added each sync so switching NPCs cannot leave sticky effects.
		/// </summary>
		public static readonly int[] DummySyncedBuffTypes = {
			BuffID.Wrath,
			BuffID.Rage,
			BuffID.MagicPower,
			BuffID.Archery,
			BuffID.Summoning,
			BuffID.Bewitched,
			BuffID.Ironskin,
			BuffID.WeaponImbuePoison,
			BuffID.WeaponImbueFire,
			BuffID.WeaponImbueGold,
			BuffID.WeaponImbueIchor,
			BuffID.WeaponImbueCursedFlames,
			BuffID.WeaponImbueNanites,
			BuffID.WeaponImbueConfetti,
			BuffID.WeaponImbueVenom
		};

		public static bool ShouldSyncToDummy(int buffType) {
			for (int i = 0; i < DummySyncedBuffTypes.Length; i++) {
				if (DummySyncedBuffTypes[i] == buffType) {
					return true;
				}
			}

			return false;
		}

		public static bool TryGet(int buffType, out PotionBuffNpcEffect effect) {
			effect = PotionBuffNpcEffect.Empty;
			switch (buffType) {
				case BuffID.Ironskin:
					effect.DefenseAdd = 8;
					return true;
				case BuffID.Regeneration:
					effect.LifeRegenAdd = 4;
					return true;
				case BuffID.Endurance:
					effect.DamageTakenMult = 0.9f;
					return true;
				case BuffID.Swiftness:
					effect.MoveSpeedMult = 1.25f;
					return true;
				case BuffID.Lifeforce:
					effect.Lifeforce = true;
					return true;
				case BuffID.Shine:
					effect.LightRgb = new Vector3(0.8f, 0.95f, 1f);
					return true;
				case BuffID.NightOwl:
					effect.LightRgb = new Vector3(0.15f, 0.15f, 0.2f);
					return true;
				case BuffID.Thorns:
					effect.ThornsFraction = 1f;
					return true;
				case BuffID.ObsidianSkin:
					effect.LavaImmune = true;
					return true;
				case BuffID.Summoning:
				case BuffID.Bewitched:
					effect.MinionSlotAdd = 1;
					return true;
				default:
					return false;
			}
		}

		/// <summary>Vanilla Lifeforce: base + floor(floor(base/5)/20)*20</summary>
		public static int ApplyLifeforceLifeMax(int baseLifeMax) {
			if (baseLifeMax <= 0) {
				return baseLifeMax;
			}

			return baseLifeMax + baseLifeMax / 5 / 20 * 20;
		}
	}
}
