using System;
using Terraria.Localization;

namespace AITownNPCs.Common.Superpower
{
	/// <summary>Localized superpower display strings (game language).</summary>
	public static class SuperpowerLoc
	{
		public const string RefuseStyleMarker = "[[RS]]";
		public const string LineTemplateMarker = "[[LT]]";

		public static string DisplayName(string id) =>
			Language.GetTextValue("Mods.AITownNPCs.Superpower." + id + ".DisplayName");

		public static string Description(string id) =>
			Language.GetTextValue("Mods.AITownNPCs.Superpower." + id + ".Description");

		public static string RefuseStyle(string id) =>
			Language.GetTextValue("Mods.AITownNPCs.Superpower." + id + ".RefuseStyle");

		public static string FormatRefuseStyleFact(string style) =>
			RefuseStyleMarker + Language.GetTextValue("Mods.AITownNPCs.Superpower.RefuseStyleFact", style ?? "");

		public static bool IsRefuseStyleFact(string fact) {
			if (string.IsNullOrWhiteSpace(fact)) {
				return false;
			}

			return fact.StartsWith(RefuseStyleMarker, StringComparison.Ordinal);
		}

		public static bool IsLineTemplateFact(string fact) {
			if (string.IsNullOrWhiteSpace(fact)) {
				return false;
			}

			return fact.StartsWith(LineTemplateMarker, StringComparison.Ordinal);
		}

		public static string StripFactMarker(string fact) {
			if (string.IsNullOrEmpty(fact)) {
				return fact;
			}

			if (fact.StartsWith(RefuseStyleMarker, StringComparison.Ordinal)) {
				return fact.Substring(RefuseStyleMarker.Length);
			}

			if (fact.StartsWith(LineTemplateMarker, StringComparison.Ordinal)) {
				return fact.Substring(LineTemplateMarker.Length);
			}

			return fact;
		}

		public static string CooldownNotReady(string displayName) =>
			Language.GetTextValue("Mods.AITownNPCs.Superpower.CooldownNotReady", displayName);

		public static string RandomRefuse() =>
			Language.GetTextValue("Mods.AITownNPCs.Superpower.RandomRefuse");
	}
}
