using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Configs;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerIntentParser
	{
		public static async Task<SuperpowerIntent> ClassifyAsync(
			AinpcServerConfig config,
			NPC npc,
			Player player,
			string utterance) {
			var allowed = new StringBuilder();
			allowed.Append(SuperpowerIds.ListPowers).Append(", ").Append(SuperpowerIds.Chat);
			foreach (ISuperpower power in SuperpowerRegistry.GetPowers(npc.type)) {
				allowed.Append(", ").Append(power.Id);
			}

			string paramHelp = BuildParamHelp(npc.type);
			string examples = BuildExamples(npc.type);
			string system =
				"你是意图分类器。只输出一个 JSON 对象，不要其它文字。格式："
				+ "{\"intent\":\"...\",\"params\":{},\"confidence\":0.0}。"
				+ "intent 只能是：[" + allowed + "]。"
				+ "闲聊或不确定用 chat；问超能力用 list_powers。"
				+ "结合上下文：刚拒过某能力后的恳求/坚持（求你了、真的、拜托）倾向同一 intent；纠正条件后宜重试能力；换题/取消/无关闲聊 → chat。"
				+ examples + paramHelp
				+ "玩家名：" + (player?.name ?? "") + "。NPC类型id：" + npc.type + "。";

			var messages = new List<ChatMessageDto> {
				new("system", system)
			};

			string context = BuildClassifyContext(player, npc);
			if (!string.IsNullOrEmpty(context)) {
				messages.Add(new ChatMessageDto("system", context));
			}

			messages.Add(new ChatMessageDto("user", utterance ?? ""));

			string raw = null;
			try {
				raw = await AiChatClient.CompleteAsync(config, messages, 160, 0.2f).ConfigureAwait(false);
			}
			catch {
				raw = null;
			}

			return Parse(raw, npc.type);
		}

		private static string BuildClassifyContext(Player player, NPC npc) {
			if (player == null || npc == null) {
				return null;
			}

			string sessionKey = AiConversationMemory.MakeKey(
				AiMemoryStore.PlayerKey(player),
				AiMemoryStore.NpcKey(npc));
			IReadOnlyList<ChatTurn> turns = AiConversationMemory.GetTurns(sessionKey);

			var events = new List<string>();
			var dialogue = new List<ChatTurn>();
			foreach (ChatTurn turn in turns) {
				if (turn.IsEvent) {
					events.Add(turn.Content);
				}
				else {
					dialogue.Add(turn);
				}
			}

			var sb = new StringBuilder();
			if (events.Count > 0) {
				sb.Append("近期事件：");
				int start = Math.Max(0, events.Count - 6);
				for (int i = start; i < events.Count; i++) {
					if (i > start) {
						sb.Append('；');
					}

					sb.Append(events[i]);
				}

				sb.AppendLine();
			}

			string ability = AiPromptBuilder.BuildAbilityStatePrompt(player, npc);
			if (!string.IsNullOrEmpty(ability)) {
				sb.AppendLine(ability);
			}

			if (dialogue.Count > 0) {
				int take = Math.Min(dialogue.Count, 4);
				int start = dialogue.Count - take;
				sb.Append("近期闲聊：");
				for (int i = start; i < dialogue.Count; i++) {
					if (i > start) {
						sb.Append('；');
					}

					ChatTurn t = dialogue[i];
					sb.Append(t.Role == "user" ? "玩家" : "NPC").Append('：').Append(t.Content);
				}
			}

			string text = sb.ToString().Trim();
			return string.IsNullOrEmpty(text) ? null : text;
		}

		private static string BuildParamHelp(int npcType) => npcType switch {
			NPCID.Guide =>
				"params：guide_weather.action=rain_start|rain_stop|sandstorm_start|sandstorm_stop|windy_day_start|windy_day_stop|thunderstorm_start|thunderstorm_stop|wind_start|wind_stop|wind_set|clear，可选 windSpeed=40~60；"
				+ "guide_world_query.query=biome|structure|ore|chest|life_crystal|life_fruit，target=中文目标。其它 params 可空。",
			NPCID.Merchant => "params：merchant_delivery.itemHint=物品中文简述。其它可空。",
			_ => "params 可为空对象。"
		};

		private static string BuildExamples(int npcType) => npcType switch {
			NPCID.GoblinTinkerer => "示例：重铸/加词缀/附魔 → goblin_temp_prefix。",
			NPCID.Merchant => "示例：收垃圾/卖垃圾 → merchant_buy_trash；送点火把/送货 → merchant_delivery。"
			                 + "收购与送货冷却独立；卖垃圾≠玩家买货≠送货。",
			NPCID.Nurse => "示例：我要死了/救我/快来救我 → nurse_emergency_heal；刚被拒后说求你了/真的 → 仍 nurse_emergency_heal。",
			NPCID.Dryad => "示例：来个增益/来个群体 → dryad_group_buff；刚被拒后说求你了/真的 → 仍 dryad_group_buff。",
			NPCID.ArmsDealer => "示例：给我一把枪 → arms_temp_weapon。",
			NPCID.Demolitionist => "示例：炸开这里/放雷管 → demo_blast；火力支援/扔雷掩护 → demo_fire_support；冷却独立。",
			NPCID.Cyborg => "示例：火力支援 → cyborg_fire_support。",
			NPCID.TaxCollector => "示例：给我点钱 → tax_grant_gold。",
			NPCID.Guide => "示例：下雨/暴雪/沙尘暴/雷雨/大风天/调风/晴天 → guide_weather；清理掉落物 → guide_clear_items；丛林/沙漠/花岗岩/大理石/地牢/空岛/微光/暗影珠/猩红之心/附魔剑/祭坛/地狱熔炉/金矿/钻石/生命水晶在哪、箱子里有没有xx → guide_world_query。天气/清物/指路冷却独立。",
			_ => ""
		};

		public static SuperpowerIntent Parse(string raw, int npcType) {
			var fallback = new SuperpowerIntent { Intent = SuperpowerIds.Chat, Confidence = 0f };
			if (string.IsNullOrWhiteSpace(raw)) {
				return fallback;
			}

			string json = ExtractJsonObject(raw);
			if (json == null) {
				return fallback;
			}

			try {
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				string intent = root.TryGetProperty("intent", out JsonElement intentEl)
					? intentEl.GetString()
					: SuperpowerIds.Chat;
				float confidence = 0f;
				if (root.TryGetProperty("confidence", out JsonElement confEl)
				    && confEl.ValueKind == JsonValueKind.Number) {
					confidence = confEl.GetSingle();
				}

				var parms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				if (root.TryGetProperty("params", out JsonElement paramsEl)
				    && paramsEl.ValueKind == JsonValueKind.Object) {
					foreach (JsonProperty prop in paramsEl.EnumerateObject()) {
						parms[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
							? prop.Value.GetString()
							: prop.Value.ToString();
					}
				}

				intent = (intent ?? SuperpowerIds.Chat).Trim();
				if (intent != SuperpowerIds.Chat
				    && intent != SuperpowerIds.ListPowers
				    && !SuperpowerRegistry.IsAbilityForNpc(npcType, intent)) {
					return fallback;
				}

				return new SuperpowerIntent {
					Intent = intent,
					Confidence = confidence,
					Params = parms
				};
			}
			catch {
				return fallback;
			}
		}

		private static string ExtractJsonObject(string raw) {
			raw = raw.Trim();
			int start = raw.IndexOf('{');
			int end = raw.LastIndexOf('}');
			if (start < 0 || end <= start) {
				return null;
			}

			return raw.Substring(start, end - start + 1);
		}
	}
}
