using System;
using System.Collections.Generic;
using AITownNPCs.Common.Host;
using AITownNPCs.Common.Loc;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Chat
{
	public static class NpcAddressResolver
	{
		private static readonly (string key, int npcType)[] AddressKeys = {
			("GoblinTinkerer", NPCID.GoblinTinkerer),
			("ArmsDealer", NPCID.ArmsDealer),
			("Demolitionist", NPCID.Demolitionist),
			("Cyborg", NPCID.Cyborg),
			("TaxCollector", NPCID.TaxCollector),
			("Guide", NPCID.Guide),
			("Nurse", NPCID.Nurse),
			("Merchant", NPCID.Merchant),
			("Dryad", NPCID.Dryad)
		};

		private static Dictionary<string, int> _aliases;

		private static Dictionary<string, int> Aliases {
			get {
				if (_aliases != null) {
					return _aliases;
				}

				_aliases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				foreach ((string key, int npcType) in AddressKeys) {
					foreach (string alias in AliasLists.Get("Address." + key)) {
						if (!_aliases.ContainsKey(alias)) {
							_aliases[alias] = npcType;
						}
					}
				}

				return _aliases;
			}
		}

		public readonly struct AddressMatch
		{
			public readonly NPC Npc;
			public readonly string StrippedText;

			public AddressMatch(NPC npc, string strippedText) {
				Npc = npc;
				StrippedText = strippedText;
			}
		}

		public static bool TryResolve(string text, out AddressMatch match) {
			match = default;
			if (string.IsNullOrWhiteSpace(text)) {
				return false;
			}

			string message = text.Trim();
			NPC best = null;
			string bestName = null;
			int bestNameLen = -1;
			int bestAtIndex = -1;

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!NpcHostRules.IsManagedTownNpc(npc)) {
					continue;
				}

				Consider(message, npc, Lang.GetNPCNameValue(npc.type), ref best, ref bestName, ref bestNameLen, ref bestAtIndex);
				if (!string.IsNullOrEmpty(npc.GivenName)) {
					Consider(message, npc, npc.GivenName, ref best, ref bestName, ref bestNameLen, ref bestAtIndex);
				}

				Consider(message, npc, npc.GivenOrTypeName, ref best, ref bestName, ref bestNameLen, ref bestAtIndex);
				foreach (var kv in Aliases) {
					if (kv.Value == npc.type) {
						Consider(message, npc, kv.Key, ref best, ref bestName, ref bestNameLen, ref bestAtIndex);
					}
				}
			}

			if (best == null || bestName == null || bestAtIndex < 0) {
				return false;
			}

			match = new AddressMatch(best, StripMention(message, bestAtIndex, bestName));
			return true;
		}

		private static void Consider(
			string message,
			NPC npc,
			string name,
			ref NPC best,
			ref string bestName,
			ref int bestNameLen,
			ref int bestAtIndex) {
			if (string.IsNullOrWhiteSpace(name)) {
				return;
			}

			int atIndex = message.IndexOf("@" + name, StringComparison.OrdinalIgnoreCase);
			if (atIndex < 0) {
				return;
			}

			if (name.Length > bestNameLen) {
				bestNameLen = name.Length;
				bestName = name;
				best = npc;
				bestAtIndex = atIndex;
			}
		}

		private static string StripMention(string message, int atIndex, string name) {
			int end = atIndex + 1 + name.Length;
			string before = atIndex > 0 ? message.Substring(0, atIndex) : string.Empty;
			string after = end < message.Length ? message.Substring(end) : string.Empty;
			return (before + after).Trim();
		}
	}
}
