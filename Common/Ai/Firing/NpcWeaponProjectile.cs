using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Marks projectiles created by the NPC dummy kernel.
	/// SpectatorVisual: full AI/collision for motion, zero damage, never reports hits.
	/// </summary>
	public class NpcWeaponProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public bool FromNpcWeapon;
		public int FollowNpcWhoAmI = -1;

		/// <summary>Set once from NpcFiringState.SpectatorClient at spawn. Never re-derived from Main.myPlayer.</summary>
		public bool SpectatorVisual;

		private bool _localSwap;
		private bool[] _activeBeforeAi;

		private static readonly Stack<bool[]> SnapshotPool = new();

		public static void Tag(Projectile proj, int npcWhoAmI) {
			if (proj == null || !proj.active) {
				return;
			}

			proj.friendly = true;
			proj.hostile = false;
			proj.noDropItem = true;

			NpcWeaponProjectile global = proj.GetGlobalProjectile<NpcWeaponProjectile>();
			global.FromNpcWeapon = true;
			global.FollowNpcWhoAmI = npcWhoAmI;
		}

		public static bool IsDummyOwned(Projectile proj) {
			if (proj == null || !proj.active) {
				return false;
			}

			if (NpcDummyPool.IsDummy(proj.owner)) {
				return true;
			}

			return proj.GetGlobalProjectile<NpcWeaponProjectile>().FromNpcWeapon;
		}

		public static bool IsSpectatorVisual(Projectile proj) {
			return proj != null && proj.active && proj.GetGlobalProjectile<NpcWeaponProjectile>().SpectatorVisual;
		}

		public override bool PreAI(Projectile projectile) {
			_localSwap = false;
			_activeBeforeAi = null;
			if (!IsDummyOwned(projectile)) {
				return true;
			}

			NpcDummyLocal.PushForProjectile(projectile.owner);
			_localSwap = true;
			_activeBeforeAi = RentSnapshot();
			if (FollowNpcWhoAmI < 0) {
				NpcDummyPool.TryGetNpcWho(projectile.owner, out FollowNpcWhoAmI);
			}

			return true;
		}

		public override void PostAI(Projectile projectile) {
			try {
				if (_activeBeforeAi != null && NpcDummyPool.IsDummy(projectile.owner)) {
					int npcWho = FollowNpcWhoAmI;
					if (npcWho < 0) {
						NpcDummyPool.TryGetNpcWho(projectile.owner, out npcWho);
					}

					if (npcWho >= 0) {
						NPC npc = Main.npc[npcWho];
						if (npc.active) {
							TagNewOwned(_activeBeforeAi, projectile.owner, npc);
						}
					}
				}
			}
			finally {
				ReturnSnapshot(_activeBeforeAi);
				_activeBeforeAi = null;
				if (_localSwap) {
					NpcDummyLocal.PopForProjectile();
					_localSwap = false;
				}
			}
		}

		public override bool? CanHitNPC(Projectile projectile, NPC target) {
			if (!IsDummyOwned(projectile)) {
				return null;
			}

			if (target == null || !target.active || target.friendly || target.townNPC || target.dontTakeDamage) {
				return false;
			}

			if (!target.CanBeChasedBy()) {
				return false;
			}

			return null;
		}

		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
			if (!IsSpectatorVisual(projectile)) {
				return;
			}

			// Visual-only sim: allow AI collision, never change HP. Numbers sync via SendStrikeNPC (HitVisual is legacy).
			modifiers.FinalDamage *= 0f;
			modifiers.Knockback *= 0f;
			modifiers.DisableCrit();
		}

		internal static bool[] RentSnapshot() {
			bool[] active = SnapshotPool.Count > 0 ? SnapshotPool.Pop() : null;
			if (active == null || active.Length < Main.maxProjectiles) {
				active = new bool[Main.maxProjectiles];
			}

			for (int i = 0; i < Main.maxProjectiles; i++) {
				active[i] = Main.projectile[i].active;
			}

			return active;
		}

		internal static void ReturnSnapshot(bool[] buffer) {
			if (buffer == null) {
				return;
			}

			SnapshotPool.Push(buffer);
		}

		internal static int TagNewOwned(bool[] wasActive, int dummyWho, NPC npc) {
			int tagged = 0;
			bool spectator = NpcFiringState.IsSpectatorClient(npc);
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (!proj.active || wasActive[i] || proj.owner != dummyWho) {
					continue;
				}

				var global = proj.GetGlobalProjectile<NpcWeaponProjectile>();
				if (global.FromNpcWeapon) {
					continue;
				}

				Tag(proj, npc.whoAmI);
				// Growth-table flat only; potion damage/crit/archery come from the dummy's vanilla buffs.
				proj.damage = NpcDamageMods.ApplyOutgoing(npc, proj.damage);

				if (spectator) {
					global.SpectatorVisual = true;
				}

				tagged++;
			}

			return tagged;
		}
	}

	public class NpcWeaponProjectileDamageSystem : ModSystem
	{
		public override void Load() {
			On_Projectile.Damage += Damage_DummyAsLocalPlayer;
			On_Projectile.Kill += Kill_DummyAsLocalPlayer;
			On_NPC.StrikeNPC_HitInfo_bool_bool += RouteDummyStrike;
			On_NetMessage.SendData += BlockDummyVanillaPackets;
		}

		private static int RouteDummyStrike(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction) {
			if (Main.netMode == NetmodeID.MultiplayerClient && !fromNet
				&& TryResolveDummyHit(out int followNpcWho, out bool spectator)) {
				if (spectator) {
					// Vanilla lifts a 0 modifier to 1. Drop the local strike so spectators
					// neither chip HP nor spawn a "1" combat text. Real numbers arrive via SendStrikeNPC.
					hit.Damage = 0;
					hit.Knockback = 0f;
					hit.HideCombatText = true;
					return orig(self, hit, fromNet, noPlayerInteraction);
				}

				NpcClientUseNet.QueueHit(followNpcWho, self, hit);
			}

			return orig(self, hit, fromNet, noPlayerInteraction);
		}

		/// <summary>
		/// SpectatorVisual projectiles, or a dummy swing whose NPC is marked spectator.
		/// Authority hits return the town NPC that should report damage.
		/// </summary>
		private static bool TryResolveDummyHit(out int followNpcWho, out bool spectator) {
			followNpcWho = -1;
			spectator = false;

			Projectile source = NpcClientUseNet.RoutingProjectile;
			if (source != null && source.active && NpcWeaponProjectile.IsDummyOwned(source)) {
				if (NpcWeaponProjectile.IsSpectatorVisual(source)) {
					spectator = true;
					return true;
				}

				followNpcWho = source.GetGlobalProjectile<NpcWeaponProjectile>().FollowNpcWhoAmI;
				if (followNpcWho < 0) {
					NpcDummyPool.TryGetNpcWho(source.owner, out followNpcWho);
				}
			}
			else if (NpcDummyLocal.IsRunning && NpcDummyLocal.ActiveDummyWho >= 0) {
				// Do not read Main.myPlayer for authority — ActiveDummyWho is the Enter identity.
				NpcDummyPool.TryGetNpcWho(NpcDummyLocal.ActiveDummyWho, out followNpcWho);
			}

			if (followNpcWho < 0 || followNpcWho >= Main.maxNPCs) {
				return false;
			}

			NPC sourceNpc = Main.npc[followNpcWho];
			if (sourceNpc == null || !sourceNpc.active) {
				return false;
			}

			spectator = NpcFiringState.IsSpectatorClient(sourceNpc);
			return true;
		}

		private static void BlockDummyVanillaPackets(
			On_NetMessage.orig_SendData orig,
			int msgType,
			int remoteClient,
			int ignoreClient,
			NetworkText text,
			int number,
			float number2,
			float number3,
			float number4,
			int number5,
			int number6,
			int number7) {
			if (Main.netMode == NetmodeID.MultiplayerClient && NpcDummyLocal.SuppressVanillaSend) {
				return;
			}

			orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
		}

		private static void Damage_DummyAsLocalPlayer(On_Projectile.orig_Damage orig, Projectile self) {
			if (self == null || !NpcWeaponProjectile.IsDummyOwned(self)) {
				orig(self);
				return;
			}

			NpcDummyLocal.PushForProjectile(self.owner);
			Projectile previous = NpcClientUseNet.RoutingProjectile;
			NpcClientUseNet.RoutingProjectile = self;
			try {
				orig(self);
			}
			finally {
				NpcClientUseNet.RoutingProjectile = previous;
				NpcDummyLocal.PopForProjectile();
			}
		}

		private static void Kill_DummyAsLocalPlayer(On_Projectile.orig_Kill orig, Projectile self) {
			if (self == null || !NpcWeaponProjectile.IsDummyOwned(self)) {
				orig(self);
				return;
			}

			int owner = self.owner;
			int npcWho = self.GetGlobalProjectile<NpcWeaponProjectile>().FollowNpcWhoAmI;
			if (npcWho < 0) {
				NpcDummyPool.TryGetNpcWho(owner, out npcWho);
			}

			bool[] wasActive = NpcWeaponProjectile.RentSnapshot();
			NpcDummyLocal.PushForProjectile(owner);
			try {
				orig(self);
			}
			finally {
				try {
					if (npcWho >= 0 && npcWho < Main.maxNPCs) {
						NPC npc = Main.npc[npcWho];
						if (npc != null && npc.active) {
							NpcWeaponProjectile.TagNewOwned(wasActive, owner, npc);
						}
					}
				}
				finally {
					NpcWeaponProjectile.ReturnSnapshot(wasActive);
					NpcDummyLocal.PopForProjectile();
				}
			}
		}
	}
}
