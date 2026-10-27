using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Rents unused <see cref="Main.player"/> slots as weapon actors for town NPCs.
	/// </summary>
	public static partial class NpcDummyPool
	{
		internal const int DummyManaCap = 400;
		internal const int DummyLifeCap = 100;
		internal const int DummyImmuneTime = 60;
		internal const int DummyMaxMinions = 8;
		internal const int DummyMaxTurrets = 4;

		private static readonly HashSet<int> DummyWho = new();
		private static readonly HashSet<int> InitializedSlots = new();
		private static readonly Dictionary<int, int> NpcToDummy = new();
		private static readonly Dictionary<int, int> DummyToNpc = new();

		public static bool IsDummy(int whoAmI) {
			return whoAmI >= 0 && DummyWho.Contains(whoAmI);
		}

		/// <summary>
		/// SetupPlayer resets a player to 20 mana. Fill after init and each ResetEffects.
		/// </summary>
		public static void FillMana(Player p) {
			if (p == null) {
				return;
			}

			p.statManaMax = DummyManaCap;
			p.statManaMax2 = DummyManaCap;
			p.statMana = DummyManaCap;
		}

		public static void KeepAlive(Player p) {
			if (p == null) {
				return;
			}

			p.immune = true;
			p.immuneTime = DummyImmuneTime;
			p.immuneNoBlink = true;
			p.dead = false;
			p.ghost = false;
			p.statLifeMax = DummyLifeCap;
			p.statLifeMax2 = DummyLifeCap;
			p.statLife = DummyLifeCap;
			p.maxMinions = DummyMaxMinions;
			p.maxTurrets = DummyMaxTurrets;
		}

		public static bool TryGetDummyWho(int npcWhoAmI, out int dummyWho) {
			return NpcToDummy.TryGetValue(npcWhoAmI, out dummyWho);
		}

		public static bool TryGetDummy(int npcWhoAmI, out Player dummy) {
			dummy = null;
			if (!NpcToDummy.TryGetValue(npcWhoAmI, out int who) || who < 0 || who >= Main.maxPlayers) {
				return false;
			}

			dummy = Main.player[who];
			return dummy != null;
		}

		public static Player Acquire(NPC npc) {
			if (npc == null || !npc.active) {
				return null;
			}

			if (NpcToDummy.TryGetValue(npc.whoAmI, out int existing)) {
				return Prepare(existing);
			}

			int slot = FindFreeSlot();
			if (slot < 0) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn($"No free player slot for NPC weapon dummy (npc {npc.whoAmI}).");
				NpcDummySlotTips.NotifyExhausted();
				return null;
			}

			DummyWho.Add(slot);
			NpcToDummy[npc.whoAmI] = slot;
			DummyToNpc[slot] = npc.whoAmI;
			return Prepare(slot, firstBind: true);
		}

		public static void BindClient(int npcWhoAmI, int dummyWho) {
			if (dummyWho < 0 || dummyWho >= Main.maxPlayers || npcWhoAmI < 0) {
				return;
			}

			if (NpcToDummy.TryGetValue(npcWhoAmI, out int old) && old != dummyWho) {
				UnbindSlot(old);
			}

			DummyWho.Add(dummyWho);
			NpcToDummy[npcWhoAmI] = dummyWho;
			DummyToNpc[dummyWho] = npcWhoAmI;
			Prepare(dummyWho);
		}

		/// <summary>Unbind only when the dummy has no live projectiles (natural end of fire).</summary>
		public static void ReleaseWhenIdle(int npcWhoAmI) {
			Release(npcWhoAmI, force: false);
		}

		/// <summary>Unbind immediately (equip change, world unload, net sync, stop combat).</summary>
		public static void ForceRelease(int npcWhoAmI) {
			Release(npcWhoAmI, force: true);
		}

		private static void Release(int npcWhoAmI, bool force) {
			if (!NpcToDummy.TryGetValue(npcWhoAmI, out int slot)) {
				return;
			}

			if (!force && OwnsProjectiles(slot)) {
				return;
			}

			UnbindSlot(slot);
			NpcToDummy.Remove(npcWhoAmI);
		}

		public static void ReleaseAll() {
			foreach (int slot in new List<int>(DummyWho)) {
				UnbindSlot(slot);
			}

			NpcToDummy.Clear();
			DummyToNpc.Clear();
			InitializedSlots.Clear();
			_searchSuspendDepth = 0;
			_ownsCacheTick = -1;
			_ownsCache.Clear();
		}

		private static int _ownsCacheTick = -1;
		private static readonly HashSet<int> _ownsCache = new();

		/// <summary>True if any active projectile is owned by this dummy. Cached once per game tick.</summary>
		public static bool OwnsProjectiles(int dummyWho) {
			if (dummyWho < 0) {
				return false;
			}

			int tick = (int)Main.GameUpdateCount;
			if (_ownsCacheTick != tick) {
				_ownsCacheTick = tick;
				_ownsCache.Clear();
				if (DummyWho.Count > 0) {
					for (int i = 0; i < Main.maxProjectiles; i++) {
						Projectile proj = Main.projectile[i];
						if (proj.active && DummyWho.Contains(proj.owner)) {
							_ownsCache.Add(proj.owner);
						}
					}
				}
			}

			return _ownsCache.Contains(dummyWho);
		}

		public static bool TryGetNpcWho(int dummyWho, out int npcWhoAmI) {
			return DummyToNpc.TryGetValue(dummyWho, out npcWhoAmI);
		}

		public static void LockPose(Player dummy, NPC npc) {
			if (dummy == null || npc == null) {
				return;
			}

			dummy.position.X = npc.Center.X - dummy.width * 0.5f + dummy.direction * 6f;
			dummy.position.Y = npc.position.Y + npc.height - dummy.height;
			dummy.velocity = Vector2.Zero;
			dummy.oldPosition = dummy.position;
			dummy.oldVelocity = Vector2.Zero;
			dummy.gfxOffY = npc.gfxOffY;
		}

		public static void LockAllToNpcs() {
			List<int> stale = null;
			foreach (KeyValuePair<int, int> pair in NpcToDummy) {
				int npcWho = pair.Key;
				int dummyWho = pair.Value;
				if (npcWho < 0 || npcWho >= Main.maxNPCs || dummyWho < 0 || dummyWho >= Main.maxPlayers) {
					stale ??= new List<int>();
					stale.Add(npcWho);
					continue;
				}

				NPC npc = Main.npc[npcWho];
				Player dummy = Main.player[dummyWho];
				if (npc == null || !npc.active) {
					stale ??= new List<int>();
					stale.Add(npcWho);
					continue;
				}

				if (dummy == null) {
					continue;
				}

				LockPose(dummy, npc);
			}

			if (stale == null) {
				return;
			}

			for (int i = 0; i < stale.Count; i++) {
				NpcDummyActor.StopAndRelease(stale[i]);
			}
		}

		private static int _searchSuspendDepth;

		public static void SuspendForPlayerSearch() {
			_searchSuspendDepth++;
			if (_searchSuspendDepth != 1) {
				return;
			}

			foreach (int who in DummyWho) {
				if (who >= 0 && who < Main.maxPlayers && Main.player[who] != null) {
					Main.player[who].active = false;
				}
			}
		}

		public static void ResumeAfterPlayerSearch() {
			if (_searchSuspendDepth <= 0) {
				return;
			}

			_searchSuspendDepth--;
			if (_searchSuspendDepth != 0) {
				return;
			}

			foreach (int who in DummyWho) {
				if (who >= 0 && who < Main.maxPlayers && Main.player[who] != null) {
					Main.player[who].active = true;
				}
			}
		}

		private static void UnbindSlot(int slot) {
			if (slot >= 0 && slot < Main.maxPlayers) {
				Player p = Main.player[slot];
				if (p != null) {
					int held = p.heldProj;
					p.heldProj = -1;
					if (held >= 0 && held < Main.maxProjectiles) {
						Projectile proj = Main.projectile[held];
						if (proj != null && proj.active && proj.owner == slot) {
							proj.Kill();
						}
					}

					p.channel = false;
					p.itemAnimation = 0;
					p.itemTime = 0;
					p.controlUseItem = false;
					p.active = false;
					NpcDummyLocal.Forget(slot);
				}
			}

			DummyWho.Remove(slot);
			DummyToNpc.Remove(slot);
		}

		private static Player Prepare(int slot, bool firstBind = false) {
			Player p = Main.player[slot];
			if (p == null) {
				p = new Player();
				p.whoAmI = slot;
				Main.player[slot] = p;
			}

			if (firstBind || !InitializedSlots.Contains(slot) || !PlayerKernelSized(p)) {
				p = EnsureInitialized(slot);
				InitializedSlots.Add(slot);
			}
			else {
				EnsureKernelReady(p);
			}

			p.whoAmI = slot;
			p.active = true;
			p.name = string.Empty;
			p.hostile = false;
			p.team = 0;
			p.aggro = -1000;
			p.gravDir = 1f;
			p.selectedItem = 0;
			p.statLifeMax = DummyLifeCap;
			p.statLifeMax2 = DummyLifeCap;
			p.statLife = DummyLifeCap;

			KeepAlive(p);
			FillMana(p);

			if (firstBind) {
				p.itemAnimation = 0;
				p.itemTime = 0;
				p.channel = false;
				p.heldProj = -1;
				if (p.ownedProjectileCounts != null) {
					Array.Clear(p.ownedProjectileCounts, 0, p.ownedProjectileCounts.Length);
				}
			}

			return p;
		}

		private static int FindFreeSlot() {
			for (int i = Main.maxPlayers - 1; i >= 0; i--) {
				if (i == Main.myPlayer) {
					continue;
				}

				if (DummyWho.Contains(i)) {
					continue;
				}

				if (IsReservedByRealPlayer(i)) {
					continue;
				}

				Player existing = Main.player[i];
				if (existing != null && existing.active) {
					continue;
				}

				return i;
			}

			return -1;
		}

		private static bool IsReservedByRealPlayer(int i) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				return i == Main.myPlayer;
			}

			if (Main.netMode == NetmodeID.Server) {
				return i < 255 && Netplay.Clients[i].IsActive;
			}

			Player p = Main.player[i];
			return p != null && p.active && !DummyWho.Contains(i);
		}
	}

	public class NpcDummyWorldCleanup : ModSystem
	{
		public override void OnWorldUnload() {
			NpcDummyPool.ReleaseAll();
			NpcClientUseNet.ClearClientFireTracking();
		}
	}
}
