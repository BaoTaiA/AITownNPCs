using System;
using AITownNPCs.Common.Roles;
using Terraria;
using Terraria.Localization;

namespace AITownNPCs.Common.Equip
{
	public sealed class NpcEquipSlotDef
	{
		public NpcEquipSlotId SlotId { get; init; }
		public string LabelKey { get; init; }
		public Func<Item, Item[], bool> IsAllowed { get; init; }
		public Func<Item, string> RejectMessage { get; init; }

		public string Label => Language.GetTextValue(LabelKey);
	}

	public sealed class NpcEquipProfile
	{
		public bool CombatEnabled { get; init; } = true;
		public NpcEquipSlotDef[] Slots { get; init; } = Array.Empty<NpcEquipSlotDef>();

		public NpcEquipSlotDef GetSlot(NpcEquipSlotId id) {
			for (int i = 0; i < Slots.Length; i++) {
				if (Slots[i].SlotId == id) {
					return Slots[i];
				}
			}

			return null;
		}

		public bool IsItemAllowed(NpcEquipSlotId slotId, Item item, Item[] allSlots) {
			if (item == null || item.IsAir) {
				return true;
			}

			NpcEquipSlotDef def = GetSlot(slotId);
			if (def?.IsAllowed == null) {
				return false;
			}

			return def.IsAllowed(item, allSlots ?? Array.Empty<Item>());
		}

		public string GetRejectMessage(NpcEquipSlotId slotId, Item item) {
			NpcEquipSlotDef def = GetSlot(slotId);
			if (def?.RejectMessage != null) {
				return def.RejectMessage(item);
			}

			return Language.GetTextValue("Mods.AITownNPCs.UI.WeaponNotSupported");
		}

		public static NpcEquipProfile CreateDefaultWeaponAmmo() {
			return new NpcEquipProfile {
				CombatEnabled = true,
				Slots = new[] {
					new NpcEquipSlotDef {
						SlotId = NpcEquipSlotId.Weapon,
						LabelKey = "Mods.AITownNPCs.UI.WeaponSlot",
						IsAllowed = (item, _) => NpcEquipHelper.IsAllowedWeapon(item),
						RejectMessage = NpcEquipHelper.GetWeaponRejectedMessage
					},
					new NpcEquipSlotDef {
						SlotId = NpcEquipSlotId.SummonStaff,
						LabelKey = "Mods.AITownNPCs.UI.SummonSlot",
						IsAllowed = (item, _) => NpcEquipHelper.IsAllowedSummonOrSentry(item),
						RejectMessage = _ => Language.GetTextValue("Mods.AITownNPCs.UI.SummonSlotOnly")
					},
					new NpcEquipSlotDef {
						SlotId = NpcEquipSlotId.Ammo,
						LabelKey = "Mods.AITownNPCs.UI.AmmoSlot",
						IsAllowed = (item, slots) => NpcEquipHelper.IsAllowedAmmo(item, slots.Length > 0 ? slots[0] : new Item()),
						RejectMessage = _ => Language.GetTextValue("Mods.AITownNPCs.UI.AmmoNotSupported")
					},
					CreateHeartSlot(),
					CreateWingSlot(NpcEquipSlotId.Wings)
				}
			};
		}

		public const string HeartSlotLabelKey = "Mods.AITownNPCs.UI.HeartSlot";
		public const string WingSlotLabelKey = "Mods.AITownNPCs.UI.WingSlot";

		public static NpcEquipSlotDef CreateWingSlot(NpcEquipSlotId slotId) {
			return new NpcEquipSlotDef {
				SlotId = slotId,
				LabelKey = WingSlotLabelKey,
				IsAllowed = (item, _) => NpcEquipHelper.IsAllowedWing(item),
				RejectMessage = _ => Language.GetTextValue("Mods.AITownNPCs.UI.WingSlotOnly")
			};
		}

		public static NpcEquipSlotDef CreateHeartSlot(NpcEquipSlotId slotId = NpcEquipSlotId.Heart) {
			return new NpcEquipSlotDef {
				SlotId = slotId,
				LabelKey = HeartSlotLabelKey,
				IsAllowed = (item, _) => RoleItemRules.IsHeartSlotItem(item),
				RejectMessage = _ => Language.GetTextValue("Mods.AITownNPCs.UI.HeartSlotOnly")
			};
		}

		/// <summary>Index of the Reinforcement Heart slot in the profile, or -1.</summary>
		public int FindHeartSlotIndex() {
			for (int i = 0; i < Slots.Length; i++) {
				if (Slots[i].LabelKey == HeartSlotLabelKey) {
					return (int)Slots[i].SlotId;
				}
			}

			return -1;
		}

		/// <summary>Index of the wing slot in the profile, or -1.</summary>
		public int FindWingSlotIndex() {
			for (int i = 0; i < Slots.Length; i++) {
				if (Slots[i].LabelKey == WingSlotLabelKey) {
					return (int)Slots[i].SlotId;
				}
			}

			return -1;
		}
	}
}
