using System.IO;
using AITownNPCs.Common.Net;
using AITownNPCs.Common.Superpower.ArmsDealer;
using AITownNPCs.Common.Superpower.Guide;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower
{
	public class SuperpowerNetSystem : ModSystem
	{
		public static void SendNurseTeleportFx(Vector2 pos) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)SuperpowerMessageType.NurseTeleportFx);
			packet.Write(pos.X);
			packet.Write(pos.Y);
			packet.Send();
		}

		public static void SendGuidanceStart(int playerWhoAmI, Vector2 targetTile, int durationTicks) {
			if (Main.netMode != NetmodeID.Server || playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)SuperpowerMessageType.GuidanceStart);
			packet.Write((byte)playerWhoAmI);
			packet.Write(targetTile.X);
			packet.Write(targetTile.Y);
			packet.Write(durationTicks);
			packet.Send(toClient: playerWhoAmI);
		}

		/// <summary>Client: arms loan buff ended locally; ask server to reclaim gear.</summary>
		public static void RequestArmsWeaponReclaim() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)SuperpowerMessageType.RequestArmsWeaponReclaim);
			packet.Send();
		}

		public static void HandlePacket(byte type, BinaryReader reader, int whoAmI) {
			switch ((SuperpowerMessageType)type) {
				case SuperpowerMessageType.RequestArmsWeaponReclaim: {
					if (Main.netMode != NetmodeID.Server) {
						break;
					}

					if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
						break;
					}

					Player player = Main.player[whoAmI];
					if (player == null || !player.active) {
						break;
					}

					ArmsDealerTempWeaponPower.ReclaimAll(player, notify: true);
					break;
				}
				case SuperpowerMessageType.ArmsWeaponReclaimNotify: {
					string text = reader.ReadString();
					if (Main.netMode != NetmodeID.Server) {
						Main.NewText(text, Color.Orange);
					}

					break;
				}
				case SuperpowerMessageType.NurseTeleportFx: {
					float x = reader.ReadSingle();
					float y = reader.ReadSingle();
					if (Main.netMode != NetmodeID.Server) {
						var pos = new Vector2(x, y);
						for (int i = 0; i < 20; i++) {
							Dust.NewDust(pos, 16, 16, DustID.Teleporter);
						}
					}

					break;
				}
				case SuperpowerMessageType.GuidanceStart: {
					int playerWho = reader.ReadByte();
					float tx = reader.ReadSingle();
					float ty = reader.ReadSingle();
					int duration = reader.ReadInt32();
					if (Main.netMode == NetmodeID.Server) {
						break;
					}

					if (playerWho < 0 || playerWho >= Main.maxPlayers) {
						break;
					}

					Player player = Main.player[playerWho];
					if (player == null || !player.active) {
						break;
					}

					player.GetModPlayer<GuideGuidancePlayer>().ApplyGuidanceFromServer(
						new Vector2(tx, ty),
						duration);
					break;
				}
			}
		}
	}
}
