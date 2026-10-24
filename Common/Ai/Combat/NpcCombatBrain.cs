using AITownNPCs.Common.Ai.Combat.Boss;
using AITownNPCs.Common.Ai.Combat.Normal;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Host;
using Terraria;

namespace AITownNPCs.Common.Ai.Combat
{
	public static class NpcCombatBrain
	{
		public const int BossClearFrames = 300;
		public const int TargetAcquireInterval = 8;

		public static bool ShouldRunFollow(NPC npc, NpcCombatState state) {
			if (npc == null || state == null) {
				return true;
			}

			return state.Mode != NpcCombatMode.Boss;
		}

		public static void EvaluateMode(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return;
			}

			NpcCombatState state = npc.GetGlobalNPC<NpcCombatState>();
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();

			bool canFight = follow.IsFollowing
				&& profile.CombatEnabled
				&& !equip.Weapon.IsAir
				&& NpcEquipHelper.IsAllowedWeapon(equip.Weapon)
				&& NpcCombatFire.CanFireWeapon(equip);

			if (!canFight) {
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
					if (player.Distance(locked.Center) <= NpcBossCombat.PlayerKeepRadius) {
						state.BossClearTicks = 0;
						state.HasEngagedTarget = true;
						return;
					}

					state.EnterFollowOnly();
					return;
				}

				NPC fresh = NpcBossCombat.FindNearestBoss(player, npc, NpcNormalCombat.LeashDist);
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

			NPC nearestBoss = NpcBossCombat.FindNearestBoss(player, npc, NpcNormalCombat.LeashDist);
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
				if (boss != null && player.Distance(boss.Center) <= NpcBossCombat.PlayerKeepRadius) {
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

				NPC sticky = NpcNormalCombat.GetCachedIfValid(npc, state);
				if (sticky != null && player.Distance(sticky.Center) <= NpcNormalCombat.StickyRadius) {
					state.HasEngagedTarget = true;
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

			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) {
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

			if (state.Mode != NpcCombatMode.FollowOnly) {
				NpcInteractGate.DismissOpenEquip(npc.whoAmI);
			}

			switch (state.Mode) {
				case NpcCombatMode.Normal: {
					if (state.HasEngagedTarget) {
						NpcNormalCombat.TickFire(npc, equip, state);
					}
					else {
						state.UseCombatFlight = false;
						NpcCombatFire.Tick(npc, equip, target: null, allowFire: false);
					}

					break;
				}
				case NpcCombatMode.Boss: {
					NpcBossMove.Tick(npc, state);
					bool alive = NpcBossCombat.GetLockedBoss(state) != null;
					if (alive) {
						NpcBossCombat.TickFire(npc, equip, state);
					}
					else {
						NpcCombatFire.Tick(npc, equip, target: null, allowFire: false);
					}

					break;
				}
				default:
					NpcCombatFire.Stop(npc);
					state.UseCombatFlight = false;
					state.HasEngagedTarget = false;
					break;
			}
		}

		public static void SuppressVanillaTownAttack(NPC npc, ref int cooldown, ref int randExtraCooldown) {
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			if (!profile.CombatEnabled) {
				return;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			if (!equip.Weapon.IsAir && NpcEquipHelper.IsAllowedWeapon(equip.Weapon)) {
				cooldown = 3600;
				randExtraCooldown = 0;
			}
		}
	}
}
