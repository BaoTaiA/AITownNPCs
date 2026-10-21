namespace AITownNPCs.Common.Net
{
	public enum NpcFiringMessageType : byte
	{
		/// <summary>Follower client runs the dummy weapon use. Sub-byte selects intent, report, or hit.</summary>
		ClientUse = 29
	}
}
