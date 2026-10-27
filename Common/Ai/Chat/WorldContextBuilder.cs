using System.Collections.Generic;
using System.Linq;
using System.Text;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Loc;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Chat
{
	public static class WorldContextBuilder
	{
		public static string Build(Player player, NPC speaker, float nearbyRadius = 800f) {
			if (player == null || !player.active) {
				return ModText.Get("Runtime.Chat.NoPlayerContext");
			}

			Vector2 origin = speaker != null && speaker.active
				? speaker.Center
				: player.Center;

			PerceptionSnapshot snap = Capture(player, speaker, nearbyRadius);

			var sb = new StringBuilder();
			sb.AppendLine(ModText.Get("Runtime.Chat.EnvHeader"));
			sb.Append(ModText.Get("Runtime.Chat.Biome")).Append(snap.Biome).Append('；');
			sb.Append(ModText.Get("Runtime.Chat.Layer")).Append(snap.Layer).Append('；');
			sb.Append(ModText.Get("Runtime.Chat.Coords")).Append('(').Append((int)(origin.X / 16f)).Append(',').Append((int)(origin.Y / 16f)).Append(")；");
			sb.Append(ModText.Get("Runtime.Chat.Depth")).Append(snap.Layer).Append('；');
			sb.Append(ModText.Get("Runtime.Chat.Time")).Append(snap.DayTime ? ModText.Get("Runtime.Chat.Day") : ModText.Get("Runtime.Chat.Night"));
			sb.Append(' ').Append(snap.GameClock).Append('；');
			if (snap.BloodMoon) sb.Append(ModText.Get("Runtime.Chat.BloodMoon"));
			if (snap.Eclipse) sb.Append(ModText.Get("Runtime.Chat.Eclipse"));
			sb.Append(ModText.Get("Runtime.Chat.Weather")).Append(snap.Weather).Append('；');
			sb.AppendLine();

			sb.Append(ModText.Get("Runtime.Chat.Player")).Append(player.name);
			sb.Append(ModText.Get("Runtime.Chat.Life")).Append(player.statLife).Append('/').Append(player.statLifeMax2);
			sb.Append(ModText.Get("Runtime.Chat.Mana")).Append(player.statMana).Append('/').Append(player.statManaMax2);
			if (snap.PlayerLowLife) {
				sb.Append(ModText.Get("Runtime.Chat.LowLifeParen"));
			}

			sb.AppendLine();

			sb.Append(ModText.Get("Runtime.Chat.EnemyCount")).Append(snap.EnemyCount);
			if (snap.EnemySummary != null) {
				sb.Append(ModText.Get("Runtime.Chat.EnemyMain")).Append(snap.EnemySummary);
			}

			if (snap.EnemiesTargetingOwner > 0) {
				sb.Append(ModText.Get("Runtime.Chat.EnemiesTargeting")).Append(snap.EnemiesTargetingOwner);
			}

			sb.AppendLine();
			if (snap.BossName != null) {
				sb.Append(ModText.Get("Runtime.Chat.BossPresent")).Append(snap.BossName)
					.Append(ModText.Get("Runtime.Chat.BossAbout")).Append(snap.BossLifePct).Append('%').AppendLine();
			}
			else {
				sb.AppendLine(ModText.Get("Runtime.Chat.BossAbsent"));
			}

			sb.Append(ModText.Get("Runtime.Chat.NearbyTown")).Append(
				string.IsNullOrEmpty(snap.NearbyTownNpcs) ? ModText.Get("Runtime.Chat.None") : snap.NearbyTownNpcs);
			sb.AppendLine();
			AppendSpeakerLoadout(sb, speaker);
			return sb.ToString();
		}

		/// <summary>Compact world line for proactive / superpower-result replies.</summary>
		public static string BuildCompact(Player player, NPC speaker) {
			if (player == null || !player.active) {
				return ModText.Get("Runtime.Chat.NoPlayerContext");
			}

			PerceptionSnapshot snap = Capture(player, speaker, 800f);
			var sb = new StringBuilder();
			sb.Append(ModText.Get("Runtime.Chat.CompactEnv")).Append(snap.Biome);
			sb.Append('；').Append(snap.DayTime ? ModText.Get("Runtime.Chat.Day") : ModText.Get("Runtime.Chat.Night"));
			sb.Append('；').Append(ModText.Get("Runtime.Chat.Weather")).Append(snap.Weather);
			if (snap.PlayerLowLife) {
				sb.Append(ModText.Get("Runtime.Chat.CompactLowLife"));
			}

			if (snap.BossName != null) {
				sb.Append(ModText.Get("Runtime.Chat.CompactBoss")).Append(snap.BossName)
					.Append(ModText.Get("Runtime.Chat.BossAbout")).Append(snap.BossLifePct).Append('%');
			}

			AppendSpeakerLoadoutInline(sb, speaker);
			return sb.ToString();
		}

		private static void AppendSpeakerLoadout(StringBuilder sb, NPC speaker) {
			if (speaker == null || !speaker.active) {
				sb.Append(ModText.Get("Runtime.Chat.WeaponNone"));
				return;
			}

			EquippedTownNPC equip = speaker.GetGlobalNPC<EquippedTownNPC>();
			Item weapon = equip?.Weapon;
			if (weapon == null || weapon.IsAir) {
				sb.Append(ModText.Get("Runtime.Chat.WeaponNone"));
			}
			else {
				sb.Append(ModText.Get("Runtime.Chat.WeaponHas")).Append(Lang.GetItemNameValue(weapon.type));
			}

			NpcCombatState combat = speaker.GetGlobalNPC<NpcCombatState>();
			if (combat != null && combat.CombatControlsLocomotion) {
				sb.Append(ModText.Get("Runtime.Chat.InCombat"));
			}
		}

		private static void AppendSpeakerLoadoutInline(StringBuilder sb, NPC speaker) {
			if (speaker == null || !speaker.active) {
				sb.Append(ModText.Get("Runtime.Chat.WeaponNoneInline"));
				return;
			}

			EquippedTownNPC equip = speaker.GetGlobalNPC<EquippedTownNPC>();
			Item weapon = equip?.Weapon;
			if (weapon == null || weapon.IsAir) {
				sb.Append(ModText.Get("Runtime.Chat.WeaponNoneInline"));
			}
			else {
				sb.Append(ModText.Get("Runtime.Chat.WeaponHasInline")).Append(Lang.GetItemNameValue(weapon.type));
			}

			NpcCombatState combat = speaker.GetGlobalNPC<NpcCombatState>();
			if (combat != null && combat.CombatControlsLocomotion) {
				sb.Append(ModText.Get("Runtime.Chat.InCombat"));
			}
		}

		/// <summary>Structured snapshot used by follower ambient sensing and Build.</summary>
		public static PerceptionSnapshot Capture(Player player, NPC speaker, float nearbyRadius = 800f) {
			var snap = new PerceptionSnapshot {
				Biome = player != null && player.active ? DescribeBiome(player) : ModText.Get("Runtime.Context.Biome.unknown"),
				Layer = player != null && player.active ? DescribeLayer(player) : ModText.Get("Runtime.Context.Layer.unknown"),
				Weather = player != null && player.active ? DescribeWeather(player) : ModText.Get("Runtime.Context.Weather.unknown"),
				BloodMoon = Main.bloodMoon,
				Eclipse = Main.eclipse,
				Raining = Main.raining,
				DayTime = Main.dayTime,
				GameClock = FormatGameTime(),
				PlayerLowLife = player != null && player.active && player.statLife < player.statLifeMax2 * 0.3f
			};

			if (player == null || !player.active) {
				return snap;
			}

			Vector2 origin = speaker != null && speaker.active
				? speaker.Center
				: player.Center;

			var enemyNames = new Dictionary<string, int>();
			var townNames = new List<string>();

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (!n.active) {
					continue;
				}

				float dist = Vector2.Distance(origin, n.Center);
				if (dist > nearbyRadius) {
					continue;
				}

				if (n.townNPC || NPCID.Sets.IsTownPet[n.type]) {
					if (speaker == null || n.whoAmI != speaker.whoAmI) {
						townNames.Add(n.GivenOrTypeName);
					}

					continue;
				}

				if (n.friendly || n.dontTakeDamage || NPCID.Sets.CountsAsCritter[n.type]) {
					continue;
				}

				snap.EnemyCount++;
				string name = Lang.GetNPCNameValue(n.type);
				enemyNames.TryGetValue(name, out int c);
				enemyNames[name] = c + 1;

				if (n.target == player.whoAmI) {
					snap.EnemiesTargetingOwner++;
				}

				if (n.boss && (snap.BossName == null || dist < snap.BossDist)) {
					snap.BossName = name;
					snap.BossDist = dist;
					snap.BossLifePct = n.lifeMax > 0 ? (int)(100f * n.life / n.lifeMax) : 0;
				}
			}

			if (enemyNames.Count > 0) {
				snap.EnemySummary = string.Join("、", enemyNames.OrderByDescending(kv => kv.Value).Take(5)
					.Select(kv => kv.Key + "x" + kv.Value));
			}

			if (townNames.Count > 0) {
				snap.NearbyTownNpcs = string.Join("、", townNames.Take(8));
			}

			return snap;
		}

		/// <summary>
		/// Rare ambient follower chat: one focused topic, natural companion tone (not a status dump).
		/// </summary>
		public static bool TryBuildFollowerAmbientTrigger(
			Player player,
			NPC follower,
			string lastTopic,
			out string trigger,
			out string topic) {
			trigger = null;
			topic = null;
			if (player == null || !player.active || follower == null || !follower.active) {
				return false;
			}

			PerceptionSnapshot snap = Capture(player, follower);
			if (!TryPickAmbientFocus(snap, out string focus, out topic)) {
				return false;
			}

			if (!string.IsNullOrEmpty(lastTopic) && lastTopic == topic) {
				return false;
			}

			trigger = ModText.Get("Runtime.Chat.FollowerAmbient", player.name, focus);
			return true;
		}

		/// <summary>Focused event line for biome/boss/low-life style triggers.</summary>
		public static string BuildFollowerEventTrigger(Player player, string focus) {
			return ModText.Get("Runtime.Chat.FollowerEvent", player.name, focus);
		}

		private static bool TryPickAmbientFocus(PerceptionSnapshot snap, out string focus, out string topic) {
			focus = null;
			topic = null;

			if (snap.BossName != null) {
				topic = "boss:" + snap.BossName;
				focus = ModText.Get("Runtime.Chat.FocusBoss", snap.BossName, snap.BossLifePct);
				return true;
			}

			if (snap.PlayerLowLife) {
				topic = "lowlife";
				focus = ModText.Get("Runtime.Chat.FocusLowLife");
				return true;
			}

			if (snap.BloodMoon) {
				topic = "bloodmoon";
				focus = ModText.Get("Runtime.Chat.FocusBloodMoon");
				return true;
			}

			if (snap.Eclipse) {
				topic = "eclipse";
				focus = ModText.Get("Runtime.Chat.FocusEclipse");
				return true;
			}

			if (snap.EnemyCount >= 4) {
				topic = "enemies:" + snap.EnemyCount;
				focus = snap.EnemySummary != null
					? ModText.Get("Runtime.Chat.FocusEnemies", snap.EnemySummary)
					: ModText.Get("Runtime.Chat.FocusEnemiesPlain");
				return true;
			}

			if (snap.EnemiesTargetingOwner > 0) {
				topic = "aggro:" + snap.EnemiesTargetingOwner;
				focus = ModText.Get("Runtime.Chat.FocusAggro");
				return true;
			}

			// Soft ambient: biome + day/night only when nothing urgent.
			topic = "soft:" + snap.Biome + ":" + (snap.DayTime ? "day" : "night") + ":" + snap.Weather;
			focus = ModText.Get(
				"Runtime.Chat.FocusSoft",
				snap.Biome,
				snap.DayTime ? ModText.Get("Runtime.Chat.Day") : ModText.Get("Runtime.Chat.Night"),
				snap.Weather);
			return true;
		}

		public static string DescribeBiome(Player player) {
			if (player.ZoneDungeon) return ModText.Get("Runtime.Context.Biome.dungeon");
			if (player.ZoneCorrupt) return ModText.Get("Runtime.Context.Biome.corruption");
			if (player.ZoneCrimson) return ModText.Get("Runtime.Context.Biome.crimson");
			if (player.ZoneHallow) return ModText.Get("Runtime.Context.Biome.hallow");
			if (player.ZoneJungle) return ModText.Get("Runtime.Context.Biome.jungle");
			if (player.ZoneSnow) return ModText.Get("Runtime.Context.Biome.snow");
			if (player.ZoneDesert) return ModText.Get("Runtime.Context.Biome.desert");
			if (player.ZoneBeach) return ModText.Get("Runtime.Context.Biome.beach");
			if (player.ZoneGlowshroom) return ModText.Get("Runtime.Context.Biome.mushroom");
			if (player.ZoneMeteor) return ModText.Get("Runtime.Context.Biome.meteor");
			if (player.ZoneUnderworldHeight) return ModText.Get("Runtime.Context.Biome.underworld");
			if (player.ZoneSkyHeight) return ModText.Get("Runtime.Context.Biome.sky");
			if (player.ZoneForest) return ModText.Get("Runtime.Context.Biome.forest");
			return ModText.Get("Runtime.Context.Biome.unknown");
		}

		public static string DescribeLayer(Player player) {
			if (player.ZoneUnderworldHeight) return ModText.Get("Runtime.Context.Layer.hell");
			if (player.ZoneRockLayerHeight) return ModText.Get("Runtime.Context.Layer.cave");
			if (player.ZoneDirtLayerHeight) return ModText.Get("Runtime.Context.Layer.underground");
			if (player.ZoneSkyHeight) return ModText.Get("Runtime.Context.Layer.sky");
			return ModText.Get("Runtime.Context.Layer.surface");
		}

		public static string DescribeWeather(Player player) {
			if (Sandstorm.Happening) return ModText.Get("Runtime.Context.Weather.sandstorm");
			bool strongWind = System.Math.Abs(Main.windSpeedCurrent) >= 0.4f;
			if (Main.raining && player.ZoneSnow) return ModText.Get("Runtime.Context.Weather.blizzard");
			if (Main.raining && strongWind) return ModText.Get("Runtime.Context.Weather.thunderstorm");
			if (Main.raining) return ModText.Get("Runtime.Context.Weather.rain");
			if (Main.IsItAHappyWindyDay || (Main.dayTime && strongWind)) return ModText.Get("Runtime.Context.Weather.windy");
			if (Main.cloudAlpha > 0.45f) return ModText.Get("Runtime.Context.Weather.cloudy");
			return ModText.Get("Runtime.Context.Weather.clear");
		}

		public static string FormatGameTime() {
			double time = Main.time;
			int hours;
			int minutes;
			if (Main.dayTime) {
				double dayLen = 54000.0;
				double t = time / dayLen;
				double hoursF = 4.5 + t * 15.0;
				hours = ((int)hoursF) % 24;
				minutes = (int)((hoursF - (int)hoursF) * 60);
			}
			else {
				double nightLen = 32400.0;
				double t = time / nightLen;
				double hoursF = 19.5 + t * 9.0;
				hours = ((int)hoursF) % 24;
				minutes = (int)((hoursF - (int)hoursF) * 60);
			}

			return $"{hours:00}:{minutes:00}";
		}
	}

	public class PerceptionSnapshot
	{
		public string Biome;
		public string Layer;
		public string Weather;
		public string GameClock;
		public bool BloodMoon;
		public bool Eclipse;
		public bool Raining;
		public bool DayTime;
		public bool PlayerLowLife;
		public int EnemyCount;
		public int EnemiesTargetingOwner;
		public string EnemySummary;
		public string NearbyTownNpcs;
		public string BossName;
		public float BossDist = float.MaxValue;
		public int BossLifePct;
	}
}
