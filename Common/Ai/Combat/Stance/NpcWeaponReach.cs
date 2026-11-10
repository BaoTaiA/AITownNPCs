using System;
using AITownNPCs.Common.Ai.Firing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Combat.Stance
{
	/// <summary>
	/// Full port of Palico CompanionCombatReach. Host is town NPC instead of companion cat.
	/// </summary>
	public static class NpcWeaponReach
	{
		public static float SearchRange => 640f; // 40 tiles
		public static float TrackingRange => 1600f;
		public static float RangedRange => TrackingRange;

		public static bool Clear(Vector2 from, Vector2 to) =>
			NpcCombatReachability.Clear(from, to, TrackingRange);

		public static Projectile Sample(Item item) =>
			item != null && !item.IsAir && item.shoot > ProjectileID.None
			&& ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile p)
				? p
				: null;

		/// <summary>Palico CompanionWeapons.Range</summary>
		public static float ContactRange(Item i) =>
			Math.Clamp(Math.Max(i.width, i.height) * i.scale, 28f, 64f);

		static bool SafeThrow(Item i) =>
			i.type is ItemID.Shuriken or ItemID.ThrowingKnife or ItemID.PoisonedKnife or ItemID.Snowball
				or ItemID.SpikyBall or ItemID.Grenade or ItemID.StickyGrenade or ItemID.BouncyGrenade
				or ItemID.Beenade or ItemID.FrostDaggerfish or ItemID.Javelin or ItemID.BoneJavelin
				or ItemID.MolotovCocktail or ItemID.RottenEgg or ItemID.Bone;

		static bool Rain(Item item) =>
			item.type is ItemID.CrimsonRod or ItemID.NimbusRod;

		static bool Summoning(Item item) =>
			item.shoot > ProjectileID.None
			&& ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile p)
			&& (p.minion || p.sentry);

		public static float Range(Item item, NPC follower = null) {
			if (item == null || item.IsAir) {
				return RangedRange;
			}

			if (NpcThoriumWeaponBridge.ReachOverride?.Invoke(item) is float thoriumReach) {
				return thoriumReach;
			}

			if (item.consumable && (SafeThrow(item) || NpcThoriumWeaponBridge.IsSafeThrow(item))) {
				return Math.Clamp(item.shootSpeed * 18f, 100f, 240f);
			}

			Projectile shot = Sample(item);
			if (shot == null || shot.aiStyle == ProjAIStyleID.ShortSword) {
				return ContactRange(item) + 24f;
			}

			if (shot.aiStyle == ProjAIStyleID.Spear) {
				return item.channel ? 120f : Math.Clamp(item.shootSpeed * item.useAnimation * 0.75f, 64f, 180f);
			}

			if (shot.aiStyle == ProjAIStyleID.Harpoon) {
				return 0.85f * (shot.type switch {
					ProjectileID.ChainKnife => Main.remixWorld ? 300f : 150f,
					ProjectileID.BoxingGlove => 200f,
					ProjectileID.GolemFist => 500f,
					ProjectileID.ChainGuillotine => 525f,
					_ => 700f
				});
			}

			if (shot.aiStyle == ProjAIStyleID.Flail) {
				return VanillaFlailReach(shot, follower);
			}

			if (shot.aiStyle == ProjAIStyleID.Yoyo) {
				return YoyoRope(shot);
			}

			if (ProjectileID.Sets.IsAWhip[shot.type]) {
				return 220f;
			}

			if (shot.aiStyle == ProjAIStyleID.LightsBane) {
				return ContactRange(item) + 24f;
			}

			if (shot.aiStyle == ProjAIStyleID.NightsEdge && shot.type != ProjectileID.TerraBlade2) {
				return 100f;
			}

			if (shot.aiStyle == ProjAIStyleID.SleepyOctopod || shot.aiStyle == ProjAIStyleID.ForwardStab) {
				return 80f;
			}

			if (shot.aiStyle == ProjAIStyleID.HeldProjectile && item.CountsAsClass(DamageClass.Melee)) {
				if (shot.type == ProjectileID.PiercingStarlight) {
					return shot.width * 4f * item.scale;
				}

				return shot.type is ProjectileID.Arkhalis or ProjectileID.Terragrim
					? shot.width * 0.5f
					: 260f;
			}

			// 附魔斧会一直追踪，没有固定折返，不在这里估算。
			if (shot.aiStyle == ProjAIStyleID.Boomerang && TryBoomerangReach(item, shot, follower, out float boomerang)) {
				return boomerang;
			}

			if (TryBaseFlailThrow(item, shot, out float thrown)) {
				return thrown;
			}

			if (item.type == ItemID.IceSickle) {
				return 160f;
			}

			if (shot.aiStyle == ProjAIStyleID.MoveShort && !Rain(item)) {
				return 180f;
			}

			if (shot.aiStyle == ProjAIStyleID.ShadowFlame) {
				return 300f;
			}

			if (shot.aiStyle == ProjAIStyleID.Vilethorn) {
				return Math.Clamp(item.shootSpeed * 7f, 100f, 350f);
			}

			if (shot.aiStyle == ProjAIStyleID.BloodThorn) {
				return 400f;
			}

			if (!item.noMelee && shot.aiStyle == ProjAIStyleID.SuperStarBeam) {
				return ContactRange(item) + 24f;
			}

			if (TryAnchoredMelee(item, shot, out float swing)) {
				return swing;
			}

			if (TryShortMeleeShot(item, shot, out float meleeShot)) {
				return meleeShot;
			}

			return RangedRange;
		}

		public static bool IsRangedStance(Item item) {
			Projectile shot = Sample(item);
			return shot?.aiStyle is ProjAIStyleID.Yoyo or ProjAIStyleID.Spear or ProjAIStyleID.Harpoon
					or ProjAIStyleID.Flail or ProjAIStyleID.SleepyOctopod or ProjAIStyleID.ForwardStab
				|| shot != null && item.CountsAsClass(DamageClass.Melee)
					&& shot.aiStyle is ProjAIStyleID.HeldProjectile or ProjAIStyleID.LightsBane or ProjAIStyleID.NightsEdge
				|| item.consumable && SafeThrow(item)
				|| Range(item) > 120f
				|| Summoning(item);
		}

		public static float EffectiveRange(Item item, NPC target = null, NPC follower = null) {
			float range = Range(item, follower); // ReachFactor = 1
			if (target != null && Sample(item)?.aiStyle == ProjAIStyleID.BloodThorn && !TearSurface(target.Center)) {
				range = Math.Min(range, 96f);
			}

			return range;
		}

		static bool TearSurface(Vector2 at) {
			Point tile = at.ToTileCoordinates();
			for (int x = tile.X - 6; x <= tile.X + 6; x++) {
				for (int y = tile.Y - 12; y <= tile.Y + 12; y++) {
					if (WorldGen.InWorld(x, y, 40)
						&& Vector2.DistanceSquared(new Vector2(x * 16 + 8, y * 16 + 8), at) < 180 * 180
						&& WorldGen.SolidTile2(x, y)) {
						return true;
					}
				}
			}

			return false;
		}

		public static Vector2 HandPoint(NPC npc) {
			if (npc == null) {
				return Vector2.Zero;
			}

			return npc.Center + new Vector2(npc.direction * 12f, -4f);
		}

		/// <param name="holdInRange">
		/// When true, do not back away once the weapon can hit. A body-to-body enemy is sidestepped. Boss chase passes false.
		/// </param>
		public static Vector2 Stand(Item item, NPC follower, NPC target, bool cautious = false, bool holdInRange = true) {
			Vector2 cat = follower.Center;
			float edge = Vector2.Distance(cat, target.Hitbox.ClosestPointInRect(cat));
			float range = EffectiveRange(item, target, follower);
			float gap = IsRangedStance(item)
				? Math.Clamp(range * 0.5f, 24f, 100f)
				: Math.Clamp(ContactRange(item) * 0.3f, 8f, 18f);

			if (item.channel && Sample(item)?.aiStyle == ProjAIStyleID.Spear) {
				gap = range * 0.7f;
			}

			if (cautious) {
				gap = Math.Max(gap, 80f);
			}

			float side = cat.X < target.Center.X ? -1f : 1f;
			if (Math.Abs(cat.X - target.Center.X) < 1f) {
				side = follower.direction != 0 ? follower.direction : -1f;
			}

			float y = Math.Clamp(
				cat.Y,
				target.Top.Y + Math.Min(8, target.height / 2f),
				target.Bottom.Y - Math.Min(8, target.height / 2f));

			// Weapon already reaches: stay and shoot. Only slide aside when the enemy is inside the body.
			if (holdInRange && CanUse(follower, item, target)) {
				float crowd = follower.width * 0.5f + 12f;
				if (edge > crowd) {
					return cat;
				}

				return new Vector2(
					(side < 0 ? target.Left.X : target.Right.X) + side * crowd,
					y);
			}

			if (IsRangedStance(item) && edge > range * 0.8f) {
				gap = range * 0.65f;
			}

			return new Vector2(
				(side < 0 ? target.Left.X : target.Right.X) + side * (follower.width / 2f + gap),
				y);
		}

		public static Vector2 AimPoint(Item item, Vector2 hand, NPC target) {
			if (IsRangedStance(item)) {
				return target.Center;
			}

			Vector2 surface = target.Hitbox.ClosestPointInRect(hand);
			return surface + (target.Center - surface).SafeNormalize(Vector2.UnitX) * 4f;
		}

		public static bool CanUse(NPC follower, Item item, NPC target) {
			if (follower == null || target == null || item == null || item.IsAir) {
				return false;
			}

			Vector2 hand = HandPoint(follower);
			Vector2 hit = target.Hitbox.ClosestPointInRect(hand);
			float range = EffectiveRange(item, target, follower);
			return Vector2.DistanceSquared(hand, hit) <= range * range;
		}

		/// <summary>表内用绳长本身。0 或越出 80–800 时按 160，不当成远程。</summary>
		static float YoyoRope(Projectile shot) {
			float rope = 0f;
			if ((uint)shot.type < (uint)ProjectileID.Sets.YoyosMaximumRange.Length) {
				rope = ProjectileID.Sets.YoyosMaximumRange[shot.type];
			}

			return rope >= 80f && rope <= 800f ? rope : 160f;
		}

		/// <summary>
		/// 原版链枷出手前的长度：min(出手速度 × 收回时限, 链长上限)。
		/// 速度先套上模组的 FlailStats，再乘假人近战攻速。
		/// </summary>
		static float VanillaFlailReach(Projectile shot, NPC follower) {
			int launchTime = 10;
			float launchSpeed = 24f;
			float maxLaunch = 800f;
			switch (shot.type) {
				case ProjectileID.BallOHurt:
					launchTime = 15;
					launchSpeed = 14f;
					break;
				case ProjectileID.BlueMoon:
					launchTime = 15;
					launchSpeed = 16f;
					break;
				case ProjectileID.Sunfury:
					launchTime = 15;
					launchSpeed = 17f;
					break;
				case ProjectileID.TheDaoofPow:
					launchTime = 13;
					launchSpeed = 21f;
					break;
				case ProjectileID.TheMeatball:
					launchTime = 15;
					launchSpeed = 15f;
					break;
				case ProjectileID.FlowerPow:
					launchTime = 13;
					launchSpeed = 23f;
					break;
				case ProjectileID.DripplerFlail:
					launchTime = 13;
					launchSpeed = 22f;
					break;
				case ProjectileID.Mace:
				case ProjectileID.FlamingMace:
					launchTime = 13;
					launchSpeed = 12f;
					break;
			}

			float retractAcceleration = 3f;
			float maxRetractSpeed = 16f;
			float forcedRetractAcceleration = 6f;
			float maxForcedRetractSpeed = 48f;
			int ricochetTime = 15;
			float spinDistance = 30f;
			ProjectileLoader.FlailStats(
				shot,
				ref launchTime,
				ref launchSpeed,
				ref maxLaunch,
				ref retractAcceleration,
				ref maxRetractSpeed,
				ref forcedRetractAcceleration,
				ref maxForcedRetractSpeed,
				ref ricochetTime,
				ref spinDistance);
			launchSpeed *= MeleeSpeed(follower);
			if (launchTime < 1) {
				launchTime = 1;
			}

			return Math.Min(launchSpeed * launchTime, Math.Max(1f, maxLaunch));
		}

		/// <summary>
		/// 折返计数每更新一次加一，extraUpdates 会让一帧更新多次，所以次数本身已经含进去。
		/// 圣骑士锤 20 次，光辉飞盘 45 次，其余固定折返的是 30 次。
		/// </summary>
		static bool TryBoomerangReach(Item item, Projectile shot, NPC follower, out float reach) {
			reach = 0f;
			if (shot.type == ProjectileID.PossessedHatchet || item.shootSpeed <= 0f) {
				return false;
			}

			float speed = item.shootSpeed * MeleeSpeed(item, shot, follower);
			if (shot.type == ProjectileID.Anchor) {
				reach = 800f;
				return true;
			}

			if (shot.type == ProjectileID.BloodyMachete) {
				// 前 10 次原速，之后水平速度每次乘 0.95，直到停住。
				reach = speed * 29f;
				return true;
			}

			int steps = shot.type switch {
				ProjectileID.LightDisc => 45,
				ProjectileID.PaladinsHammerFriendly => 20,
				_ => 30
			};
			reach = speed * steps;
			return reach > 0f;
		}

		/// <summary>暗影奥秘 BaseFlailProj：满蓄甩出，不拿身边的旋转半径当距离。</summary>
		static bool TryBaseFlailThrow(Item item, Projectile shot, out float reach) {
			reach = 0f;
			if (!IsBaseFlailProj(shot) || shot.ModProjectile == null) {
				return false;
			}

			object mod = shot.ModProjectile;
			float speedMultY = 1f;
			for (Type type = mod.GetType(); type != null; type = type.BaseType) {
				if (type.Name != "BaseFlailProj") {
					continue;
				}

				if (type.GetField("SpeedMult")?.GetValue(mod) is Vector2 speed) {
					speedMultY = speed.Y;
				}

				break;
			}

			if (speedMultY <= 0f) {
				speedMultY = 1f;
			}

			float shootSpeed = item.shootSpeed > 0f ? item.shootSpeed : 12f;
			// 满蓄飞行不超过 30 帧。阿托恩、北极星松手再乘 0.85，粉碎之光乘 1.2。
			reach = shootSpeed * speedMultY * 30f * BaseFlailLaunchScale(mod.GetType().Name);
			return reach > 0f;
		}

		static float BaseFlailLaunchScale(string typeName) => typeName switch {
			"Aten" or "NorthStar" => 0.85f,
			"Shattershine" => 1.2f,
			_ => 1f
		};

		static bool IsBaseFlailProj(Projectile shot) {
			if (shot?.ModProjectile == null) {
				return false;
			}

			for (Type type = shot.ModProjectile.GetType(); type != null; type = type.BaseType) {
				if (type.Name == "BaseFlailProj") {
					return true;
				}
			}

			return false;
		}

		/// <summary>贴在身上、自己不位移的近战弹幕。距离用武器和弹幕尺寸，不按飞行时间。</summary>
		static bool TryAnchoredMelee(Item item, Projectile shot, out float reach) {
			reach = 0f;
			if (item == null || shot?.ModProjectile == null || shot.ModProjectile.ShouldUpdatePosition()) {
				return false;
			}

			bool melee = item.CountsAsClass(DamageClass.Melee) || item.CountsAsClass(DamageClass.MeleeNoSpeed);
			if (!melee && shot.DamageType != null) {
				melee = shot.DamageType.CountsAsClass(DamageClass.Melee)
					|| shot.DamageType.CountsAsClass(DamageClass.MeleeNoSpeed);
			}

			if (!melee) {
				return false;
			}

			float itemSize = Math.Max(item.width, item.height) * item.scale;
			float shotSize = Math.Max(shot.width, shot.height) * shot.scale;
			reach = Math.Clamp(Math.Max(itemSize, shotSize) + 24f, 48f, 160f);
			return true;
		}

		/// <summary>只收现在会落到 1600、近战、会撞方块、估算在 16–200 的弹幕。</summary>
		static bool TryShortMeleeShot(Item item, Projectile shot, out float reach) {
			reach = 0f;
			if (item == null || shot == null || !shot.tileCollide || !item.CountsAsClass(DamageClass.Melee)) {
				return false;
			}

			int updates = shot.extraUpdates + 1;
			if (updates < 1 || item.shootSpeed <= 0f || shot.timeLeft <= 0) {
				return false;
			}

			reach = item.shootSpeed * shot.timeLeft / updates;
			return reach >= 16f && reach <= 200f;
		}

		static float MeleeSpeed(NPC follower) {
			if (follower != null
				&& NpcDummyPool.TryGetDummy(follower.whoAmI, out Player dummy)
				&& dummy != null) {
				float speed = dummy.GetTotalAttackSpeed(DamageClass.Melee);
				if (speed > 0.01f) {
					return speed;
				}
			}

			return 1f;
		}

		/// <summary>和出手时一样：近战且这发弹幕吃攻速，才把射速乘上去。</summary>
		static float MeleeSpeed(Item item, Projectile shot, NPC follower) {
			if (item != null
				&& item.CountsAsClass(DamageClass.Melee)
				&& (uint)shot.type < (uint)ProjectileID.Sets.NoMeleeSpeedVelocityScaling.Length
				&& !ProjectileID.Sets.NoMeleeSpeedVelocityScaling[shot.type]) {
				return MeleeSpeed(follower);
			}

			return 1f;
		}
	}
}
