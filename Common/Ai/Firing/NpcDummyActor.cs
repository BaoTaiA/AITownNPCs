using System;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Full in-memory player kernel; NPC is only the body and locomotion shell.
	/// Lease ends via <see cref="NpcDummyPool.ReleaseWhenIdle"/> or <see cref="NpcDummyPool.ForceRelease"/>.
	/// </summary>
	public static class NpcDummyActor
	{
		private const int TempAmmoSlot = 54;

		public static void Tick(NPC npc, EquippedTownNPC equip, NpcCombatState combat, NPC target, bool wantUse) {
			if (npc == null || !npc.active || combat == null || equip == null) {
				return;
			}

			if (Main.netMode == NetmodeID.Server) {
				NpcClientUseNet.SendIntent(npc, equip, combat, target, wantUse);
				return;
			}

			RunLocal(npc, equip, combat, target, wantUse, aimOverride: null);
		}

		/// <summary>Follower client: run the player weapon pipeline from a server intent.</summary>
		public static void RunFromIntent(NPC npc, EquippedTownNPC equip, Vector2 aimWorld, bool wantUse) {
			if (npc == null || !npc.active || equip == null) {
				return;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			RunLocal(npc, equip, combat, target: null, wantUse, aimWorld);
		}

		private static void RunLocal(NPC npc, EquippedTownNPC equip, NpcCombatState combat, NPC target, bool wantUse, Vector2? aimOverride) {
			bool hasDummy = NpcDummyPool.TryGetDummy(npc.whoAmI, out Player live) && live != null;
			bool shouldLease = wantUse
				|| (hasDummy && (live.itemAnimation > 0 || live.channel || live.heldProj >= 0))
				|| (NpcDummyPool.TryGetDummyWho(npc.whoAmI, out int dummyWho) && NpcDummyPool.OwnsProjectiles(dummyWho));

			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
				if (!shouldLease) {
				FlushPendingEquipSync(npc, equip, firing);
				NpcDummyPool.ReleaseWhenIdle(npc.whoAmI);
				combat.ItemAnimation = 0;
				combat.ItemAnimationMax = 0;
				combat.IsChanneling = false;
				firing.ResetUseCycle();
				if (Main.netMode == NetmodeID.MultiplayerClient) {
					firing.ClientHoldingUse = false;
					if (NpcFiringState.IsAuthorityClient(npc)) {
						firing.NeedsReport = true;
					}
				}

				return;
			}

			Player dummy = NpcDummyPool.Acquire(npc);
			if (dummy == null) {
				return;
			}

			ApplyStealth(dummy);
			Vector2 aimWorld = aimOverride ?? (target != null && target.active
				? target.Center
				: npc.Center + new Vector2(npc.direction * 320f, 0f));
			ApplyPose(dummy, npc, aimWorld);
			InstallLoadout(dummy, equip);
			dummy.noItems = false;
			dummy.silence = false;
			dummy.frozen = false;
			dummy.webbed = false;
			dummy.stoned = false;

			int ammoTypeBefore = equip.Ammo.type;
			int ammoStackBefore = equip.Ammo.IsAir ? 0 : equip.Ammo.stack;
			int weaponTypeBefore = equip.Weapon.type;
			int weaponPrefixBefore = equip.Weapon.prefix;
			int weaponStackBefore = equip.Weapon.IsAir ? 0 : equip.Weapon.stack;
			bool[] wasActive = NpcWeaponProjectile.RentSnapshot();

			try {
				NpcDummyLocal.RememberAim(dummy.whoAmI, aimWorld);
				RunPlayerUpdate(dummy, aimWorld, wantUse);
				LogFireProbe(npc, dummy, wantUse);
				ApplyAimFacing(npc, dummy, aimWorld);
				NpcDummyPool.LockPose(dummy, npc);
				ApplyStealth(dummy);

				SyncCombatFromDummy(combat, dummy);
				NpcWeaponProjectile.TagNewOwned(wasActive, dummy.whoAmI, npc);
				ReadbackLoadout(dummy, equip);
				if (Main.netMode == NetmodeID.MultiplayerClient && NpcFiringState.IsAuthorityClient(npc)) {
					firing.NeedsReport = true;
				}
			}
			finally {
				NpcWeaponProjectile.ReturnSnapshot(wasActive);
			}

			bool ammoChanged = equip.Ammo.type != ammoTypeBefore
				|| (equip.Ammo.IsAir ? 0 : equip.Ammo.stack) != ammoStackBefore;
			if (ammoChanged) {
				SyncAmmo(npc, equip, firing, force: equip.Ammo.IsAir || equip.Ammo.type != ammoTypeBefore);
			}

			bool weaponChanged = equip.Weapon.type != weaponTypeBefore
				|| equip.Weapon.prefix != weaponPrefixBefore
				|| (equip.Weapon.IsAir ? 0 : equip.Weapon.stack) != weaponStackBefore;
			if (weaponChanged) {
				SyncWeapon(npc, equip, firing,
					force: equip.Weapon.IsAir
						|| equip.Weapon.type != weaponTypeBefore
						|| equip.Weapon.prefix != weaponPrefixBefore);
			}

			if (equip.Weapon.IsAir) {
				dummy.controlUseItem = false;
				FlushPendingEquipSync(npc, equip, firing);
				NpcDummyPool.ReleaseWhenIdle(npc.whoAmI);
				if (!NpcDummyPool.TryGetDummyWho(npc.whoAmI, out _)) {
					combat.ResetUse();
					combat.IsChanneling = false;
					firing.ResetUseCycle();
				}

				return;
			}

			FlushPendingEquipSync(npc, equip, firing, onlyIfIntervalElapsed: true);
		}

		public static void StopAndRelease(int npcWhoAmI) {
			if (npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs) {
				NPC npc = Main.npc[npcWhoAmI];
				if (npc.active) {
					NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
					NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
					EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
					if (Main.netMode == NetmodeID.Server) {
						NpcClientUseNet.SendIntent(npc, equip, combat, target: null, allowFire: false);
					}

					FlushPendingEquipSync(npc, equip, firing);
					combat.ResetUse();
					combat.IsChanneling = false;
					firing.ResetUseCycle();
					firing.RemoteBusy = false;
					firing.IntentOpen = false;
					firing.HasPendingIntent = false;
					firing.ClientHoldingUse = false;
					firing.SpectatorClient = false;
					firing.NeedsReport = false;
					firing.ResetIntentThrottle();
				}
			}

			NpcClientUseNet.UntrackClientFire(npcWhoAmI);

			if (NpcDummyPool.TryGetDummy(npcWhoAmI, out Player dummy) && dummy != null) {
				dummy.controlUseItem = false;
				dummy.channel = false;
			}

			NpcDummyPool.ForceRelease(npcWhoAmI);
		}

		private static void ApplyStealth(Player dummy) {
			NpcDummyPool.KeepAlive(dummy);
			dummy.aggro = -1000;
			dummy.hostile = false;
			dummy.controlUseTile = false;
			dummy.releaseUseTile = false;
		}

		private static void ApplyPose(Player dummy, NPC npc, Vector2 aimWorld) {
			dummy.gravDir = 1f;
			dummy.gravity = 0f;
			dummy.maxFallSpeed = 0f;
			dummy.velocity = Vector2.Zero;

			ApplyAimFacing(npc, dummy, aimWorld);
			NpcDummyPool.LockPose(dummy, npc);
		}

		private static void ApplyAimFacing(NPC npc, Player dummy, Vector2 aimWorld) {
			float dx = aimWorld.X - npc.Center.X;
			int dir = dummy != null ? dummy.direction : npc.direction;
			if (dx > NpcEquipHelper.FacingDeadzone) {
				dir = 1;
			}
			else if (dx < -NpcEquipHelper.FacingDeadzone) {
				dir = -1;
			}

			if (dummy != null) {
				dummy.direction = dir;
			}

			npc.direction = dir;
			npc.spriteDirection = dir;
		}

		private static void InstallLoadout(Player dummy, EquippedTownNPC equip) {
			Item current = dummy.inventory[dummy.selectedItem];
			bool weaponChanged = current == null || current.IsAir
				|| equip.Weapon.IsAir
				|| current.type != equip.Weapon.type
				|| current.prefix != equip.Weapon.prefix;
			CopySlot(ref dummy.inventory[dummy.selectedItem], equip.Weapon);
			if (weaponChanged) {
				dummy.channel = false;
				dummy.heldProj = -1;
				dummy.itemTime = 0;
				dummy.itemAnimation = 0;
				NpcDummyLocal.Forget(dummy.whoAmI);
			}
			for (int s = 0; s < 4; s++) {
				int slot = TempAmmoSlot + s;
				if (s == 0 && !equip.Ammo.IsAir) {
					CopySlot(ref dummy.inventory[slot], equip.Ammo);
				}
				else {
					ClearSlot(ref dummy.inventory[slot]);
				}
			}
		}

		private static void ClearSlot(ref Item dest) {
			if (dest == null) {
				dest = new Item();
				return;
			}

			dest.TurnToAir();
		}

		private static void CopySlot(ref Item dest, Item source) {
			if (source == null || source.IsAir) {
				ClearSlot(ref dest);
				return;
			}

			if (dest == null || dest.type != source.type || dest.prefix != source.prefix) {
				dest = source.Clone();
				return;
			}

			dest.stack = source.stack;
		}

		private static void ReadbackLoadout(Player dummy, EquippedTownNPC equip) {
			Item held = dummy.inventory[dummy.selectedItem];
			if (held == null || held.IsAir) {
				if (equip.Weapon.consumable) {
					equip.Weapon.TurnToAir();
				}
			}
			else if (equip.Weapon.type == held.type && equip.Weapon.prefix == held.prefix) {
				equip.Weapon.stack = held.stack;
			}
			else {
				equip.Weapon = held.Clone();
			}

			if (equip.Weapon.useAmmo == AmmoID.None) {
				return;
			}

			Item ammo = dummy.inventory[TempAmmoSlot];
			if (ammo == null || ammo.IsAir) {
				if (!IsEndlessAmmo(equip.Ammo)) {
					equip.Ammo.TurnToAir();
				}

				return;
			}

			if (equip.Ammo.type == ammo.type && equip.Ammo.prefix == ammo.prefix) {
				equip.Ammo.stack = ammo.stack;
			}
			else {
				equip.Ammo = ammo.Clone();
			}
		}

		private static bool IsEndlessAmmo(Item ammo) {
			return ammo != null && !ammo.IsAir
				&& (ammo.type == ItemID.EndlessQuiver || ammo.type == ItemID.EndlessMusketPouch);
		}

		private static uint _lastUpdateFailLogTick;
		private const uint UpdateFailLogInterval = 300;
		private static uint _lastFireProbeTick;
		private const uint FireProbeInterval = 180;

		private static void LogFireProbe(NPC npc, Player dummy, bool wantUse) {
			if (Main.netMode != NetmodeID.Server || !wantUse || dummy == null) {
				return;
			}

			uint now = Main.GameUpdateCount;
			if (now - _lastFireProbeTick < FireProbeInterval) {
				return;
			}

			_lastFireProbeTick = now;
			int weapon = dummy.inventory[dummy.selectedItem]?.type ?? 0;
			ModContent.GetInstance<AITownNPCs>()?.Logger.Info(
				$"Dummy fire npc={npc.whoAmI} anim={dummy.itemAnimation} channel={dummy.channel} held={dummy.heldProj} weapon={weapon}");
		}

		private static void RunPlayerUpdate(Player dummy, Vector2 aimWorld, bool wantUse) {
			try {
				NpcDummyPool.EnsureKernelReady(dummy);
				NpcDummyLocal.Enter(dummy, aimWorld, wantUse);
				dummy.Update(dummy.whoAmI);
				dummy.controlUseItem = wantUse;
			}
			catch (Exception ex) {
				LogUpdateFailed(dummy, wantUse, ex);
			}
			finally {
				NpcDummyLocal.Leave(dummy, wantUse);
			}
		}

		private static void LogUpdateFailed(Player dummy, bool wantUse, Exception ex) {
			uint now = Main.GameUpdateCount;
			if (now - _lastUpdateFailLogTick < UpdateFailLogInterval) {
				return;
			}

			_lastUpdateFailLogTick = now;
			int who = dummy?.whoAmI ?? -1;
			ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
				$"Dummy Update failed (player {who}, wantUse={wantUse}): {ex}");
		}

		private static void SyncCombatFromDummy(NpcCombatState combat, Player dummy) {
			combat.ItemAnimation = dummy.itemAnimation;
			combat.ItemAnimationMax = dummy.itemAnimationMax;
			combat.IsChanneling = dummy.channel;
			combat.ItemRotation = dummy.itemRotation;
		}

		private const uint EquipSyncMinInterval = 10;

		private static void SyncAmmo(NPC npc, EquippedTownNPC equip, NpcFiringState firing, bool force) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			uint now = Main.GameUpdateCount;
			if (!force && now - firing.LastAmmoSyncTick < EquipSyncMinInterval) {
				firing.PendingAmmoSync = true;
				return;
			}

			firing.PendingAmmoSync = false;
			firing.LastAmmoSyncTick = now;
			NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, NpcEquipSlotId.Ammo, equip.Ammo);
		}

		private static void SyncWeapon(NPC npc, EquippedTownNPC equip, NpcFiringState firing, bool force) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			uint now = Main.GameUpdateCount;
			if (!force && now - firing.LastWeaponSyncTick < EquipSyncMinInterval) {
				firing.PendingWeaponSync = true;
				return;
			}

			firing.PendingWeaponSync = false;
			firing.LastWeaponSyncTick = now;
			NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, NpcEquipSlotId.Weapon, equip.Weapon);
		}

		/// <summary>
		/// Flush throttled stack syncs. Lease end always flushes; mid-fire only when interval elapsed.
		/// </summary>
		private static void FlushPendingEquipSync(
			NPC npc,
			EquippedTownNPC equip,
			NpcFiringState firing,
			bool onlyIfIntervalElapsed = false) {
			if (Main.netMode != NetmodeID.Server || firing == null || equip == null) {
				return;
			}

			uint now = Main.GameUpdateCount;
			if (firing.PendingAmmoSync) {
				if (!onlyIfIntervalElapsed || now - firing.LastAmmoSyncTick >= EquipSyncMinInterval) {
					firing.PendingAmmoSync = false;
					firing.LastAmmoSyncTick = now;
					NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, NpcEquipSlotId.Ammo, equip.Ammo);
				}
			}

			if (firing.PendingWeaponSync) {
				if (!onlyIfIntervalElapsed || now - firing.LastWeaponSyncTick >= EquipSyncMinInterval) {
					firing.PendingWeaponSync = false;
					firing.LastWeaponSyncTick = now;
					NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, NpcEquipSlotId.Weapon, equip.Weapon);
				}
			}
		}
	}
}


