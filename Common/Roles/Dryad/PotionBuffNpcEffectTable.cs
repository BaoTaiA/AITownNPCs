using Microsoft.Xna.Framework;
using Terraria.ID;

namespace AITownNPCs.Common.Roles.Dryad
{
	/// <summary>
	/// Dryad-aura-only NPC settlement table. Stays under Roles/Dryad (not global).
	/// Numbers match Player.UpdateBuffs / wiki; player-only stats are omitted.
	/// </summary>
	public struct PotionBuffNpcEffect
	{
		public int DefenseAdd;
		public int LifeRegenAdd;
		public float DamageTakenMult; // 1 = none; 0.9 = Endurance
		public float MoveSpeedMult;   // 1 = none; 1.25 = Swiftness
		public bool Lifeforce;        // + floor(floor(base/5)/20)*20 lifeMax
		public Vector3 LightRgb;      // zero = no light
		/// <summary>Wrath = all weapons; MagicPower/Archery also use this field but TownNpcAuraBuffs splits by buff id.</summary>
		public float OutgoingDamageMult;
		public float CritChanceAdd;   // 0-100 style; Rage = 10
		public bool LavaImmune;       // Obsidian Skin: lava + OnFire immune settle
		public float ThornsFraction;  // 1 = Thorns potion

		public static PotionBuffNpcEffect Empty => new() {
			DamageTakenMult = 1f,
			MoveSpeedMult = 1f,
			OutgoingDamageMult = 1f,
			LightRgb = Vector3.Zero
		};
	}

	public static class PotionBuffNpcEffectTable
	{
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
					// Same ballpark as player shine aura.
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
				case BuffID.Wrath:
					effect.OutgoingDamageMult = 1.1f;
					return true;
				case BuffID.Rage:
					effect.CritChanceAdd = 10f;
					return true;
				case BuffID.MagicPower:
					effect.OutgoingDamageMult = 1.2f;
					return true;
				case BuffID.Archery:
					effect.OutgoingDamageMult = 1.1f;
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
