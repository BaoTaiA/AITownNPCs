namespace AITownNPCs.Common.Net
{
	public enum NpcChatMessageType : byte
	{
		SpeechBubble = 20,
		/// <summary>Client → server: player chat that must not appear in the chat box (follower range).</summary>
		PlayerChatHidden = 21,
		/// <summary>Server → clients: show player overhead bubble without chat-box line.</summary>
		PlayerOverhead = 22,
		/// <summary>Client → server: request persisted public + follow chat history.</summary>
		ChatHistoryRequest = 25,
		/// <summary>Server → client: chunk of persisted chat history.</summary>
		ChatHistoryChunk = 26,
		/// <summary>Server → client: live follow-channel line.</summary>
		FollowChatLine = 27,
		/// <summary>Client → server: normal chat line for follow keywords / AI / superpowers.</summary>
		PlayerChatLine = 28
	}
}
