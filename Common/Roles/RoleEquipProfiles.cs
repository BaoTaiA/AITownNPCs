using AITownNPCs.Common.Equip;
using Terraria.Localization;

namespace AITownNPCs.Common.Roles
{
	/// <summary>Role-specific equip profile factories (uses RoleItemRules).</summary>
	public static class RoleEquipProfiles
	{
		public static NpcEquipProfile CreateNursePotions() {
			return new NpcEquipProfile {
				CombatEnabled = false,
				Slots = new[] {
					new NpcEquipSlotDef {
						SlotId = NpcEquipSlotId.HealLife,
						LabelKey = "Mods.AITownNPCs.UI.PotionSlotA",
						IsAllowed = (item, _) => RoleItemRules.IsLifePotion(item),
						RejectMessage = _ => Language.GetTextValue("Mods.AITownNPCs.UI.LifePotionOnly")
					},
					new NpcEquipSlotDef {
						SlotId = NpcEquipSlotId.HealMana,
						LabelKey = "Mods.AITownNPCs.UI.PotionSlotB",
						IsAllowed = (item, _) => RoleItemRules.IsManaPotion(item),
						RejectMessage = _ => Language.GetTextValue("Mods.AITownNPCs.UI.ManaPotionOnly")
					},
					NpcEquipProfile.CreateHeartSlot(),
					NpcEquipProfile.CreateWingSlot(NpcEquipSlotId.Wings)
				}
			};
		}

		public static NpcEquipProfile CreateDryadBuffs() {
			// 9 buff slots + Reinforcement Heart (slot index 9) + wings (slot index 10).
			var slots = new NpcEquipSlotDef[11];
			for (int i = 0; i < 9; i++) {
				int n = i + 1;
				slots[i] = new NpcEquipSlotDef {
					SlotId = (NpcEquipSlotId)i,
					LabelKey = $"Mods.AITownNPCs.UI.BuffSlot{n}",
					IsAllowed = (item, _) => RoleItemRules.IsBuffPotion(item),
					RejectMessage = _ => Language.GetTextValue("Mods.AITownNPCs.UI.BuffPotionOnly")
				};
			}

			slots[9] = NpcEquipProfile.CreateHeartSlot(NpcEquipSlotId.Slot9);
			slots[10] = NpcEquipProfile.CreateWingSlot(NpcEquipSlotId.Slot10);

			return new NpcEquipProfile {
				CombatEnabled = false,
				Slots = slots
			};
		}
	}
}
