using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.World
{
	public sealed class WorldScanSystem : ModSystem
	{
		private const int ColumnsPerTick = 32;
		private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);

		private WorldScanner.ScanSession _session;
		private DateTime _lastCompletedUtc;

		public override void OnWorldLoad() {
			// Cache is only consumed by server/SP guide powers; skip full-map scan on clients.
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			_session = null;
			WorldScanner.ScanSession session = WorldScanner.Begin();
			if (session == null) {
				WorldCache.Clear();
				_lastCompletedUtc = default;
				return;
			}

			session.Step(Main.maxTilesX);
			WorldCache.Apply(session.Buffer);
			_lastCompletedUtc = DateTime.UtcNow;
		}

		public override void OnWorldUnload() {
			_session = null;
			_lastCompletedUtc = default;
			WorldCache.Clear();
		}

		public override void PostUpdateWorld() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			if (_session != null) {
				if (_session.Step(ColumnsPerTick)) {
					WorldCache.Apply(_session.Buffer);
					_lastCompletedUtc = DateTime.UtcNow;
					_session = null;
				}

				return;
			}

			if (!WorldCache.Ready || _lastCompletedUtc == default) {
				return;
			}

			if (DateTime.UtcNow - _lastCompletedUtc < RefreshInterval) {
				return;
			}

			_session = WorldScanner.Begin();
		}
	}
}
