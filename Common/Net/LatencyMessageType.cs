namespace AITownNPCs.Common.Net
{
	public enum LatencyMessageType : byte
	{
		/// <summary>Client → server: echo request with client tick.</summary>
		Ping = 50,
		/// <summary>Server → client: echo response with original tick.</summary>
		Pong = 51
	}
}
