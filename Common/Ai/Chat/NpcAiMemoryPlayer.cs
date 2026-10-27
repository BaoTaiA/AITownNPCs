using AITownNPCs.Common.Ai.Follow;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Ai.Chat
{
	public class NpcAiMemoryPlayer : ModPlayer
	{
		private string _lastBiome;
		private int _noHitTimer;
		private bool _lowLifeLatch;
		private bool _bossLatch;
		private bool _dayLatch;
		private bool _dayInited;
		private string _pendingTrigger;

		public string PeekProactiveTrigger() => _pendingTrigger;

		public void ClearProactiveTrigger() {
			_pendingTrigger = null;
		}

		public string ConsumeProactiveTrigger() {
			string t = _pendingTrigger;
			_pendingTrigger = null;
			return t;
		}

		private void QueueTrigger(string trigger) {
			if (string.IsNullOrEmpty(_pendingTrigger)) {
				_pendingTrigger = trigger;
			}
		}

		private void RememberWithFollower(string note) {
			NPC follower = NpcFollowQuery.FindFollower(Player);
			if (follower != null) {
				AiConversationMemory.AppendEvent(Player, follower, note);
			}
		}

		private void QueueFollowerEvent(string focus) {
			if (NpcFollowQuery.FindFollower(Player) == null) {
				return;
			}

			QueueTrigger(WorldContextBuilder.BuildFollowerEventTrigger(Player, focus));
		}

		public override void PostUpdate() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (!Player.active || Player.dead) {
				return;
			}

			string biome = WorldContextBuilder.DescribeBiome(Player);
			if (!string.IsNullOrEmpty(biome) && biome != _lastBiome) {
				if (!string.IsNullOrEmpty(_lastBiome)) {
					RememberWithFollower(ModText.Get("Runtime.Chat.MemEnterBiome", biome));
					QueueFollowerEvent(ModText.Get("Runtime.Chat.MemArriveBiome", biome));
				}

				_lastBiome = biome;
			}

			if (!_dayInited) {
				_dayLatch = Main.dayTime;
				_dayInited = true;
			}
			else if (_dayLatch != Main.dayTime) {
				_dayLatch = Main.dayTime;
				RememberWithFollower(Main.dayTime ? ModText.Get("Runtime.Chat.MemDay") : ModText.Get("Runtime.Chat.MemNight"));
				QueueFollowerEvent(Main.dayTime ? ModText.Get("Runtime.Chat.FocusDawn") : ModText.Get("Runtime.Chat.FocusDusk"));
			}

			bool bossNear = false;
			string bossName = null;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (!n.active || !n.boss) {
					continue;
				}

				if (Player.Distance(n.Center) < 1200f) {
					bossNear = true;
					bossName = Lang.GetNPCNameValue(n.type);
					break;
				}
			}

			if (bossNear && !_bossLatch) {
				_bossLatch = true;
				RememberWithFollower(ModText.Get("Runtime.Chat.MemBoss", bossName));
				QueueFollowerEvent(ModText.Get("Runtime.Chat.FocusBossAppear", bossName));
			}
			else if (!bossNear) {
				_bossLatch = false;
			}

			bool low = Player.statLife < Player.statLifeMax2 * 0.3f;
			if (low && !_lowLifeLatch) {
				_lowLifeLatch = true;
				RememberWithFollower(ModText.Get("Runtime.Chat.MemLowLife"));
				QueueFollowerEvent(ModText.Get("Runtime.Chat.FocusHurt"));
			}
			else if (!low) {
				_lowLifeLatch = false;
			}

			_noHitTimer++;
			// Rare idle chatter only while followed; 5 minutes quiet.
			if (_noHitTimer > 60 * 300) {
				_noHitTimer = 0;
				if (NpcFollowQuery.FindFollower(Player) != null) {
					QueueFollowerEvent(ModText.Get("Runtime.Chat.FocusQuiet"));
				}
			}
		}

		public override void OnHurt(Player.HurtInfo info) {
			_noHitTimer = 0;
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
			TryRecordBossKill(target);
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
			TryRecordBossKill(target);
		}

		private void TryRecordBossKill(NPC target) {
			if (target == null || !target.boss) {
				return;
			}

			if (target.life > 0) {
				return;
			}

			string name = Lang.GetNPCNameValue(target.type);
			RememberWithFollower(ModText.Get("Runtime.Chat.MemBossKill", name));
			QueueFollowerEvent(ModText.Get("Runtime.Chat.FocusBossWin", name));
		}

	}
}
