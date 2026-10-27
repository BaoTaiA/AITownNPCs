namespace AITownNPCs.Common.Net
{
	public enum NpcFiringMessageType : byte
	{
		/// <summary>
		/// Follower client runs the dummy weapon use.
		/// Sub-bytes: Intent=1, Report=2, NpcHit=4, HitVisual=5 (legacy drain-only; pose/release retired).
		/// </summary>
		ClientUse = 29
	}
}
