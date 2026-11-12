using System.IO;
using AITownNPCs.Common.ChatLog;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs
{
	public class AITownNPCs : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI) {
			ModPacketRouter.Handle(reader, whoAmI);
		}

		/// <summary>
		/// Soft API for other mods (e.g. AIPalico).
		/// AppendFollowLine(playerWhoAmI, text [, r, g, b]) → bool
		/// </summary>
		public override object Call(params object[] args) {
			if (args == null || args.Length == 0 || args[0] is not string command) {
				return null;
			}

			if (command == "AppendFollowLine") {
				if (args.Length < 3 || args[1] is not int playerWhoAmI || args[2] is not string text) {
					return false;
				}

				Color color = Color.SandyBrown;
				if (args.Length >= 6
					&& args[3] is byte r
					&& args[4] is byte g
					&& args[5] is byte b) {
					color = new Color(r, g, b);
				}

				if (Main.netMode == NetmodeID.MultiplayerClient) {
					ChatLogWorldSystem.AddFollowLocalOptimistic(text, color);
					return true;
				}

				ChatLogWorldSystem.AppendFollowForPlayer(playerWhoAmI, text, color);
				return true;
			}

			return null;
		}
	}
}
