using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using AITownNPCs.Common.Loc;

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
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			string action = intent.GetParam("action", "").ToLowerInvariant();
			if (string.IsNullOrEmpty(action)) {
				return SuperpowerResult.Failed(Id, "bad_params", ModText.Get("Runtime.Superpower.Fixed.weather_bad"));
			}

			switch (action) {
				case "rain_start":
					Main.StartRain();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.rain_on"));
				case "rain_stop":
					Main.StopRain();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.rain_off"));
				case "sandstorm_start":
					Sandstorm.StartSandstorm();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.sandstorm_on"));
				case "sandstorm_stop":
					Sandstorm.StopSandstorm();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.sandstorm_off"));
				case "windy_day_start": {
					EnsureDaytime();
					int mph = RollStormMph();
					SetWindFromMph(mph);
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.windy_on", mph));
				}
				case "windy_day_stop":
					ClearWind();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.windy_off"));
				case "thunderstorm_start": {
					EnsureDaytime();
					Main.StartRain();
					int mph = RollStormMph();
					SetWindFromMph(mph);
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.thunder_on", mph));
				}
				case "thunderstorm_stop":
					Main.StopRain();
					ClearWind();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.thunder_off"));
				case "wind_start":
				case "wind_set": {
					int mph = ResolveStormMph(intent);
					SetWindFromMph(mph);
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.wind_set", mph));
				}
				case "wind_stop":
					ClearWind();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.wind_off"));
				case "clear":
					Main.StopRain();
					Sandstorm.StopSandstorm();
					ClearWind();
					SyncWorld();
					return OkAnnounce(ModText.Get("Runtime.Superpower.Fixed.weather_clear"));
				default:
					return SuperpowerResult.Failed(Id, "bad_params", ModText.Get("Runtime.Superpower.Fixed.weather_unknown"));
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
