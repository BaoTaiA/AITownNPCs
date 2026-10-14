using System.IO;
using AITownNPCs.Common.Equip;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Roles.Dryad
{
	public class DryadRole : INpcRole
	{
		public int NpcType => NPCID.Dryad;
		public NpcEquipProfile EquipProfile { get; } = RoleEquipProfiles.CreateDryadBuffs();

		public void Update(NPC npc) {
			DryadAuraAI.Update(npc, npc.GetGlobalNPC<DryadRoleGlobal>().State);
		}

		public bool IsBusy(NPC npc) {
			return DryadAuraAI.IsBusy(npc.GetGlobalNPC<DryadRoleGlobal>().State);
		}
	}

	public class DryadRoleGlobal : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public DryadRoleState State { get; } = new();

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
			return entity.type == NPCID.Dryad;
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bitWriter.WriteBit(State.WardActive);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			State.WardActive = bitReader.ReadBit();
		}
	}
}
