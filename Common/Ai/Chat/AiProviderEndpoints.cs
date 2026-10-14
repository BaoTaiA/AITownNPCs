using AITownNPCs.Common.Configs;

namespace AITownNPCs.Common.Ai.Chat
{
	public static class AiProviderEndpoints
	{
		public const string MiniMaxBaseUrl = "https://api.minimax.cn/v1";
		public const string VolcArkCodingBaseUrl = "https://ark.cn-beijing.volces.com/api/coding/v3";
		public const string MiniMaxModelId = "MiniMax-M3";
		public const string VolcArkModelId = "ark-code-latest";
		public const float DefaultTemperature = 1f;

		public static string GetBaseUrl(AiProvider provider) {
			return provider switch {
				AiProvider.MiniMax => MiniMaxBaseUrl,
				AiProvider.VolcArk => VolcArkCodingBaseUrl,
				_ => MiniMaxBaseUrl
			};
		}

		public static string GetModelId(AiProvider provider) {
			return provider switch {
				AiProvider.VolcArk => VolcArkModelId,
				_ => MiniMaxModelId
			};
		}
	}
}
