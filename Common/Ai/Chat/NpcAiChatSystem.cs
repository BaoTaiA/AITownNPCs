using System.Collections.Generic;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Ai.Follow;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Chat
{
	public class NpcAiChatSystem : ModSystem
	{
		private static readonly bool[] NotifiedNoModel = new bool[Main.maxPlayers];
		private static readonly HashSet<int> PendingTipWho = new();

		public static float ChatRange() => NpcDialoguePolicy.ChatRange();

		public static List<NPC> CollectNpcsInChatRange(Player player, float range = -1f) {
			var list = new List<NPC>();
			if (player == null || !player.active) {
				return list;
			}

			if (range < 0f) {
				range = ChatRange();
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!NpcDialoguePolicy.IsDistanceChatCandidate(npc)) {
					continue;
				}

				if (Vector2.Distance(player.Center, npc.Center) < range) {
					list.Add(npc);
				}
			}

			return list;
		}

		public static int CountPlayersNearNpc(NPC npc, float range = -1f) {
			if (npc == null || !npc.active) {
				return 0;
			}

			if (range < 0f) {
				range = ChatRange();
			}

			int count = 0;
			for (int p = 0; p < Main.maxPlayers; p++) {
				Player other = Main.player[p];
				if (other == null || !other.active || other.dead) {
					continue;
				}

				if (Vector2.Distance(other.Center, npc.Center) < range) {
					count++;
				}
			}

			return count;
		}

		public static void TryHandlePlayerChat(int playerWhoAmI, string text) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			bool startedAi = false;
			try {
				AinpcServerConfig config = AinpcServerConfig.Instance;
				if (config == null || !config.AiEnabled || !AiRuntimeGate.CanUseAiFeatures) {
					return;
				}

				if (string.IsNullOrWhiteSpace(text)) {
					return;
				}

				if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
					return;
				}

				Player player = Main.player[playerWhoAmI];
				if (player == null || !player.active) {
					return;
				}

				if (!NpcAiFacade.IsReady(config)) {
					return;
				}

				string trimmed = text.Trim();
				NPC target = null;
				string utterance = trimmed;

				if (NpcAddressResolver.TryResolve(trimmed, out NpcAddressResolver.AddressMatch addressed)) {
					target = addressed.Npc;
					utterance = string.IsNullOrWhiteSpace(addressed.StrippedText)
						? "（玩家在呼叫你）"
						: addressed.StrippedText;
				}
				else if (trimmed.StartsWith("@")) {
					target = NpcFollowQuery.FindFollower(player);
					if (target == null) {
						return;
					}

					utterance = trimmed.Length > 1
						? trimmed.Substring(1).TrimStart()
						: "";
					if (string.IsNullOrWhiteSpace(utterance)) {
						utterance = "（玩家在呼叫你）";
					}
				}
				else {
					List<NPC> near = CollectNpcsInChatRange(player);
					if (near.Count == 0) {
						return;
					}

					if (near.Count >= 2) {
						return;
					}

					target = near[0];
				}

				if (target == null || !target.active) {
					return;
				}

				if (!NpcAiRateLimit.TryAllow(playerWhoAmI)) {
					NpcAiSpeech.NotifyPlayer(playerWhoAmI, "Mods.AITownNPCs.Ai.RateLimited", NpcAiSpeech.WarningColor);
					return;
				}

				if (NpcAiFacade.RequestReply(player, target, utterance, proactive: false)) {
					NpcAiRateLimit.Record(playerWhoAmI);
					startedAi = true;
				}
			}
			finally {
				if (!startedAi) {
					Superpower.PlayerSuperpowerClientSnapshot.Unpin(playerWhoAmI);
				}
			}
		}

		public override void OnWorldLoad() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			ClearNoModelNoticeFlags();
			PendingTipWho.Clear();
			StartWorldProbe();
		}

		public override void OnWorldUnload() {
			NpcAiFacade.ClearWorldState();
			AiConversationMemory.ClearAll();
			NpcAiRateLimit.Clear();
			AiRuntimeGate.Clear();
			ClearNoModelNoticeFlags();
			PendingTipWho.Clear();
		}

		/// <summary>Config changed: re-probe; tip online players if probe fails.</summary>
		public static void OnConfigChangedRestartProbe() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			ClearNoModelNoticeFlags();
			PendingTipWho.Clear();
			for (int p = 0; p < Main.maxPlayers; p++) {
				Player player = Main.player[p];
				if (player != null && player.active) {
					PendingTipWho.Add(p);
				}
			}

			StartWorldProbe();
		}

		private static void StartWorldProbe() {
			AiRuntimeGate.StartProbe(AinpcServerConfig.Instance, () => {
				Main.QueueMainThreadAction(OnProbeFinishedMainThread);
			});
		}

		private static void OnProbeFinishedMainThread() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (AiRuntimeGate.Phase == AiProbePhase.Fail) {
				FlushPendingTips();
				if (!Main.gameMenu
				    && (Main.netMode == NetmodeID.SinglePlayer
				        || (Main.netMode == NetmodeID.Server && !Main.dedServ))) {
					TryNotifyNoModel(Main.myPlayer);
				}
			}
			else {
				PendingTipWho.Clear();
			}
		}

		/// <summary>
		/// Called when a player has joined the world (server: after ChatHistoryRequest).
		/// </summary>
		public static void OnPlayerJoinedWorld(int playerWhoAmI) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			// Dedicated rejoin does not unload the world — reset so each join can tip again.
			NotifiedNoModel[playerWhoAmI] = false;

			if (AiRuntimeGate.Phase == AiProbePhase.Running || AiRuntimeGate.Phase == AiProbePhase.Idle) {
				if (AiRuntimeGate.Phase == AiProbePhase.Running) {
					PendingTipWho.Add(playerWhoAmI);
				}

				return;
			}

			TryNotifyNoModel(playerWhoAmI);
		}

		public static void TryNotifyNoModel(int playerWhoAmI) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (!AiRuntimeGate.AutoDisabledNoModel) {
				return;
			}

			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			if (NotifiedNoModel[playerWhoAmI]) {
				return;
			}

			string modName = ModContent.GetInstance<AITownNPCs>()?.DisplayName ?? "AI Town NPCs";
			string text = Language.GetTextValue("Mods.AITownNPCs.Ai.NoModelAutoDisabled", modName);
			if (string.IsNullOrWhiteSpace(text)
			    || text.StartsWith("Mods.", System.StringComparison.Ordinal)) {
				text = modName + "没有可用的大模型，AI对话与超能力已自动关闭";
			}

			if (NpcAiSpeech.NotifyPlayerLiteral(playerWhoAmI, text, NpcAiSpeech.WarningColor)) {
				NotifiedNoModel[playerWhoAmI] = true;
				PendingTipWho.Remove(playerWhoAmI);
			}
		}

		private static void FlushPendingTips() {
			if (PendingTipWho.Count == 0) {
				return;
			}

			int[] copy = new int[PendingTipWho.Count];
			PendingTipWho.CopyTo(copy);
			PendingTipWho.Clear();
			for (int i = 0; i < copy.Length; i++) {
				TryNotifyNoModel(copy[i]);
			}
		}

		private static void ClearNoModelNoticeFlags() {
			for (int i = 0; i < NotifiedNoModel.Length; i++) {
				NotifiedNoModel[i] = false;
			}
		}
	}
}
