using System;
using System.Reflection;
using AITownNPCs.Common.Ai.Follow;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Soft-manage Thorium inspiration / SOTS void on combat dummies (Palico-style).
	/// Caps follow the follow-owner; both drain natively then top off when empty / below cast cost.
	/// </summary>
	public sealed class NpcDummyModResources : ModSystem
	{
		/// <summary>Thorium default when no follow owner is available.</summary>
		internal const int InspirationFallback = 10;
		/// <summary>Fixed dummy healBonus (Additional Healing); not copied from follow owner.</summary>
		internal const int HealBonusFixed = 10;
		/// <summary>SOTS default void max when no follow owner / VoidPlayer is available.</summary>
		internal const int VoidFallback = 100;

		private static bool _thoriumReady;
		private static bool _sotsReady;
		private static Type _thoriumPlayerType;
		private static Type _voidPlayerType;
		private static Type _bardItemType;
		private static Type _voidItemType;
		private static FieldInfo _bardResource;
		private static FieldInfo _bardResourceMax;
		private static FieldInfo _bardResourceMax2;
		private static FieldInfo _consumedInspirationGem;
		private static FieldInfo _healBonus;
		private static FieldInfo _healingSpeed;
		private static FieldInfo _voidMeter;
		private static FieldInfo _voidMeterMax;
		private static FieldInfo _voidMeterMax2;
		private static FieldInfo _voidMinionConsumption;
		private static FieldInfo _safetySwitch;
		private static FieldInfo _voidAnkh;
		private static FieldInfo _voidStar;
		private static FieldInfo _voidSoul;
		private static MethodInfo _getInspirationCost;
		private static MethodInfo _voidCost;
		private static MethodInfo _registerVoidMinions;

		public override void PostSetupContent() {
			ResolveThorium();
			ResolveSots();
		}

		public override void Unload() {
			_thoriumReady = _sotsReady = false;
			_thoriumPlayerType = _voidPlayerType = _bardItemType = _voidItemType = null;
			_bardResource = _bardResourceMax = _bardResourceMax2 = null;
			_consumedInspirationGem = _healBonus = _healingSpeed = null;
			_voidMeter = _voidMeterMax = _voidMeterMax2 = null;
			_voidMinionConsumption = _safetySwitch = null;
			_voidAnkh = _voidStar = _voidSoul = null;
			_getInspirationCost = _voidCost = _registerVoidMinions = null;
		}

		public static void Fill(Player dummy) {
			if (dummy == null) {
				return;
			}

			if (_thoriumReady) {
				FillThorium(dummy);
			}

			if (_sotsReady) {
				FillSots(dummy);
			}
		}

		/// <summary>True when the held mod item is SOTS VoidItem (soft type check).</summary>
		public static bool IsVoidItem(Item item) {
			if (item?.ModItem == null || item.ModItem.Mod?.Name != "SOTS") {
				return false;
			}

			if (_voidItemType != null) {
				try {
					return _voidItemType.IsInstanceOfType(item.ModItem);
				}
				catch {
					// fall through to name heuristic
				}
			}

			for (Type t = item.ModItem.GetType(); t != null; t = t.BaseType) {
				if (t.Name.IndexOf("VoidItem", StringComparison.OrdinalIgnoreCase) >= 0) {
					return true;
				}
			}

			return false;
		}

		/// <summary>SOTS void summon staffs (Nature Spirit etc.): capacity, not meter drain.</summary>
		public static bool IsVoidSummon(Item item) {
			return IsVoidItem(item) && item.CountsAsClass(DamageClass.Summon);
		}

		/// <summary>
		/// Palico ResourceReady: void summons always ready; other void weapons need meter.
		/// </summary>
		public static bool IsVoidReady(Player dummy) {
			if (!_sotsReady || dummy == null) {
				return true;
			}

			Item held = dummy.HeldItem;
			if (!IsVoidItem(held)) {
				return true;
			}

			if (IsVoidSummon(held)) {
				return true;
			}

			ModPlayer mp = GetModPlayer(dummy, _voidPlayerType);
			if (mp == null) {
				return true;
			}

			float meter = GetNumericFloat(_voidMeter, mp, 0f);
			if (meter < 0f) {
				return false;
			}

			if (dummy.itemAnimation > 0) {
				return true;
			}

			float need = GetHeldVoidCost(dummy);
			return need <= 0f || meter >= need;
		}

		private static void ResolveThorium() {
			_thoriumReady = false;
			_getInspirationCost = null;
			_bardItemType = null;
			if (!ModLoader.TryGetMod("ThoriumMod", out Mod thorium) || thorium.Code == null) {
				return;
			}

			_thoriumPlayerType = thorium.Code.GetType("ThoriumMod.ThoriumPlayer");
			if (_thoriumPlayerType == null) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("ThoriumPlayer type missing; inspiration fill disabled.");
				return;
			}

			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			_bardResource = _thoriumPlayerType.GetField("bardResource", flags);
			_bardResourceMax = _thoriumPlayerType.GetField("bardResourceMax", flags);
			_bardResourceMax2 = _thoriumPlayerType.GetField("bardResourceMax2", flags);
			_consumedInspirationGem = _thoriumPlayerType.GetField("consumedInspirationGem", flags);
			_healBonus = _thoriumPlayerType.GetField("healBonus", flags);
			_healingSpeed = _thoriumPlayerType.GetField("healingSpeed", flags);
			if (_bardResource == null || _bardResourceMax == null || _bardResourceMax2 == null) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("Thorium inspiration fields missing; fill disabled.");
				return;
			}

			_bardItemType = thorium.Code.GetType("ThoriumMod.Items.BardItems.BardItem");
			if (_bardItemType != null) {
				_getInspirationCost = _bardItemType.GetMethod(
					"GetInspirationCost",
					flags,
					binder: null,
					types: new[] { typeof(Player) },
					modifiers: null);
			}

			_thoriumReady = true;
		}

		private static void ResolveSots() {
			_sotsReady = false;
			_voidCost = null;
			_voidItemType = null;
			_registerVoidMinions = null;
			_voidAnkh = _voidStar = _voidSoul = null;
			_voidMeter = _voidMeterMax = _voidMeterMax2 = null;
			_voidMinionConsumption = _safetySwitch = null;
			_voidPlayerType = null;
			if (!ModLoader.TryGetMod("SOTS", out Mod sots) || sots.Code == null) {
				return;
			}

			_voidPlayerType = sots.Code.GetType("SOTS.Void.VoidPlayer");
			if (_voidPlayerType == null) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("VoidPlayer type missing; void fill disabled.");
				return;
			}

			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			_voidMeter = _voidPlayerType.GetField("voidMeter", flags);
			_voidMeterMax = _voidPlayerType.GetField("voidMeterMax", flags);
			_voidMeterMax2 = _voidPlayerType.GetField("voidMeterMax2", flags);
			_voidMinionConsumption = _voidPlayerType.GetField("VoidMinionConsumption", flags);
			_safetySwitch = _voidPlayerType.GetField("safetySwitch", flags);
			// Permanent void progression (Palico PermanentResources); optional if renamed.
			_voidAnkh = _voidPlayerType.GetField("voidAnkh", flags);
			_voidStar = _voidPlayerType.GetField("voidStar", flags);
			_voidSoul = _voidPlayerType.GetField("voidSoul", flags);
			if (_voidMeter == null || _voidMeterMax == null || _voidMeterMax2 == null) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("SOTS void fields missing; fill disabled.");
				return;
			}

			_registerVoidMinions = _voidPlayerType.GetMethod(
				"RegisterVoidMinions",
				flags,
				binder: null,
				types: Type.EmptyTypes,
				modifiers: null);

			_voidItemType = sots.Code.GetType("SOTS.Void.VoidItem");
			if (_voidItemType != null) {
				_voidCost = _voidItemType.GetMethod(
					"VoidCost",
					flags,
					binder: null,
					types: new[] { typeof(Player) },
					modifiers: null);
			}

			_sotsReady = true;
		}

		/// <summary>True when held item is a Thorium BardItem (soft).</summary>
		public static bool IsBardItem(Item item) {
			if (item?.ModItem == null || _bardItemType == null) {
				return false;
			}

			try {
				return _bardItemType.IsInstanceOfType(item.ModItem);
			}
			catch {
				return false;
			}
		}

		public static int GetInspiration(Player dummy) {
			ModPlayer mp = GetModPlayer(dummy, _thoriumPlayerType);
			return GetNumeric(_bardResource, mp, 0);
		}

		public static int GetInspirationMax2(Player dummy) {
			ModPlayer mp = GetModPlayer(dummy, _thoriumPlayerType);
			return Math.Max(1, GetNumeric(_bardResourceMax2, mp, InspirationFallback));
		}

		public static int GetHeldBardInspirationCost(Player dummy) => GetHeldInspirationCost(dummy);

		private static void FillThorium(Player dummy) {
			ModPlayer mp = GetModPlayer(dummy, _thoriumPlayerType);
			if (mp == null) {
				return;
			}

			int max = InspirationFallback;
			int max2 = InspirationFallback;
			if (TryGetFollowOwner(dummy, out Player owner)) {
				ModPlayer ownerMp = GetModPlayer(owner, _thoriumPlayerType);
				if (ownerMp != null) {
					max = Math.Clamp(GetNumeric(_bardResourceMax, ownerMp, InspirationFallback), InspirationFallback, 40);
					max2 = Math.Max(max, GetNumeric(_bardResourceMax2, ownerMp, max));
					if (_consumedInspirationGem != null
						&& _consumedInspirationGem.FieldType == typeof(bool)
						&& _consumedInspirationGem.GetValue(ownerMp) is bool gem) {
						_consumedInspirationGem.SetValue(mp, gem);
					}

					if (_healingSpeed != null) {
						try {
							object src = _healingSpeed.GetValue(ownerMp);
							if (src is float f) {
								_healingSpeed.SetValue(mp, f);
							}
							else if (src is double d) {
								_healingSpeed.SetValue(mp, (float)d);
							}
						}
						catch {
							// ignore
						}
					}
				}
			}

			// Fixed Additional Healing for NPC dummies (Dream Weaver Hood-tier baseline).
			if (_healBonus != null) {
				SetNumeric(_healBonus, mp, HealBonusFixed);
			}

			SetNumeric(_bardResourceMax, mp, max);
			SetNumeric(_bardResourceMax2, mp, max2);

			// Drain normally; top off when empty or below the next cast cost.
			int current = GetNumeric(_bardResource, mp, 0);
			if (current > max2) {
				current = max2;
				SetNumeric(_bardResource, mp, current);
			}

			int need = GetHeldInspirationCost(dummy);
			if (current <= 0 || (need > 0 && current < need)) {
				SetNumeric(_bardResource, mp, max2);
			}
		}

		private static int GetHeldInspirationCost(Player dummy) {
			Item held = dummy?.HeldItem;
			if (held?.ModItem == null || _bardItemType == null || _getInspirationCost == null) {
				return 0;
			}

			if (!_bardItemType.IsInstanceOfType(held.ModItem)) {
				return 0;
			}

			try {
				object result = _getInspirationCost.Invoke(held.ModItem, new object[] { dummy });
				return result switch {
					int i => Math.Max(0, i),
					float f => Math.Max(0, (int)f),
					_ => 0
				};
			}
			catch {
				return 0;
			}
		}

		private static bool TryGetFollowOwner(Player dummy, out Player owner) {
			owner = null;
			if (dummy == null
				|| !NpcDummyPool.TryGetNpcWho(dummy.whoAmI, out int npcWho)
				|| npcWho < 0
				|| npcWho >= Main.maxNPCs) {
				return false;
			}

			NPC npc = Main.npc[npcWho];
			if (npc == null || !npc.active) {
				return false;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing
				|| follow.FollowPlayerWhoAmI < 0
				|| follow.FollowPlayerWhoAmI >= Main.maxPlayers) {
				return false;
			}

			owner = Main.player[follow.FollowPlayerWhoAmI];
			return owner != null && owner.active;
		}

		private static void FillSots(Player dummy) {
			ModPlayer mp = GetModPlayer(dummy, _voidPlayerType);
			if (mp == null) {
				return;
			}

			// Palico PermanentResources (BeforeReset).
			int max = VoidFallback;
			if (TryGetFollowOwner(dummy, out Player owner)) {
				ModPlayer ownerMp = GetModPlayer(owner, _voidPlayerType);
				if (ownerMp != null) {
					max = Math.Max(VoidFallback, GetNumeric(_voidMeterMax, ownerMp, VoidFallback));
					CopyField(_voidAnkh, ownerMp, mp);
					CopyField(_voidStar, ownerMp, mp);
					CopyField(_voidSoul, ownerMp, mp);
				}
			}

			SetNumeric(_voidMeterMax, mp, max);
			// Match real player after ResetEffects: max2 = voidMeterMax (+bonuses).
			// CanUseItem alone does max2 - VoidMinionConsumption — do not also
			// subtract into max2 here (that double-count blocked a 2nd Lemegeton at 200).
			SetNumeric(_voidMeterMax2, mp, max);

			// Register under myPlayer scope (Palico Scope) so dummy-owned wisps count.
			int consumed = RegisterVoidMinionsAsLocal(dummy, mp);
			if (_voidMinionConsumption != null) {
				SetNumeric(_voidMinionConsumption, mp, consumed);
			}

			if (_safetySwitch != null && _safetySwitch.FieldType == typeof(bool)) {
				_safetySwitch.SetValue(mp, true);
			}

			Item held = dummy.HeldItem;
			// Palico Ready: void summons reserve capacity, never top off the meter.
			if (IsVoidSummon(held)) {
				float meter = GetNumericFloat(_voidMeter, mp, 0f);
				if (meter < 0f) {
					SetNumericFloat(_voidMeter, mp, 0f);
				}

				return;
			}

			// Non-summon void: drain natively; top off when empty or below next VoidCost.
			float current = GetNumericFloat(_voidMeter, mp, 0f);
			if (current > max) {
				current = max;
				SetNumericFloat(_voidMeter, mp, current);
			}

			float need = GetHeldVoidCost(dummy);
			if (current <= 0f || (need > 0f && current < need)) {
				SetNumericFloat(_voidMeter, mp, max);
			}
		}

		/// <summary>
		/// Palico Scope: temporarily treat the dummy as Main.myPlayer so SOTS
		/// RegisterVoidMinions / void hooks see this actor's projectiles.
		/// </summary>
		private static int RegisterVoidMinionsAsLocal(Player dummy, ModPlayer mp) {
			if (dummy == null || mp == null) {
				return 0;
			}

			int restoreMy = Main.myPlayer;
			bool restoreActive = dummy.active;
			Player slotPlayer = null;
			bool swappedSlot = false;
			int who = dummy.whoAmI;
			try {
				dummy.active = true;
				if (who >= 0 && who < Main.maxPlayers) {
					slotPlayer = Main.player[who];
					if (!ReferenceEquals(slotPlayer, dummy)) {
						Main.player[who] = dummy;
						swappedSlot = true;
					}
				}

				Main.myPlayer = who;
				return InvokeRegisterVoidMinions(mp);
			}
			finally {
				Main.myPlayer = restoreMy;
				dummy.active = restoreActive;
				if (swappedSlot && who >= 0 && who < Main.maxPlayers) {
					Main.player[who] = slotPlayer;
				}
			}
		}

		private static void CopyField(FieldInfo field, object source, object dest) {
			if (field == null || source == null || dest == null) {
				return;
			}

			try {
				field.SetValue(dest, field.GetValue(source));
			}
			catch {
				// Field missing/renamed or incompatible — soft skip.
			}
		}

		/// <summary>
		/// SOTS void-capacity minions (Nature Spirit, Lemegeton wisps, etc.).
		/// Do not require Projectile.minion — some wisps omit that flag.
		/// </summary>
		public static bool IsVoidCapacityMinion(Projectile proj) {
			if (proj == null || !proj.active) {
				return false;
			}

			ModProjectile modProj = proj.ModProjectile;
			if (modProj?.Mod != null && modProj.Mod.Name == "SOTS") {
				string name = modProj.Name ?? string.Empty;
				string full = modProj.GetType().FullName ?? string.Empty;
				if (name.IndexOf("Spirit", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Tesseract", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Lemegeton", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Wisp", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Virtues", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("CursedBlade", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Bethany", StringComparison.OrdinalIgnoreCase) >= 0
					|| full.IndexOf(".Void.", StringComparison.OrdinalIgnoreCase) >= 0) {
					return true;
				}
			}

			return proj.minion && proj.minionSlots <= 0f;
		}

		/// <summary>Palico Scope-style probe: Fill then native CanUseItem as local player.</summary>
		public static bool ProbeCanUseItem(Player dummy, Item item) {
			if (dummy == null || item == null || item.IsAir) {
				return true;
			}

			Fill(dummy);
			int restoreMy = Main.myPlayer;
			bool restoreActive = dummy.active;
			Player slotPlayer = null;
			bool swappedSlot = false;
			int who = dummy.whoAmI;
			try {
				dummy.active = true;
				if (who >= 0 && who < Main.maxPlayers) {
					slotPlayer = Main.player[who];
					if (!ReferenceEquals(slotPlayer, dummy)) {
						Main.player[who] = dummy;
						swappedSlot = true;
					}
				}

				Main.myPlayer = who;
				return CombinedHooks.CanUseItem(dummy, item);
			}
			finally {
				Main.myPlayer = restoreMy;
				dummy.active = restoreActive;
				if (swappedSlot && who >= 0 && who < Main.maxPlayers) {
					Main.player[who] = slotPlayer;
				}
			}
		}

		private static int InvokeRegisterVoidMinions(ModPlayer mp) {
			if (mp == null || _registerVoidMinions == null) {
				return GetNumeric(_voidMinionConsumption, mp, 0);
			}

			try {
				object result = _registerVoidMinions.Invoke(mp, null);
				return result switch {
					int i => Math.Max(0, i),
					_ => 0
				};
			}
			catch {
				return GetNumeric(_voidMinionConsumption, mp, 0);
			}
		}

		private static float GetHeldVoidCost(Player dummy) {
			Item held = dummy?.HeldItem;
			if (held?.ModItem == null || _voidItemType == null || _voidCost == null) {
				return 0f;
			}

			if (!_voidItemType.IsInstanceOfType(held.ModItem)) {
				return 0f;
			}

			try {
				object result = _voidCost.Invoke(held.ModItem, new object[] { dummy });
				return result switch {
					float f => Math.Max(0f, f),
					int i => Math.Max(0f, i),
					double d => Math.Max(0f, (float)d),
					_ => 0f
				};
			}
			catch {
				return 0f;
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
				// ModPlayer enumeration can throw if a mod failed setup on this slot.
			}

			return null;
		}

		private static int GetNumeric(FieldInfo field, object target, int fallback) {
			if (field == null || target == null) {
				return fallback;
			}

			try {
				object value = field.GetValue(target);
				return value switch {
					int i => i,
					float f => (int)f,
					double d => (int)d,
					_ => fallback
				};
			}
			catch {
				return fallback;
			}
		}

		private static float GetNumericFloat(FieldInfo field, object target, float fallback) {
			if (field == null || target == null) {
				return fallback;
			}

			try {
				object value = field.GetValue(target);
				return value switch {
					float f => f,
					int i => i,
					double d => (float)d,
					_ => fallback
				};
			}
			catch {
				return fallback;
			}
		}

		private static void SetNumeric(FieldInfo field, object target, int value) {
			if (field == null || target == null) {
				return;
			}

			Type t = field.FieldType;
			if (t == typeof(int)) {
				field.SetValue(target, value);
			}
			else if (t == typeof(float)) {
				field.SetValue(target, (float)value);
			}
			else if (t == typeof(double)) {
				field.SetValue(target, (double)value);
			}
		}

		private static void SetNumericFloat(FieldInfo field, object target, float value) {
			if (field == null || target == null) {
				return;
			}

			Type t = field.FieldType;
			if (t == typeof(float)) {
				field.SetValue(target, value);
			}
			else if (t == typeof(int)) {
				field.SetValue(target, (int)value);
			}
			else if (t == typeof(double)) {
				field.SetValue(target, (double)value);
			}
		}
	}
}
