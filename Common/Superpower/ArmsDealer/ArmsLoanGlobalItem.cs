using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Superpower.ArmsDealer
{
	public class ArmsLoanGlobalItem : GlobalItem
	{
		public override bool InstancePerEntity => true;
		public bool IsArmsLoan;

		public override GlobalItem Clone(Item from, Item to) {
			var clone = (ArmsLoanGlobalItem)base.Clone(from, to);
			clone.IsArmsLoan = IsArmsLoan;
			return clone;
		}

		public override void NetSend(Item item, BinaryWriter writer) {
			writer.Write(IsArmsLoan);
		}

		public override void NetReceive(Item item, BinaryReader reader) {
			IsArmsLoan = reader.ReadBoolean();
		}

		public override void SaveData(Item item, TagCompound tag) {
			if (IsArmsLoan) {
				tag["isArmsLoan"] = true;
			}
		}

		public override void LoadData(Item item, TagCompound tag) {
			IsArmsLoan = tag.ContainsKey("isArmsLoan") && tag.GetBool("isArmsLoan");
		}

		public override bool CanStack(Item destination, Item source) {
			bool a = destination.TryGetGlobalItem(out ArmsLoanGlobalItem ga) && ga.IsArmsLoan;
			bool b = source.TryGetGlobalItem(out ArmsLoanGlobalItem gb) && gb.IsArmsLoan;
			if (a || b) {
				return false;
			}

			return base.CanStack(destination, source);
		}
	}

	public class ArmsLoanPlayerHooks : ModPlayer
	{
		public override bool CanSellItem(NPC vendor, Item[] shopInventory, Item item) {
			if (item != null && item.TryGetGlobalItem(out ArmsLoanGlobalItem g) && g.IsArmsLoan) {
				return false;
			}

			return true;
		}

		public override void UpdateDead() {
			ArmsDealerTempWeaponPower.ReclaimAll(Player, notify: false);
		}
	}
}
