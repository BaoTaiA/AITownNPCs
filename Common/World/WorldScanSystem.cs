using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.World
{
	public sealed class WorldScanSystem : ModSystem
	{
		public override void OnWorldLoad() {
			// Cache is only consumed by server/SP guide powers; skip full-map scan on clients.
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			WorldCache.Clear();
			WorldScanner.ScanAll();
		}

		public override void OnWorldUnload() {
			WorldCache.Clear();
		}
	}
}
