using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;

namespace AITownNPCs.Common.Superpower.Guide
{
	public sealed class GuideWeatherPower : ISuperpower
	{
		private const float MaxMph = 60f;
		private const int StormMphMin = 40;
		private const int StormMphMaxInclusive = 60;

		private static readonly Color NoticeColor = new(120, 200, 255);

		public string Id => SuperpowerIds.GuideWeather;
		public int NpcType => NPCID.Guide;
		public int CooldownFrames => 60 * 60;
		public string DisplayNameZh => "调整天气";
		public string DescriptionZh => "可下雨/暴雪、沙尘暴、雷雨、大风天，或单独调整风速";
		public string RefuseStyleZh => "温和";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			string action = intent.GetParam("action", "").ToLowerInvariant();
			if (string.IsNullOrEmpty(action)) {
				return SuperpowerResult.Failed(Id, "bad_params", "未识别天气动作");
			}

			switch (action) {
				case "rain_start":
					Main.StartRain();
					SyncWorld();
					return OkAnnounce("雨已开启");
				case "rain_stop":
					Main.StopRain();
					SyncWorld();
					return OkAnnounce("雨已停止");
				case "sandstorm_start":
					Sandstorm.StartSandstorm();
					SyncWorld();
					return OkAnnounce("沙尘暴已开启");
				case "sandstorm_stop":
					Sandstorm.StopSandstorm();
					SyncWorld();
					return OkAnnounce("沙尘暴已停止");
				case "windy_day_start": {
					EnsureDaytime();
					int mph = RollStormMph();
					SetWindFromMph(mph);
					SyncWorld();
					return OkAnnounce("大风天已开启（约 " + mph + " mph）");
				}
				case "windy_day_stop":
					ClearWind();
					SyncWorld();
					return OkAnnounce("大风天已停止");
				case "thunderstorm_start": {
					EnsureDaytime();
					Main.StartRain();
					int mph = RollStormMph();
					SetWindFromMph(mph);
					SyncWorld();
					return OkAnnounce("雷雨已开启（约 " + mph + " mph）");
				}
				case "thunderstorm_stop":
					Main.StopRain();
					ClearWind();
					SyncWorld();
					return OkAnnounce("雷雨已停止");
				case "wind_start":
				case "wind_set": {
					int mph = ResolveStormMph(intent);
					SetWindFromMph(mph);
					SyncWorld();
					return OkAnnounce("风速已调整（约 " + mph + " mph）");
				}
				case "wind_stop":
					ClearWind();
					SyncWorld();
					return OkAnnounce("风已停止");
				case "clear":
					Main.StopRain();
					Sandstorm.StopSandstorm();
					ClearWind();
					SyncWorld();
					return OkAnnounce("天气已恢复晴朗");
				default:
					return SuperpowerResult.Failed(Id, "bad_params", "未知天气动作");
			}
		}

		private SuperpowerResult OkAnnounce(string fixedResultZh) {
			Announce(fixedResultZh);
			return SuperpowerResult.Ok(Id, true, fixedResultZh);
		}

		private static int ResolveStormMph(SuperpowerIntent intent) {
			string raw = intent.GetParam("windSpeed", null);
			if (string.IsNullOrWhiteSpace(raw)) {
				return RollStormMph();
			}

			if (!float.TryParse(raw, System.Globalization.NumberStyles.Float,
				    System.Globalization.CultureInfo.InvariantCulture, out float value)) {
				return RollStormMph();
			}

			float mph = value <= 1f ? value * MaxMph : value;
			return (int)Utils.Clamp(System.MathF.Round(mph), StormMphMin, StormMphMaxInclusive);
		}

		private static int RollStormMph() => Main.rand.Next(StormMphMin, StormMphMaxInclusive + 1);

		private static float MphToWind(float mph) =>
			Utils.Clamp(mph / MaxMph, 0f, 1f);

		private static void SetWindFromMph(int mph) {
			SetWind(MphToWind(mph));
		}

		private static void EnsureDaytime() {
			if (!Main.dayTime) {
				Main.dayTime = true;
				Main.time = 27000.0;
			}
		}

		private static void SetWind(float absoluteSpeed) {
			float speed = Utils.Clamp(absoluteSpeed, 0f, 1f);
			if (Main.rand.NextBool()) {
				speed = -speed;
			}

			Main.windSpeedTarget = speed;
			Main.windSpeedCurrent = speed;
		}

		private static void ClearWind() {
			Main.windSpeedTarget = 0f;
			Main.windSpeedCurrent = 0f;
		}

		private static void SyncWorld() {
			if (Main.netMode == NetmodeID.Server) {
				NetMessage.SendData(MessageID.WorldData);
			}
		}

		private static void Announce(string text) {
			if (Main.netMode == NetmodeID.Server) {
				ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(text), NoticeColor);
			}
			else {
				Main.NewText(text, NoticeColor);
			}
		}
	}
}
