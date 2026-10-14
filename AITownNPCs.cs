using System.IO;
using AITownNPCs.Common.Net;
using Terraria.ModLoader;

namespace AITownNPCs
{
	public class AITownNPCs : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI) {
			ModPacketRouter.Handle(reader, whoAmI);
		}
	}
}
