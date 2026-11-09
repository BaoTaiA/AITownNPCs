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

		public static float Range(Item item) {
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

			// Palico: cat==null → 100
			if (shot.aiStyle == ProjAIStyleID.Flail) {
				return 100f;
			}

			if (shot.aiStyle == ProjAIStyleID.Yoyo) {
				return Math.Clamp(ProjectileID.Sets.YoyosMaximumRange[shot.type] * 0.85f, 80f, 700f);
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

			if (shot.aiStyle == ProjAIStyleID.Boomerang) {
				return item.type == ItemID.PaladinsHammer
					? 150f
					: Math.Clamp(item.shootSpeed * 24f, 120f, 600f);
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

		public static float EffectiveRange(Item item, NPC target = null) {
			float range = Range(item); // ReachFactor = 1
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
		/// When true (Palico / normal), ranged stays put once CanUse. Boss pass false to always get the approach point.
		/// </param>
		public static Vector2 Stand(Item item, NPC follower, NPC target, bool cautious = false, bool holdInRange = true) {
			Vector2 cat = follower.Center;
			float edge = Vector2.Distance(cat, target.Hitbox.ClosestPointInRect(cat));
			float range = EffectiveRange(item, target);
			float gap = IsRangedStance(item)
				? Math.Clamp(range * 0.5f, 24f, 100f)
				: Math.Clamp(ContactRange(item) * 0.3f, 8f, 18f);

			if (item.channel && Sample(item)?.aiStyle == ProjAIStyleID.Spear) {
				gap = range * 0.7f;
			}

			if (cautious) {
				gap = Math.Max(gap, 80f);
				if (holdInRange && edge >= gap + follower.width / 2f) {
					return cat;
				}
			}

			if (holdInRange
				&& IsRangedStance(item)
				&& edge >= gap + follower.width / 2f
				&& CanUse(follower, item, target)) {
				return cat;
			}

			float side = cat.X < target.Center.X ? -1f : 1f;
			float y = Math.Clamp(
				cat.Y,
				target.Top.Y + Math.Min(8, target.height / 2f),
				target.Bottom.Y - Math.Min(8, target.height / 2f));
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
			float range = EffectiveRange(item, target);
			return Vector2.DistanceSquared(hand, hit) <= range * range;
		}
	}
}
