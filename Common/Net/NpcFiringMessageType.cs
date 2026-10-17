namespace AITownNPCs.Common.Net
{
	public enum NpcFiringMessageType : byte
	{
		DummyPose = 23,
		DummyRelease = 24,
		/// <summary>Follower client runs the dummy weapon use. Sub-byte selects intent, report, visual, or hit.</summary>
		ClientUse = 29
	}
}
