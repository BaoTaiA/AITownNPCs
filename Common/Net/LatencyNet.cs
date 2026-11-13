using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Net
{
	/// <summary>Client↔server echo RTT for the latency HUD.</summary>
	public static class LatencyNet
	{
		/// <summary>Smoothed round-trip milliseconds; -1 until first sample.</summary>
		public static int DisplayMs { get; private set; } = -1;

		private static long _pendingSentTick = -1;

		public static void Reset() {
			DisplayMs = -1;
			_pendingSentTick = -1;
		}

		public static void SendPing() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			long sent = System.Environment.TickCount64;
			_pendingSentTick = sent;

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)LatencyMessageType.Ping);
			packet.Write(sent);
			packet.Send();
		}

		public static void HandlePacket(byte type, BinaryReader reader, int whoAmI) {
			switch ((LatencyMessageType)type) {
				case LatencyMessageType.Ping:
					if (Main.netMode != NetmodeID.Server) {
						return;
					}

					long echo = reader.ReadInt64();
					ModPacket pong = ModContent.GetInstance<AITownNPCs>().GetPacket();
					pong.Write((byte)LatencyMessageType.Pong);
					pong.Write(echo);
					pong.Send(toClient: whoAmI);
					break;

				case LatencyMessageType.Pong:
					if (Main.netMode != NetmodeID.MultiplayerClient) {
						return;
					}

					long sentTick = reader.ReadInt64();
					if (_pendingSentTick < 0 || sentTick != _pendingSentTick) {
						return;
					}

					_pendingSentTick = -1;
					long rtt = System.Environment.TickCount64 - sentTick;
					if (rtt < 0) {
						rtt = 0;
					}
					else if (rtt > 9999) {
						rtt = 9999;
					}

					int sample = (int)rtt;
					if (DisplayMs < 0) {
						DisplayMs = sample;
					}
					else {
						DisplayMs = (int)(DisplayMs * 0.7f + sample * 0.3f);
					}

					break;
			}
		}
	}
}
