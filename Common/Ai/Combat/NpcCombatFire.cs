using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Ai.Firing;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat
{
	/// <summary>Unified fire path: stance aim → WeaponChannel → DummyActor.</summary>
	public static class NpcCombatFire
	{
		public static bool CanFireWeapon(EquippedTownNPC equip) {
			if (equip.Weapon.IsAir || !NpcEquipHelper.IsAllowedWeapon(equip.Weapon)) {
				return false;
			}

			if (equip.Weapon.useAmmo != AmmoID.None) {
				return !equip.Ammo.IsAir && equip.Ammo.ammo == equip.Weapon.useAmmo;
			}

			return true;
		}

		public static bool HasLineOfSight(NPC from, NPC target) {
			if (from == null || target == null || !from.active || !target.active) {
				return false;
			}

			return Collision.CanHit(from, target)
				|| Collision.CanHitLine(from.Center, 1, 1, target.Center, 1, 1);
		}

		public static void ApplyFacing(NPC npc, Vector2 aimWorld) {
			float dx = aimWorld.X - npc.Center.X;
			int dir = npc.direction;
			if (dx > NpcEquipHelper.FacingDeadzone) {
				dir = 1;
			}
			else if (dx < -NpcEquipHelper.FacingDeadzone) {
				dir = -1;
			}

			npc.direction = dir;
			npc.spriteDirection = dir;
		}

		public static void Tick(NPC npc, EquippedTownNPC equip, NPC target, bool allowFire) {
			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			if (equip.Weapon.IsAir || !NpcEquipHelper.IsAllowedWeapon(equip.Weapon)) {
				if (NpcDummyPool.TryGetDummyWho(npc.whoAmI, out int dummyWho) && NpcDummyPool.OwnsProjectiles(dummyWho)) {
					NpcDummyActor.Tick(npc, equip, combat, target: null, wantUse: false);
					return;
				}

				NpcDummyActor.StopAndRelease(npc.whoAmI);
				return;
			}

			bool allow = allowFire
				&& target != null
				&& target.active
				&& CanFireWeapon(equip)
				&& HasLineOfSight(npc, target);

			if (target != null && target.active) {
				AimAtTarget(npc, combat, equip, target);
			}

			// Dedicated server only says whether combat may fire. The follower client
			// applies the press rhythm against its own dummy, same as single player.
			if (Main.netMode != NetmodeID.Server) {
				allow = NpcWeaponChannel.Apply(npc, equip, allow);
			}

			NpcDummyActor.Tick(npc, equip, combat, target, allow);
		}

		public static void Stop(NPC npc) {
			NpcDummyActor.StopAndRelease(npc.whoAmI);
		}

		private static void AimAtTarget(NPC npc, NpcCombatState combat, EquippedTownNPC equip, NPC target) {
			WeaponStance stance = NpcWeaponStance.Resolve(equip.Weapon);
			float dx = target.Center.X - npc.Center.X;
			float elev = stance.AimElevation;
			Vector2 aim = target.Center;
			if (elev > 0f) {
				aim.Y -= elev * System.Math.Abs(dx);
			}

			ApplyFacing(npc, aim);
			combat.ItemRotation = (aim - npc.Center).ToRotation();
		}
	}
}
