using System.IO;
using AITownNPCs.Common.Ai.Chat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace AITownNPCs.Common.Superpower.ArmsDealer
{
	public class ArmsLoanGlobalItem : GlobalItem
	{
		public const string RestrictMessage = "军火商借用的武器只能自己用";

		public override bool InstancePerEntity => true;
		public bool IsArmsLoan;
		public int OwnerWhoAmI = -1;

		public override GlobalItem Clone(Item from, Item to) {
			var clone = (ArmsLoanGlobalItem)base.Clone(from, to);
			clone.IsArmsLoan = IsArmsLoan;
			clone.OwnerWhoAmI = OwnerWhoAmI;
			return clone;
		}

		public override void NetSend(Item item, BinaryWriter writer) {
			writer.Write(IsArmsLoan);
			writer.Write(OwnerWhoAmI);
		}

		public override void NetReceive(Item item, BinaryReader reader) {
			IsArmsLoan = reader.ReadBoolean();
			OwnerWhoAmI = reader.ReadInt32();
		}

		public override void SaveData(Item item, TagCompound tag) {
			if (IsArmsLoan) {
				tag["isArmsLoan"] = true;
				tag["armsLoanOwner"] = OwnerWhoAmI;
			}
		}

		public override void LoadData(Item item, TagCompound tag) {
			IsArmsLoan = tag.ContainsKey("isArmsLoan") && tag.GetBool("isArmsLoan");
			OwnerWhoAmI = tag.ContainsKey("armsLoanOwner") ? tag.GetInt("armsLoanOwner") : -1;
		}

		public override bool CanStack(Item destination, Item source) {
			bool a = destination.TryGetGlobalItem(out ArmsLoanGlobalItem ga) && ga.IsArmsLoan;
			bool b = source.TryGetGlobalItem(out ArmsLoanGlobalItem gb) && gb.IsArmsLoan;
			if (a || b) {
				return false;
			}

			return base.CanStack(destination, source);
		}

		public override bool CanPickup(Item item, Player player) {
			if (!IsArmsLoan) {
				return true;
			}

			if (OwnerWhoAmI >= 0 && player.whoAmI != OwnerWhoAmI) {
				ArmsLoanRestrict.Notify(player);
				return false;
			}

			return true;
		}

		/// <summary>World drops of loan gear snap back onto the owner's mouse (local / SP).</summary>
		public override void Update(Item item, ref float gravity, ref float maxFallSpeed) {
			if (!IsArmsLoan || item == null || !item.active || item.IsAir) {
				return;
			}

			int owner = OwnerWhoAmI;
			if (owner < 0 || owner >= Main.maxPlayers) {
				item.TurnToAir();
				item.active = false;
				return;
			}

			Player player = Main.player[owner];
			if (player == null || !player.active) {
				item.TurnToAir();
				item.active = false;
				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				item.TurnToAir();
				item.active = false;
				return;
			}

			if (player.whoAmI != Main.myPlayer) {
				return;
			}

			ArmsLoanRestrict.Notify(player);
			player.GetModPlayer<ArmsLoanPlayerHooks>().ForceLoanOntoMouse(item);
			item.TurnToAir();
			item.active = false;
		}

		public static bool IsLoan(Item item) {
			return item != null
				&& !item.IsAir
				&& item.TryGetGlobalItem(out ArmsLoanGlobalItem g)
				&& g.IsArmsLoan;
		}

		public static void MarkLoan(Item item, int ownerWhoAmI) {
			if (item == null || item.IsAir) {
				return;
			}

			var g = item.GetGlobalItem<ArmsLoanGlobalItem>();
			g.IsArmsLoan = true;
			g.OwnerWhoAmI = ownerWhoAmI;
		}
	}

	public static class ArmsLoanRestrict
	{
		private const int NotifyCooldownTicks = 60;

		public static void Notify(Player player) {
			if (player == null || !player.active) {
				return;
			}

			var mp = player.GetModPlayer<ArmsLoanPlayerHooks>();
			if (mp.RestrictNotifyCooldown > 0) {
				return;
			}

			mp.RestrictNotifyCooldown = NotifyCooldownTicks;
			NpcAiSpeech.NotifyPlayerLiteral(player.whoAmI, ArmsLoanGlobalItem.RestrictMessage, Color.Orange);
		}

		/// <summary>Only these contexts may receive a loan from the mouse (own bag).</summary>
		public static bool IsPlayerBagContext(int context) {
			return context == ItemSlot.Context.InventoryItem
				|| context == ItemSlot.Context.InventoryCoin
				|| context == ItemSlot.Context.InventoryAmmo
				|| context == ItemSlot.Context.HotbarItem;
		}

		/// <summary>Strip matching loan copies from trash / vanilla banks / open chest (reduces dupes).</summary>
		public static void StripMatchingLoanExtras(Player player, int type, int prefix) {
			if (player == null) {
				return;
			}

			StripMatching(player.trashItem, type, prefix);
			StripMatchingArray(player.bank?.item, type, prefix);
			StripMatchingArray(player.bank2?.item, type, prefix);
			StripMatchingArray(player.bank3?.item, type, prefix);
			StripMatchingArray(player.bank4?.item, type, prefix);
			if (player.chest >= 0 && player.chest < Main.chest.Length) {
				Chest open = Main.chest[player.chest];
				if (open != null) {
					StripMatchingArray(open.item, type, prefix);
				}
			}
		}

		private static void StripMatching(Item item, int type, int prefix) {
			if (ArmsLoanGlobalItem.IsLoan(item) && item.type == type && item.prefix == prefix) {
				item.TurnToAir();
			}
		}

		private static void StripMatchingArray(Item[] slots, int type, int prefix) {
			if (slots == null) {
				return;
			}

			for (int i = 0; i < slots.Length; i++) {
				StripMatching(slots[i], type, prefix);
			}
		}
	}

	/// <summary>Block ItemSlot deposits while a loan sits on the mouse (covers most UI that uses ItemSlot).</summary>
	public class ArmsLoanItemSlotSystem : ModSystem
	{
		public override void Load() {
			On_ItemSlot.LeftClick_ItemArray_int_int += LeftClickArray;
			On_ItemSlot.RightClick_ItemArray_int_int += RightClickArray;
			On_ItemSlot.LeftClick_refItem_int += LeftClickRef;
			On_ItemSlot.RightClick_refItem_int += RightClickRef;
		}

		public override void Unload() {
			On_ItemSlot.LeftClick_ItemArray_int_int -= LeftClickArray;
			On_ItemSlot.RightClick_ItemArray_int_int -= RightClickArray;
			On_ItemSlot.LeftClick_refItem_int -= LeftClickRef;
			On_ItemSlot.RightClick_refItem_int -= RightClickRef;
		}

		private static bool BlockIfLoanMouseOutsideBag(int context) {
			if (!ArmsLoanGlobalItem.IsLoan(Main.mouseItem) || ArmsLoanRestrict.IsPlayerBagContext(context)) {
				return false;
			}

			ArmsLoanRestrict.Notify(Main.LocalPlayer);
			return true;
		}

		private static void LeftClickArray(On_ItemSlot.orig_LeftClick_ItemArray_int_int orig, Item[] inv, int context, int slot) {
			if (BlockIfLoanMouseOutsideBag(context)) {
				return;
			}

			orig(inv, context, slot);
		}

		private static void RightClickArray(On_ItemSlot.orig_RightClick_ItemArray_int_int orig, Item[] inv, int context, int slot) {
			if (BlockIfLoanMouseOutsideBag(context)) {
				return;
			}

			orig(inv, context, slot);
		}

		private static void LeftClickRef(On_ItemSlot.orig_LeftClick_refItem_int orig, ref Item inv, int context) {
			if (BlockIfLoanMouseOutsideBag(context)) {
				return;
			}

			orig(ref inv, context);
		}

		private static void RightClickRef(On_ItemSlot.orig_RightClick_refItem_int orig, ref Item inv, int context) {
			if (BlockIfLoanMouseOutsideBag(context)) {
				return;
			}

			orig(ref inv, context);
		}
	}

	/// <summary>
	/// Sticky-mouse gate: once a loan is on the cursor, it may only return to the player's own bag.
	/// If it vanishes elsewhere (mod storage, throw, etc.), force it back onto the mouse.
	/// </summary>
	public class ArmsLoanPlayerHooks : ModPlayer
	{
		public int RestrictNotifyCooldown;

		private bool _loanOnMouse;
		private int _mouseType;
		private int _mouseStack;
		private int _mousePrefix;

		public override void PostUpdate() {
			if (RestrictNotifyCooldown > 0) {
				RestrictNotifyCooldown--;
			}

			if (Player.whoAmI != Main.myPlayer && Main.netMode != NetmodeID.SinglePlayer) {
				return;
			}

			TickStickyMouse();
		}

		public void ForceLoanOntoMouse(Item source) {
			if (source == null || source.IsAir) {
				return;
			}

			Main.mouseItem = source.Clone();
			ArmsLoanGlobalItem.MarkLoan(Main.mouseItem, Player.whoAmI);
			RememberMouseLoan(Main.mouseItem);
			ArmsLoanRestrict.StripMatchingLoanExtras(Player, Main.mouseItem.type, Main.mouseItem.prefix);
		}

		private void TickStickyMouse() {
			if (ArmsLoanGlobalItem.IsLoan(Main.mouseItem)) {
				RememberMouseLoan(Main.mouseItem);
				return;
			}

			if (!_loanOnMouse) {
				return;
			}

			// Legal: loan of same type+prefix is back in our bag (stack may change via ammo use/split).
			if (BagHasMatchingLoan(_mouseType, _mousePrefix)) {
				ClearMouseLoanMemory();
				return;
			}

			// Illegal move (mod storage / drop / etc.): stick back on mouse and scrub vanilla extras.
			ArmsLoanRestrict.Notify(Player);
			ArmsLoanRestrict.StripMatchingLoanExtras(Player, _mouseType, _mousePrefix);
			Main.mouseItem = new Item();
			Main.mouseItem.SetDefaults(_mouseType);
			Main.mouseItem.stack = System.Math.Max(1, _mouseStack);
			if (_mousePrefix > 0) {
				Main.mouseItem.Prefix(_mousePrefix);
			}

			ArmsLoanGlobalItem.MarkLoan(Main.mouseItem, Player.whoAmI);
			RememberMouseLoan(Main.mouseItem);
		}

		private void RememberMouseLoan(Item item) {
			_loanOnMouse = true;
			_mouseType = item.type;
			_mouseStack = item.stack;
			_mousePrefix = item.prefix;
		}

		public void ClearStickyMouseState() {
			ClearMouseLoanMemory();
		}

		private void ClearMouseLoanMemory() {
			_loanOnMouse = false;
			_mouseType = 0;
			_mouseStack = 0;
			_mousePrefix = 0;
		}

		private bool BagHasMatchingLoan(int type, int prefix) {
			int len = Player.inventory != null ? Player.inventory.Length : 0;
			for (int i = 0; i < len; i++) {
				Item slot = Player.inventory[i];
				if (!ArmsLoanGlobalItem.IsLoan(slot)) {
					continue;
				}

				if (slot.type == type && slot.prefix == prefix) {
					return true;
				}
			}

			return false;
		}

		public override void SetControls() {
			if (!Player.controlThrow) {
				return;
			}

			Item held = Player.inventory[Player.selectedItem];
			if (!ArmsLoanGlobalItem.IsLoan(held)) {
				return;
			}

			Player.controlThrow = false;
			ArmsLoanRestrict.Notify(Player);
		}

		public override bool ShiftClickSlot(Item[] inventory, int context, int slot) {
			if (inventory == null || slot < 0 || slot >= inventory.Length) {
				return false;
			}

			if (!ArmsLoanGlobalItem.IsLoan(inventory[slot])) {
				return false;
			}

			if (ArmsLoanRestrict.IsPlayerBagContext(context)
			    && (Player.chest >= 0 || Main.npcShop > 0)) {
				ArmsLoanRestrict.Notify(Player);
				return true;
			}

			return false;
		}

		public override bool CanSellItem(NPC vendor, Item[] shopInventory, Item item) {
			if (ArmsLoanGlobalItem.IsLoan(item)) {
				ArmsLoanRestrict.Notify(Player);
				return false;
			}

			return true;
		}

		public override void UpdateDead() {
			ClearMouseLoanMemory();
			ArmsDealerTempWeaponPower.ReclaimAll(Player, notify: false);
		}
	}
}
