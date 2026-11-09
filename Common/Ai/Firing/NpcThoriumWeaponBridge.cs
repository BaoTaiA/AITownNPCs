using System;
using System.Reflection;
using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Ai.Follow;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Soft-reflection port of Palico ThoriumCompanionBridge for combat dummies.
	/// No thrower-exhaustion Ready (plan carve-out).
	/// </summary>
	public sealed class NpcThoriumWeaponBridge : ModSystem
	{
		private static bool _ready;
		private static Type _thoriumPlayerType;
		private static Type _bardItemType;
		private static Type _thoriumItemType;
		private static Type _mousePlayerType;
		private static FieldInfo _isHealer;
		private static FieldInfo _isThrower;
		private static FieldInfo _empowerOnUse;
		private static FieldInfo _playOnUse;
		private static FieldInfo _netInfo;
		private static FieldInfo _playedSound;
		private static MethodInfo _applyEmpowerments;
		private static MethodInfo _playSound;
		private static MethodInfo _onPlayInstrument;
		private static MethodInfo _getInspirationCost;
		private static MethodInfo _findAlly;
		private static Hook _getThoriumPlayer;
		private static Hook _tryGetMouse;
		private static Hook _useItemInner;
		private static Hook _playInstrument;
		private static Hook _netEmpower;

		private delegate object GetThoriumPlayerOrig(Player player);
		private delegate bool TryGetMouseOrig(object mousePlayer, out Vector2 position);
		private delegate void UseItemInnerOrig(object bardItem, Player player);
		private delegate void PlayInstrumentOrig(object bardItem, Player player, float pitch);
		private delegate void NetEmpowerOrig(object bardItem, Player player, BitsByte info);

		public static Func<Item, float?> ReachOverride { get; private set; }

		public override void PostSetupContent() {
			UnloadHooks();
			_ready = false;
			ReachOverride = null;
			if (!ModLoader.TryGetMod("ThoriumMod", out Mod thorium) || thorium.Code == null) {
				return;
			}

			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

			_thoriumPlayerType = thorium.Code.GetType("ThoriumMod.ThoriumPlayer");
			_bardItemType = thorium.Code.GetType("ThoriumMod.Items.BardItems.BardItem");
			_thoriumItemType = thorium.Code.GetType("ThoriumMod.Items.ThoriumItem");
			_mousePlayerType = thorium.Code.GetType("ThoriumMod.MousePlayer")
				?? thorium.Code.GetType("ThoriumMod.Utilities.MousePlayer");

			if (_thoriumItemType != null) {
				_isHealer = _thoriumItemType.GetField("isHealer", flags);
				_isThrower = _thoriumItemType.GetField("isThrower", flags);
			}

			if (_bardItemType != null) {
				_empowerOnUse = _bardItemType.GetField("EmpowerOnUse", flags)
					?? BoundFieldFromProperty(_bardItemType, "EmpowerOnUse", flags);
				_playOnUse = _bardItemType.GetField("PlayOnUse", flags)
					?? BoundFieldFromProperty(_bardItemType, "PlayOnUse", flags);
				_netInfo = _bardItemType.GetField("NetInfo", flags)
					?? BoundFieldFromProperty(_bardItemType, "NetInfo", flags);
				_playedSound = _bardItemType.GetField("PlayedSound", flags)
					?? BoundFieldFromProperty(_bardItemType, "PlayedSound", flags);
				_applyEmpowerments = _bardItemType.GetMethod(
					"ApplyEmpowerments",
					flags,
					binder: null,
					types: new[] { typeof(Player), typeof(Player) },
					modifiers: null);
				_onPlayInstrument = _bardItemType.GetMethod(
					"OnPlayInstrument",
					flags,
					binder: null,
					types: new[] { typeof(Player) },
					modifiers: null);
				_getInspirationCost = _bardItemType.GetMethod(
					"GetInspirationCost",
					flags,
					binder: null,
					types: new[] { typeof(Player) },
					modifiers: null);
				_playSound = _bardItemType.GetMethod(
					"PlaySound",
					staticFlags,
					binder: null,
					types: new[] { typeof(Player), typeof(SoundStyle), typeof(float) },
					modifiers: null);

				MethodInfo useInner = _bardItemType.GetMethod("UseItem_Inner", flags);
				MethodInfo playInst = _bardItemType.GetMethod("PlayInstrument", flags);
				MethodInfo netEmp = _bardItemType.GetMethod("NetApplyEmpowerments", flags);
				if (useInner != null) {
					_useItemInner = new Hook(useInner, OnUseItemInner);
				}

				if (playInst != null) {
					_playInstrument = new Hook(playInst, OnPlayInstrumentHook);
				}

				if (netEmp != null) {
					_netEmpower = new Hook(netEmp, OnNetEmpower);
				}
			}

			if (_thoriumPlayerType != null) {
				_findAlly = _thoriumPlayerType.GetMethod("HandleLowestLifeAlly", flags);
			}

			Type helper = thorium.Code.GetType("ThoriumMod.Utilities.PlayerHelper");
			MethodInfo getTp = helper?.GetMethod(
				"GetThoriumPlayer",
				staticFlags,
				binder: null,
				types: new[] { typeof(Player) },
				modifiers: null);
			if (getTp != null) {
				_getThoriumPlayer = new Hook(getTp, OnGetThoriumPlayer);
			}

			MethodInfo tryMouse = _mousePlayerType?.GetMethod(
				"TryGetMousePosition",
				flags,
				binder: null,
				types: new[] { typeof(Vector2).MakeByRefType() },
				modifiers: null);
			if (tryMouse != null) {
				_tryGetMouse = new Hook(tryMouse, OnTryGetMouse);
			}

			ReachOverride = ResolveReach;
			_ready = true;
			ModContent.GetInstance<AITownNPCs>()?.Logger.Info("Thorium weapon bridge ready (soft).");
		}

		public override void Unload() {
			UnloadHooks();
			_ready = false;
			ReachOverride = null;
			_thoriumPlayerType = _bardItemType = _thoriumItemType = _mousePlayerType = null;
			_isHealer = _isThrower = _empowerOnUse = _playOnUse = _netInfo = _playedSound = null;
			_applyEmpowerments = _playSound = _onPlayInstrument = _getInspirationCost = _findAlly = null;
		}

		private static void UnloadHooks() {
			_getThoriumPlayer?.Dispose();
			_tryGetMouse?.Dispose();
			_useItemInner?.Dispose();
			_playInstrument?.Dispose();
			_netEmpower?.Dispose();
			_getThoriumPlayer = _tryGetMouse = _useItemInner = _playInstrument = _netEmpower = null;
		}

		/// <summary>Thorium thrower consumables allowed as main-hand weapons (Palico SafeThrow).</summary>
		public static bool IsSafeThrow(Item item) {
			if (!_ready || item == null || item.IsAir || item.ModItem == null || item.damage <= 0 || item.shoot <= ProjectileID.None) {
				return false;
			}

			if (item.createTile > -1 || item.createWall > -1 || item.ammo != AmmoID.None) {
				return false;
			}

			if (item.ModItem.Mod?.Name != "ThoriumMod" || _isThrower == null) {
				return false;
			}

			try {
				return _isThrower.GetValue(item.ModItem) is bool thrower && thrower;
			}
			catch {
				return false;
			}
		}

		/// <summary>
		/// Drum PlanUse + healer findAlly. Inspiration tops off in Fill (no wait-for-full gate).
		/// </summary>
		public static bool BeforeUse(Player dummy, NPC npc, bool wantUse) {
			if (!_ready || dummy == null || !wantUse) {
				return wantUse;
			}

			// Always clear/plan drum alt; findAlly only for Thorium healers (see TickFindAlly).
			PlanDrum(dummy);
			TickFindAlly(dummy);
			return true;
		}

		private static void TickFindAlly(Player dummy) {
			if (_findAlly == null || _isHealer == null || _thoriumItemType == null) {
				return;
			}

			ModItem modItem = dummy.HeldItem?.ModItem;
			if (modItem == null || !_thoriumItemType.IsInstanceOfType(modItem)) {
				return;
			}

			try {
				if (_isHealer.GetValue(modItem) is not bool healer || !healer) {
					return;
				}

				ModPlayer tp = GetModPlayer(dummy, _thoriumPlayerType);
				if (tp != null) {
					_findAlly.Invoke(tp, null);
				}
			}
			catch {
				// ignore
			}
		}

		private static void PlanDrum(Player dummy) {
			Item held = dummy.HeldItem;
			if (held?.ModItem == null || !IsDrumMallet(held)) {
				dummy.altFunctionUse = 0;
				return;
			}

			if (dummy.itemAnimation > 1) {
				return;
			}

			dummy.altFunctionUse = 0;
			if (!NpcDummyLocal.TryGetAim(dummy.whoAmI, out Vector2 aim)) {
				aim = dummy.Center + new Vector2(dummy.direction * 80f, 0f);
			}

			dummy.direction = aim.X < dummy.Center.X ? -1 : 1;
			Vector2 strike = dummy.Center + new Vector2(dummy.direction * 34f, 0f);
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile shot = Main.projectile[i];
				if (shot == null
					|| !shot.active
					|| shot.owner != dummy.whoAmI
					|| shot.type != held.shoot) {
					continue;
				}

				if (Vector2.DistanceSquared(shot.Center, strike) < 28f * 28f
					|| shot.Hitbox.Intersects(dummy.Hitbox)) {
					return;
				}
			}

			// Pending secondary → ItemCheck promotes to altFunctionUse == 2.
			dummy.altFunctionUse = 1;
			NpcDummyLocal.RememberAim(dummy.whoAmI, dummy.Center + new Vector2(dummy.direction * 60f, -12f));
		}

		private static bool IsDrumMallet(Item item) {
			if (item?.ModItem == null) {
				return false;
			}

			string name = item.ModItem.Name;
			string full = item.ModItem.GetType().FullName ?? string.Empty;
			return name.Equals("DrumMallet", StringComparison.OrdinalIgnoreCase)
				|| full.EndsWith(".DrumMallet", StringComparison.Ordinal);
		}

		private static bool IsScythePro(Projectile shot) {
			if (shot?.ModProjectile == null) {
				return false;
			}

			string full = shot.ModProjectile.GetType().FullName ?? string.Empty;
			return full.IndexOf("ScythePro", StringComparison.Ordinal) >= 0
				|| shot.ModProjectile.GetType().Name.Equals("ScythePro", StringComparison.Ordinal);
		}

		private static float? ResolveReach(Item item) {
			if (item == null || item.IsAir) {
				return null;
			}

			if (IsDrumMallet(item)) {
				return 120f;
			}

			Projectile sample = NpcWeaponReach.Sample(item);
			if (sample != null && IsScythePro(sample)) {
				return sample.width * sample.scale * 0.5f + 8f;
			}

			return null;
		}

		private static object OnGetThoriumPlayer(GetThoriumPlayerOrig orig, Player player) {
			if (player != null && NpcDummyPool.IsDummy(player.whoAmI) && _thoriumPlayerType != null) {
				ModPlayer mp = GetModPlayer(player, _thoriumPlayerType);
				if (mp != null) {
					return mp;
				}
			}

			return orig(player);
		}

		private static bool OnTryGetMouse(TryGetMouseOrig orig, object mousePlayer, out Vector2 position) {
			Player player = null;
			try {
				if (mousePlayer != null) {
					PropertyInfo prop = mousePlayer.GetType().GetProperty("Player");
					player = prop?.GetValue(mousePlayer) as Player;
					if (player == null) {
						FieldInfo field = mousePlayer.GetType().GetField("Player")
							?? mousePlayer.GetType().GetField("player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						player = field?.GetValue(mousePlayer) as Player;
					}
				}
			}
			catch {
				player = null;
			}

			if (player != null
				&& NpcDummyPool.IsDummy(player.whoAmI)
				&& NpcDummyLocal.TryGetAim(player.whoAmI, out position)) {
				return true;
			}

			return orig(mousePlayer, out position);
		}

		private static void OnUseItemInner(UseItemInnerOrig orig, object bardItem, Player player) {
			if (player == null || !NpcDummyPool.IsDummy(player.whoAmI)) {
				orig(bardItem, player);
				return;
			}

			if (!IsInstrument(bardItem)) {
				return;
			}

			ApplyBardEffects(bardItem, player, empower: ReadBoolNamed(bardItem, "EmpowerOnUse"), play: true);
			if (ReadBoolNamed(bardItem, "PlayOnUse")
				&& bardItem is ModItem modItem
				&& ItemID.Sets.SkipsInitialUseSound[modItem.Type]) {
				OnPlayInstrumentHook(null, bardItem, player, 0f);
			}
		}

		private static void OnPlayInstrumentHook(PlayInstrumentOrig orig, object bardItem, Player player, float pitch) {
			if (player == null || !NpcDummyPool.IsDummy(player.whoAmI)) {
				orig?.Invoke(bardItem, player, pitch);
				return;
			}

			PlayBardSound(bardItem, player, pitch);
		}

		private static void OnNetEmpower(NetEmpowerOrig orig, object bardItem, Player player, BitsByte info) {
			if (player == null || !NpcDummyPool.IsDummy(player.whoAmI)) {
				orig(bardItem, player, info);
				return;
			}

			object previous = _netInfo?.GetValue(bardItem);
			try {
				_netInfo?.SetValue(bardItem, info);
				ApplyBardEffects(bardItem, player, empower: true, play: false);
			}
			finally {
				if (previous != null) {
					_netInfo?.SetValue(bardItem, previous);
				}
			}
		}

		private static void ApplyBardEffects(object bardItem, Player dummy, bool empower, bool play) {
			if (!NpcDummyPool.TryGetNpcWho(dummy.whoAmI, out int npcWho)
				|| npcWho < 0
				|| npcWho >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWho];
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			Player target = null;
			if (follow.IsFollowing
				&& follow.FollowPlayerWhoAmI >= 0
				&& follow.FollowPlayerWhoAmI < Main.maxPlayers) {
				target = Main.player[follow.FollowPlayerWhoAmI];
			}

			if (target == null || !target.active) {
				target = Main.LocalPlayer;
			}

			NpcDummyPool.SyncTeamFromFollowOwner(dummy);

			try {
				if (empower && _applyEmpowerments != null && target != null) {
					_applyEmpowerments.Invoke(bardItem, new object[] { dummy, target });
				}

				if (play && _onPlayInstrument != null) {
					_onPlayInstrument.Invoke(bardItem, new object[] { dummy });
				}
			}
			catch (Exception ex) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Debug($"Thorium bard effect failed: {ex.Message}");
			}
		}

		private static void PlayBardSound(object bardItem, Player dummy, float pitch) {
			try {
				SoundStyle? style = null;
				if (_playedSound?.GetValue(bardItem) is SoundStyle played) {
					style = played;
				}
				else if (bardItem is ModItem modItem) {
					style = modItem.Item.UseSound ?? ContentSamples.ItemsByType[modItem.Type].UseSound;
				}

				if (style is SoundStyle sound && _playSound != null) {
					_playSound.Invoke(null, new object[] { dummy, sound, pitch });
				}
				else if (style is SoundStyle fallback) {
					SoundEngine.PlaySound(fallback, dummy.Center);
				}
			}
			catch {
				// ignore
			}
		}

		private static bool IsInstrument(object bardItem) {
			if (bardItem == null) {
				return false;
			}

			try {
				// BardItemType.Instrument — typically enum value 0 or named Instrument.
				object typeValue = null;
				PropertyInfo prop = bardItem.GetType().GetProperty(
					"ItemType",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (prop != null) {
					typeValue = prop.GetValue(bardItem);
				}
				else {
					FieldInfo field = bardItem.GetType().GetField(
						"ItemType",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					typeValue = field?.GetValue(bardItem);
				}

				if (typeValue == null) {
					return true;
				}

				string name = typeValue.ToString() ?? string.Empty;
				return name.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) >= 0
					|| (typeValue is int i && i == 0)
					|| (typeValue is byte b && b == 0)
					|| (typeValue is Enum e && Convert.ToInt32(e) == 0);
			}
			catch {
				return true;
			}
		}

		private static FieldInfo BoundFieldFromProperty(Type type, string name, BindingFlags flags) {
			// Auto-props may only expose a property; skip if backing field unavailable.
			PropertyInfo prop = type.GetProperty(name, flags);
			return prop == null ? null : type.GetField($"<{name}>k__BackingField", flags);
		}

		private static bool ReadBool(FieldInfo field, object target) {
			if (target == null) {
				return false;
			}

			try {
				if (field != null && field.GetValue(target) is bool fromField) {
					return fromField;
				}

				PropertyInfo prop = target.GetType().GetProperty(
					field?.Name ?? string.Empty,
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field == null) {
					prop = target.GetType().GetProperty("EmpowerOnUse", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						?? target.GetType().GetProperty("PlayOnUse", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}

				return prop?.GetValue(target) is bool fromProp && fromProp;
			}
			catch {
				return false;
			}
		}

		private static bool ReadBoolNamed(object target, string name) {
			if (target == null || string.IsNullOrEmpty(name)) {
				return false;
			}

			try {
				FieldInfo field = target.GetType().GetField(
					name,
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field?.GetValue(target) is bool fb) {
					return fb;
				}

				PropertyInfo prop = target.GetType().GetProperty(
					name,
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				return prop?.GetValue(target) is bool pb && pb;
			}
			catch {
				return false;
			}
		}

		private static ModPlayer GetModPlayer(Player player, Type type) {
			if (player == null || type == null) {
				return null;
			}

			try {
				foreach (ModPlayer mp in player.ModPlayers) {
					if (mp != null && type.IsInstanceOfType(mp)) {
						return mp;
					}
				}
			}
			catch {
				// ignore
			}

			return null;
		}
	}
}
