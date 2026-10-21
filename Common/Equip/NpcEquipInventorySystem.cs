using System.Reflection;
using AITownNPCs.Common.Host;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace AITownNPCs.Common.Equip
{
	public class NpcEquipInventorySystem : ModSystem
	{
		private Item[] _syncSnapshots = CreateSnapshots();
		private int _syncNpcWhoAmI = -1;

		private static bool _equipButtonHovered;
		private bool _wroteHoverLastDraw;

		public Item PendingHoverItem = new();

		private static Item[] CreateSnapshots() {
			var items = new Item[EquippedTownNPC.MaxSlots];
			for (int i = 0; i < items.Length; i++) {
				items[i] = new Item();
			}

			return items;
		}

		public override void Load() {
			MethodInfo drawButtons = typeof(Main).GetMethod(
				"DrawNPCChatButtons",
				BindingFlags.NonPublic | BindingFlags.Static);
			if (drawButtons != null) {
				MonoModHooks.Add(drawButtons, DrawNPCChatButtonsHook);
			}
		}

		private delegate void OrigDrawNPCChatButtons(int superColor, Color chatColor, int numLines, string focusText, string focusText3);

		private static void DrawNPCChatButtonsHook(OrigDrawNPCChatButtons orig, int superColor, Color chatColor, int numLines, string focusText, string focusText3) {
			orig(superColor, chatColor, numLines, focusText, focusText3);
			TryDrawEquipChatButton(chatColor, numLines, focusText, focusText3);
			TryDrawEquipSlotsUnderChat(numLines);
		}

		private static void TryDrawEquipChatButton(Color chatColor, int numLines, string focusText, string focusText3) {
			Player player = Main.LocalPlayer;
			if (player.talkNPC < 0) {
				_equipButtonHovered = false;
				return;
			}

			NpcEquipPlayer modPlayer = player.GetModPlayer<NpcEquipPlayer>();
			if (modPlayer.IsEditing && modPlayer.EditingNpcWhoAmI == player.talkNPC) {
				_equipButtonHovered = false;
				return;
			}

			NPC npc = Main.npc[player.talkNPC];
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				_equipButtonHovered = false;
				return;
			}

			float y = 130 + numLines * 30;
			float x = 180 + (Main.screenWidth - 800) / 2;

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Vector2 scale = new(0.9f);

			x = AdvancePastButton(x, focusText ?? string.Empty, font, scale);
			x = AdvancePastButton(x, Lang.inter[52].Value, font, scale);
			if (!string.IsNullOrWhiteSpace(focusText3)) {
				x = AdvancePastButton(x, focusText3, font, scale);
			}

			if (!Main.remixWorld && player.currentShoppingSettings.HappinessReport != "") {
				x = AdvancePastButton(x, Language.GetTextValue("UI.NPCCheckHappiness"), font, scale);
			}

			string text = NpcEquipHelper.EquipButtonText;
			Vector2 stringSize = ChatManager.GetStringSize(font, text, scale);
			Vector2 drawPos = new(x, y);
			Vector2 mouse = new(Main.mouseX, Main.mouseY);

			bool hovered = !PlayerInput.IgnoreMouseInterface
				&& mouse.X >= drawPos.X
				&& mouse.Y >= drawPos.Y
				&& mouse.X <= drawPos.X + stringSize.X * scale.X
				&& mouse.Y <= drawPos.Y + stringSize.Y * scale.Y;

			Vector2 drawScale = scale;
			if (hovered) {
				player.mouseInterface = true;
				player.releaseUseItem = false;
				drawScale *= 1.2f;
				if (!_equipButtonHovered) {
					SoundEngine.PlaySound(SoundID.MenuTick);
				}

				_equipButtonHovered = true;

				if (Main.mouseLeft && Main.mouseLeftRelease) {
					SoundEngine.PlaySound(SoundID.MenuOpen);
					player.GetModPlayer<NpcEquipPlayer>().RequestOpenEquip(npc.whoAmI);
				}
			}
			else {
				if (_equipButtonHovered) {
					SoundEngine.PlaySound(SoundID.MenuTick);
				}

				_equipButtonHovered = false;
			}

			Color shadow = hovered ? Color.Brown : Color.Black;
			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch,
				font,
				text,
				drawPos + stringSize * 0.5f,
				chatColor,
				shadow,
				0f,
				stringSize * 0.5f,
				drawScale);
		}

		private static void TryDrawEquipSlotsUnderChat(int numLines) {
			Player player = Main.LocalPlayer;
			NpcEquipPlayer modPlayer = player.GetModPlayer<NpcEquipPlayer>();
			NpcEquipInventorySystem system = ModContent.GetInstance<NpcEquipInventorySystem>();
			system.PendingHoverItem.TurnToAir();

			if (!modPlayer.IsEditing || player.talkNPC < 0 || player.talkNPC != modPlayer.EditingNpcWhoAmI) {
				return;
			}

			EquippedTownNPC equip = modPlayer.EditingEquip;
			if (equip == null) {
				return;
			}

			NPC npc = Main.npc[modPlayer.EditingNpcWhoAmI];
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			system.EnsureSyncSnapshots(modPlayer.EditingNpcWhoAmI, equip);

			float chatLeft = 180 + (Main.screenWidth - 800) / 2;
			float buttonY = 130 + numLines * 30;
			float originX = chatLeft;
			float originY = buttonY + 36f;

			float oldScale = Main.inventoryScale;
			Main.inventoryScale = 0.9f;
			float scale = Main.inventoryScale;
			int slotSize = (int)(TextureAssets.InventoryBack.Width() * scale);
			const int gap = 14;
			const int columns = 7;
			int slotCount = System.Math.Clamp(profile.Slots.Length, 0, EquippedTownNPC.MaxSlots);

			for (int i = 0; i < slotCount; i++) {
				NpcEquipSlotDef def = profile.Slots[i];
				int col = i % columns;
				int row = i / columns;
				int x = (int)originX + col * (slotSize + gap);
				int y = (int)originY + row * (slotSize + 22);
				string label = def?.Label ?? $"#{i + 1}";
				var slotId = (NpcEquipSlotId)i;
				system.DrawLabeledSlot(label, ref equip.Slots[i], x, y, slotSize, slotId, profile, equip);
			}

			Main.inventoryScale = oldScale;
			system.SyncChangedSlots(modPlayer.EditingNpcWhoAmI, equip, slotCount);
		}

		private static float AdvancePastButton(float x, string text, DynamicSpriteFont font, Vector2 scale) {
			if (string.IsNullOrEmpty(text)) {
				return x;
			}

			Vector2 size = ChatManager.GetStringSize(font, text, scale);
			float width = size.X > 260f ? 260f : size.X;
			return x + width + 30f;
		}

		public override void UpdateUI(GameTime gameTime) {
			PendingHoverItem.TurnToAir();
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers) {
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1) {
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"AI Town NPCs: NPC Equip Hover",
					delegate {
						if (!PendingHoverItem.IsAir) {
							Main.HoverItem = PendingHoverItem.Clone();
							Main.hoverItemName = PendingHoverItem.Name;
							_wroteHoverLastDraw = true;
						}
						else if (_wroteHoverLastDraw) {
							Main.HoverItem.TurnToAir();
							Main.hoverItemName = string.Empty;
							_wroteHoverLastDraw = false;
						}

						return true;
					},
					InterfaceScaleType.UI));
			}
		}

		public void EnsureSyncSnapshots(int npcWhoAmI, EquippedTownNPC equip) {
			if (_syncNpcWhoAmI != npcWhoAmI) {
				_syncNpcWhoAmI = npcWhoAmI;
				for (int i = 0; i < EquippedTownNPC.MaxSlots; i++) {
					_syncSnapshots[i] = equip.Slots[i].Clone();
				}
			}
		}

		public void DrawLabeledSlot(
			string label,
			ref Item slotItem,
			int x,
			int y,
			int slotSize,
			NpcEquipSlotId slotId,
			NpcEquipProfile profile,
			EquippedTownNPC equip) {
			Vector2 pos = new(x, y);
			Rectangle hitbox = new(x, y, slotSize, slotSize);
			bool hovered = hitbox.Contains(Main.mouseX, Main.mouseY);

			// Draw-only: vanilla ItemSlot.Draw(ChestItem) also handles clicks/shift-deposit
			// into the slot and can alias or pull from the player inventory.
			bool oldLeft = Main.mouseLeft;
			bool oldLeftRelease = Main.mouseLeftRelease;
			bool oldRight = Main.mouseRight;
			bool oldRightRelease = Main.mouseRightRelease;
			Main.mouseLeft = false;
			Main.mouseLeftRelease = false;
			Main.mouseRight = false;
			Main.mouseRightRelease = false;
			ItemSlot.Draw(Main.spriteBatch, ref slotItem, ItemSlot.Context.InventoryItem, pos);
			Main.mouseLeft = oldLeft;
			Main.mouseLeftRelease = oldLeftRelease;
			Main.mouseRight = oldRight;
			Main.mouseRightRelease = oldRightRelease;

			float labelWidth = FontAssets.MouseText.Value.MeasureString(label).X * 0.8f;
			float labelX = x + (slotSize - labelWidth) / 2f;
			Utils.DrawBorderStringFourWay(
				Main.spriteBatch,
				FontAssets.MouseText.Value,
				label,
				labelX,
				y + slotSize + 2,
				Color.White,
				Color.Black,
				Vector2.Zero,
				0.8f);

			if (!hovered) {
				return;
			}

			Main.LocalPlayer.mouseInterface = true;
			HandleSlotClick(ref slotItem, slotId, profile, equip);

			if (!slotItem.IsAir) {
				PendingHoverItem = slotItem.Clone();
				Main.HoverItem = slotItem.Clone();
				Main.hoverItemName = slotItem.Name;
			}
		}

		private static void HandleSlotClick(ref Item slotItem, NpcEquipSlotId slotId, NpcEquipProfile profile, EquippedTownNPC equip) {
			Item mouse = Main.mouseItem;

			// Right-click: take all from slot, or deposit one from mouse into empty/same-type slot.
			if (Main.mouseRight && Main.mouseRightRelease) {
				if (!slotItem.IsAir && mouse.IsAir) {
					Main.mouseItem = slotItem.Clone();
					slotItem.TurnToAir();
					Main.mouseRightRelease = false;
					SoundEngine.PlaySound(SoundID.Grab);
					Recipe.FindRecipes();
					return;
				}

				if (!mouse.IsAir && (slotItem.IsAir || (slotItem.type == mouse.type && slotItem.stack < mouse.maxStack))) {
					Item probeOne = mouse.Clone();
					probeOne.stack = slotItem.IsAir ? 1 : slotItem.stack + 1;
					Item[] probe = (Item[])equip.Slots.Clone();
					probe[(int)slotId] = probeOne;
					if (!profile.IsItemAllowed(slotId, probeOne, probe)) {
						Main.NewText(profile.GetRejectMessage(slotId, mouse), Color.OrangeRed);
						return;
					}

					if (slotItem.IsAir) {
						slotItem = mouse.Clone();
						slotItem.stack = 1;
					}
					else {
						slotItem.stack++;
					}

					mouse.stack--;
					if (mouse.stack <= 0) {
						mouse.TurnToAir();
					}

					Main.mouseRightRelease = false;
					SoundEngine.PlaySound(SoundID.Grab);
					Recipe.FindRecipes();
					return;
				}
			}

			if (!(Main.mouseLeft && Main.mouseLeftRelease)) {
				return;
			}

			if (mouse.IsAir && slotItem.IsAir) {
				return;
			}

			// Same-type stack merge (e.g. Reinforcement Heart 1–10).
			if (!mouse.IsAir && !slotItem.IsAir && mouse.type == slotItem.type && mouse.maxStack > 1) {
				int space = mouse.maxStack - slotItem.stack;
				if (space > 0) {
					Item merged = slotItem.Clone();
					int move = System.Math.Min(space, mouse.stack);
					merged.stack += move;
					Item[] probe = (Item[])equip.Slots.Clone();
					probe[(int)slotId] = merged;
					if (!profile.IsItemAllowed(slotId, merged, probe)) {
						Main.NewText(profile.GetRejectMessage(slotId, mouse), Color.OrangeRed);
						return;
					}

					slotItem.stack = merged.stack;
					mouse.stack -= move;
					if (mouse.stack <= 0) {
						mouse.TurnToAir();
					}

					Main.mouseLeftRelease = false;
					SoundEngine.PlaySound(SoundID.Grab);
					Recipe.FindRecipes();
					return;
				}
			}

			if (!mouse.IsAir) {
				Item[] probe = (Item[])equip.Slots.Clone();
				probe[(int)slotId] = mouse;
				if (!profile.IsItemAllowed(slotId, mouse, probe)) {
					Main.NewText(profile.GetRejectMessage(slotId, mouse), Color.OrangeRed);
					return;
				}
			}

			// Always clone — never share Item instances with mouse/inventory.
			Item fromSlot = slotItem.IsAir ? new Item() : slotItem.Clone();
			Item fromMouse = mouse.IsAir ? new Item() : mouse.Clone();
			slotItem = fromMouse;
			Main.mouseItem = fromSlot;
			Main.mouseLeftRelease = false;
			SoundEngine.PlaySound(SoundID.Grab);
			Recipe.FindRecipes();
		}

		public void SyncChangedSlots(int npcWhoAmI, EquippedTownNPC equip, int slotCount) {
			for (int i = 0; i < slotCount; i++) {
				if (ItemMatches(_syncSnapshots[i], equip.Slots[i])) {
					continue;
				}

				_syncSnapshots[i] = equip.Slots[i].Clone();
				if (i == 0 && npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs) {
					EquippedTownNPC.NotifyWeaponChanged(npcWhoAmI);
				}

				NpcEquipNetSystem.SendSlotSync(npcWhoAmI, (NpcEquipSlotId)i, equip.Slots[i]);
			}
		}

		/// <summary>Inbound authoritative slot: update UI snapshot so we do not echo it back.</summary>
		public void ApplyAuthoritativeSlot(int npcWhoAmI, NpcEquipSlotId slot, Item item) {
			if (_syncNpcWhoAmI != npcWhoAmI) {
				return;
			}

			int index = (int)slot;
			if (index < 0 || index >= EquippedTownNPC.MaxSlots) {
				return;
			}

			_syncSnapshots[index] = item == null || item.IsAir ? new Item() : item.Clone();
		}

		public void ResetSnapshotsFromEquip(int npcWhoAmI) {
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				ClearSnapshots();
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			if (!npc.active) {
				ClearSnapshots();
				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			_syncNpcWhoAmI = npcWhoAmI;
			for (int i = 0; i < EquippedTownNPC.MaxSlots; i++) {
				_syncSnapshots[i] = equip.Slots[i].Clone();
			}
		}

		public void ClearSnapshots() {
			_syncNpcWhoAmI = -1;
			for (int i = 0; i < EquippedTownNPC.MaxSlots; i++) {
				_syncSnapshots[i] = new Item();
			}
		}

		private static bool ItemMatches(Item a, Item b) {
			if (a.IsAir && b.IsAir) {
				return true;
			}

			return a.type == b.type && a.stack == b.stack && a.prefix == b.prefix;
		}
	}
}
