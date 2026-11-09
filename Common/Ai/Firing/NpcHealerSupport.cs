using System;
using System.Collections.Generic;
using System.Reflection;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Thorium healer tools in the main weapon slot: follow-slot aim at injured allies; Renew redirects onto the town NPC / follower.
	/// Proactive support (shields / buffs / shoulder guardians) may cast on full-HP allies.
	/// </summary>
	public sealed class NpcHealerSupport : ModSystem
	{
		public const float PatientRange = 600f;

		/// <summary>Frames before another full-HP support cast (shield / bubble) after a shot is out.</summary>
		public const uint ProactiveSupportCooldown = 300;

		/// <summary>
		/// Name substrings for support / guardians that should cast without waiting for damage
		/// (healAmount may still be &gt; 0 for display, e.g. Cherub).
		/// </summary>
		private static readonly string[] ProactiveNameContains = {
			"GraniteIon",
			"BubbleBulwark",
			"SerpentsBubble",
			"ShieldDrone",
			"Cherub",
			"BloomGuard",
			"PocketGuardian",
			"WarForger",
			"AuraStaff",
			"NullZone",
			"GlitteringScepter",
			"LifePulse",
			"DreamMegaphone",
			"PhoenixStaff",
			"Valhalla",
			"DragonHeart",
			"DreamCatcher",
			"Redeemer",
			"MarrowScepter",
			"Liberation",
			"WhisperingDagger",
			"Revivify",
			"BarrierGenerator",
			"Cryotherapy",
		};

		/// <summary>Weapons that write ThoriumPlayer.shieldHealth (life shield), not a buff.</summary>
		private static readonly string[] LifeShieldNameContains = {
			"GraniteIon",
			"WarForger",
			"BarrierGenerator",
			"Cryotherapy",
		};

		/// <summary>Item ModItem.Name → Thorium buff internal name (soft Resolve).</summary>
		private static readonly (string ItemName, string BuffName)[] SupportBuffNames = {
			("BubbleBulwarkWand", "BubbleBulwarkWandBuff"),
			("SerpentsBubbleWand", "SerpentsBubbleWandBuff"),
			("ShieldDroneBeacon", "ShieldDroneBeaconBuff"),
			("LilCherubsWand", "LilCherubsWandBuff"),
			("DreamMegaphone", "DreamMegaphoneBuff"),
			("PhoenixStaff", "PhoenixStaffBuff"),
			("MarrowScepter", "MarrowScepterBuff"),
			("WhisperingDagger", "WhisperingDaggerBuff"),
			("NullZoneStaff", "NullZoneBuff"),
			("DreamCatcher", "DreamCatcherBuff"),
			("DragonHeartWand", "DragonHeartWandBuff"),
		};

		private static bool _thoriumPresent;
		private static Type _thoriumItemType;
		private static Type _thoriumPlayerType;
		private static FieldInfo _isHealer;
		private static FieldInfo _healType;
		private static FieldInfo _healAmount;
		private static FieldInfo _shieldHealth;
		private static Hook _healLifeHook;
		private static readonly Dictionary<string, int> SupportBuffByItemName = new(StringComparer.OrdinalIgnoreCase);

		private delegate int HealLifeOrig(Player player, int amount, Player healer, bool overMax, bool statistics);

		public override void PostSetupContent() {
			_thoriumPresent = false;
			_thoriumItemType = null;
			_thoriumPlayerType = null;
			_isHealer = null;
			_healType = null;
			_healAmount = null;
			_shieldHealth = null;
			SupportBuffByItemName.Clear();
			_healLifeHook?.Dispose();
			_healLifeHook = null;

			if (!ModLoader.TryGetMod("ThoriumMod", out Mod thorium) || thorium.Code == null) {
				return;
			}

			_thoriumPresent = true;
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			_thoriumItemType = thorium.Code.GetType("ThoriumMod.Items.ThoriumItem");
			_thoriumPlayerType = thorium.Code.GetType("ThoriumMod.ThoriumPlayer");
			if (_thoriumItemType != null) {
				_isHealer = _thoriumItemType.GetField("isHealer", flags);
				_healType = _thoriumItemType.GetField("healType", flags);
				_healAmount = _thoriumItemType.GetField("healAmount", flags);
			}

			_shieldHealth = _thoriumPlayerType?.GetField("shieldHealth", flags);

			for (int i = 0; i < SupportBuffNames.Length; i++) {
				(string itemName, string buffName) = SupportBuffNames[i];
				if (ModContent.TryFind($"ThoriumMod/{buffName}", out ModBuff buff) && buff != null) {
					SupportBuffByItemName[itemName] = buff.Type;
				}
			}

			Type helper = thorium.Code.GetType("ThoriumMod.Utilities.PlayerHelper");
			MethodInfo heal = helper?.GetMethod(
				"HealLife",
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
				binder: null,
				types: new[] { typeof(Player), typeof(int), typeof(Player), typeof(bool), typeof(bool) },
				modifiers: null);
			if (heal != null) {
				_healLifeHook = new Hook(heal, OnHealLife);
			}
			else {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn("Thorium HealLife not found; Renew redirect disabled.");
			}
		}

		public override void Unload() {
			_healLifeHook?.Dispose();
			_healLifeHook = null;
			_thoriumItemType = null;
			_thoriumPlayerType = null;
			_isHealer = null;
			_healType = null;
			_healAmount = null;
			_shieldHealth = null;
			SupportBuffByItemName.Clear();
			_thoriumPresent = false;
		}

		/// <summary>
		/// Shields, buffs, and persistent guardians — castable on full-HP allies.
		/// Direct HP heals stay hurt-gated in <see cref="FindPatient"/>.
		/// </summary>
		public static bool IsProactiveSupport(Item tool) {
			if (!IsTool(tool) || IsRenew(tool)) {
				return false;
			}

			if (NameLooksProactive(tool)) {
				return true;
			}

			if (_healAmount == null || tool.ModItem == null) {
				return false;
			}

			if (_thoriumItemType != null && !_thoriumItemType.IsInstanceOfType(tool.ModItem)) {
				return false;
			}

			try {
				return _healAmount.GetValue(tool.ModItem) is int amount && amount <= 0;
			}
			catch {
				return false;
			}
		}

		private static bool NameLooksProactive(Item tool) {
			string name = tool?.ModItem?.Name;
			if (string.IsNullOrEmpty(name)) {
				return false;
			}

			for (int i = 0; i < ProactiveNameContains.Length; i++) {
				string needle = ProactiveNameContains[i];
				if (!string.IsNullOrEmpty(needle)
					&& name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) {
					return true;
				}
			}

			return false;
		}

		public static bool IsTool(Item item) {
			if (!_thoriumPresent || item == null || item.IsAir || item.consumable || item.damage > 0) {
				return false;
			}

			ModItem modItem = item.ModItem;
			if (modItem?.Mod == null || modItem.Mod.Name != "ThoriumMod") {
				return false;
			}

			if (_isHealer != null) {
				if (_thoriumItemType != null && !_thoriumItemType.IsInstanceOfType(modItem)) {
					return false;
				}

				try {
					if (_isHealer.GetValue(modItem) is not bool healer || !healer) {
						return false;
					}
				}
				catch {
					return false;
				}
			}
			else if (!IsRenew(item)
				&& modItem.GetType().FullName?.Contains("Healer", StringComparison.Ordinal) != true) {
				return false;
			}

			return item.shoot > ProjectileID.None || IsRenew(item);
		}

		public static bool IsRenew(Item item) {
			if (item?.ModItem == null) {
				return false;
			}

			string name = item.ModItem.Name;
			string full = item.ModItem.GetType().FullName;
			return name.Equals("Renew", StringComparison.OrdinalIgnoreCase)
				|| (full != null && full.EndsWith(".Renew", StringComparison.Ordinal));
		}

		public static void Tick(NPC npc, EquippedTownNPC equip) {
			if (npc == null || !npc.active || equip == null || !IsTool(equip.Weapon)) {
				return;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing) {
				NpcCombatFire.Stop(npc, preserveSummons: false);
				return;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
			Vector2 aim = npc.Center + new Vector2(npc.direction * 320f, 0f);
			bool wantUse;
			bool proactive = false;
			if (IsRenew(equip.Weapon)) {
				wantUse = NeedsRenew(npc, follow, out aim);
			}
			else {
				proactive = IsProactiveSupport(equip.Weapon);
				Player patient = FindPatient(npc, equip.Weapon, follow.FollowPlayerWhoAmI, out aim);
				wantUse = patient != null;
				// Already shielded / buffed: do not re-cast (re-hit clears or wastes the effect).
				if (wantUse && proactive && PatientHasSupportEffect(patient, equip.Weapon)) {
					wantUse = false;
				}

				// Full-HP shield/buff spam: wait out cooldown after a cast is in flight.
				if (wantUse && proactive && Main.GameUpdateCount < firing.SupportRetryUntil) {
					wantUse = false;
				}

				// Palico: single-cast support waits for its shot; shoulder guardians
				// / persistent summons keep an owned body — do not re-press.
				if (wantUse && !equip.Weapon.channel && HasOwnedSupportShot(npc, equip.Weapon, patient)) {
					wantUse = false;
					if (proactive) {
						firing.SupportRetryUntil = Main.GameUpdateCount + ProactiveSupportCooldown;
					}
				}
			}

			if (wantUse) {
				NpcCombatFire.ApplyFacing(npc, aim);
				combat.ItemRotation = (aim - NpcWeaponReach.HandPoint(npc)).ToRotation();
			}

			if (Main.netMode != NetmodeID.Server && !NpcSummonCombat.BlocksMainHand(npc, equip, target: null)) {
				wantUse = NpcWeaponChannel.Apply(npc, equip, wantUse);
				// Channel support (Ion Staff): start cooldown once the hold-release fires.
				if (proactive
					&& equip.Weapon.channel
					&& firing.ChannelThrowing
					&& !wantUse
					&& Main.GameUpdateCount >= firing.SupportRetryUntil) {
					firing.SupportRetryUntil = Main.GameUpdateCount + ProactiveSupportCooldown;
				}
			}

			NpcDummyActor.TickAimed(npc, equip, combat, aim, wantUse);
		}

		/// <summary>
		/// Palico SupportTarget gate: owned shoot in flight, near the patient, or
		/// any persistent body of that type (Cherub / Bloom Guard / Shield Drone).
		/// </summary>
		private static bool HasOwnedSupportShot(NPC npc, Item tool, Player patient) {
			if (npc == null || tool == null || tool.IsAir || tool.shoot <= ProjectileID.None) {
				return false;
			}

			if (!NpcDummyPool.TryGetDummyWho(npc.whoAmI, out int dummyWho) || !NpcDummyPool.IsDummy(dummyWho)) {
				return false;
			}

			const float nearPatientSq = 120f * 120f;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile shot = Main.projectile[i];
				if (shot == null || !shot.active || shot.owner != dummyWho || shot.type != tool.shoot) {
					continue;
				}

				if (shot.velocity.LengthSquared() > 0.1f) {
					return true;
				}

				if (patient != null && Vector2.DistanceSquared(shot.Center, patient.Center) < nearPatientSq) {
					return true;
				}

				// Persistent guardian / field: body stays near the caster.
				if (shot.velocity.LengthSquared() <= 0.1f) {
					return true;
				}
			}

			return false;
		}

		private static bool NeedsRenew(NPC npc, FollowingTownNPC follow, out Vector2 aim) {
			aim = npc.Center;
			bool npcHurt = npc.life < npc.lifeMax;
			Player owner = null;
			if (follow.IsFollowing
				&& follow.FollowPlayerWhoAmI >= 0
				&& follow.FollowPlayerWhoAmI < Main.maxPlayers) {
				owner = Main.player[follow.FollowPlayerWhoAmI];
				if (owner != null && owner.active) {
					aim = owner.Center;
					if (npc.Distance(owner.Center) > PatientRange) {
						return false;
					}
				}
			}

			bool ownerHurt = IsHurtAlly(owner);
			return npcHurt || ownerHurt;
		}

		public static Player FindPatient(NPC npc, Item tool, int followPlayerWho, out Vector2 aim) {
			aim = npc.Center + new Vector2(npc.direction * 320f, 0f);
			if (npc == null || tool == null || tool.IsAir || IsRenew(tool)) {
				return null;
			}

			// Palico healType: Player = self-only via Renew path; AllyAndPlayer includes
			// self (dummy has no HP — NPC Renew handles self); Ally = injured allies.
			HealIntent intent = ReadHealIntent(tool);
			if (intent == HealIntent.PlayerOnly) {
				return null;
			}

			bool proactive = IsProactiveSupport(tool);
			Player best = null;
			float bestScore = float.MaxValue;
			Player owner = null;
			if (followPlayerWho >= 0 && followPlayerWho < Main.maxPlayers) {
				owner = Main.player[followPlayerWho];
				if (IsPatientCandidate(owner, proactive, tool) && InRange(npc, owner)) {
					best = owner;
					bestScore = LifeScore(owner) - 200f;
					aim = owner.Center;
				}
			}

			foreach (Player player in Main.ActivePlayers) {
				if (!IsPatientCandidate(player, proactive, tool) || !InRange(npc, player)) {
					continue;
				}

				if (owner != null && owner.active) {
					if (owner.team != 0 && player.team != owner.team) {
						continue;
					}

					if (player.hostile && (player.team == 0 || player.team != owner.team)) {
						continue;
					}
				}

				float rank = LifeScore(player) + MathF.Sqrt(npc.DistanceSQ(player.Center));
				if (rank < bestScore) {
					best = player;
					bestScore = rank;
					aim = player.Center;
				}
			}

			return best;
		}

		/// <summary>
		/// True when the ally already has this support's lasting effect
		/// (buff and/or Thorium life shield). Skip re-cast until it drops.
		/// </summary>
		public static bool PatientHasSupportEffect(Player patient, Item tool) {
			if (!_thoriumPresent || patient == null || !IsLivingAlly(patient) || tool?.ModItem == null) {
				return false;
			}

			if (tool.buffType > 0 && patient.HasBuff(tool.buffType)) {
				return true;
			}

			string itemName = tool.ModItem.Name;
			if (!string.IsNullOrEmpty(itemName)
				&& SupportBuffByItemName.TryGetValue(itemName, out int buffType)
				&& buffType > 0
				&& patient.HasBuff(buffType)) {
				return true;
			}

			// Soft fallback: ThoriumMod/{ItemName}Buff
			if (!string.IsNullOrEmpty(itemName)
				&& ModContent.TryFind($"ThoriumMod/{itemName}Buff", out ModBuff namedBuff)
				&& namedBuff != null
				&& patient.HasBuff(namedBuff.Type)) {
				return true;
			}

			if (IsLifeShieldSupport(tool) && GetShieldHealth(patient) > 0) {
				return true;
			}

			return false;
		}

		private static bool IsLifeShieldSupport(Item tool) {
			string name = tool?.ModItem?.Name;
			if (string.IsNullOrEmpty(name)) {
				return false;
			}

			for (int i = 0; i < LifeShieldNameContains.Length; i++) {
				string needle = LifeShieldNameContains[i];
				if (!string.IsNullOrEmpty(needle)
					&& name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) {
					return true;
				}
			}

			return false;
		}

		private static int GetShieldHealth(Player player) {
			if (_shieldHealth == null || _thoriumPlayerType == null || player == null) {
				return 0;
			}

			try {
				foreach (ModPlayer mp in player.ModPlayers) {
					if (mp == null || !_thoriumPlayerType.IsInstanceOfType(mp)) {
						continue;
					}

					if (_shieldHealth.GetValue(mp) is int amount) {
						return amount;
					}
				}
			}
			catch {
				// Soft: ThoriumPlayer shape drift.
			}

			return 0;
		}

		private enum HealIntent
		{
			Ally,
			AllyAndPlayer,
			PlayerOnly,
			None
		}

		private static HealIntent ReadHealIntent(Item tool) {
			if (_healType == null || tool?.ModItem == null) {
				return HealIntent.Ally;
			}

			if (_thoriumItemType != null && !_thoriumItemType.IsInstanceOfType(tool.ModItem)) {
				return HealIntent.Ally;
			}

			try {
				object value = _healType.GetValue(tool.ModItem);
				if (value == null) {
					return HealIntent.Ally;
				}

				string name = value.ToString() ?? string.Empty;
				if (name.Equals("Player", StringComparison.OrdinalIgnoreCase)) {
					return HealIntent.PlayerOnly;
				}

				if (name.Equals("AllyAndPlayer", StringComparison.OrdinalIgnoreCase)) {
					return HealIntent.AllyAndPlayer;
				}

				if (name.Equals("None", StringComparison.OrdinalIgnoreCase)) {
					return HealIntent.None;
				}

				return HealIntent.Ally;
			}
			catch {
				return HealIntent.Ally;
			}
		}

		private static bool IsPatientCandidate(Player player, bool proactive, Item tool = null) {
			if (!IsLivingAlly(player)) {
				return false;
			}

			if (proactive && tool != null && PatientHasSupportEffect(player, tool)) {
				return false;
			}

			return proactive || player.statLife < player.statLifeMax2;
		}

		private static bool IsLivingAlly(Player player) {
			return player != null
				&& player.active
				&& !player.dead
				&& !player.ghost;
		}

		private static bool IsHurtAlly(Player player) {
			return IsLivingAlly(player) && player.statLife < player.statLifeMax2;
		}

		private static bool InRange(NPC npc, Player player) {
			if (npc.Distance(player.Center) > PatientRange) {
				return false;
			}

			return NpcWeaponReach.Clear(npc.Center, player.Center);
		}

		private static float LifeScore(Player player) {
			return (float)player.statLife / Math.Max(1, player.statLifeMax2) * 1000f;
		}

		private static int OnHealLife(HealLifeOrig orig, Player player, int amount, Player healer, bool overMax, bool statistics) {
			int result = orig(player, amount, healer, overMax, statistics);
			TryRedirectRenew(amount, healer);
			return result;
		}

		private static void TryRedirectRenew(int amount, Player healer) {
			if (amount <= 0
				|| healer == null
				|| !NpcDummyPool.IsDummy(healer.whoAmI)
				|| !IsRenew(healer.HeldItem)
				|| Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (!NpcDummyPool.TryGetNpcWho(healer.whoAmI, out int npcWho)
				|| npcWho < 0
				|| npcWho >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWho];
			if (npc == null || !npc.active) {
				return;
			}

			int remaining = amount;
			if (npc.life < npc.lifeMax) {
				int before = npc.life;
				npc.life = Math.Min(npc.lifeMax, npc.life + remaining);
				int applied = npc.life - before;
				remaining -= applied;
				if (applied > 0) {
					npc.HealEffect(applied, broadcast: true);
					npc.netUpdate = true;
				}
			}

			if (remaining <= 0) {
				return;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing
				|| follow.FollowPlayerWhoAmI < 0
				|| follow.FollowPlayerWhoAmI >= Main.maxPlayers) {
				return;
			}

			Player owner = Main.player[follow.FollowPlayerWhoAmI];
			if (!IsHurtAlly(owner)) {
				return;
			}

			ServerPlayerNet.HealLifeSynced(owner, remaining);
		}
	}
}
