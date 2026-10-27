using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Configs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerIntentParser
	{
		private const string LocRoot = "Mods.AITownNPCs.Superpower.IntentClassify.";

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
			string system = Language.GetTextValue(
				LocRoot + "System",
				allowed.ToString(),
				examples,
				paramHelp,
				player?.name ?? "",
				npc.type).Trim();

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

			string partSep = Language.GetTextValue("Mods.AITownNPCs.Superpower.Prompt.PartSep");
			string roleSep = Language.GetTextValue(LocRoot + "RoleSep");
			string playerLabel = Language.GetTextValue(LocRoot + "PlayerLabel");
			string npcLabel = Language.GetTextValue(LocRoot + "NpcLabel");

			var sb = new StringBuilder();
			if (events.Count > 0) {
				sb.Append(Language.GetTextValue(LocRoot + "RecentEvents"));
				int start = Math.Max(0, events.Count - 6);
				for (int i = start; i < events.Count; i++) {
					if (i > start) {
						sb.Append(partSep);
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
				sb.Append(Language.GetTextValue(LocRoot + "RecentChat"));
				for (int i = start; i < dialogue.Count; i++) {
					if (i > start) {
						sb.Append(partSep);
					}

					ChatTurn t = dialogue[i];
					sb.Append(t.Role == "user" ? playerLabel : npcLabel).Append(roleSep).Append(t.Content);
				}
			}

			string text = sb.ToString().Trim();
			return string.IsNullOrEmpty(text) ? null : text;
		}

		private static string BuildParamHelp(int npcType) {
			string key = npcType switch {
				NPCID.Guide => LocRoot + "ParamHelp.Guide",
				NPCID.Merchant => LocRoot + "ParamHelp.Merchant",
				_ => LocRoot + "ParamHelp.Default"
			};
			return Language.GetTextValue(key).Trim();
		}

		private static string BuildExamples(int npcType) {
			string suffix = npcType switch {
				NPCID.GoblinTinkerer => "GoblinTinkerer",
				NPCID.Merchant => "Merchant",
				NPCID.Nurse => "Nurse",
				NPCID.Dryad => "Dryad",
				NPCID.ArmsDealer => "ArmsDealer",
				NPCID.Demolitionist => "Demolitionist",
				NPCID.Cyborg => "Cyborg",
				NPCID.TaxCollector => "TaxCollector",
				NPCID.Guide => "Guide",
				_ => null
			};

			if (suffix == null) {
				return "";
			}

			return Language.GetTextValue(LocRoot + "Examples." + suffix).Trim();
		}

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
