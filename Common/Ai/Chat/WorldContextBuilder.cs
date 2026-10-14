using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;

namespace AITownNPCs.Common.Ai.Chat
{
	public static class WorldContextBuilder
	{
		public static string Build(Player player, NPC speaker, float nearbyRadius = 800f) {
			if (player == null || !player.active) {
				return "无玩家上下文。";
			}

			Vector2 origin = speaker != null && speaker.active
				? speaker.Center
				: player.Center;

			PerceptionSnapshot snap = Capture(player, speaker, nearbyRadius);

			var sb = new StringBuilder();
			sb.AppendLine("当前环境（结构化感知）：");
			sb.Append("生物群系=").Append(snap.Biome).Append('；');
			sb.Append("地形层=").Append(snap.Layer).Append('；');
			sb.Append("坐标=(").Append((int)(origin.X / 16f)).Append(',').Append((int)(origin.Y / 16f)).Append(")；");
			sb.Append("深度=").Append(snap.Layer).Append('；');
			sb.Append("时间=").Append(snap.DayTime ? "白天" : "夜晚");
			sb.Append(' ').Append(snap.GameClock).Append('；');
			if (snap.BloodMoon) sb.Append("血月；");
			if (snap.Eclipse) sb.Append("日食；");
			sb.Append("天气=").Append(snap.Weather).Append('；');
			sb.AppendLine();

			sb.Append("玩家=").Append(player.name);
			sb.Append(" 生命=").Append(player.statLife).Append('/').Append(player.statLifeMax2);
			sb.Append(" 魔力=").Append(player.statMana).Append('/').Append(player.statManaMax2);
			if (snap.PlayerLowLife) {
				sb.Append("（低血量）");
			}

			sb.AppendLine();

			sb.Append("附近敌怪数量=").Append(snap.EnemyCount);
			if (snap.EnemySummary != null) {
				sb.Append(" 主要=").Append(snap.EnemySummary);
			}

			if (snap.EnemiesTargetingOwner > 0) {
				sb.Append(" 正在攻击玩家=").Append(snap.EnemiesTargetingOwner);
			}

			sb.AppendLine();
			if (snap.BossName != null) {
				sb.Append("Boss在场=是：").Append(snap.BossName).Append(" 约").Append(snap.BossLifePct).Append('%').AppendLine();
			}
			else {
				sb.AppendLine("Boss在场=否");
			}

			sb.Append("附近其他城镇NPC=").Append(
				string.IsNullOrEmpty(snap.NearbyTownNpcs) ? "无" : snap.NearbyTownNpcs);
			return sb.ToString();
		}

		/// <summary>Compact world line for proactive / superpower-result replies.</summary>
		public static string BuildCompact(Player player, NPC speaker) {
			if (player == null || !player.active) {
				return "无玩家上下文。";
			}

			PerceptionSnapshot snap = Capture(player, speaker, 800f);
			var sb = new StringBuilder();
			sb.Append("环境精简：生物群系=").Append(snap.Biome);
			sb.Append('；').Append(snap.DayTime ? "白天" : "夜晚");
			sb.Append("；天气=").Append(snap.Weather);
			if (snap.PlayerLowLife) {
				sb.Append("；玩家低血量");
			}

			if (snap.BossName != null) {
				sb.Append("；Boss=").Append(snap.BossName).Append("约").Append(snap.BossLifePct).Append('%');
			}

			return sb.ToString();
		}

		/// <summary>Structured snapshot used by follower ambient sensing and Build.</summary>
		public static PerceptionSnapshot Capture(Player player, NPC speaker, float nearbyRadius = 800f) {
			var snap = new PerceptionSnapshot {
				Biome = player != null && player.active ? DescribeBiome(player) : "未知",
				Layer = player != null && player.active ? DescribeLayer(player) : "未知",
				Weather = player != null && player.active ? DescribeWeather(player) : "未知",
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

			var sb = new StringBuilder();
			sb.Append("（跟随闲聊）你正跟着玩家").Append(player.name).Append("。");
			sb.Append("眼下最值得随口一提的是：").Append(focus).Append('。');
			sb.Append("结合你们近期相处记忆，用约15字自然说一句，像同行伙伴随口搭话；");
			sb.Append("不要罗列环境、不要播报状态");
			trigger = sb.ToString();
			return true;
		}

		/// <summary>Focused event line for biome/boss/low-life style triggers.</summary>
		public static string BuildFollowerEventTrigger(Player player, string focus) {
			return "（跟随事件）你正跟着玩家" + player.name + "。"
				+ "刚发生：" + focus + "。"
				+ "结合短期记忆用约15字自然反应一句，像伙伴随口说，不要播报";
		}

		private static bool TryPickAmbientFocus(PerceptionSnapshot snap, out string focus, out string topic) {
			focus = null;
			topic = null;

			if (snap.BossName != null) {
				topic = "boss:" + snap.BossName;
				focus = "Boss「" + snap.BossName + "」还在附近（约" + snap.BossLifePct + "%）";
				return true;
			}

			if (snap.PlayerLowLife) {
				topic = "lowlife";
				focus = "玩家血量很低";
				return true;
			}

			if (snap.BloodMoon) {
				topic = "bloodmoon";
				focus = "今晚是血月";
				return true;
			}

			if (snap.Eclipse) {
				topic = "eclipse";
				focus = "外面在日食";
				return true;
			}

			if (snap.EnemyCount >= 4) {
				topic = "enemies:" + snap.EnemyCount;
				focus = snap.EnemySummary != null
					? "附近敌怪不少（" + snap.EnemySummary + "）"
					: "附近敌怪不少";
				return true;
			}

			if (snap.EnemiesTargetingOwner > 0) {
				topic = "aggro:" + snap.EnemiesTargetingOwner;
				focus = "有敌怪正在盯着玩家";
				return true;
			}

			// Soft ambient: biome + day/night only when nothing urgent.
			topic = "soft:" + snap.Biome + ":" + (snap.DayTime ? "day" : "night") + ":" + snap.Weather;
			focus = snap.Biome + "里，" + (snap.DayTime ? "白天" : "夜晚") + "，天气" + snap.Weather;
			return true;
		}

		public static string DescribeBiome(Player player) {
			if (player.ZoneDungeon) return "地牢";
			if (player.ZoneCorrupt) return "腐化";
			if (player.ZoneCrimson) return "猩红";
			if (player.ZoneHallow) return "神圣";
			if (player.ZoneJungle) return "丛林";
			if (player.ZoneSnow) return "雪原";
			if (player.ZoneDesert) return "沙漠";
			if (player.ZoneBeach) return "海滩";
			if (player.ZoneGlowshroom) return "蘑菇地";
			if (player.ZoneMeteor) return "陨石";
			if (player.ZoneUnderworldHeight) return "地狱";
			if (player.ZoneSkyHeight) return "空岛";
			if (player.ZoneForest) return "森林";
			return "未知";
		}

		public static string DescribeLayer(Player player) {
			if (player.ZoneUnderworldHeight) return "地狱";
			if (player.ZoneRockLayerHeight) return "洞穴";
			if (player.ZoneDirtLayerHeight) return "地下";
			if (player.ZoneSkyHeight) return "天空";
			return "地表";
		}

		public static string DescribeWeather(Player player) {
			if (Sandstorm.Happening) return "沙暴";
			bool strongWind = System.Math.Abs(Main.windSpeedCurrent) >= 0.4f;
			if (Main.raining && player.ZoneSnow) return "暴雪";
			if (Main.raining && strongWind) return "雷雨";
			if (Main.raining) return "下雨";
			if (Main.IsItAHappyWindyDay || (Main.dayTime && strongWind)) return "大风天";
			if (Main.cloudAlpha > 0.45f) return "多云";
			return "晴";
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
