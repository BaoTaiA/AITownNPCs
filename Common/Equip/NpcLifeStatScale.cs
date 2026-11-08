using System;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Host;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Equip
{
	/// <summary>
	/// Combat bonuses from current <see cref="NPC.lifeMax"/> (not heart-item stacks).
	/// Peak planned around typical town base 250 + 20×500 reinforcement hearts = 10250.
	/// </summary>
	public class NpcLifeStatScale : GlobalNPC
	{
		public const int RefBaseLife = 250;
		public const int RefPeakLife = 10250;

		public const int MaxAttackBonus = 100;
		public const int MaxDefenseBonus = 150;
		public const float MaxCritChanceAdd = 50f;
		/// <summary>lifeRegen units; 2 ≈ 1 HP/s → peak ~30 HP/s.</summary>
		public const int MaxLifeRegenBonus = 60;
		public const int MaxMinions = 10;
		public const int MaxTurrets = 8;
		public const int MinMinions = 1;
		public const int MinTurrets = 1;

		public override bool InstancePerEntity => false;

		public override void Load() {
			NpcCombatHooks.RegisterCritChanceAdd(ReadCritChanceAdd);
		}

		public override void Unload() {
			NpcCombatHooks.UnregisterCritChanceAdd(ReadCritChanceAdd);
		}

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
			return entity.townNPC;
		}

		/// <summary>0–1 progress from RefBaseLife to RefPeakLife.</summary>
		public static float GetProgress(NPC npc) {
			if (npc == null || !npc.active) {
				return 0f;
			}

			return GetProgress(npc.lifeMax);
		}

		public static float GetProgress(int lifeMax) {
			float span = RefPeakLife - RefBaseLife;
			if (span <= 0f) {
				return 0f;
			}

			return Math.Clamp((lifeMax - RefBaseLife) / span, 0f, 1f);
		}

		public static int GetAttackBonus(NPC npc) {
			return (int)Math.Round(MaxAttackBonus * GetProgress(npc));
		}

		public static int GetDefenseBonus(NPC npc) {
			return (int)Math.Round(MaxDefenseBonus * GetProgress(npc));
		}

		public static float GetCritChanceAdd(NPC npc) {
			return MaxCritChanceAdd * GetProgress(npc);
		}

		public static int GetLifeRegenBonus(NPC npc) {
			return (int)Math.Round(MaxLifeRegenBonus * GetProgress(npc));
		}

		public static int GetMaxMinions(NPC npc) {
			float t = GetProgress(npc);
			return Math.Max(MinMinions, (int)Math.Round(MinMinions + (MaxMinions - MinMinions) * t));
		}

		public static int GetMaxTurrets(NPC npc) {
			float t = GetProgress(npc);
			return Math.Max(MinTurrets, (int)Math.Round(MinTurrets + (MaxTurrets - MinTurrets) * t));
		}

		private static float ReadCritChanceAdd(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 0f;
			}

			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return 0f;
			}

			return GetCritChanceAdd(npc);
		}
	}
}
