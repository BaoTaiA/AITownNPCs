using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.Nurse
{
	public sealed class SuperHeartBuff : ModBuff
	{
		public const int DurationTicks = 60 * 60 * 3;

		public override string Texture => "AITownNPCs/Content/Buffs/SuperHeart";

		public override void SetStaticDefaults() {
			Main.buffNoSave[Type] = true;
			Main.debuff[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex) {
			// 120 regen points = 1 HP. 100 per tick * 60 ticks / 120 = 50 HP per second.
			player.lifeRegen += 100;
		}
	}
}
