using System.Collections.Generic;
using System.Threading.Tasks;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Superpower.Merchant;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerOrchestrator
	{
		public static async Task<string> HandlePlayerChatAsync(
			AinpcServerConfig config,
			Player player,
			NPC npc,
			string utterance) {
			int pinWho = player?.whoAmI ?? -1;
			try {
				if (config == null
				    || !config.SuperpowerEnabled
				    || !AiRuntimeGate.CanUseAiFeatures
				    || player == null
				    || npc == null) {
					return await NormalReplyAsync(config, player, npc, utterance, null).ConfigureAwait(false);
				}

				SuperpowerRegistry.EnsureInitialized();

				if (npc.type == NPCID.Merchant
				    && MerchantPendingQuoteStore.TryGet(player.whoAmI, out MerchantPendingQuote quote)
				    && !quote.IsExpired()) {
					return await HandleMerchantConfirmAsync(player, npc, utterance, quote)
						.ConfigureAwait(false);
				}

				if (!SuperpowerRegistry.HasPowers(npc.type)) {
					return await NormalReplyAsync(config, player, npc, utterance, null).ConfigureAwait(false);
				}

				SuperpowerIntent intent = await SuperpowerIntentParser.ClassifyAsync(config, npc, player, utterance)
					.ConfigureAwait(false);

				if (intent.Intent == SuperpowerIds.Chat) {
					return await NormalReplyAsync(config, player, npc, utterance, null).ConfigureAwait(false);
				}

				SuperpowerResult result = null;
				string refuseStyle = "";

				if (intent.Intent == SuperpowerIds.ListPowers) {
					result = BuildListPowersResult(npc);
				}
				else if (SuperpowerRegistry.TryGet(intent.Intent, out ISuperpower power)
				         && power.NpcType == npc.type) {
					refuseStyle = power.RefuseStyleZh;
					result = await RunPowerAsync(player, npc, power, intent).ConfigureAwait(false);
				}
				else {
					return await NormalReplyAsync(config, player, npc, utterance, null).ConfigureAwait(false);
				}

				if (!string.IsNullOrEmpty(refuseStyle) && result != null
				    && result.Outcome == SuperpowerOutcome.Refused
				    && !result.Facts.Exists(f => f.StartsWith("拒绝风格"))) {
					result.Facts.Add("拒绝风格：" + refuseStyle);
				}

				if (result != null && result.Outcome == SuperpowerOutcome.Quoted
				    && MerchantPendingQuoteStore.TryGet(player.whoAmI, out MerchantPendingQuote fresh)
				    && !fresh.IsExpired()) {
					RecordOutcome(player, npc, result);
					return fresh.BuildQuoteAnnounce();
				}

				string reply = await NormalReplyAsync(config, player, npc, utterance, result).ConfigureAwait(false);
				RecordOutcome(player, npc, result);
				return reply;
			}
			finally {
				PlayerSuperpowerClientSnapshot.Unpin(pinWho);
			}
		}

		private static async Task<SuperpowerResult> RunPowerAsync(
			Player player,
			NPC npc,
			ISuperpower power,
			SuperpowerIntent intent) {
			SuperpowerResult gate = SuperpowerGate.TryRefuse(player, npc, power);
			if (gate != null) {
				return gate;
			}

			var tcs = new TaskCompletionSource<SuperpowerResult>();
			Main.QueueMainThreadAction(() => {
				try {
					if (!player.active || !npc.active) {
						tcs.SetResult(SuperpowerResult.Failed(power.Id, "inactive", "能力执行失败"));
						return;
					}

					SuperpowerResult exec = power.Execute(player, npc, intent);
					if (exec != null && exec.StartCooldown) {
						SuperpowerCooldownStore.Start(player.whoAmI, npc.type, power.Id, power.CooldownFrames);
					}

					tcs.SetResult(exec ?? SuperpowerResult.Failed(power.Id, "null", "能力执行失败"));
				}
				catch {
					tcs.SetResult(SuperpowerResult.Failed(power.Id, "exception", "能力执行失败"));
				}
			});
			return await tcs.Task.ConfigureAwait(false);
		}

		private static async Task<string> HandleMerchantConfirmAsync(
			Player player,
			NPC npc,
			string utterance,
			MerchantPendingQuote quote) {
			string trimmed = (utterance ?? "").Trim();
			if (MerchantConfirmRules.IsConfirm(trimmed)) {
				return await CommitQuoteScriptedAsync(player, npc, quote).ConfigureAwait(false);
			}

			if (MerchantConfirmRules.IsCancel(trimmed)) {
				MerchantPendingQuoteStore.Clear(player.whoAmI);
				RecordEvent(player, npc, "能力取消：收购垃圾 — 玩家不要了");
				return "那不要了。";
			}

			return "没听清，回复「可以」成交、「不要」取消。";
		}

		private static async Task<string> CommitQuoteScriptedAsync(
			Player player,
			NPC npc,
			MerchantPendingQuote quote) {
			long total = quote.TotalPrice;
			var tcs = new TaskCompletionSource<string>();
			Main.QueueMainThreadAction(() => {
				try {
					SuperpowerResult result = MerchantBuyTrashPower.CommitQuote(player, quote);
					RecordOutcome(player, npc, result);
					if (result.Outcome == SuperpowerOutcome.Ok) {
						tcs.SetResult("成交，货款" + SuperpowerCoinUtil.FormatCopper(total) + "收好。");
					}
					else if (result.ReasonCode == "stale" || result.ReasonCode == "expired") {
						tcs.SetResult("背包变了或报价过期，重新说收垃圾吧。");
					}
					else {
						tcs.SetResult("这单没成交。");
					}
				}
				catch {
					tcs.SetResult("这单没成交。");
				}
			});
			return await tcs.Task.ConfigureAwait(false);
		}

		private static SuperpowerResult BuildListPowersResult(NPC npc) {
			var facts = new List<string>();
			foreach (ISuperpower power in SuperpowerRegistry.GetPowers(npc.type)) {
				facts.Add(power.DisplayNameZh + "：" + power.DescriptionZh);
			}

			if (facts.Count == 0) {
				return SuperpowerResult.Info("暂无超能力");
			}

			return SuperpowerResult.Info("已说明超能力列表", facts.ToArray());
		}

		private static void RecordOutcome(Player player, NPC npc, SuperpowerResult result) {
			if (result == null) {
				return;
			}

			string note = FormatOutcomeEvent(result);
			if (!string.IsNullOrEmpty(note)) {
				RecordEvent(player, npc, note);
			}
		}

		private static void RecordEvent(Player player, NPC npc, string note) {
			if (player == null || npc == null || string.IsNullOrWhiteSpace(note)) {
				return;
			}

			AiConversationMemory.AppendEvent(player, npc, note);
		}

		private static string FormatOutcomeEvent(SuperpowerResult result) {
			string name = result.AbilityId ?? "";
			if (!string.IsNullOrEmpty(result.AbilityId)
			    && SuperpowerRegistry.TryGet(result.AbilityId, out ISuperpower power)) {
				name = power.DisplayNameZh;
			}

			string detail = result.ResolveFixedResultZh();
			return result.Outcome switch {
				SuperpowerOutcome.Ok => "能力成功：" + name + " — " + detail,
				SuperpowerOutcome.Quoted => "能力报价：" + name + " — " + detail,
				SuperpowerOutcome.Refused => "能力拒绝：" + name + " — " + detail,
				SuperpowerOutcome.Failed => "能力失败：" + name + " — " + detail,
				SuperpowerOutcome.Info => detail,
				_ => null
			};
		}

		/// <summary>Guide world-query: FixedResultZh is the spoken line; skip reply LLM.</summary>
		private static string TryGetFixedReplyLine(SuperpowerResult result) {
			if (result == null
			    || result.Outcome != SuperpowerOutcome.Ok
			    || result.AbilityId != SuperpowerIds.GuideWorldQuery) {
				return null;
			}

			string line = result.ResolveFixedResultZh();
			return string.IsNullOrWhiteSpace(line) ? null : line;
		}

		private static async Task<string> NormalReplyAsync(
			AinpcServerConfig config,
			Player player,
			NPC npc,
			string utterance,
			SuperpowerResult result) {
			if (TryGetFixedReplyLine(result) is { } readyLine) {
				return readyLine;
			}

			var messages = AiPromptBuilder.Build(npc, player, utterance,
				compactWorld: result != null,
				omitAbilityState: result != null,
				hasFixedResult: result != null);
			if (result != null) {
				messages.Insert(messages.Count - 1, new ChatMessageDto("system", result.BuildFactsBlock()));
			}

			int maxTokens = System.Math.Max(256, config?.AiMaxTokens ?? 256);
			if (result != null && result.Outcome == SuperpowerOutcome.Info) {
				maxTokens = System.Math.Max(maxTokens, 400);
			}

			float? temperature = result != null ? 0.35f : null;
			string reply = null;
			try {
				reply = await AiChatClient.CompleteAsync(config, messages, maxTokens, temperature)
					.ConfigureAwait(false);
			}
			catch {
				reply = null;
			}

			if (string.IsNullOrWhiteSpace(reply)) {
				return null;
			}

			return AiPromptBuilder.CleanReply(reply);
		}
	}
}
