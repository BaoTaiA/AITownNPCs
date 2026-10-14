using System.Collections.Generic;
using Terraria;

namespace AITownNPCs.Common.Superpower
{
	public static class SuperpowerGate
	{
		private const float RefuseChance = 0.40f;

		public static SuperpowerResult TryRefuse(Player player, NPC npc, ISuperpower power) {
			if (SuperpowerCooldownStore.IsOnCooldown(player.whoAmI, npc.type, power.Id, out _)) {
				string name = power.DisplayNameZh;
				return new SuperpowerResult {
					Outcome = SuperpowerOutcome.Refused,
					AbilityId = power.Id,
					ReasonCode = "cooldown",
					FixedResultZh = "「" + name + "」超能力没准备好"
				};
			}

			if (RefuseChance > 0f && Main.rand.NextFloat() < RefuseChance) {
				return new SuperpowerResult {
					Outcome = SuperpowerOutcome.Refused,
					AbilityId = power.Id,
					StartCooldown = false,
					ReasonCode = "random",
					FixedResultZh = "概率触发拒绝",
					Facts = new List<string> { "拒绝风格：" + power.RefuseStyleZh }
				};
			}

			return null;
		}
	}
}
