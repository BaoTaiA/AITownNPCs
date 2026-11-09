using System.Collections.Generic;
using AITownNPCs.Common.Ai.Combat.Stance;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Roles.Dryad;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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
				if (proj == null || !proj.active || proj.owner != dummyWho || !proj.minion) {
					continue;
				}

				if (NpcDummyModResources.IsVoidCapacityMinion(proj)) {
					continue;
				}

				slots += proj.minionSlots;
			}

			return slots;
		}

		public static int CountSentries(int dummyWho) {
			if (!NpcDummyPool.IsDummy(dummyWho)) {
				return 0;
			}

			int count = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj != null && proj.active && proj.owner == dummyWho && proj.sentry) {
					count++;
				}
			}

			return count;
		}

		/// <summary>
		/// When lifeMax (and thus caps) drop, kill excess minions/sentries until within limits.
		/// </summary>
		public static void CullExcess(Player dummy, int maxMinions, int maxTurrets) {
			if (dummy == null || !NpcDummyPool.IsDummy(dummy.whoAmI)) {
				return;
			}

			int dummyWho = dummy.whoAmI;
			maxMinions = System.Math.Max(1, maxMinions);
			maxTurrets = System.Math.Max(1, maxTurrets);

			var minions = new List<Projectile>();
			var sentries = new List<Projectile>();
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj == null || !proj.active || proj.owner != dummyWho) {
					continue;
				}

				if (proj.minion) {
					// SOTS void spirits reserve void capacity — never cull by vanilla slots.
					if (NpcDummyModResources.IsVoidCapacityMinion(proj)) {
						continue;
					}

					minions.Add(proj);
				}
				else if (proj.sentry) {
					sentries.Add(proj);
				}
			}

			// Newest first (higher index) so older summons are kept when possible.
			minions.Sort((a, b) => b.whoAmI.CompareTo(a.whoAmI));
			sentries.Sort((a, b) => b.whoAmI.CompareTo(a.whoAmI));

			float used = 0f;
			foreach (Projectile proj in minions) {
				used += proj.minionSlots;
			}

			foreach (Projectile proj in minions) {
				if (used <= maxMinions + 0.001f) {
					break;
				}

				used -= proj.minionSlots;
				proj.Kill();
			}

			int sentryCount = sentries.Count;
			foreach (Projectile proj in sentries) {
				if (sentryCount <= maxTurrets) {
					break;
				}

				proj.Kill();
				sentryCount--;
			}

			dummy.slotsMinions = CountMinionSlots(dummyWho);
			dummy.maxMinions = maxMinions;
			dummy.maxTurrets = maxTurrets;
		}

		public static void CullExcessForNpc(NPC npc, Player dummy) {
			if (npc == null || !npc.active || dummy == null) {
				return;
			}

			int maxMinions = NpcLifeStatScale.GetMaxMinions(npc) + TownNpcAuraBuffs.GetMinionSlotBonus(npc);
			CullExcess(dummy, maxMinions, NpcLifeStatScale.GetMaxTurrets(npc));
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

		public static bool NeedsMinion(NPC npc, Player dummy, Item staff) {
			if (!NpcEquipHelper.IsMinionStaff(staff)) {
				return false;
			}

			// SOTS void spirits: gate only on native CanUseItem (void capacity).
			if (NpcDummyModResources.IsVoidSummon(staff)) {
				return dummy == null || CanDummyUse(dummy, staff);
			}

			if (!TryResolveMaxMinions(npc, dummy, out int max) || max < 1) {
				return false;
			}

			int dummyWho = dummy?.whoAmI ?? -1;
			float used = CountMinionSlots(dummyWho);
			if (used + MinionCost(staff) > max) {
				return false;
			}

			return true;
		}

		/// <summary>Soft CanUseItem probe with the staff temporarily in hand (Palico CanEquipSummon).</summary>
		private static bool CanDummyUse(Player dummy, Item staff) {
			if (dummy == null || staff == null || staff.IsAir) {
				// No dummy yet — allow the attempt; native use pays / refuses.
				return true;
			}

			int selected = dummy.selectedItem;
			Item held = dummy.inventory[selected];
			try {
				dummy.inventory[selected] = staff;
				// Palico: Fill/Resources then CanUseItem under actor Scope; fail → 60 tick retry.
				bool allowed = NpcDummyModResources.ProbeCanUseItem(dummy, staff);
				if (!allowed
					&& NpcDummyPool.TryGetNpcWho(dummy.whoAmI, out int npcWho)
					&& npcWho >= 0
					&& npcWho < Main.maxNPCs
					&& Main.npc[npcWho].active) {
					Main.npc[npcWho].GetGlobalNPC<NpcFiringState>().SummonRetryUntil =
						Main.GameUpdateCount + 60;
				}

				return allowed;
			}
			finally {
				dummy.inventory[selected] = held;
			}
		}

		/// <summary>True when another sentry should be placed, and writes a spread aim near the target.</summary>
		public static bool TryPlanSentry(NPC npc, Player dummy, Item staff, NPC target, out Vector2 aim) {
			aim = target != null ? target.Center : npc.Center;
			if (!NpcEquipHelper.IsSentryStaff(staff) || target == null || !target.active) {
				return false;
			}

			if (!TryResolveMaxTurrets(npc, dummy, out int max) || max < 1) {
				return false;
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

		/// <summary>No NPC context → fail (do not summon with a guessed cap).</summary>
		private static bool TryResolveMaxMinions(NPC npc, Player dummy, out int max) {
			max = 0;
			if (npc != null && npc.active) {
				max = System.Math.Max(
					NpcLifeStatScale.MinMinions,
					NpcLifeStatScale.GetMaxMinions(npc) + TownNpcAuraBuffs.GetMinionSlotBonus(npc));
				return true;
			}

			if (dummy != null && NpcDummyPool.TryGetNpcWho(dummy.whoAmI, out int npcWho)
				&& npcWho >= 0 && npcWho < Main.maxNPCs) {
				NPC bound = Main.npc[npcWho];
				if (bound != null && bound.active) {
					max = System.Math.Max(
						NpcLifeStatScale.MinMinions,
						NpcLifeStatScale.GetMaxMinions(bound) + TownNpcAuraBuffs.GetMinionSlotBonus(bound));
					return true;
				}
			}

			return false;
		}

		private static bool TryResolveMaxTurrets(NPC npc, Player dummy, out int max) {
			max = 0;
			if (npc != null && npc.active) {
				max = NpcLifeStatScale.GetMaxTurrets(npc);
				return true;
			}

			if (dummy != null && NpcDummyPool.TryGetNpcWho(dummy.whoAmI, out int npcWho)
				&& npcWho >= 0 && npcWho < Main.maxNPCs) {
				NPC bound = Main.npc[npcWho];
				if (bound != null && bound.active) {
					max = NpcLifeStatScale.GetMaxTurrets(bound);
					return true;
				}
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
