using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace AITownNPCs.Common.UI
{
	public class NpcHeadChatTagSystem : ModSystem
	{
		public override void Load() {
			ChatManager.Register<NpcHeadChatTagHandler>("nh");
		}
	}
}
