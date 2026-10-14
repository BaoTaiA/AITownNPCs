namespace AITownNPCs.Common.Net
{
	/// <summary>Follow feature packet ids (10–15).</summary>
	public enum NpcFollowMessageType : byte
	{
		RequestStart = 10,
		RequestStop = 11,
		SyncFollow = 12,
		RequestSyncAll = 13,
		SyncAll = 14,
		TeleportFx = 15
	}
}
