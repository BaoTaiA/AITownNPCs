using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Superpower;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Chat
{
	/// <summary>
	/// AI entry: FIFO queue; player chat may run SuperpowerOrchestrator.
	/// </summary>
	public static class NpcAiFacade
	{
		private const int MaxQueuedPerNpc = 3;

		private sealed class PendingRequest
		{
			public int PlayerWhoAmI;
			public string Utterance;
			public bool Proactive;
			public bool Addressed;
		}

		private static readonly HashSet<int> BusyNpcs = new();
		private static readonly Dictionary<int, int> ReplyGeneration = new();
		private static readonly Dictionary<int, int> PendingRequestOwner = new();
		private static readonly Dictionary<int, Queue<PendingRequest>> ChatQueues = new();

		public static bool IsReady(AinpcServerConfig config) =>
			AiRuntimeGate.CanUseAiFeatures && AiChatClient.IsConfigured(config);

		public static bool RequestReply(
			Player player,
			NPC npc,
			string userUtterance,
			bool proactive,
			bool addressed = false) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return false;
			}

			if (player == null || npc == null || !npc.active || string.IsNullOrWhiteSpace(userUtterance)) {
				return false;
			}

			int npcWho = npc.whoAmI;
			var req = new PendingRequest {
				PlayerWhoAmI = player.whoAmI,
				Utterance = userUtterance.Trim(),
				Proactive = proactive,
				Addressed = addressed && !proactive
			};

			if (proactive) {
				if (BusyNpcs.Contains(npcWho) || HasQueuedChat(npcWho)) {
					return false;
				}

				return StartRequest(npc, req);
			}

			NpcAiProactiveSystem.MarkWalkByCooldown(player.whoAmI, npcWho);

			if (BusyNpcs.Contains(npcWho)) {
				return Enqueue(npcWho, req);
			}

			return StartRequest(npc, req);
		}

		/// <summary>True if this NPC is currently answering or queued for this player's typed chat.</summary>
		public static bool IsEngagedWithPlayer(int npcWhoAmI, int playerWhoAmI) {
			if (BusyNpcs.Contains(npcWhoAmI)
			    && PendingRequestOwner.TryGetValue(npcWhoAmI, out int owner)
			    && owner == playerWhoAmI) {
				return true;
			}

			if (!ChatQueues.TryGetValue(npcWhoAmI, out Queue<PendingRequest> q) || q.Count == 0) {
				return false;
			}

			foreach (PendingRequest r in q) {
				if (!r.Proactive && r.PlayerWhoAmI == playerWhoAmI) {
					return true;
				}
			}

			return false;
		}

		public static void InvalidateForPlayer(int playerWhoAmI) {
			List<int> bump = null;
			foreach (var kv in PendingRequestOwner) {
				if (kv.Value == playerWhoAmI) {
					bump ??= new List<int>();
					bump.Add(kv.Key);
				}
			}

			if (bump != null) {
				foreach (int npcWho in bump) {
					BumpGeneration(npcWho);
					PendingRequestOwner.Remove(npcWho);
					BusyNpcs.Remove(npcWho);
					TryDequeueNext(npcWho);
				}
			}

			foreach (var kv in ChatQueues) {
				Queue<PendingRequest> q = kv.Value;
				if (q.Count == 0) {
					continue;
				}

				var kept = new Queue<PendingRequest>();
				while (q.Count > 0) {
					PendingRequest r = q.Dequeue();
					if (r.PlayerWhoAmI != playerWhoAmI) {
						kept.Enqueue(r);
					}
				}

				while (kept.Count > 0) {
					q.Enqueue(kept.Dequeue());
				}
			}

			SuperpowerCooldownStore.ClearPlayer(playerWhoAmI);
			Superpower.Merchant.MerchantPendingQuoteStore.Clear(playerWhoAmI);
			NpcAiRateLimit.ClearPlayer(playerWhoAmI);
		}

		public static void ClearWorldState() {
			BusyNpcs.Clear();
			ReplyGeneration.Clear();
			PendingRequestOwner.Clear();
			ChatQueues.Clear();
			SuperpowerCooldownStore.Clear();
			Superpower.Merchant.MerchantPendingQuoteStore.ClearAll();
			NpcAiRateLimit.Clear();
		}

		private static bool HasQueuedChat(int npcWho) =>
			ChatQueues.TryGetValue(npcWho, out Queue<PendingRequest> q) && q.Count > 0;

		/// <summary>Returns false when the per-NPC queue is full (silent drop).</summary>
		private static bool Enqueue(int npcWho, PendingRequest req) {
			if (!ChatQueues.TryGetValue(npcWho, out Queue<PendingRequest> q)) {
				q = new Queue<PendingRequest>();
				ChatQueues[npcWho] = q;
			}

			if (q.Count >= MaxQueuedPerNpc) {
				return false;
			}

			q.Enqueue(req);
			return true;
		}

		private static void TryDequeueNext(int npcWho) {
			if (BusyNpcs.Contains(npcWho)) {
				return;
			}

			if (!ChatQueues.TryGetValue(npcWho, out Queue<PendingRequest> q) || q.Count == 0) {
				return;
			}

			PendingRequest next = q.Dequeue();
			if (npcWho < 0 || npcWho >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWho];
			if (npc == null || !npc.active) {
				TryDequeueNext(npcWho);
				return;
			}

			StartRequest(npc, next);
		}

		private static bool StartRequest(NPC npc, PendingRequest req) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return false;
			}

			int npcWho = npc.whoAmI;
			int playerWho = req.PlayerWhoAmI;
			if (playerWho < 0 || playerWho >= Main.maxPlayers) {
				return false;
			}

			Player player = Main.player[playerWho];
			if (player == null || !player.active) {
				return false;
			}

			int myGen = req.Proactive ? CurrentGeneration(npcWho) : BumpGeneration(npcWho);
			BusyNpcs.Add(npcWho);
			PendingRequestOwner[npcWho] = playerWho;

			AinpcServerConfig config = AinpcServerConfig.Instance;
			string utterance = req.Utterance;
			bool proactive = req.Proactive;
			bool addressed = req.Addressed;
			bool useSuperpower = !proactive
				&& config != null
				&& config.SuperpowerEnabled
				&& AiRuntimeGate.CanUseAiFeatures;

			Task.Run(async () => {
				string reply = null;
				try {
					if (useSuperpower) {
						reply = await SuperpowerOrchestrator.HandlePlayerChatAsync(config, player, npc, utterance)
							.ConfigureAwait(false);
					}
					else {
						try {
							var messages = AiPromptBuilder.Build(npc, player, utterance, compactWorld: proactive);
							int maxTokens = Math.Max(256, config?.AiMaxTokens ?? 256);
							reply = await AiChatClient.CompleteAsync(config, messages, maxTokens)
								.ConfigureAwait(false);
						}
						finally {
							if (!proactive) {
								Superpower.PlayerSuperpowerClientSnapshot.Unpin(playerWho);
							}
						}
					}
				}
				catch {
					reply = null;
				}

				Main.QueueMainThreadAction(() => {
					try {
						if (CurrentGeneration(npcWho) != myGen) {
							return;
						}

						BusyNpcs.Remove(npcWho);
						PendingRequestOwner.Remove(npcWho);

						try {
							if (playerWho < 0 || playerWho >= Main.maxPlayers
							    || npcWho < 0 || npcWho >= Main.maxNPCs) {
								return;
							}

							Player p = Main.player[playerWho];
							NPC n = Main.npc[npcWho];
							if (p == null || !p.active || n == null || !n.active) {
								return;
							}

							if (string.IsNullOrWhiteSpace(reply)) {
								if (!proactive) {
									NpcAiSpeech.NotifyPlayer(playerWho, "Mods.AITownNPCs.Ai.Unavailable");
								}

								return;
							}

							reply = AiPromptBuilder.CleanReply(reply);
							if (addressed) {
								reply = PrefixAddress(reply, p.name);
							}

							string sk = AiConversationMemory.MakeKey(
								AiMemoryStore.PlayerKey(p),
								AiMemoryStore.NpcKey(n));

							if (!proactive) {
								AiConversationMemory.Append(sk, "user", utterance);
								AiConversationMemory.Append(sk, "assistant", reply);
							}
							else {
								AiConversationMemory.Append(sk, "assistant", reply);
							}

							NpcAiSpeech.Announce(n, reply);
						}
						finally {
							TryDequeueNext(npcWho);
						}
					}
					catch {
						if (CurrentGeneration(npcWho) == myGen) {
							BusyNpcs.Remove(npcWho);
							PendingRequestOwner.Remove(npcWho);
							TryDequeueNext(npcWho);
						}
					}
				});
			});

			return true;
		}

		private static string PrefixAddress(string reply, string playerName) {
			if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(reply)) {
				return reply;
			}

			if (reply.TrimStart().StartsWith("@")) {
				return reply;
			}

			return "@" + playerName + " " + reply;
		}

		private static int BumpGeneration(int npcWho) {
			ReplyGeneration.TryGetValue(npcWho, out int g);
			g++;
			ReplyGeneration[npcWho] = g;
			return g;
		}

		private static int CurrentGeneration(int npcWho) {
			ReplyGeneration.TryGetValue(npcWho, out int g);
			return g;
		}
	}
}
