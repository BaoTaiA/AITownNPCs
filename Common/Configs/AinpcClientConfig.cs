using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace AITownNPCs.Common.Configs
{
	public class AinpcClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static AinpcClientConfig Instance => ModContent.GetInstance<AinpcClientConfig>();

		[Header("$Mods.AITownNPCs.Config.Headers.Hud")]
		[DefaultValue(true)]
		public bool ShowLatencyHud = true;
	}
}
