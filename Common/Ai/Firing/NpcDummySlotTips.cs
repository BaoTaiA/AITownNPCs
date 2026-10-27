using AITownNPCs.Common.Ai.Chat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>Throttle player-facing tips when dummy player slots are exhausted.</summary>
	public static class NpcDummySlotTips
	{
		private const int CooldownTicks = 60 * 8;
		private static int _cooldown;

		public static void NotifyExhausted() {
			if (Main.dedServ) {
				return;
			}

			if (_cooldown > 0) {
				return;
			}

			_cooldown = CooldownTicks;
			string text = Language.GetTextValue("Mods.AITownNPCs.Firing.DummySlotsExhausted");
			NpcAiSpeech.NotifyPlayerLiteral(Main.myPlayer, text, Color.Orange);
		}

		public static void TickCooldown() {
			if (_cooldown > 0) {
				_cooldown--;
			}
		}
	}

	public sealed class NpcDummySlotTipsSystem : ModSystem
	{
		public override void PostUpdateEverything() {
			NpcDummySlotTips.TickCooldown();
		}
	}
}
