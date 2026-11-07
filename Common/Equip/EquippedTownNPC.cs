using System;
using System.Collections.Generic;
using AITownNPCs.Common.Roles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Equip
{
	/// <summary>
	/// Equipment storage (up to 12 slots). Combat runtime lives on NpcCombatState.
	/// </summary>
	public class EquippedTownNPC : GlobalNPC
	{
		public const int MaxSlots = 12;

		/// <summary>Raised when weapon slot type/prefix changes or is cleared. Arg = npcWhoAmI.</summary>
		public static event Action<int> WeaponChanged;

		/// <summary>Raised when the summon/sentry offhand changes or is cleared. Arg = npcWhoAmI.</summary>
		public static event Action<int> SummonStaffChanged;

		public override bool InstancePerEntity => true;

		public Item[] Slots { get; private set; } = CreateEmptySlots();

		public Item Weapon {
			get => Slots[0];
			set => Slots[0] = value == null || value.IsAir ? new Item() : value.Clone();
		}

		public Item Ammo {
			get => Slots[1];
			set => Slots[1] = value == null || value.IsAir ? new Item() : value.Clone();
		}

		public Item SummonStaff {
			get => Slots[(int)NpcEquipSlotId.SummonStaff];
			set => Slots[(int)NpcEquipSlotId.SummonStaff] = value == null || value.IsAir ? new Item() : value.Clone();
		}

		private bool _droppedOnDeath;

		private static Item[] CreateEmptySlots() {
			var slots = new Item[MaxSlots];
			for (int i = 0; i < MaxSlots; i++) {
				slots[i] = new Item();
			}

			return slots;
		}

		public override bool NeedSaving(NPC npc) {
			return HasAnyEquipment();
		}

		public override void SaveData(NPC npc, TagCompound tag) {
			var list = new List<TagCompound>();
			for (int i = 0; i < MaxSlots; i++) {
				Item item = Slots[i];
				if (item.IsAir) {
					continue;
				}

				list.Add(new TagCompound {
					["i"] = i,
					["item"] = item
				});
			}

			if (list.Count > 0) {
				tag["slots"] = list;
			}
		}

		public override void LoadData(NPC npc, TagCompound tag) {
			Slots = CreateEmptySlots();

			if (tag.ContainsKey("slots")) {
				foreach (TagCompound entry in tag.GetList<TagCompound>("slots")) {
					int index = entry.GetInt("i");
					if (index < 0 || index >= MaxSlots || !entry.ContainsKey("item")) {
						continue;
					}

					Slots[index] = entry.Get<Item>("item");
				}
			}
			else {
				if (tag.ContainsKey("weapon")) {
					Slots[0] = tag.Get<Item>("weapon");
				}

				if (tag.ContainsKey("ammo")) {
					Slots[1] = tag.Get<Item>("ammo");
				}
			}

			ShiftDryadHeartAndWings(npc);
			MigrateSummonOffhand(npc);
			SanitizeForRole(npc);
		}

		/// <summary>Old saves may have a summon staff in the main hand. Move it to the offhand.</summary>
		private void MigrateSummonOffhand(NPC npc) {
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			if (profile.GetSlot(NpcEquipSlotId.SummonStaff) == null) {
				return;
			}

			int summonIndex = (int)NpcEquipSlotId.SummonStaff;
			if (!Slots[summonIndex].IsAir || !NpcEquipHelper.IsSummonOrSentryStaff(Slots[0])) {
				return;
			}

			Slots[summonIndex] = Slots[0];
			Slots[0] = new Item();
		}

		private void ShiftDryadHeartAndWings(NPC npc) {
			if (npc.type != NPCID.Dryad || !RoleItemRules.IsReinforcementHeart(Slots[9])) {
				return;
			}

			Slots[11] = Slots[10];
			Slots[10] = Slots[9];
			Slots[9] = new Item();
		}

		// Equipment runtime sync is ModPacket-only (SyncSlot / SyncAll).
		// Do not piggyback slots on ExtraAI — conflicts with follow netAlways.

		public override void OnKill(NPC npc) {
			// Do not drop equipped gear on death (including nurse / dryad).
			ClearEquipmentLocal();
		}

		private void ClearEquipmentLocal() {
			_droppedOnDeath = true;
			for (int i = 0; i < MaxSlots; i++) {
				Slots[i] = new Item();
			}
		}

		public bool HasAnyEquipment() {
			for (int i = 0; i < MaxSlots; i++) {
				if (!Slots[i].IsAir) {
					return true;
				}
			}

			return false;
		}

		public Item GetSlot(NpcEquipSlotId slot) {
			int index = (int)slot;
			if (index < 0 || index >= MaxSlots) {
				return new Item();
			}

			return Slots[index];
		}

		public void SetSlot(NpcEquipSlotId slot, Item item, int npcWhoAmI = -1) {
			int index = (int)slot;
			if (index < 0 || index >= MaxSlots) {
				return;
			}

			item ??= new Item();
			bool notifyWeapon = false;
			bool notifySummon = false;
			if (index == 0) {
				bool changed = Slots[0].type != item.type || Slots[0].prefix != item.prefix;
				notifyWeapon = changed || item.IsAir;
			}
			else if (index == (int)NpcEquipSlotId.SummonStaff) {
				bool changed = Slots[index].type != item.type || Slots[index].prefix != item.prefix;
				notifySummon = changed || item.IsAir;
			}

			// Store an independent copy so NPC gear never aliases mouse/inventory Items.
			Slots[index] = item.IsAir ? new Item() : item.Clone();

			if (notifyWeapon && npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs) {
				NotifyWeaponChanged(npcWhoAmI);
			}

			if (notifySummon && npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs) {
				SummonStaffChanged?.Invoke(npcWhoAmI);
			}

			// Push empty loadout so clients clear ExtraAI-stale slots.
			if (npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs && !HasAnyEquipment()) {
				Main.npc[npcWhoAmI].netUpdate = true;
			}
		}

		public void DropEquipment(NPC npc) {
			if (Main.netMode == NetmodeID.MultiplayerClient || _droppedOnDeath) {
				return;
			}

			_droppedOnDeath = true;
			IEntitySource source = npc.GetSource_Death();
			for (int i = 0; i < MaxSlots; i++) {
				DropOne(source, npc, i);
			}
		}

		public void SanitizeForRole(NPC npc) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			IEntitySource source = npc.GetSource_Misc("ainpcRoleSanitize");
			for (int i = 0; i < MaxSlots; i++) {
				if (Slots[i].IsAir) {
					continue;
				}

				var slotId = (NpcEquipSlotId)i;
				if (i >= profile.Slots.Length || !profile.IsItemAllowed(slotId, Slots[i], Slots)) {
					DropOne(source, npc, i);
				}
			}

			if (!HasAnyEquipment()) {
				npc.netUpdate = true;
			}
		}

		/// <summary>Notify combat layer that the weapon loadout changed (no Dummy calls from Equip).</summary>
		public static void NotifyWeaponChanged(int npcWhoAmI) {
			WeaponChanged?.Invoke(npcWhoAmI);
		}

		private void DropOne(IEntitySource source, NPC npc, int index) {
			Item item = Slots[index];
			if (item.IsAir) {
				return;
			}

			int worldIndex = Item.NewItem(source, npc.Hitbox, item.type, item.stack);
			if (worldIndex >= 0 && worldIndex < Main.maxItems && item.prefix != 0) {
				Main.item[worldIndex].Prefix(item.prefix);
			}

			Slots[index] = new Item();
		}
	}
}
