using AITownNPCs.Common.Ai.Firing;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace AITownNPCs.Common.Ai.Combat.Stance
{
	/// <summary>
	/// Same permission for acquiring a target and firing. Ordinary weapons still use Collision.CanHit.
	/// </summary>
	public static class NpcAttackPath
	{
		public static bool Allows(Item weapon, Item ammo, Vector2 from, Vector2 to) =>
			NpcWeaponReach.Clear(from, to) || ThroughWalls(weapon, ammo);

		public static bool ThroughWalls(Item weapon, Item ammo) {
			if (weapon == null || weapon.IsAir || weapon.sentry || NpcHealerSupport.IsTool(weapon)) {
				return false;
			}

			int type = weapon.shoot;
			if (weapon.useAmmo > 0) {
				if (ammo == null || ammo.IsAir || ammo.ammo != weapon.useAmmo || ammo.shoot <= ProjectileID.None) {
					return false;
				}

				type = ammo.shoot;
			}

			if (!ContentSamples.ProjectilesByType.TryGetValue(type, out Projectile shot)
				|| !shot.friendly
				|| shot.hostile
				|| shot.ownerHitCheck
				|| shot.minion
				|| shot.sentry
				|| shot.hide
				|| ProjectileID.Sets.IsAWhip[type]) {
				return false;
			}

			if (shot.aiStyle is ProjAIStyleID.HeldProjectile or ProjAIStyleID.Spear or ProjAIStyleID.ShortSword
				or ProjAIStyleID.Flail or ProjAIStyleID.Yoyo or ProjAIStyleID.Harpoon or ProjAIStyleID.NightsEdge
				or ProjAIStyleID.LightsBane or ProjAIStyleID.SleepyOctopod or ProjAIStyleID.ForwardStab
				or ProjAIStyleID.ThickLaser) {
				return false;
			}

			return !shot.tileCollide || shot.aiStyle == ProjAIStyleID.Vilethorn;
		}
	}
}
