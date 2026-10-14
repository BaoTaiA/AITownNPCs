using AITownNPCs.Common.Ai.Follow;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Chat
{
	/// <summary>Subscribes to follow events and records short-term AI memory notes.</summary>
	public class NpcFollowMemoryBridge : ModSystem
	{
		public override void Load() {
			NpcFollowEvents.FollowStarted += OnFollowStarted;
			NpcFollowEvents.FollowStopped += OnFollowStopped;
		}

		public override void Unload() {
			NpcFollowEvents.FollowStarted -= OnFollowStarted;
			NpcFollowEvents.FollowStopped -= OnFollowStopped;
		}

		private static void OnFollowStarted(Player player, NPC npc) {
			AiConversationMemory.AppendEvent(player, npc, "开始跟随该玩家");
		}

		private static void OnFollowStopped(Player player, NPC npc) {
			AiConversationMemory.AppendEvent(player, npc, "停止跟随该玩家");
		}
	}
}
