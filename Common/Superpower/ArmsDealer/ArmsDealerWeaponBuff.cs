using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.ArmsDealer
{
	public class ArmsDealerWeaponBuff : ModBuff
	{
		public override string Texture => "AITownNPCs/Content/Buffs/ArmsDealerWeapon";

		public override void SetStaticDefaults() {
			Main.buffNoSave[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex) {
			player.GetModPlayer<ArmsLoanPlayer>().HasArmsBuff = true;
		}
	}

	public class ArmsLoanPlayer : ModPlayer
	{
		public bool HasArmsBuff;
		public bool HadArmsBuff;

		public override void ResetEffects() {
			HadArmsBuff = HasArmsBuff;
			HasArmsBuff = false;
		}

		public override void PostUpdate() {
			if (!HadArmsBuff || HasArmsBuff || Player.HasBuff(ModContent.BuffType<ArmsDealerWeaponBuff>())) {
				return;
			}

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				SuperpowerNetSystem.RequestArmsWeaponReclaim();
				return;
			}

			ArmsDealerTempWeaponPower.ReclaimAll(Player, notify: true);
		}

		/// <summary>Strip loan gear after quit/reload (buffNoSave drops the timer; flag is persisted).</summary>
		public override void OnEnterWorld() {
			ArmsDealerTempWeaponPower.ReclaimAll(Player, notify: false);
		}
	}
}
