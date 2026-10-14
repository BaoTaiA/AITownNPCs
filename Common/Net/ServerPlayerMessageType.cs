namespace AITownNPCs.Common.Net
{
	/// <summary>
	/// Server → owning client player state. Routed at <see cref="PacketRanges.PlayerStateMin"/>.
	/// </summary>
	public enum ServerPlayerMessageType : byte
	{
		ApplyBuff = 40,
		ClearBuff = 41,
		ClearNurseDebuffs = 42,
		HealMana = 43,
		SetInventorySlot = 44,
		ClearInventorySlot = 45,
		SpendCopper = 46,
		GiveItem = 47,
		/// <summary>Server → client: add life (client clamps); always sent even if server copy looked full.</summary>
		HealLife = 48
	}
}
