using AITownNPCs.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Roles
{
	/// <summary>Role-specific item predicates for equip profiles (potions, etc.).</summary>
	public static class RoleItemRules
	{
		/// <summary>Reinforcement Heart slot: only that item, stack 1–10.</summary>
		public static bool IsReinforcementHeart(Item item) {
			if (item == null || item.IsAir) {
				return false;
			}

			return item.type == ModContent.ItemType<ReinforcementHeart>()
				&& item.stack >= 1
				&& item.stack <= ReinforcementHeart.MaxEquipStack;
		}

		/// <summary>Nurse HP slot: life potions (combo heal allowed).</summary>
		public static bool IsLifePotion(Item item) {
			return item != null && !item.IsAir && item.healLife > 0;
		}

		/// <summary>Nurse MP slot: pure mana potions only.</summary>
		public static bool IsManaPotion(Item item) {
			return item != null && !item.IsAir && item.healMana > 0 && item.healLife <= 0;
		}

		/// <summary>Dryad aura loadout: buff potions only (no food).</summary>
		public static bool IsBuffPotion(Item item) {
			if (item == null || item.IsAir || item.buffType <= 0) {
				return false;
			}

			return !ItemID.Sets.IsFood[item.type];
		}
	}
}
