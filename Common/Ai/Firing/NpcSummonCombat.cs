using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Combat.Boss;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Host;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Palico-style hot-swap: one dummy, summon staff only while another minion or sentry is due.
	/// </summary>
	public static class NpcSummonCombat
	{
		public struct Hand
		{
			public Item Item;
			public bool WantUse;
			public bool Summon;
			public Vector2 Aim;
		}

		/// <summary>
		/// Any following client (or single player) may spawn this NPC's summons locally.
		/// Dedicated server never does. Other clients are visual-only via SpectatorClient.
		/// </summary>
		public static bool IsAuthority(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc) || Main.netMode == NetmodeID.Server) {
				return false;
			}

			return npc.GetGlobalNPC<FollowingTownNPC>().IsFollowing;
		}

		public static bool BlocksMainHand(NPC npc, EquippedTownNPC equip, NPC target) {
			if (Main.netMode == NetmodeID.Server || npc == null || equip == null) {
				return false;
			}

			NpcDummyPool.TryGetDummy(npc.whoAmI, out Player dummy);
			Hand hand = Resolve(npc, equip, dummy, target, mainWant: true, aimOverride: null);
			return hand.Summon;
		}

		/// <summary>Out of combat, while still following: summon minions or keep the owner shell.</summary>
		public static void MaintainWhileIdle(NPC npc, EquippedTownNPC equip) {
			if (!IsAuthority(npc) || equip == null) {
				return;
			}

			// Healer/support never shares this swing with a summon staff.
			// CeasePreserve here would also cancel a shield or channel that is already out.
			if (NpcHealerSupport.IsTool(equip.Weapon)) {
				return;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			NpcDummyPool.TryGetDummy(npc.whoAmI, out Player dummy);
			if (dummy != null) {
				NpcSummonCap.CullExcessForNpc(npc, dummy);
			}

			Hand hand = Resolve(npc, equip, dummy, target: null, mainWant: false, aimOverride: null);
			if (hand.Summon) {
				NpcDummyActor.Tick(npc, equip, combat, target: null, wantUse: false);
				return;
			}

			NpcDummyActor.CeasePreserve(npc.whoAmI);
		}

		public static Hand Resolve(NPC npc, EquippedTownNPC equip, Player dummy, NPC target, bool mainWant, Vector2? aimOverride) {
			Reconcile(npc, equip);
			if (dummy != null) {
				NpcSummonCap.CullExcessForNpc(npc, dummy);
			}

			NPC foe = LiveTarget(npc, target);
			Vector2 mainAim = aimOverride ?? (foe != null ? foe.Center : npc.Center + new Vector2(npc.direction * 320f, 0f));
			Item weapon = equip.Weapon;
			bool busy = dummy != null && (dummy.itemAnimation > 0 || dummy.itemTime > 0);
			Item held = dummy != null ? dummy.inventory[dummy.selectedItem] : null;
			bool heldSummon = NpcEquipHelper.IsSummonOrSentryStaff(held);

			// Healer/support (bubble shield, ion staff, and the same class) stays on this weapon.
			// A summon staff in the same swing turns the body and drops channel.
			if (NpcHealerSupport.IsTool(weapon)) {
				return new Hand { Item = weapon, WantUse = mainWant, Summon = false, Aim = mainAim };
			}

			if (busy && heldSummon) {
				return new Hand { Item = held, WantUse = false, Summon = true, Aim = mainAim };
			}

			if (busy) {
				return new Hand { Item = weapon, WantUse = mainWant, Summon = false, Aim = mainAim };
			}

			if (TryStartSummon(npc, equip, dummy, foe, out Item staff, out Vector2 summonAim)) {
				return new Hand { Item = staff, WantUse = true, Summon = true, Aim = summonAim };
			}

			return new Hand { Item = weapon, WantUse = mainWant, Summon = false, Aim = mainAim };
		}

		public static void Reconcile(NPC npc, EquippedTownNPC equip) {
			if (npc == null || equip == null || Main.netMode == NetmodeID.Server) {
				return;
			}

			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
			Item staff = equip.SummonStaff;
			int type = staff != null && !staff.IsAir && NpcEquipHelper.IsSummonOrSentryStaff(staff) ? staff.type : -1;
			int prefix = type < 0 ? 0 : staff.prefix;
			if (firing.TrackedSummonType != -1
				&& (firing.TrackedSummonType != type || firing.TrackedSummonPrefix != prefix)) {
				NpcDummyActor.ClearSummons(npc.whoAmI);
				firing.SummonRetryUntil = 0;
			}

			firing.TrackedSummonType = type;
			firing.TrackedSummonPrefix = prefix;
		}

		private static bool TryStartSummon(NPC npc, EquippedTownNPC equip, Player dummy, NPC foe, out Item staff, out Vector2 aim) {
			staff = equip.SummonStaff;
			aim = npc.Center;
			if (!IsAuthority(npc) || !NpcEquipHelper.IsSummonOrSentryStaff(staff)) {
				return false;
			}

			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
			if (Main.GameUpdateCount < firing.SummonRetryUntil) {
				return false;
			}

			if (NpcEquipHelper.IsMinionStaff(staff)) {
				if (!NpcSummonCap.NeedsMinion(npc, dummy, staff)) {
					return false;
				}

				aim = npc.Center + new Vector2(npc.direction * 48f, -24f);
				return true;
			}

			return NpcSummonCap.TryPlanSentry(npc, dummy, staff, foe, out aim);
		}

		private static NPC LiveTarget(NPC npc, NPC target) {
			if (target != null && target.active) {
				return target;
			}

			NpcCombatState state = npc.GetGlobalNPC<NpcCombatState>();
			if (state.Mode == NpcCombatMode.Boss) {
				NPC boss = NpcBossCombat.GetLockedBoss(state);
				if (boss != null && boss.active) {
					return boss;
				}
			}

			int who = state.CachedTargetWhoAmI;
			if (who < 0 || who >= Main.maxNPCs) {
				return null;
			}

			NPC cached = Main.npc[who];
			return cached != null && cached.active ? cached : null;
		}
	}
}
