using System.Collections.Generic;
using System.Text;
using AITownNPCs.Common.Superpower;
using AITownNPCs.Common.Superpower.Merchant;
using Terraria;

namespace AITownNPCs.Common.Ai.Chat
{
	public static class AiPromptBuilder
	{
		public static List<ChatMessageDto> Build(
			NPC npc,
			Player player,
			string userUtterance,
			bool compactWorld = false,
			bool omitAbilityState = false,
			bool hasFixedResult = false) {
			string npcKey = AiMemoryStore.NpcKey(npc);
			string playerKey = AiMemoryStore.PlayerKey(player);
			string sessionKey = AiConversationMemory.MakeKey(playerKey, npcKey);

			var messages = new List<ChatMessageDto> {
				new("system", NpcPersonaCatalog.BuildIdentitySystemPrompt(npc, hasFixedResult))
			};

			string shortMemory = BuildShortMemoryPrompt(sessionKey);
			if (!string.IsNullOrEmpty(shortMemory)) {
				messages.Add(new ChatMessageDto("system", shortMemory));
			}

			if (!omitAbilityState) {
				string abilityState = BuildAbilityStatePrompt(player, npc);
				if (!string.IsNullOrEmpty(abilityState)) {
					messages.Add(new ChatMessageDto("system", abilityState));
				}
			}

			string world = compactWorld
				? WorldContextBuilder.BuildCompact(player, npc)
				: WorldContextBuilder.Build(player, npc);
			messages.Add(new ChatMessageDto("system", world));

			foreach (ChatTurn turn in AiConversationMemory.GetTurns(sessionKey)) {
				if (turn.IsEvent) {
					continue;
				}

				messages.Add(new ChatMessageDto(turn.Role, turn.Content));
			}

			messages.Add(new ChatMessageDto("user", userUtterance));
			return messages;
		}

		/// <summary>Soft cleanup only — preserves item chat tags.</summary>
		public static string CleanReply(string text) {
			if (string.IsNullOrEmpty(text)) {
				return text;
			}

			if (text.Contains("[i/") || text.Contains("[i:")) {
				return text.Replace("\r", " ").Replace("\n", " ").Trim();
			}

			text = text.Replace("\r", " ").Replace("\n", " ").Trim();
			while (text.Contains("  ")) {
				text = text.Replace("  ", " ");
			}

			return text;
		}

		public static string BuildAbilityStatePrompt(Player player, NPC npc) {
			if (player == null || npc == null) {
				return null;
			}

			SuperpowerRegistry.EnsureInitialized();
			IReadOnlyList<ISuperpower> powers = SuperpowerRegistry.GetPowers(npc.type);
			var parts = new List<string>();

			if (powers.Count > 0) {
				foreach (ISuperpower power in powers) {
					if (SuperpowerCooldownStore.IsOnCooldown(player.whoAmI, npc.type, power.Id, out int remain)) {
						int sec = (remain + 59) / 60;
						parts.Add(FormatCooldownPart(power, sec));
					}
				}
			}

			if (npc.type == Terraria.ID.NPCID.Merchant
			    && MerchantPendingQuoteStore.TryGet(player.whoAmI, out MerchantPendingQuote quote)
			    && !quote.IsExpired()) {
				parts.Add("有待确认的收购垃圾报价，总价"
					+ SuperpowerCoinUtil.FormatCopper(quote.TotalPrice));
			}

			if (parts.Count == 0) {
				return null;
			}

			var sb = new StringBuilder();
			sb.Append("能力状态：");
			if (powers.Count > 1) {
				sb.Append("冷却彼此独立。");
			}

			for (int i = 0; i < parts.Count; i++) {
				if (i > 0) {
					sb.Append('；');
				}

				sb.Append(parts[i]);
			}

			return sb.ToString();
		}

		private static string FormatCooldownPart(ISuperpower power, int sec) {
			return power.Id switch {
				SuperpowerIds.MerchantBuyTrash =>
					"收购垃圾冷却约" + sec + "秒（非送货）",
				SuperpowerIds.MerchantDelivery =>
					"紧急送货冷却约" + sec + "秒（非收购）",
				SuperpowerIds.DemoBlast =>
					"远程爆破冷却约" + sec + "秒（非火力支援）",
				SuperpowerIds.DemoFireSupport =>
					"火力支援冷却约" + sec + "秒（非放雷管）",
				SuperpowerIds.GuideWeather =>
					"调整天气冷却约" + sec + "秒",
				SuperpowerIds.GuideClearItems =>
					"清除掉落物冷却约" + sec + "秒",
				SuperpowerIds.GuideWorldQuery =>
					"世界指引冷却约" + sec + "秒",
				_ => "「" + power.DisplayNameZh + "」冷却约" + sec + "秒"
			};
		}

		/// <summary>Null when no events — omit the system block entirely.</summary>
		private static string BuildShortMemoryPrompt(string sessionKey) {
			IReadOnlyList<ChatTurn> turns = AiConversationMemory.GetTurns(sessionKey);
			var events = new List<string>();
			foreach (ChatTurn turn in turns) {
				if (turn.IsEvent) {
					events.Add(turn.Content);
				}
			}

			if (events.Count == 0) {
				return null;
			}

			var sb = new StringBuilder();
			sb.Append("短期记忆：");
			int start = System.Math.Max(0, events.Count - 12);
			for (int i = start; i < events.Count; i++) {
				if (i > start) {
					sb.Append('；');
				}

				sb.Append(events[i]);
			}

			return sb.ToString();
		}
	}
}
