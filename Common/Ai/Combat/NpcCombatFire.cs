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
				if (NpcEquipHelper.IsSummonOrSentryStaff(equip.SummonStaff)) {
					if (target != null && target.active) {
						ApplyFacing(npc, target.Center);
					}

					NpcDummyActor.Tick(npc, equip, combat, target, wantUse: false);
					return;
				}

				if (NpcDummyPool.TryGetDummyWho(npc.whoAmI, out int dummyWho) && NpcDummyPool.OwnsProjectiles(dummyWho)) {
					NpcDummyActor.Tick(npc, equip, combat, target: null, wantUse: false);
					return;
				}

				NpcDummyActor.StopAndRelease(npc.whoAmI);
				return;
			}

			// Face the target before CanUse — HandPoint offsets by npc.direction.
			if (target != null && target.active) {
				AimAtTarget(npc, combat, equip, target);
			}

			bool allow = allowFire
				&& target != null
				&& target.active
				&& CanFireWeapon(equip)
				&& NpcWeaponReach.CanUse(npc, equip.Weapon, target)
				&& NpcAttackPath.Allows(equip.Weapon, equip.Ammo, npc.Center, target.Center);

			// Dedicated server only says whether combat may fire. The follower client
			// applies the press rhythm against its own dummy, same as single player.
			if (Main.netMode != NetmodeID.Server && !NpcSummonCombat.BlocksMainHand(npc, equip, target)) {
				allow = NpcWeaponChannel.Apply(npc, equip, allow);
			}

			NpcDummyActor.Tick(npc, equip, combat, target, allow);
		}

		public static void Stop(NPC npc, bool preserveSummons = false) {
			NpcDummyActor.StopAndRelease(npc.whoAmI, preserveSummons);
		}

		private static void AimAtTarget(NPC npc, NpcCombatState combat, EquippedTownNPC equip, NPC target) {
			// Turn toward the foe first so HandPoint matches the attack side (Palico Aim order).
			ApplyFacing(npc, target.Center);
			Vector2 hand = NpcWeaponReach.HandPoint(npc);
			Vector2 aim = NpcWeaponReach.AimPoint(equip.Weapon, hand, target);
			ApplyFacing(npc, aim);
			combat.ItemRotation = (aim - hand).ToRotation();
		}
	}
}
