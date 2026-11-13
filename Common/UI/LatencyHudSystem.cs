using System.Collections.Generic;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace AITownNPCs.Common.UI
{
	/// <summary>
	/// Multiplayer latency probe + draw under/near the vanilla minimap (info-bar area).
	/// </summary>
	public class LatencyHudSystem : ModSystem
	{
		private const int ProbeIntervalTicks = 60;
		private int _probeCooldown;

		public override void OnWorldLoad() {
			LatencyNet.Reset();
			_probeCooldown = 0;
		}

		public override void OnWorldUnload() {
			LatencyNet.Reset();
			_probeCooldown = 0;
		}

		public override void PostUpdateEverything() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			if (!(AinpcClientConfig.Instance?.ShowLatencyHud ?? true)) {
				return;
			}

			if (_probeCooldown > 0) {
				_probeCooldown--;
				return;
			}

			_probeCooldown = ProbeIntervalTicks;
			LatencyNet.SendPing();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int index = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (index < 0) {
				return;
			}

			layers.Insert(index, new LegacyGameInterfaceLayer(
				"AI Town NPCs: Latency HUD",
				Draw,
				InterfaceScaleType.UI));
		}

		private static bool Draw() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return true;
			}

			if (!(AinpcClientConfig.Instance?.ShowLatencyHud ?? true)) {
				return true;
			}

			if (Main.gameMenu || Main.mapFullscreen) {
				return true;
			}

			int ms = LatencyNet.DisplayMs;
			if (ms < 0) {
				return true;
			}

			string text = ms + "ms";
			Color color = ColorForMs(ms);
			Vector2 size = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One);

			float x;
			float y;
			if (Main.mapStyle == 1 && Main.miniMapWidth > 0 && Main.miniMapHeight > 0) {
				x = Main.miniMapX + Main.miniMapWidth * 0.5f - size.X * 0.5f;
				y = Main.miniMapY + Main.miniMapHeight + 4f;
			}
			else {
				// Minimap hidden: default top-right anchor near where the minimap sits.
				var backbuffer = Main.graphics.GraphicsDevice.PresentationParameters;
				int screenW = (int)(backbuffer.BackBufferWidth / Main.UIScale);
				x = screenW - size.X - 24f;
				y = 24f;
			}

			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch,
				FontAssets.MouseText.Value,
				text,
				new Vector2(x, y),
				color,
				0f,
				Vector2.Zero,
				Vector2.One);

			return true;
		}

		private static Color ColorForMs(int ms) {
			if (ms < 80) {
				return new Color(124, 252, 0);
			}

			if (ms < 150) {
				return new Color(255, 220, 60);
			}

			return new Color(255, 80, 80);
		}
	}
}
