using AITownNPCs.Common.Ai.Combat.Boss;
using AITownNPCs.Common.Ai.Combat.Normal;
using AITownNPCs.Common.Ai.Firing;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Host;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat
{
	public static class NpcCombatBrain
	{
		public const int BossClearFrames = 120;
		public const int TargetAcquireInterval = 8;

		public static void EvaluateMode(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return;
			}

			NpcCombatState state = npc.GetGlobalNPC<NpcCombatState>();
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();

			bool healerTool = NpcHealerSupport.IsTool(equip.Weapon);
			bool mainReady = !equip.Weapon.IsAir
				&& NpcEquipHelper.IsAllowedWeapon(equip.Weapon)
				&& (healerTool || NpcCombatFire.CanFireWeapon(equip));
			bool summonReady = NpcEquipHelper.IsSummonOrSentryStaff(equip.SummonStaff);
			bool canFight = follow.IsFollowing
				&& profile.CombatEnabled
				&& (mainReady || summonReady);

			if (!canFight) {
				state.EnterFollowOnly();
				return;
			}

			// Healer main-hand: stay on follow locomotion; support fire runs in Tick(FollowOnly).
			if (healerTool) {
				state.EnterFollowOnly();
				return;
			}

			Player player = Main.player[follow.FollowPlayerWhoAmI];
			if (player == null || !player.active || player.dead) {
				state.EnterFollowOnly();
				return;
			}

			if (state.TargetRefreshTicks > 0) {
				state.TargetRefreshTicks--;
				if (TryMaintainCachedTargets(npc, player, state)) {
					return;
				}
			}

			state.TargetRefreshTicks = TargetAcquireInterval;

			if (state.Mode == NpcCombatMode.Boss) {
				NPC locked = NpcBossCombat.GetLockedBoss(state);
				if (locked != null) {
					if (NpcBossCombat.PlayerWithinKeepRadius(player, locked)) {
						state.BossClearTicks = 0;
						state.HasEngagedTarget = true;
						return;
					}

					state.EnterFollowOnly();
					return;
				}

				// Keep radius (not trash leash): worm splits / phase swaps often leave the new root >40 tiles out.
				NPC fresh = NpcBossCombat.FindNearestBoss(player, npc, NpcBossCombat.PlayerKeepRadius);
				if (fresh != null) {
					state.Mode = NpcCombatMode.Boss;
					state.LockedBossWhoAmI = fresh.whoAmI;
					state.BossClearTicks = 0;
					state.HasEngagedTarget = true;
					state.ClearNormalTarget();
					return;
				}

				state.HasEngagedTarget = false;
				state.BossClearTicks++;
				if (state.BossClearTicks >= BossClearFrames) {
					state.EnterFollowOnly();
				}

				return;
			}

			NPC nearestBoss = NpcBossCombat.FindNearestBoss(player, npc, NpcBossCombat.PlayerKeepRadius);
			if (nearestBoss != null) {
				state.Mode = NpcCombatMode.Boss;
				state.LockedBossWhoAmI = nearestBoss.whoAmI;
				state.BossClearTicks = 0;
				state.HasEngagedTarget = true;
				state.ClearNormalTarget();
				return;
			}

			if (state.Mode == NpcCombatMode.Normal && npc.Distance(player.Center) > NpcNormalCombat.LeashDist) {
				state.EnterFollowOnly();
				return;
			}

			NPC target = NpcNormalCombat.ResolveTarget(npc, player, state);
			if (target != null) {
				state.Mode = NpcCombatMode.Normal;
				state.CachedTargetWhoAmI = target.whoAmI;
				state.HasEngagedTarget = true;
				return;
			}

			state.EnterFollowOnly();
		}

		/// <summary>Between full scans, keep valid boss lock / sticky normal target without world NPC sweeps.</summary>
		private static bool TryMaintainCachedTargets(NPC npc, Player player, NpcCombatState state) {
			if (state.Mode == NpcCombatMode.Boss) {
				NPC boss = NpcBossCombat.GetLockedBoss(state);
				if (boss != null && NpcBossCombat.PlayerWithinKeepRadius(player, boss)) {
					state.BossClearTicks = 0;
					state.HasEngagedTarget = true;
					return true;
				}

				return false;
			}

			if (state.Mode == NpcCombatMode.Normal) {
				if (npc.Distance(player.Center) > NpcNormalCombat.LeashDist) {
					return false;
				}

				NpcCombatTargeting targeting = state.Targeting;
				if (targeting.Current == null) {
					return false;
				}

				// Retain between full Select scans: clear track or SightGrace Obscured hold.
				if (targeting.Holding || targeting.InTrackingReach(player, npc.Center)) {
					state.HasEngagedTarget = true;
					state.CachedTargetWhoAmI = targeting.Current.whoAmI;
					return true;
				}

				return false;
			}

			// FollowOnly: skip acquire until refresh ticks hit 0.
			return true;
		}

		public static void Tick(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return;
			}

			NpcCombatState state = npc.GetGlobalNPC<NpcCombatState>();
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();

			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			if (!profile.CombatEnabled) {
				NpcCombatFire.Stop(npc);
				state.EnterFollowOnly();
				return;
			}

			// Combat weapons: clients follow server intents.
			// Thorium healer/support: follow-owner client decides + fires locally
			// (sees real life/buffs); CeasePreserve must not wipe that use each tick.
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				FollowingTownNPC followClient = npc.GetGlobalNPC<FollowingTownNPC>();
				if (followClient.IsFollowing
					&& followClient.FollowPlayerWhoAmI == Main.myPlayer
					&& NpcHealerSupport.IsTool(equip.Weapon)) {
					NpcHealerSupport.Tick(npc, equip);
					state.HasEngagedTarget = false;
					return;
				}

				if (state.Mode == NpcCombatMode.FollowOnly) {
					bool healerMain = followClient.IsFollowing && NpcHealerSupport.IsTool(equip.Weapon);
					if (healerMain && followClient.FollowPlayerWhoAmI != Main.myPlayer) {
						NpcSummonCombat.MaintainWhileIdle(npc, equip);
					}
					else if (!healerMain) {
						StopFollowOnlyIfNeeded(npc, state);
					}
				}

				return;
			}

			if (state.Mode != NpcCombatMode.FollowOnly) {
				NpcInteractGate.DismissOpenEquip(npc.whoAmI);
			}

			switch (state.Mode) {
				case NpcCombatMode.Normal: {
					if (state.HasEngagedTarget) {
						NpcNormalCombat.TickFire(npc, equip, state);
					}
					else {
						NpcCombatFire.Tick(npc, equip, target: null, allowFire: false);
					}

					break;
				}
				case NpcCombatMode.Boss: {
					if (state.HasEngagedTarget && NpcBossCombat.GetLockedBoss(state) != null) {
						NpcBossCombat.TickFire(npc, equip, state);
					}
					else {
						NpcCombatFire.Tick(npc, equip, target: null, allowFire: false);
					}

					break;
				}
				default:
					TickFollowOnly(npc, equip, state);
					break;
			}
		}

		private static void TickFollowOnly(NPC npc, EquippedTownNPC equip, NpcCombatState state) {
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (follow.IsFollowing && NpcHealerSupport.IsTool(equip.Weapon)) {
				// Dedicated: follow-owner client owns healer AI (local life/status).
				// Listen host only ticks when they are the follow owner.
				if (Main.netMode == NetmodeID.Server && Main.dedServ) {
					state.HasEngagedTarget = false;
					return;
				}

				if (Main.netMode == NetmodeID.Server && follow.FollowPlayerWhoAmI != Main.myPlayer) {
					state.HasEngagedTarget = false;
					return;
				}

				// Do not call MaintainWhileIdle here: it CeasePreserve/ForceRelease and
				// cancels the healer use started in the same tick.
				NpcHealerSupport.Tick(npc, equip);
				state.HasEngagedTarget = false;
				return;
			}

			StopFollowOnlyIfNeeded(npc, state);
		}

		private static void StopFollowOnlyIfNeeded(NPC npc, NpcCombatState state) {
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			bool following = follow.IsFollowing;
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			if (NpcSummonCombat.IsAuthority(npc)) {
				if (following) {
					NpcSummonCombat.MaintainWhileIdle(npc, equip);
				}
				else if (NpcDummyPool.HasLiveCombatResidue(npc.whoAmI)) {
					NpcCombatFire.Stop(npc, preserveSummons: false);
				}
			}
			else if (NpcDummyPool.HasLiveCombatResidue(npc.whoAmI)) {
				NpcCombatFire.Stop(npc, preserveSummons: following);
			}

			state.HasEngagedTarget = false;
		}

		public static void SuppressVanillaTownAttack(NPC npc, ref int cooldown, ref int randExtraCooldown) {
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			if (!profile.CombatEnabled) {
				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			bool mainReady = !equip.Weapon.IsAir && NpcEquipHelper.IsAllowedWeapon(equip.Weapon);
			if (mainReady || NpcEquipHelper.IsSummonOrSentryStaff(equip.SummonStaff)) {
				cooldown = 3600;
				randExtraCooldown = 0;
			}
		}
	}
}
