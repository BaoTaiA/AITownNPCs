namespace AITownNPCs.Common.Net
{
	/// <summary>
	/// Mod packet id ranges for AITownNPCs.HandlePacket / ModPacketRouter.
	/// </summary>
	public static class PacketRanges
	{
		/// <summary>Equip packets: 0–9 (values unused by router; documented for authors).</summary>
		public const byte EquipMin = 0;
		public const byte EquipMax = 9;
		public const byte FollowMin = 10;
		/// <summary>Follow packets: 10–19.</summary>
		public const byte FollowMax = 19;
		public const byte AiMin = 20;
		public const byte SuperpowerMin = 30;
		/// <summary>Server→client player state: 40–49.</summary>
		public const byte PlayerStateMin = 40;
		public const byte PlayerStateMax = 49;
	}
}
