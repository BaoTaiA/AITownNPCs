using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Soft SOTS bridge: dummy-owned CursedBlade spawns CursedStab with the dummy owner
	/// (not Main.myPlayer), dedupes per target, times out stuck stabs, and ticks foam on
	/// dummies (SOTS FoamStuff only runs for LocalPlayer in singleplayer).
	/// </summary>
	public sealed class NpcSotsCursedBladeBridge : ModSystem
	{
		private const int StabTimeoutFrames = 90;

		private static bool _ready;
		private static int _cursedStabType = -1;
		private static Type _sotsPlayerType;
		private static FieldInfo _canAttack;
		private static MethodInfo _foamStuff;
		private static Hook _onHitHook;

		private delegate void OnHitNpcOrig(object self, NPC target, NPC.HitInfo hit, int damageDone);

		internal static int StabTimeout => StabTimeoutFrames;

		public override void PostSetupContent() {
			UnloadHooks();
			_ready = false;
			_cursedStabType = -1;
			_sotsPlayerType = null;
			_canAttack = null;
			_foamStuff = null;

			if (!ModLoader.TryGetMod("SOTS", out Mod sots) || sots.Code == null) {
				return;
			}

			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

			Type bladeType = sots.Code.GetType("SOTS.Projectiles.Minions.CursedBlade");
			Type stabType = sots.Code.GetType("SOTS.Projectiles.Pyramid.CursedStab");
			_sotsPlayerType = sots.Code.GetType("SOTS.SOTSPlayer");
			if (bladeType == null || stabType == null) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("SOTS CursedBlade/CursedStab missing; bridge disabled.");
				return;
			}

			_canAttack = bladeType.GetField("canAttack", flags);
			_foamStuff = _sotsPlayerType?.GetMethod(
				"FoamStuff",
				flags,
				binder: null,
				types: Type.EmptyTypes,
				modifiers: null);

			if (!ModContent.TryFind<ModProjectile>("SOTS/CursedStab", out ModProjectile stabMod)) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("SOTS CursedStab type id missing; bridge disabled.");
				return;
			}

			_cursedStabType = stabMod.Type;

			MethodInfo onHit = bladeType.GetMethod(
				"OnHitNPC",
				flags,
				binder: null,
				types: new[] { typeof(NPC), typeof(NPC.HitInfo), typeof(int) },
				modifiers: null);
			if (onHit == null) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("SOTS CursedBlade.OnHitNPC missing; bridge disabled.");
				return;
			}

			_onHitHook = new Hook(onHit, OnCursedBladeHit);
			_ready = true;
			ModContent.GetInstance<AITownNPCs>()?.Logger.Info("SOTS CursedBlade bridge ready (soft).");
		}

		public override void Unload() {
			UnloadHooks();
		}

		public override void PostUpdateEverything() {
			if (!_ready || _foamStuff == null || _sotsPlayerType == null) {
				return;
			}

			for (int i = 0; i < Main.maxPlayers; i++) {
				if (!NpcDummyPool.IsDummy(i)) {
					continue;
				}

				TickFoam(Main.player[i]);
			}
		}

		private static void UnloadHooks() {
			_onHitHook?.Dispose();
			_onHitHook = null;
			_ready = false;
			_cursedStabType = -1;
			_sotsPlayerType = null;
			_canAttack = null;
			_foamStuff = null;
		}

		private static void TickFoam(Player dummy) {
			if (dummy == null || !dummy.active) {
				return;
			}

			try {
				ModPlayer mp = GetSotsPlayer(dummy);
				if (mp != null) {
					_foamStuff.Invoke(mp, null);
				}
			}
			catch {
				// Soft: SOTSPlayer shape drift must not break combat.
			}
		}

		private static ModPlayer GetSotsPlayer(Player player) {
			if (player == null || _sotsPlayerType == null) {
				return null;
			}

			try {
				foreach (ModPlayer mp in player.ModPlayers) {
					if (mp != null && _sotsPlayerType.IsInstanceOfType(mp)) {
						return mp;
					}
				}
			}
			catch {
				// ignore
			}

			return null;
		}

		private static void OnCursedBladeHit(OnHitNpcOrig orig, object self, NPC target, NPC.HitInfo hit, int damageDone) {
			if (self is not ModProjectile modProj || target == null || !_ready || _cursedStabType < 0) {
				orig(self, target, hit, damageDone);
				return;
			}

			Projectile blade = modProj.Projectile;
			if (blade == null || !NpcDummyPool.IsDummy(blade.owner)) {
				orig(self, target, hit, damageDone);
				return;
			}

			try {
				_canAttack?.SetValue(self, false);
			}
			catch {
				// ignore
			}

			blade.netUpdate = true;

			if (HasActiveStab(blade.owner, target.whoAmI)) {
				return;
			}

			int idx = Projectile.NewProjectile(
				blade.GetSource_OnHit(target),
				target.Center,
				Vector2.Zero,
				_cursedStabType,
				blade.damage,
				0f,
				blade.owner,
				0f,
				target.whoAmI);

			if (idx >= 0
				&& idx < Main.maxProjectiles
				&& NpcDummyPool.TryGetNpcWho(blade.owner, out int npcWho)
				&& npcWho >= 0) {
				NpcWeaponProjectile.Tag(Main.projectile[idx], npcWho);
			}
		}

		internal static bool IsCursedStab(Projectile proj) {
			return _ready && proj != null && _cursedStabType >= 0 && proj.type == _cursedStabType;
		}

		internal static bool HasActiveStab(int owner, int targetWho) {
			if (_cursedStabType < 0 || owner < 0 || targetWho < 0) {
				return false;
			}

			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p == null || !p.active || p.type != _cursedStabType || p.owner != owner) {
					continue;
				}

				if ((int)p.ai[1] == targetWho) {
					return true;
				}
			}

			return false;
		}
	}

	/// <summary>Force-kill dummy-owned CursedStab that outlive their ~75f lifetime.</summary>
	public sealed class NpcSotsCursedStabGuard : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private int _aliveFrames;
		private bool _track;

		public override void OnSpawn(Projectile projectile, IEntitySource source) {
			_aliveFrames = 0;
			_track = NpcSotsCursedBladeBridge.IsCursedStab(projectile)
				&& NpcDummyPool.IsDummy(projectile.owner);
		}

		public override void PostAI(Projectile projectile) {
			if (!_track || projectile == null || !projectile.active) {
				return;
			}

			if (!NpcSotsCursedBladeBridge.IsCursedStab(projectile)
				|| !NpcDummyPool.IsDummy(projectile.owner)) {
				_track = false;
				return;
			}

			_aliveFrames++;
			if (_aliveFrames > NpcSotsCursedBladeBridge.StabTimeout) {
				projectile.Kill();
			}
		}
	}
}
