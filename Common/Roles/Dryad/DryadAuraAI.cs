using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Roles.Dryad
{
	/// <summary>
	/// Hooks vanilla Dryad's Ward projectile: on each new ward, apply equipped buff potions
	/// for 30 minutes. Matching buffs refresh duration; new buff types are added. Does not clear
	/// previously applied auras. Consumes 1 potion per slot.
	/// While the Dryad is following, vanilla town AI is skipped so she will not open a new ward;
	/// an already-active ward can still pulse. Unfollowed dryads keep vanilla ward behavior.
	/// </summary>
	public static class DryadAuraAI
	{
		public const int BuffDurationTicks = 30 * 60 * 60; // 30 minutes
		public const float AuraRange = 1020f;
		private const float WardAttachDistance = 96f;

		public static void Update(NPC npc, DryadRoleState state) {
			if (npc.type != NPCID.Dryad || !npc.active) {
				return;
			}

			if (!TryFindActiveWard(npc, out int wardWhoAmI, out Vector2 wardCenter)) {
				state.WardActive = false;
				state.LastPulsedWardWhoAmI = -1;
				return;
			}

			state.WardActive = true;

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (state.LastPulsedWardWhoAmI == wardWhoAmI) {
				return;
			}

			state.LastPulsedWardWhoAmI = wardWhoAmI;
			PulseBuffs(npc, wardCenter);
		}

		public static bool IsBusy(DryadRoleState state) => state.WardActive;

		private static bool TryFindActiveWard(NPC dryad, out int wardWhoAmI, out Vector2 wardCenter) {
			wardWhoAmI = -1;
			wardCenter = dryad.Center;
			float bestDist = WardAttachDistance;

			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (!proj.active || proj.type != ProjectileID.DryadsWardCircle) {
					continue;
				}

				float dist = Vector2.Distance(proj.Center, dryad.Center);
				if (dist >= bestDist) {
					continue;
				}

				bestDist = dist;
				wardWhoAmI = i;
				wardCenter = proj.Center;
			}

			return wardWhoAmI >= 0;
		}

		private static void PulseBuffs(NPC npc, Vector2 auraCenter) {
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			int slotCount = System.Math.Clamp(profile.Slots.Length, 0, EquippedTownNPC.MaxSlots);
			bool anyConsumed = false;

			for (int s = 0; s < slotCount; s++) {
				Item potion = equip.Slots[s];
				if (potion == null || potion.IsAir || potion.type <= ItemID.None || potion.buffType <= 0) {
					continue;
				}

				if (IsSharedWithPlayerInventory(potion)) {
					potion = potion.Clone();
					equip.Slots[s] = potion;
				}

				ApplyBuffInRange(auraCenter, potion.buffType);

				Item next = potion.Clone();
				next.stack--;
				if (next.stack <= 0) {
					next.TurnToAir();
				}

				equip.Slots[s] = next;
				NpcEquipNetSystem.BroadcastSlot(npc.whoAmI, (NpcEquipSlotId)s, equip.Slots[s]);
				anyConsumed = true;
			}

			if (anyConsumed) {
				npc.netUpdate = true;
			}
		}

		private static bool IsSharedWithPlayerInventory(Item item) {
			if (item == null) {
				return false;
			}

			for (int p = 0; p < Main.maxPlayers; p++) {
				Player player = Main.player[p];
				if (player == null || !player.active) {
					continue;
				}

				Item[] inv = player.inventory;
				for (int i = 0; i < inv.Length; i++) {
					if (ReferenceEquals(inv[i], item)) {
						return true;
					}
				}
			}

			return false;
		}

		private static void ApplyBuffInRange(Vector2 center, int buffType) {
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (!player.active || player.dead) {
					continue;
				}

				if (Vector2.Distance(center, player.Center) > AuraRange) {
					continue;
				}

				ServerPlayerNet.AddBuffSynced(player, buffType, BuffDurationTicks);
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || !other.townNPC) {
					continue;
				}

				if (Vector2.Distance(center, other.Center) > AuraRange) {
					continue;
				}

				other.GetGlobalNPC<TownNpcAuraBuffs>().Apply(other, buffType, BuffDurationTicks);
				other.netUpdate = true;
			}
		}
	}

	public class DryadRoleState
	{
		public bool WardActive;
		public int LastPulsedWardWhoAmI = -1;
	}
}
