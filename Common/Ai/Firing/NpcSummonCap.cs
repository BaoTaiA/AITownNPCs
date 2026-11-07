using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Minion and sentry capacity, matching Palico's "press only when another summon fits".
	/// </summary>
	public static class NpcSummonCap
	{
		public const float SentryRange = 700f;

		public static float CountMinionSlots(int dummyWho) {
			if (!NpcDummyPool.IsDummy(dummyWho)) {
				return 0f;
			}

			float slots = 0f;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj != null && proj.active && proj.owner == dummyWho && proj.minion) {
					slots += proj.minionSlots;
				}
			}

			return slots;
		}

		public static int CountUsefulSentries(int dummyWho, NPC target) {
			if (!NpcDummyPool.IsDummy(dummyWho) || target == null || !target.active) {
				return 0;
			}

			int useful = 0;
			float rangeSq = SentryRange * SentryRange;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj == null || !proj.active || proj.owner != dummyWho || !proj.sentry) {
					continue;
				}

				Vector2 closest = target.Hitbox.ClosestPointInRect(proj.Center);
				if (Vector2.DistanceSquared(proj.Center, closest) <= rangeSq) {
					useful++;
				}
			}

			return useful;
		}

		public static bool NeedsMinion(Player dummy, Item staff) {
			if (!NpcEquipHelper.IsMinionStaff(staff)) {
				return false;
			}

			int dummyWho = dummy?.whoAmI ?? -1;
			float used = CountMinionSlots(dummyWho);
			int max = dummy != null ? dummy.maxMinions : NpcDummyPool.DummyMaxMinions;
			if (max < 1) {
				max = NpcDummyPool.DummyMaxMinions;
			}

			return used + MinionCost(staff) <= max;
		}

		/// <summary>True when another sentry should be placed, and writes a spread aim near the target.</summary>
		public static bool TryPlanSentry(NPC npc, Player dummy, Item staff, NPC target, out Vector2 aim) {
			aim = target != null ? target.Center : npc.Center;
			if (!NpcEquipHelper.IsSentryStaff(staff) || target == null || !target.active) {
				return false;
			}

			int max = dummy != null ? dummy.maxTurrets : NpcDummyPool.DummyMaxTurrets;
			if (max < 1) {
				max = NpcDummyPool.DummyMaxTurrets;
			}

			int useful = dummy != null ? CountUsefulSentries(dummy.whoAmI, target) : 0;
			if (useful >= max) {
				return false;
			}

			float side = npc.Center.X < target.Center.X ? -1f : 1f;
			for (int candidate = 0; candidate < 6; candidate++) {
				float offset = (64f + (useful + candidate / 2) * 96f) * side * (candidate % 2 == 0 ? 1f : -1f);
				Vector2 point = target.Center + new Vector2(offset, -16f);
				if (!WorldGen.InWorld((int)point.X / 16, (int)point.Y / 16, 10)) {
					continue;
				}

				if (Collision.SolidCollision(point - new Vector2(8f), 16, 16)) {
					continue;
				}

				if (!NpcWeaponReach.Clear(npc.Center, point)) {
					continue;
				}

				aim = point;
				return true;
			}

			return false;
		}

		private static float MinionCost(Item staff) {
			float cost = 1f;
			if (staff.type > ItemID.None && staff.type < ItemID.Sets.StaffMinionSlotsRequired.Length) {
				cost = ItemID.Sets.StaffMinionSlotsRequired[staff.type];
			}

			return System.Math.Max(0.01f, cost);
		}
	}
}
