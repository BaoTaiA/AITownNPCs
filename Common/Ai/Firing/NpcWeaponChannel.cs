using AITownNPCs.Common.Equip;
using Terraria;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Applies weapon press rhythm to wantUse before DummyActor.Tick.
	/// Rules live in <see cref="NpcWeaponChannelRules"/> (whitelist hold-release clusters + blacklist).
	/// Default is continuous hold; add new guns to the matching cluster or blacklist when needed.
	/// Not full vanilla parity — curated for town-NPC dummy firing.
	/// </summary>
	public static class NpcWeaponChannel
	{
		/// <summary>
		/// Refine combat's wantUse: hold-release cycle or continuous channel stuck-release.
		/// </summary>
		public static bool Apply(NPC npc, EquippedTownNPC equip, bool wantUse) {
			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
			if (NpcWeaponChannelRules.NeedsHoldReleaseCycle(equip.Weapon)) {
				return ApplyHoldReleaseCycle(npc, firing, equip, wantUse);
			}

			firing.ResetChannelCycle();
			return ReleaseIfChannelStuck(npc, equip, wantUse);
		}

		/// <summary>Hold N frames, release to fire, then recharge as soon as channel/heldProj ends.</summary>
		private static bool ApplyHoldReleaseCycle(NPC npc, NpcFiringState firing, EquippedTownNPC equip, bool wantUse) {
			if (!wantUse) {
				firing.ResetChannelCycle();
				return false;
			}

			Item weapon = equip.Weapon;
			if (NpcDummyPool.TryGetDummy(npc.whoAmI, out Player dummy) && dummy != null) {
				Item held = dummy.inventory[dummy.selectedItem];
				if (held != null && !held.IsAir) {
					weapon = held;
				}
			}

			int holdFrames = NpcWeaponChannelRules.GetHoldFrames(weapon);

			if (!firing.ChannelThrowing) {
				firing.ChannelHoldTicks++;
				if (firing.ChannelHoldTicks >= holdFrames) {
					firing.ChannelThrowing = true;
					return false;
				}

				return true;
			}

			bool stillOut = false;
			if (NpcDummyPool.TryGetDummy(npc.whoAmI, out Player live) && live != null) {
				stillOut = live.channel || live.heldProj >= 0;
			}

			if (stillOut) {
				return false;
			}

			firing.ResetChannelCycle();
			firing.ChannelHoldTicks = 1;
			return true;
		}

		/// <summary>
		/// Continuous channel: hold like a player. Tap-release only when channel ended but key still down.
		/// </summary>
		private static bool ReleaseIfChannelStuck(NPC npc, EquippedTownNPC equip, bool wantUse) {
			if (!wantUse || equip.Weapon.IsAir || !equip.Weapon.channel) {
				return wantUse;
			}

			if (!NpcDummyPool.TryGetDummy(npc.whoAmI, out Player dummy) || dummy == null) {
				return wantUse;
			}

			if (!dummy.channel || dummy.heldProj >= 0 || NpcDummyPool.OwnsProjectiles(dummy.whoAmI)) {
				return wantUse;
			}

			if (!NpcDummyLocal.WasWantUse(dummy.whoAmI)) {
				return wantUse;
			}

			return false;
		}
	}
}
