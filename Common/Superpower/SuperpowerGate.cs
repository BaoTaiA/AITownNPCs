using System.Collections.Generic;
using Terraria;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerGate
	{
		private const float RefuseChance = 0.40f;

		public static SuperpowerResult TryRefuse(Player player, NPC npc, ISuperpower power) {
			if (SuperpowerCooldownStore.IsOnCooldown(player.whoAmI, npc.type, power.Id, out _)) {
				return new SuperpowerResult {
					Outcome = SuperpowerOutcome.Refused,
					AbilityId = power.Id,
					ReasonCode = "cooldown",
					FixedResultZh = SuperpowerLoc.CooldownNotReady(power.DisplayName)
				};
			}

			if (RefuseChance > 0f && Main.rand.NextFloat() < RefuseChance) {
				return new SuperpowerResult {
					Outcome = SuperpowerOutcome.Refused,
					AbilityId = power.Id,
					StartCooldown = false,
					ReasonCode = "random",
					FixedResultZh = SuperpowerLoc.RandomRefuse(),
					Facts = new List<string> { SuperpowerLoc.FormatRefuseStyleFact(power.RefuseStyle) }
				};
			}

			return null;
		}
	}
}
