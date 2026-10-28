using System;
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

		public static string GetBaseUrl(AiProvider provider, AinpcServerConfig config = null) {
			return provider switch {
				AiProvider.VolcArk => VolcArkCodingBaseUrl,
				AiProvider.Custom => NormalizeBaseUrl(config?.CustomBaseUrl),
				_ => MiniMaxBaseUrl
			};
		}

		public static string GetModelId(AiProvider provider) {
			return provider switch {
				AiProvider.VolcArk => VolcArkModelId,
				AiProvider.Custom => "",
				_ => MiniMaxModelId
			};
		}

		/// <summary>
		/// Strip whitespace, trailing slashes, and a mistaken /chat/completions suffix.
		/// </summary>
		public static string NormalizeBaseUrl(string url) {
			if (string.IsNullOrWhiteSpace(url)) {
				return "";
			}

			string trimmed = url.Trim();
			while (trimmed.EndsWith("/", StringComparison.Ordinal)) {
				trimmed = trimmed.Substring(0, trimmed.Length - 1);
			}

			const string completions = "/chat/completions";
			if (trimmed.EndsWith(completions, StringComparison.OrdinalIgnoreCase)) {
				trimmed = trimmed.Substring(0, trimmed.Length - completions.Length);
				while (trimmed.EndsWith("/", StringComparison.Ordinal)) {
					trimmed = trimmed.Substring(0, trimmed.Length - 1);
				}
			}

			return trimmed;
		}
	}
}
