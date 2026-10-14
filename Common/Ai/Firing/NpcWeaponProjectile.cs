using System;
using System.Collections.Generic;
using AITownNPCs.Common.Net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Marks projectiles created by the NPC dummy kernel. Dummy-owned shots act as local-player shots during AI/damage.
	/// </summary>
	public class NpcWeaponProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public bool FromNpcWeapon;
		public int FollowNpcWhoAmI = -1;

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

		/// <summary>Tag and sync newly spawned dummy-owned projectiles. Returns how many were tagged.</summary>
		internal static int TagNewOwned(bool[] wasActive, int dummyWho, NPC npc) {
			int tagged = 0;
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
				proj.damage = NpcDamageMods.ApplyOutgoing(npc, proj.damage);
				NpcProjectileSpawn.SyncExisting(proj.whoAmI);
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
		}

		private static void Damage_DummyAsLocalPlayer(On_Projectile.orig_Damage orig, Projectile self) {
			if (self == null || !NpcWeaponProjectile.IsDummyOwned(self)) {
				orig(self);
				return;
			}

			NpcDummyLocal.PushForProjectile(self.owner);
			try {
				orig(self);
			}
			finally {
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
