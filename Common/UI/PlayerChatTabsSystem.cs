using System;
using System.Collections.Generic;
using System.Reflection;
using AITownNPCs.Common.ChatLog;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace AITownNPCs.Common.UI
{
	public class PlayerChatTabsSystem : ModSystem
	{
		public enum ChatTab
		{
			All = 0,
			Follow = 1
		}

		/// <summary>Vanilla RemadeChatMonitor._showCount default is 10; raise visible history lines.</summary>
		public const int TargetShowCount = 16;

		/// <summary>Vanilla DrawChat: screenHeight - 30 - 28 - lineIndex * 21.</summary>
		private const int VanillaBottomPad = 30;
		private const int VanillaInputExtra = 28;
		private const float VanillaLineHeight = 21f;
		private const float ChatLeft = 88f;
		private const int FollowLineLifeTicks = 60 * 10;
		private const int FollowLineFadeTicks = 60;

		private static FieldInfo _showCountField;
		private static ChatTab _activeTab = ChatTab.All;
		private static Rectangle _allTabRect;
		private static Rectangle _followTabRect;

		public static ChatTab ActiveTab => _activeTab;
		public static bool IsFollowTabActive => _activeTab == ChatTab.Follow;

		private static float ChatBottomY => Main.screenHeight - VanillaBottomPad - VanillaInputExtra;

		private static float HistoryTopY() =>
			ChatBottomY - TargetShowCount * VanillaLineHeight;

		public override void Load() {
			_showCountField = typeof(RemadeChatMonitor).GetField(
				"_showCount",
				BindingFlags.Instance | BindingFlags.NonPublic);

			On_Main.DrawPlayerChat += OnDrawPlayerChat;
			On_RemadeChatMonitor.DrawChat += OnDrawChat;
			On_RemadeChatMonitor.OnResolutionChange += OnResolutionChange;
		}

		public override void Unload() {
			On_Main.DrawPlayerChat -= OnDrawPlayerChat;
			On_RemadeChatMonitor.DrawChat -= OnDrawChat;
			On_RemadeChatMonitor.OnResolutionChange -= OnResolutionChange;
			_showCountField = null;
			_activeTab = ChatTab.All;
		}

		public override void OnWorldUnload() {
			_activeTab = ChatTab.All;
			ChatLogClientBuffer.ScrollOffset = 0;
		}

		public override void PostUpdateEverything() {
			if (Main.dedServ) {
				return;
			}

			ApplyShowCount(Main.chatMonitor as RemadeChatMonitor);
		}

		public override void PostUpdateInput() {
			if (Main.dedServ || !Main.drawingPlayerChat) {
				return;
			}

			// Keep mouseInterface while hovering so world clicks don't steal focus.
			Point mouse = MousePoint();
			if (_allTabRect.Contains(mouse) || _followTabRect.Contains(mouse)) {
				Main.LocalPlayer.mouseInterface = true;
			}

			HandleFollowScroll();
		}

		private static Point MousePoint() => new(Main.mouseX, Main.mouseY);

		private static void ApplyShowCount(RemadeChatMonitor monitor) {
			if (monitor == null || _showCountField == null) {
				return;
			}

			try {
				object current = _showCountField.GetValue(monitor);
				if (current is int value && value != TargetShowCount) {
					_showCountField.SetValue(monitor, TargetShowCount);
				}
			}
			catch {
				// leave vanilla
			}
		}

		private static void OnResolutionChange(On_RemadeChatMonitor.orig_OnResolutionChange orig, RemadeChatMonitor self) {
			orig(self);
			ApplyShowCount(self);
		}

		private static void OnDrawChat(On_RemadeChatMonitor.orig_DrawChat orig, RemadeChatMonitor self, bool drawingPlayerChat) {
			ApplyShowCount(self);

			if (_activeTab == ChatTab.Follow) {
				DrawFollowHistory(drawingPlayerChat);
				return;
			}

			orig(self, drawingPlayerChat);
		}

		private static void OnDrawPlayerChat(On_Main.orig_DrawPlayerChat orig, Main self) {
			orig(self);

			if (Main.dedServ || !Main.drawingPlayerChat) {
				return;
			}

			DrawTabsAndHandleClicks();
		}

		private static void DrawTabsAndHandleClicks() {
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string allText = Language.GetTextValue("Mods.AITownNPCs.UI.ChatTabAll");
			string followText = Language.GetTextValue("Mods.AITownNPCs.UI.ChatTabFollow");

			Vector2 allSize = ChatManager.GetStringSize(font, allText, Vector2.One);
			Vector2 followSize = ChatManager.GetStringSize(font, followText, Vector2.One);

			float padX = 12f;
			float padY = 5f;
			float gap = 8f;
			float tabH = Math.Max(allSize.Y, followSize.Y) + padY * 2f;
			float tabY = HistoryTopY() - tabH - 4f;
			float x = ChatLeft;

			_allTabRect = new Rectangle(
				(int)x,
				(int)tabY,
				(int)(allSize.X + padX * 2f),
				(int)tabH);

			_followTabRect = new Rectangle(
				_allTabRect.Right + (int)gap,
				(int)tabY,
				(int)(followSize.X + padX * 2f + (ChatLogClientBuffer.HasUnread ? 14f : 0f)),
				(int)tabH);

			Point mouse = MousePoint();
			bool allHover = _allTabRect.Contains(mouse);
			bool followHover = _followTabRect.Contains(mouse);

			if (allHover || followHover) {
				Main.LocalPlayer.mouseInterface = true;
			}

			// Same click pattern as NpcEquipInventorySystem (reliable while chat is open).
			if (Main.mouseLeft && Main.mouseLeftRelease) {
				if (allHover && _activeTab != ChatTab.All) {
					_activeTab = ChatTab.All;
					Main.chatMonitor?.ResetOffset();
					Main.mouseLeftRelease = false;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
				else if (followHover && _activeTab != ChatTab.Follow) {
					_activeTab = ChatTab.Follow;
					ChatLogClientBuffer.HasUnread = false;
					ChatLogClientBuffer.ScrollOffset = 0;
					Main.mouseLeftRelease = false;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}

			DrawTabButton(_allTabRect, allText, _activeTab == ChatTab.All, allHover, unread: false);
			DrawTabButton(_followTabRect, followText, _activeTab == ChatTab.Follow, followHover, ChatLogClientBuffer.HasUnread);
		}

		private static void DrawTabButton(Rectangle rect, string text, bool active, bool hovered, bool unread) {
			Color back;
			if (active) {
				back = new Color(70, 100, 150, 220);
			}
			else if (hovered) {
				back = new Color(50, 55, 75, 200);
			}
			else {
				back = new Color(30, 30, 40, 180);
			}

			Color border = active || hovered ? new Color(180, 210, 255) : new Color(100, 100, 120);
			Color textColor = active || hovered ? Color.White : new Color(200, 200, 210);

			Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, back);
			DrawRectBorder(rect, border);

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Vector2 size = ChatManager.GetStringSize(font, text, Vector2.One);
			Vector2 pos = new(
				rect.X + (rect.Width - size.X) * 0.5f - (unread ? 4f : 0f),
				rect.Y + (rect.Height - size.Y) * 0.5f);

			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch,
				font,
				text,
				pos,
				textColor,
				0f,
				Vector2.Zero,
				Vector2.One);

			if (unread) {
				var dot = new Rectangle(rect.Right - 12, rect.Y + 6, 7, 7);
				Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, dot, new Color(255, 80, 80));
			}
		}

		private static void DrawRectBorder(Rectangle rect, Color color) {
			Texture2D px = TextureAssets.MagicPixel.Value;
			Main.spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
			Main.spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
			Main.spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
			Main.spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
		}

		private static void HandleFollowScroll() {
			if (_activeTab != ChatTab.Follow) {
				return;
			}

			int delta = Terraria.GameInput.PlayerInput.ScrollWheelDeltaForUI;
			if (delta == 0) {
				delta = Terraria.GameInput.PlayerInput.ScrollWheelDelta;
			}

			if (delta == 0) {
				return;
			}

			int maxOffset = Math.Max(0, CountFollowVisualLines() - TargetShowCount);
			ChatLogClientBuffer.ScrollOffset = Utils.Clamp(
				ChatLogClientBuffer.ScrollOffset + (delta > 0 ? -1 : 1),
				0,
				maxOffset);
		}

		private static int ChatWidth() => Math.Max(200, Main.screenWidth - 320);

		private static int LineRemain(ChatLogEntry entry) {
			if (entry.ClientShowTick == 0 || Main.GameUpdateCount < entry.ClientShowTick) {
				return 0;
			}

			int age = (int)(Main.GameUpdateCount - entry.ClientShowTick);
			return FollowLineLifeTicks - age;
		}

		private static List<PreparedFollowLine> BuildPreparedLines(bool historyOpen) {
			var result = new List<PreparedFollowLine>();
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			int width = ChatWidth();
			IReadOnlyList<ChatLogEntry> entries = ChatLogClientBuffer.FollowEntries;

			for (int i = 0; i < entries.Count; i++) {
				ChatLogEntry entry = entries[i];
				float alpha = 1f;
				if (!historyOpen) {
					int remain = LineRemain(entry);
					if (remain <= 0) {
						continue;
					}

					alpha = MathHelper.Clamp(remain / (float)FollowLineFadeTicks, 0f, 1f);
				}

				Color color = entry.Color * alpha;
				List<List<TextSnippet>> wrapped = Utils.WordwrapStringSmart(entry.Text, color, font, width, 100);
				if (wrapped == null || wrapped.Count == 0) {
					TextSnippet[] snippets = ChatManager.ParseMessage(entry.Text, color).ToArray();
					result.Add(new PreparedFollowLine(snippets, color));
					continue;
				}

				foreach (List<TextSnippet> line in wrapped) {
					result.Add(new PreparedFollowLine(line.ToArray(), color));
				}
			}

			return result;
		}

		private static int CountFollowVisualLines() => BuildPreparedLines(historyOpen: true).Count;

		private static void DrawFollowHistory(bool historyOpen) {
			List<PreparedFollowLine> lines = BuildPreparedLines(historyOpen);
			float bottom = ChatBottomY;

			if (lines.Count == 0) {
				if (!historyOpen) {
					return;
				}

				DynamicSpriteFont font = FontAssets.MouseText.Value;
				string empty = Language.GetTextValue("Mods.AITownNPCs.UI.ChatFollowEmpty");
				// Same as vanilla lineIndex 0 (bottom row).
				float y = bottom;
				ChatManager.DrawColorCodedStringWithShadow(
					Main.spriteBatch,
					font,
					empty,
					new Vector2(ChatLeft, y),
					new Color(160, 160, 170),
					0f,
					Vector2.Zero,
					Vector2.One);
				return;
			}

			int offset = historyOpen ? ChatLogClientBuffer.ScrollOffset : 0;
			int maxOffset = Math.Max(0, lines.Count - TargetShowCount);
			if (historyOpen && offset > maxOffset) {
				offset = maxOffset;
				ChatLogClientBuffer.ScrollOffset = offset;
			}

			if (offset > maxOffset) {
				offset = maxOffset;
			}

			// Vanilla: y = screenHeight - 30 - 28 - lineIndex * 21 (lineIndex 0 = bottom).
			int end = lines.Count - offset;
			int start = Math.Max(0, end - TargetShowCount);
			int visible = end - start;

			for (int i = 0; i < visible; i++) {
				PreparedFollowLine line = lines[start + i];
				int fromBottom = visible - 1 - i;
				float y = bottom - fromBottom * VanillaLineHeight;
				ChatManager.DrawColorCodedStringWithShadow(
					Main.spriteBatch,
					FontAssets.MouseText.Value,
					line.Snippets,
					new Vector2(ChatLeft, y),
					0f,
					line.Color,
					Vector2.Zero,
					Vector2.One,
					out _);
			}
		}

		private readonly struct PreparedFollowLine
		{
			public readonly TextSnippet[] Snippets;
			public readonly Color Color;

			public PreparedFollowLine(TextSnippet[] snippets, Color color) {
				Snippets = snippets;
				Color = color;
			}
		}
	}
}
