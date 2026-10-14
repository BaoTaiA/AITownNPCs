using System.ComponentModel;

namespace AITownNPCs.Common.Configs
{
	/// <summary>
	/// Legacy nested provider blob used only for deserializing older config JSON.
	/// </summary>
	public class AiProviderSettings
	{
		[DefaultValue(AiProvider.MiniMax)]
		public AiProvider Provider = AiProvider.MiniMax;

		[DefaultValue("")]
		public string MiniMaxApiKey = "";

		[DefaultValue("")]
		public string VolcArkApiKey = "";
	}
}
