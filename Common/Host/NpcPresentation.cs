using Terraria;
using Terraria.GameContent;

namespace AITownNPCs.Common.Host
{
	/// <summary>Neutral NPC display helpers shared by Follow announcements and Chat speech.</summary>
	public static class NpcPresentation
	{
		public static string FormatDisplayName(NPC npc) {
			string typeName = Lang.GetNPCNameValue(npc.type);
			string givenName = npc.GivenName;
			if (string.IsNullOrWhiteSpace(givenName) || givenName == typeName) {
				return typeName;
			}

			return $"{typeName}{givenName}";
		}

		public static string GetNpcHeadTag(NPC npc) {
			int headIndex = TownNPCProfiles.GetHeadIndexSafe(npc);
			if (headIndex < 0) {
				headIndex = NPC.TypeToDefaultHeadIndex(npc.type);
			}

			if (headIndex < 0) {
				return string.Empty;
			}

			return $"[nh:{headIndex}]";
		}
	}
}
