using AITownNPCs.Common.Ai.Follow;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Chat
{
	/// <summary>Registers Chat handlers onto Follow's player-chat ingress.</summary>
	public class NpcChatIngressBridge : ModSystem
	{
		public override void Load() {
			NpcPlayerChatIngress.OnNonKeywordChat = NpcAiChatSystem.TryHandlePlayerChat;
			NpcPlayerChatIngress.IsAddressedToFollower = IsAddressedToFollower;
		}

		public override void Unload() {
			if (NpcPlayerChatIngress.OnNonKeywordChat == (System.Action<int, string>)NpcAiChatSystem.TryHandlePlayerChat) {
				NpcPlayerChatIngress.OnNonKeywordChat = null;
			}

			if (NpcPlayerChatIngress.IsAddressedToFollower == (System.Func<string, NPC, bool>)IsAddressedToFollower) {
				NpcPlayerChatIngress.IsAddressedToFollower = null;
			}
		}

		private static bool IsAddressedToFollower(string trimmed, NPC follower) {
			if (string.IsNullOrWhiteSpace(trimmed) || !trimmed.StartsWith("@")) {
				return false;
			}

			if (NpcAddressResolver.TryResolve(trimmed, out NpcAddressResolver.AddressMatch addressed)) {
				return addressed.Npc != null && addressed.Npc.whoAmI == follower.whoAmI;
			}

			// Bare @ (no name match) → current follower.
			return true;
		}
	}
}
