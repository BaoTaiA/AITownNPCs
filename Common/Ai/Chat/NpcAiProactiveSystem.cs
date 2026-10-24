using System.Collections.Generic;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Host;
using AITownNPCs.Common.Ai.Follow;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Chat
{
	public class NpcAiProactiveSystem : ModSystem
	{
		/// <summary>(playerWho, npcWho) -> consecutive ticks inside walk-by range.</summary>
		private static readonly Dictionary<long, int> StayTicks = new();

		/// <summary>Last GameUpdateCount when a walk-by greeting fired for (playerWho, npcWho).</summary>
		private static readonly Dictionary<long, uint> LastWalkByTick = new();

		/// <summary>Last ambient topic fingerprint per player (avoid repeating the same remark).</summary>
		private static readonly string[] LastAmbientTopic = new string[Main.maxPlayers];

		/// <summary>Next GameUpdateCount when follower proactive may fire for this player.</summary>
		private static readonly uint[] NextFollowerProactiveTick = new uint[Main.maxPlayers];

		public override void PostUpdatePlayers() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			AinpcServerConfig config = AinpcServerConfig.Instance;
			if (config == null || !config.AiEnabled || !config.AiProactiveEnabled) {
				return;
			}

			if (!NpcAiFacade.IsReady(config)) {
				return;
			}

			uint now = Main.GameUpdateCount;

			for (int p = 0; p < Main.maxPlayers; p++) {
				Player player = Main.player[p];
				if (player == null || !player.active || player.dead) {
					ClearStayForPlayer(p);
					LastAmbientTopic[p] = null;
					continue;
				}

				NPC follower = NpcFollowQuery.FindFollower(player);
				string trigger = null;
				NPC speaker = null;
				bool followerProactive = false;
				bool walkBy = false;
				bool consumePending = false;
				var memPlayer = player.GetModPlayer<NpcAiMemoryPlayer>();

				// 1) Follower speech: every 2 min, 50% chance; event first, else ambient.
				if (follower != null && now >= NextFollowerProactiveTick[p]) {
					if (Main.rand.NextFloat() >= NpcDialoguePolicy.FollowerSpeakChance) {
						NextFollowerProactiveTick[p] =
							now + (uint)NpcDialoguePolicy.RollFollowerProactiveDelayTicks();
					}
					else {
						string pending = memPlayer.PeekProactiveTrigger();
						if (pending != null) {
							speaker = follower;
							trigger = pending;
							followerProactive = true;
							consumePending = true;
						}
						else if (WorldContextBuilder.TryBuildFollowerAmbientTrigger(
							         player, follower, LastAmbientTopic[p], out string ambient, out string topic)) {
							LastAmbientTopic[p] = topic;
							speaker = follower;
							trigger = ambient;
							followerProactive = true;
						}
						else {
							NextFollowerProactiveTick[p] =
								now + (uint)NpcDialoguePolicy.RollFollowerProactiveDelayTicks();
						}
					}
				}

				// 2) Walk-by: stay 10s within 50px; 10min CD; any follower never greets.
				if (trigger == null) {
					TryUpdateWalkBy(player, p, now, out speaker, out trigger);
					walkBy = speaker != null && trigger != null;
				}

				if (speaker == null || trigger == null) {
					continue;
				}

				if (!NpcAiFacade.RequestReply(player, speaker, trigger, proactive: true)) {
					continue;
				}

				if (followerProactive) {
					if (consumePending) {
						memPlayer.ClearProactiveTrigger();
					}

					NextFollowerProactiveTick[p] = now + (uint)NpcDialoguePolicy.RollFollowerProactiveDelayTicks();
				}
				else if (walkBy) {
					MarkWalkByCooldown(p, speaker.whoAmI);
					AiConversationMemory.AppendEvent(player, speaker, "向玩家打过招呼");
				}
			}
		}

		/// <summary>Start shared 10min walk-by CD (typed chat or successful greet).</summary>
		public static void MarkWalkByCooldown(int playerWhoAmI, int npcWhoAmI) {
			if (playerWhoAmI < 0 || npcWhoAmI < 0) {
				return;
			}

			long key = WalkByKey(playerWhoAmI, npcWhoAmI);
			LastWalkByTick[key] = Main.GameUpdateCount;
			StayTicks.Remove(key);
		}

		private static void TryUpdateWalkBy(
			Player player,
			int playerWhoAmI,
			uint now,
			out NPC speaker,
			out string trigger) {
			speaker = null;
			trigger = null;

			NPC bestReady = null;
			int bestStay = -1;

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!NpcHostRules.IsManagedTownNpc(npc)) {
					continue;
				}

				long key = WalkByKey(playerWhoAmI, npc.whoAmI);
				float dist = Vector2.Distance(player.Center, npc.Center);
				if (dist >= NpcDialoguePolicy.WalkByRange) {
					StayTicks.Remove(key);
					continue;
				}

				if (NpcDialoguePolicy.IsFollowingAnyone(npc)) {
					StayTicks.Remove(key);
					continue;
				}

				// Still chatting: do not accumulate walk-by stay.
				if (NpcAiFacade.IsEngagedWithPlayer(npc.whoAmI, playerWhoAmI)) {
					StayTicks.Remove(key);
					continue;
				}

				StayTicks.TryGetValue(key, out int stay);
				stay++;
				StayTicks[key] = stay;

				if (stay < NpcDialoguePolicy.WalkByStayTicks) {
					continue;
				}

				if (LastWalkByTick.TryGetValue(key, out uint last)
				    && now - last < NpcDialoguePolicy.WalkByCooldownTicks) {
					continue;
				}

				if (stay > bestStay) {
					bestStay = stay;
					bestReady = npc;
				}
			}

			if (bestReady == null) {
				return;
			}

			speaker = bestReady;
			trigger =
				$"（路过停留）玩家{player.name}在你旁边连续停留了一会儿。"
				+ $"请结合你对该玩家的短期记忆，用约15字打招呼，必须称呼其名字「{player.name}」";
		}

		private static void ClearStayForPlayer(int playerWhoAmI) {
			List<long> remove = null;
			foreach (long key in StayTicks.Keys) {
				if ((int)(key >> 32) == playerWhoAmI) {
					remove ??= new List<long>();
					remove.Add(key);
				}
			}

			if (remove == null) {
				return;
			}

			foreach (long key in remove) {
				StayTicks.Remove(key);
			}
		}

		private static long WalkByKey(int playerWhoAmI, int npcWhoAmI) {
			return ((long)playerWhoAmI << 32) | (uint)npcWhoAmI;
		}

		public override void OnWorldUnload() {
			StayTicks.Clear();
			LastWalkByTick.Clear();
			for (int i = 0; i < NextFollowerProactiveTick.Length; i++) {
				NextFollowerProactiveTick[i] = 0;
				LastAmbientTopic[i] = null;
			}
		}
	}
}
