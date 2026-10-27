using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace AITownNPCs.Common.Ai.Chat
{
	public static class NpcPersonaCatalog
	{
		public static string GetPersona(NPC npc) {
			if (npc != null) {
				string key = NpcTypeKey(npc.type);
				if (key != null) {
					string localized = Language.GetTextValue("Mods.AITownNPCs.Persona." + key);
					if (!string.IsNullOrWhiteSpace(localized)
					    && !localized.StartsWith("Mods.", System.StringComparison.Ordinal)) {
						return localized;
					}
				}
			}

			return Language.GetTextValue("Mods.AITownNPCs.Persona.Default");
		}

		/// <param name="hasFixedResult">
		/// When true (superpower reply path), omit the no-invent rule — BuildFactsBlock covers result constraints.
		/// </param>
		public static string BuildIdentitySystemPrompt(NPC npc, bool hasFixedResult = false) {
			string typeName = Lang.GetNPCNameValue(npc.type);
			string given = string.IsNullOrWhiteSpace(npc.GivenName) ? typeName : npc.GivenName;
			string persona = GetPersona(npc);
			string lengthRule = Language.GetTextValue("Mods.AITownNPCs.Persona.Prompt.LengthRule");
			string prompt = Language.GetTextValue(
				"Mods.AITownNPCs.Persona.Prompt.Identity",
				typeName,
				given,
				persona,
				lengthRule);
			if (!hasFixedResult) {
				prompt += Language.GetTextValue("Mods.AITownNPCs.Persona.Prompt.NoInvent");
			}

			return prompt;
		}

		private static string NpcTypeKey(int npcType) => npcType switch {
			NPCID.Guide => "Guide",
			NPCID.Merchant => "Merchant",
			NPCID.Nurse => "Nurse",
			NPCID.Demolitionist => "Demolitionist",
			NPCID.Dryad => "Dryad",
			NPCID.ArmsDealer => "ArmsDealer",
			NPCID.Clothier => "Clothier",
			NPCID.Mechanic => "Mechanic",
			NPCID.GoblinTinkerer => "GoblinTinkerer",
			NPCID.Wizard => "Wizard",
			NPCID.WitchDoctor => "WitchDoctor",
			NPCID.Truffle => "Truffle",
			NPCID.Steampunker => "Steampunker",
			NPCID.DyeTrader => "DyeTrader",
			NPCID.PartyGirl => "PartyGirl",
			NPCID.Cyborg => "Cyborg",
			NPCID.Painter => "Painter",
			NPCID.Pirate => "Pirate",
			NPCID.Stylist => "Stylist",
			NPCID.TravellingMerchant => "TravellingMerchant",
			NPCID.Angler => "Angler",
			NPCID.TaxCollector => "TaxCollector",
			NPCID.SkeletonMerchant => "SkeletonMerchant",
			NPCID.DD2Bartender => "DD2Bartender",
			NPCID.Golfer => "Golfer",
			NPCID.BestiaryGirl => "BestiaryGirl",
			NPCID.Princess => "Princess",
			_ => null
		};
	}
}
