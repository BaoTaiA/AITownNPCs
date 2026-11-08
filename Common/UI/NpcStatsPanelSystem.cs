using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace AITownNPCs.Common.UI
{
	/// <summary>Draggable closable stats window (Palico CompanionPanel-style shell).</summary>
	public class NpcStatsPanelSystem : ModSystem
	{
		// Slightly more opaque than vanilla UIPanel 0.7 for readability.
		private static readonly Color PanelBackground = new Color(63, 82, 151) * 0.85f;
		private static readonly Color PanelBorder = new Color(89, 116, 213) * 0.85f;
		private static readonly Color Accent = new Color(255, 231, 120);

		private Vector2 _position = new(520f, 180f);
		private bool _dragging;
		private Vector2 _dragOffset;
		private bool _prevMouseLeft;
		private UIPanel _nativePanel;

		public override void Load() {
			if (!Main.dedServ) {
				_nativePanel = new UIPanel();
			}
		}

		public override void Unload() {
			_nativePanel = null;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int index = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (index >= 0) {
				layers.Insert(index, new LegacyGameInterfaceLayer(
					"AI Town NPCs: NPC Stats",
					Draw,
					InterfaceScaleType.UI));
			}
		}

		private bool Draw() {
			Player player = Main.LocalPlayer;
			NpcStatsPlayer stats = player.GetModPlayer<NpcStatsPlayer>();
			if (!stats.PanelOpen) {
				_dragging = false;
				_prevMouseLeft = Main.mouseLeft;
				return true;
			}

			NPC npc = Main.npc[stats.ViewingNpcWhoAmI];
			if (npc == null || !npc.active) {
				stats.CloseStatsQuiet();
				_prevMouseLeft = Main.mouseLeft;
				return true;
			}

			var backbuffer = Main.graphics.GraphicsDevice.PresentationParameters;
			int screenW = (int)(backbuffer.BackBufferWidth / Main.UIScale);
			int screenH = (int)(backbuffer.BackBufferHeight / Main.UIScale);

			int width = NpcStatsPanel.Width;
			int height = NpcStatsPanel.MeasureHeight(npc);
			int x = (int)Math.Clamp(_position.X, 4, Math.Max(4, screenW - width - 4));
			int y = (int)Math.Clamp(_position.Y, 4, Math.Max(4, screenH - height - 4));
			_position = new Vector2(x, y);

			var panel = new Rectangle(x, y, width, height);
			var mouse = new Point(Main.mouseX, Main.mouseY);
			bool clicked = Main.mouseLeft && !_prevMouseLeft;
			_prevMouseLeft = Main.mouseLeft;

			if (!Main.mouseLeft) {
				_dragging = false;
			}

			if (_dragging) {
				_position = new Vector2(mouse.X, mouse.Y) - _dragOffset;
				x = (int)Math.Clamp(_position.X, 4, Math.Max(4, screenW - width - 4));
				y = (int)Math.Clamp(_position.Y, 4, Math.Max(4, screenH - height - 4));
				_position = new Vector2(x, y);
				panel = new Rectangle(x, y, width, height);
				player.mouseInterface = true;
			}

			SpriteBatch sb = Main.spriteBatch;
			Box(panel, PanelBackground, PanelBorder);
			if (panel.Contains(mouse)) {
				player.mouseInterface = true;
			}

			string title = Language.GetTextValue("Mods.AITownNPCs.UI.StatsTitle", npc.FullName);
			Utils.DrawBorderString(sb, title, new Vector2(panel.X + 16, panel.Y + 14), Accent, 0.9f);

			var titleBar = new Rectangle(panel.X + 8, panel.Y + 4, panel.Width - 56, 40);
			var close = new Rectangle(panel.Right - 40, panel.Y + 8, 32, 32);
			bool closeHover = close.Contains(mouse);
			Utils.DrawBorderString(sb, "X", new Vector2(close.X + 10, close.Y + 6),
				closeHover ? Main.OurFavoriteColor : Color.White, 0.9f);
			if (closeHover) {
				player.mouseInterface = true;
				Main.instance.MouseText(Language.GetTextValue("LegacyInterface.52"));
				if (clicked) {
					stats.CloseStats();
					return true;
				}
			}
			else if (titleBar.Contains(mouse) && Main.mouseLeft && !_dragging && Main.mouseItem.IsAir) {
				_dragging = true;
				_dragOffset = new Vector2(mouse.X, mouse.Y) - _position;
				player.mouseInterface = true;
			}

			var body = new Rectangle(panel.X + 16, panel.Y + NpcStatsPanel.BodyTop, panel.Width - 32, panel.Height - NpcStatsPanel.BodyTop - 12);
			NpcStatsPanel.Draw(sb, npc, body, mouse, clicked);
			return true;
		}

		private void Box(Rectangle r, Color background, Color border) {
			if (_nativePanel == null) {
				return;
			}

			_nativePanel.Left.Set(r.X, 0);
			_nativePanel.Top.Set(r.Y, 0);
			_nativePanel.Width.Set(r.Width, 0);
			_nativePanel.Height.Set(r.Height, 0);
			_nativePanel.BackgroundColor = background;
			_nativePanel.BorderColor = border;
			_nativePanel.Recalculate();
			_nativePanel.Draw(Main.spriteBatch);
		}
	}
}
