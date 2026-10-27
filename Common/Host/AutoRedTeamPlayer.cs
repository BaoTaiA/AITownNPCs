using AITownNPCs.Common.Configs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Host
{
	/// <summary>Optional: join the red team a few seconds after entering a world (config, default off).</summary>
	public sealed class AutoRedTeamPlayer : ModPlayer
	{
		private const int DelayTicks = 60 * 5;
		private const int RedTeam = 1;

		private int _joinCountdown = -1;

		public override void OnEnterWorld() {
			AinpcServerConfig config = AinpcServerConfig.Instance;
			if (config == null || !config.AutoJoinRedTeam) {
				_joinCountdown = -1;
				return;
			}

			_joinCountdown = DelayTicks;
		}

		public override void PostUpdate() {
			if (_joinCountdown < 0) {
				return;
			}

			// Dedicated has no local player. Listen-server host is Server + myPlayer — still join.
			if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
				_joinCountdown = -1;
				return;
			}

			AinpcServerConfig config = AinpcServerConfig.Instance;
			if (config == null || !config.AutoJoinRedTeam) {
				_joinCountdown = -1;
				return;
			}

			if (_joinCountdown > 0) {
				_joinCountdown--;
				return;
			}

			_joinCountdown = -1;
			if (Player.team == RedTeam) {
				return;
			}

			Player.team = RedTeam;
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, Player.whoAmI);
			}
		}
	}
}
