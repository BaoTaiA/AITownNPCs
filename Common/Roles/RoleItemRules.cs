using AITownNPCs.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Roles
{
	/// <summary>Role-specific item predicates for equip profiles (potions, etc.).</summary>
	public static class RoleItemRules
	{
		/// <summary>Heart slot: Reinforcement Heart or Life Crystal, stack 1–20.</summary>
		public static bool IsHeartSlotItem(Item item) {
			if (item == null || item.IsAir) {
				return false;
			}

			if (item.stack < 1 || item.stack > ReinforcementHeart.MaxEquipStack) {
				return false;
			}

			return item.type == ModContent.ItemType<ReinforcementHeart>()
				|| item.type == ItemID.LifeCrystal;
		}

		/// <summary>Legacy name for heart-slot items (Reinforcement Heart or Life Crystal).</summary>
		public static bool IsReinforcementHeart(Item item) => IsHeartSlotItem(item);

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
