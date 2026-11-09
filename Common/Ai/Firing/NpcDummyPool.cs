using System;
using System.Collections.Generic;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Roles.Dryad;
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
		/// <summary>KeepAlive fallback only when dummy has no bound NPC (summon checks refuse instead).</summary>
		internal const int DummyMaxMinions = NpcLifeStatScale.MinMinions;
		internal const int DummyMaxTurrets = NpcLifeStatScale.MinTurrets;

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

		/// <summary>Top off soft-mod resources (Thorium inspiration / SOTS void) like mana.</summary>
		public static void FillModCombatResources(Player p) {
			NpcDummyModResources.Fill(p);
		}

		/// <summary>
		/// Mirror follow-owner team/hostile onto the dummy (Thorium ally / PvP gates).
		/// Unbound dummies stay peaceful on team 0.
		/// </summary>
		public static void SyncTeamFromFollowOwner(Player dummy) {
			if (dummy == null) {
				return;
			}

			if (TryGetNpcWho(dummy.whoAmI, out int npcWho)
				&& npcWho >= 0
				&& npcWho < Main.maxNPCs) {
				NPC npc = Main.npc[npcWho];
				if (npc != null && npc.active) {
					FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
					if (follow.IsFollowing
						&& follow.FollowPlayerWhoAmI >= 0
						&& follow.FollowPlayerWhoAmI < Main.maxPlayers) {
						Player owner = Main.player[follow.FollowPlayerWhoAmI];
						if (owner != null && owner.active) {
							dummy.team = owner.team;
							dummy.hostile = owner.hostile;
							return;
						}
					}
				}
			}

			dummy.team = 0;
			dummy.hostile = false;
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

			int maxMinions = DummyMaxMinions;
			int maxTurrets = DummyMaxTurrets;
			if (TryGetNpcWho(p.whoAmI, out int npcWho)
				&& npcWho >= 0
				&& npcWho < Main.maxNPCs) {
				NPC npc = Main.npc[npcWho];
				if (npc != null && npc.active) {
					maxMinions = NpcLifeStatScale.GetMaxMinions(npc) + TownNpcAuraBuffs.GetMinionSlotBonus(npc);
					maxTurrets = NpcLifeStatScale.GetMaxTurrets(npc);
					SyncAuraBuffsToDummy(p, npc);
				}
			}

			p.maxMinions = System.Math.Max(NpcLifeStatScale.MinMinions, maxMinions);
			p.maxTurrets = maxTurrets;
		}

		/// <summary>
		/// Mirror Dryad-aura buffs onto the dummy so vanilla Player.UpdateBuffs applies them.
		/// Always clears the synced set first so switching NPCs cannot leave sticky effects.
		/// </summary>
		public static void SyncAuraBuffsToDummy(Player dummy, NPC npc) {
			if (dummy == null) {
				return;
			}

			foreach (int type in PotionBuffNpcEffectTable.DummySyncedBuffTypes) {
				dummy.ClearBuff(type);
			}

			if (npc == null || !npc.active || !npc.townNPC) {
				return;
			}

			npc.GetGlobalNPC<TownNpcAuraBuffs>().SyncBuffsToDummy(dummy);
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
			OwnerFramePos.Clear();
			OwnerFrameVel.Clear();
			OwnerFrameTick.Clear();
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

		/// <summary>
		/// True if this NPC still has a leased dummy or tagged weapon projectiles to clear.
		/// </summary>
		public static bool HasLiveCombatResidue(int npcWhoAmI) {
			if (npcWhoAmI < 0) {
				return false;
			}

			if (TryGetDummyWho(npcWhoAmI, out int dummyWho) && IsDummy(dummyWho)) {
				return true;
			}

			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj == null || !proj.active) {
					continue;
				}

				NpcWeaponProjectile tag = proj.GetGlobalProjectile<NpcWeaponProjectile>();
				if (tag.FromNpcWeapon && tag.FollowNpcWhoAmI == npcWhoAmI) {
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Kill every projectile tied to this town NPC's dummy (owner or FromNpcWeapon tag).
		/// Used on combat stop so the dummy can unbind immediately.
		/// Never kills by owner unless the slot is a confirmed dummy (avoids player 0).
		/// </summary>
		public static void KillNpcOwnedProjectiles(int npcWhoAmI) {
			KillNpcOwnedProjectiles(npcWhoAmI, preserveSummons: false);
		}

		/// <param name="preserveSummons">When true, leave minion and sentry bodies. Their shots are removed.</param>
		public static void KillNpcOwnedProjectiles(int npcWhoAmI, bool preserveSummons) {
			KillOwned(npcWhoAmI, preserveSummons, summonsOnly: false);
		}

		/// <summary>Kill this NPC's minions and sentries only. Other weapon projectiles stay.</summary>
		public static void KillSummonProjectiles(int npcWhoAmI) {
			KillOwned(npcWhoAmI, preserveSummons: false, summonsOnly: true);
		}

		public static bool OwnsSummonProjectiles(int npcWhoAmI) {
			return AnyOwned(npcWhoAmI, summonsOnly: true);
		}

		private static void KillOwned(int npcWhoAmI, bool preserveSummons, bool summonsOnly) {
			if (npcWhoAmI < 0) {
				return;
			}

			bool hasDummy = TryGetDummyWho(npcWhoAmI, out int dummyWho) && IsDummy(dummyWho);
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (!OwnedBy(proj, npcWhoAmI, hasDummy, dummyWho)) {
					continue;
				}

				if (preserveSummons && IsSummonBody(proj)) {
					continue;
				}

				bool summon = IsSummonProjectile(proj);

				if (summonsOnly && !summon) {
					continue;
				}

				proj.Kill();
			}

			_ownsCacheTick = -1;
			_ownsCache.Clear();
		}

		private static bool AnyOwned(int npcWhoAmI, bool summonsOnly) {
			if (npcWhoAmI < 0) {
				return false;
			}

			bool hasDummy = TryGetDummyWho(npcWhoAmI, out int dummyWho) && IsDummy(dummyWho);
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (!OwnedBy(proj, npcWhoAmI, hasDummy, dummyWho)) {
					continue;
				}

				if (!summonsOnly || IsSummonProjectile(proj)) {
					return true;
				}
			}

			return false;
		}

		private static bool IsSummonBody(Projectile proj) =>
			proj.minion || proj.sentry || NpcDummyModResources.IsVoidCapacityMinion(proj);

		private static bool IsSummonProjectile(Projectile proj) {
			if (IsSummonBody(proj)) {
				return true;
			}

			int type = proj.type;
			if (type > ProjectileID.None && type < ProjectileID.Sets.MinionShot.Length && ProjectileID.Sets.MinionShot[type]) {
				return true;
			}

			return type > ProjectileID.None && type < ProjectileID.Sets.SentryShot.Length && ProjectileID.Sets.SentryShot[type];
		}

		private static bool OwnedBy(Projectile proj, int npcWhoAmI, bool hasDummy, int dummyWho) {
			if (proj == null || !proj.active) {
				return false;
			}

			if (hasDummy && proj.owner == dummyWho) {
				return true;
			}

			NpcWeaponProjectile tag = proj.GetGlobalProjectile<NpcWeaponProjectile>();
			return tag.FromNpcWeapon && tag.FollowNpcWhoAmI == npcWhoAmI;
		}

		public static bool TryGetNpcWho(int dummyWho, out int npcWhoAmI) {
			return DummyToNpc.TryGetValue(dummyWho, out npcWhoAmI);
		}

		private static readonly Dictionary<int, Vector2> OwnerFramePos = new();
		private static readonly Dictionary<int, Vector2> OwnerFrameVel = new();
		private static readonly Dictionary<int, uint> OwnerFrameTick = new();

		/// <summary>
		/// Puts the owner on the NPC. Minion and sentry placement stays vanilla projectile AI.
		/// </summary>
		public static void LockPose(Player dummy, NPC npc) {
			if (dummy == null || npc == null) {
				return;
			}

			int dir = npc.direction != 0 ? npc.direction : (npc.spriteDirection < 0 ? -1 : 1);
			dummy.direction = dir;
			uint tick = Main.GameUpdateCount;
			if (!OwnerFrameTick.TryGetValue(dummy.whoAmI, out uint seen) || seen != tick) {
				OwnerFramePos[dummy.whoAmI] = dummy.position;
				OwnerFrameVel[dummy.whoAmI] = dummy.velocity;
				OwnerFrameTick[dummy.whoAmI] = tick;
			}

			if (OwnerFramePos.TryGetValue(dummy.whoAmI, out Vector2 frameStart)) {
				dummy.oldPosition = frameStart;
				dummy.oldVelocity = OwnerFrameVel.TryGetValue(dummy.whoAmI, out Vector2 frameVel)
					? frameVel
					: dummy.velocity;
			}

			dummy.position.X = npc.Center.X - dummy.width * 0.5f;
			dummy.position.Y = npc.Center.Y - dummy.height * 0.5f;
			dummy.velocity = npc.velocity;
			dummy.gfxOffY = npc.gfxOffY;
		}

		private static void ForgetPose(int slot) {
			OwnerFramePos.Remove(slot);
			OwnerFrameVel.Remove(slot);
			OwnerFrameTick.Remove(slot);
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
				KeepAlive(dummy);
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
					ForgetPose(slot);
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
			p.aggro = -1000;
			p.gravDir = 1f;
			p.selectedItem = 0;
			p.statLifeMax = DummyLifeCap;
			p.statLifeMax2 = DummyLifeCap;
			p.statLife = DummyLifeCap;

			KeepAlive(p);
			SyncTeamFromFollowOwner(p);
			FillMana(p);
			FillModCombatResources(p);

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
