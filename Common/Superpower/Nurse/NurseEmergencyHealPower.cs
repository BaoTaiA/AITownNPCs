using Terraria;
using Terraria.ID;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Nurse
{
	public sealed class NurseEmergencyHealPower : ISuperpower
	{
		public string Id => SuperpowerIds.NurseEmergencyHeal;
		public int NpcType => NPCID.Nurse;
		public int CooldownFrames => 60 * 60 * 5;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (player.dead) {
				return SuperpowerResult.Failed(Id, "dead", ModText.Get("Runtime.Superpower.Fixed.player_dead"));
			}

			if (!TryGetTrustedLife(player, out int life, out int lifeMax)) {
				return SuperpowerResult.Failed(Id, "no_client_state", ModText.Get("Runtime.Superpower.Fixed.no_client_state"));
			}

			if (lifeMax > 0 && life >= lifeMax) {
				return SuperpowerResult.Failed(Id, "full_hp", ModText.Get("Runtime.Superpower.Fixed.full_hp"),
					SuperpowerLoc.FormatRefuseStyleFact(RefuseStyle));
			}

			if (NurseEmergencyHealState.IsBusy(npc.whoAmI)) {
				return SuperpowerResult.Failed(Id, "busy", ModText.Get("Runtime.Superpower.Fixed.nurse_busy"));
			}

			if (!NurseEmergencyHealState.Start(npc, player)) {
				return SuperpowerResult.Failed(Id, "start_failed", ModText.Get("Runtime.Superpower.Fixed.heal_start_fail"));
			}

			return SuperpowerResult.Ok(Id, true, ModText.Get("Runtime.Superpower.Fixed.heal_ok"));
		}

		/// <summary>
		/// Single-player and the listen host use the local life total.
		/// Other clients report life with the chat packet; the server copy is not trustworthy without SSC.
		/// </summary>
		private static bool TryGetTrustedLife(Player player, out int life, out int lifeMax) {
			bool localCopy = Main.netMode == NetmodeID.SinglePlayer
				|| (Main.netMode == NetmodeID.Server && !Main.dedServ && player.whoAmI == Main.myPlayer);
			if (localCopy) {
				life = player.statLife;
				lifeMax = player.statLifeMax2;
				return true;
			}

			return PlayerSuperpowerClientSnapshot.TryGetLife(player.whoAmI, out life, out lifeMax);
		}
	}
}
