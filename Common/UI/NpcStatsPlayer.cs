using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Host;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.UI
{
	public class NpcStatsPlayer : ModPlayer
	{
		public int ViewingNpcWhoAmI = -1;

		public bool PanelOpen => ViewingNpcWhoAmI >= 0 && ViewingNpcWhoAmI < Main.maxNPCs;

		public void RequestOpenStats(int npcWhoAmI) {
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			if (!NpcHostRules.IsManagedTownNpc(npc) || !NpcEquipHelper.IsPlayerNearNpc(Player, npc)) {
				return;
			}

			if (NpcInteractGate.TryGetBlockMessage(Player, npc, out string blocked)) {
				Main.NewText(blocked, NpcInteractGate.NoticeColor);
				return;
			}

			OpenStatsLocal(npcWhoAmI);
		}

		public void OpenStatsLocal(int npcWhoAmI) {
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return;
			}

			NpcEquipPlayer equip = Player.GetModPlayer<NpcEquipPlayer>();
			if (equip.IsEditing) {
				equip.CloseEquip();
			}

			ViewingNpcWhoAmI = npcWhoAmI;
			if (Player.talkNPC != npcWhoAmI) {
				Player.SetTalkNPC(npcWhoAmI);
			}

			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void CloseStats() {
			if (!PanelOpen) {
				return;
			}

			ViewingNpcWhoAmI = -1;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public void CloseStatsQuiet() {
			ViewingNpcWhoAmI = -1;
		}

		public override void OnEnterWorld() {
			ViewingNpcWhoAmI = -1;
		}

		public override void PostUpdate() {
			if (!PanelOpen || Player.whoAmI != Main.myPlayer) {
				return;
			}

			NPC npc = Main.npc[ViewingNpcWhoAmI];
			if (!npc.active
				|| !NpcHostRules.IsManagedTownNpc(npc)
				|| !NpcEquipHelper.IsPlayerNearNpc(Player, npc)
				|| Player.talkNPC != ViewingNpcWhoAmI
				|| Player.dead) {
				CloseStatsQuiet();
			}
		}
	}
}
