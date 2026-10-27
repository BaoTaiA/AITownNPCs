using Terraria.Localization;

namespace AITownNPCs.Common.Loc
{
	/// <summary>Short helper for Mods.AITownNPCs.* localization keys.</summary>
	public static class ModText
	{
		public const string Prefix = "Mods.AITownNPCs.";

		public static string Get(string key) =>
			Language.GetTextValue(Prefix + key);

		public static string Get(string key, params object[] args) =>
			Language.GetTextValue(Prefix + key, args);
	}
}
