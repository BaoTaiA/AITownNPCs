using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// 武器「按键节奏」规则（Firing 层）。
	///
	/// 判定顺序：
	/// 1. 必须 channel 且 shoot &gt; 0，否则不改节奏（默认一直按）；
	/// 2. 【黑名单】命中 → 强制默认一直按（只挡白名单误伤：日耀、弹刃、溜溜球、塔纳托斯脊柱等）；
	/// 3. 【白名单】命中任一簇 → 统一节奏：按住 N 帧 → 松手发射 → 射出就再蓄；
	/// 4. 未命中 → 默认一直按。
	///
	/// 白名单分簇识别，每簇一个 HoldFrames；黑名单只挡误伤。改完重新编译即可。
	/// </summary>
	public static class NpcWeaponChannelRules
	{
		// ========== 对外 API ==========

		/// <summary>是否需要「先按后松」节奏（白名单且非黑名单）。</summary>
		public static bool NeedsHoldReleaseCycle(Item weapon) {
			if (weapon == null || weapon.IsAir || !weapon.channel || weapon.shoot <= ProjectileID.None) {
				return false;
			}

			if (IsAlwaysHoldBlacklisted(weapon)) {
				return false;
			}

			return IsVanillaFlail(weapon)
				|| IsRogueChargeThrow(weapon)
				|| IsRangedChargeFire(weapon)
				|| IsMeleeThrownFlail(weapon)
				|| IsMagicChargeShot(weapon);
		}

		/// <summary>先按后松时，本轮应按住多少帧（按命中簇取该簇 HoldFrames）。</summary>
		public static int GetHoldFrames(Item weapon) {
			if (weapon == null || weapon.IsAir) {
				return ClampHold(60);
			}

			if (IsVanillaFlail(weapon)) {
				return ClampHold(VanillaFlailHoldFrames);
			}

			if (IsRogueChargeThrow(weapon)) {
				return ClampHold(RogueChargeHoldFrames);
			}

			if (IsRangedChargeFire(weapon)) {
				return ClampHold(RangedChargeHoldFrames);
			}

			if (IsMeleeThrownFlail(weapon)) {
				return ClampHold(MeleeThrownFlailHoldFrames);
			}

			if (IsMagicChargeShot(weapon)) {
				return ClampHold(MagicChargeHoldFrames);
			}

			return ClampHold(60);
		}

		private static int ClampHold(int frames) => frames < 1 ? 1 : frames;

		// ========== 黑名单：只挡误伤（属性像白名单，玩法应一直按） ==========

		/// <summary>
		/// 显式弹体类型名（不区分大小写子串匹配）。可自行增减。
		/// 用于灾厄自定义「按住连挥」却对不上 HeldProjectile / BaseFlailProjectile 的武器。
		/// </summary>
		public static readonly string[] AlwaysHoldProjectileNameContains = {
			"SpineOfThanatos", // 塔纳托斯的脊柱：按住挥三鞭
		};

		/// <summary>
		/// 显式物品类型名（不区分大小写子串匹配）。可自行增减。
		/// </summary>
		public static readonly string[] AlwaysHoldItemNameContains = {
			"SpineOfThanatos",
		};

		/// <summary>
		/// 黑名单：强制一直按，不走先按后松。
		/// 溜溜球 / 日耀 HeldProjectile / 灾厄弹刃 BaseFlailProjectile / 显式名表。
		/// </summary>
		public static bool IsAlwaysHoldBlacklisted(Item weapon) {
			if (weapon == null || weapon.IsAir) {
				return false;
			}

			if (weapon.type > ItemID.None && weapon.type < ItemID.Sets.Yoyo.Length && ItemID.Sets.Yoyo[weapon.type]) {
				return true;
			}

			if (weapon.ModItem != null && NameContainsAny(weapon.ModItem.GetType().Name, AlwaysHoldItemNameContains)) {
				return true;
			}

			if (!ContentSamples.ProjectilesByType.TryGetValue(weapon.shoot, out Projectile sample) || sample == null) {
				return false;
			}

			if (sample.aiStyle == ProjAIStyleID.Yoyo || sample.aiStyle == ProjAIStyleID.HeldProjectile) {
				return true;
			}

			if (sample.ModProjectile != null) {
				for (System.Type t = sample.ModProjectile.GetType(); t != null; t = t.BaseType) {
					if (t.Name == "BaseFlailProjectile") {
						return true;
					}

					if (NameContainsAny(t.Name, AlwaysHoldProjectileNameContains)) {
						return true;
					}
				}
			}

			return false;
		}

		private static bool NameContainsAny(string name, string[] needles) {
			if (string.IsNullOrEmpty(name) || needles == null) {
				return false;
			}

			for (int i = 0; i < needles.Length; i++) {
				string n = needles[i];
				if (!string.IsNullOrEmpty(n)
					&& name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0) {
					return true;
				}
			}

			return false;
		}

		// ========== 白名单簇：只负责认武器；节奏统一；每簇一个 HoldFrames ==========

		// ----- 原版连枷 -----
		// 条件：弹体 aiStyle == Flail。典型：链球、陨石连枷。

		/// <summary>按住多少帧再松手（约 60 帧 = 1 秒）。</summary>
		public static int VanillaFlailHoldFrames = 30;

		public static bool IsVanillaFlail(Item weapon) {
			return ContentSamples.ProjectilesByType.TryGetValue(weapon.shoot, out Projectile sample)
				&& sample != null
				&& sample.aiStyle == ProjAIStyleID.Flail;
		}

		// ----- 盗贼蓄力投掷 -----
		// 条件：noUseGraphic + Swing。典型：Calamity Supernova。

		/// <summary>按住多少帧再松手。</summary>
		public static int RogueChargeHoldFrames = 60;

		public static bool IsRogueChargeThrow(Item weapon) {
			return weapon.noUseGraphic && weapon.useStyle == ItemUseStyleID.Swing;
		}

		// ----- 远程蓄力开火 -----
		// 条件：noUseGraphic + Shoot + Ranged。典型：Calamity Buzzkill。

		/// <summary>按住多少帧再松手（远程可单独调大，例如 150 ≈ 2.5 秒）。</summary>
		public static int RangedChargeHoldFrames = 150;

		public static bool IsRangedChargeFire(Item weapon) {
			return weapon.noUseGraphic
				&& weapon.useStyle == ItemUseStyleID.Shoot
				&& weapon.CountsAsClass(DamageClass.Ranged);
		}

		// ----- 近战投掷连枷 -----
		// 条件：noUseGraphic + Shoot + MeleeNoSpeed。
		// 误伤（日耀/弹刃/溜溜球/塔纳托斯脊柱）由黑名单挡住。
		// 典型：Ball O' Fugu / Tumbleweed / 龙魂破 / 新月链刃。

		/// <summary>按住多少帧再松手。</summary>
		public static int MeleeThrownFlailHoldFrames = 30;

		public static bool IsMeleeThrownFlail(Item weapon) {
			return weapon.noUseGraphic
				&& weapon.useStyle == ItemUseStyleID.Shoot
				&& weapon.DamageType == DamageClass.MeleeNoSpeed;
		}

		// ----- 魔法蓄力射击 -----
		// 条件：有持枪图 + Shoot + Magic。典型：Calamity CoralSpout。

		/// <summary>按住多少帧再松手。</summary>
		public static int MagicChargeHoldFrames = 80;

		public static bool IsMagicChargeShot(Item weapon) {
			return !weapon.noUseGraphic
				&& weapon.useStyle == ItemUseStyleID.Shoot
				&& weapon.CountsAsClass(DamageClass.Magic);
		}
	}
}
