namespace AITownNPCs.Common.Net
{
	public enum NpcEquipMessageType : byte
	{
		SyncSlot = 1,
		RequestSyncAll = 2,
		SyncAll = 3,
		RequestOpenEquip = 4,
		OpenEquipResult = 5,
		ReleaseEquip = 6,
		/// <summary>Server → client: force-close equip UI (lock lost).</summary>
		ForceCloseEquip = 7
	}
}
