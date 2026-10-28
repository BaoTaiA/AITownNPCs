using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AITownNPCs.Common.Configs;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Chat
{
	public sealed class ChatMessageDto
	{
		public string Role { get; set; }
		public string Content { get; set; }

		public ChatMessageDto(string role, string content) {
			Role = role;
			Content = content;
		}
	}

	/// <summary>OpenAI-compatible chat client (MiniMax / VolcArk).</summary>
	public static class AiChatClient
	{
		private static readonly HttpClient Http = CreateClient();
		private static readonly SemaphoreSlim Concurrency = new(2, 2);
		private static readonly Regex ThinkBlock = new(
			@"<think>[\s\S]*?</think>|```thinking[\s\S]*?```",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static HttpClient CreateClient() {
			var client = new HttpClient {
				Timeout = TimeSpan.FromSeconds(60)
			};
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			return client;
		}

		/// <summary>True when current provider has API key + model (ignores AiEnabled).</summary>
		public static bool HasCredentials(AinpcServerConfig config) {
			if (config == null) {
				return false;
			}

			if (string.IsNullOrWhiteSpace(ResolveApiKey(config))
				|| string.IsNullOrWhiteSpace(ResolveModel(config))) {
				return false;
			}

			if (config.Provider == AiProvider.Custom
				&& string.IsNullOrWhiteSpace(AiProviderEndpoints.NormalizeBaseUrl(config.CustomBaseUrl))) {
				return false;
			}

			return true;
		}

		public static bool IsConfigured(AinpcServerConfig config) {
			return config != null && config.AiEnabled && HasCredentials(config);
		}

		/// <summary>
		/// One-shot connectivity check using current provider credentials (ignores AiEnabled).
		/// </summary>
		public static async Task<bool> ProbeAsync(AinpcServerConfig config, CancellationToken ct = default) {
			if (!HasCredentials(config)) {
				return false;
			}

			var messages = new List<ChatMessageDto> {
				new("system", "ping"),
				new("user", "ping")
			};
			string content = await CompleteWithCredentialsAsync(
				config,
				messages,
				maxTokensOverride: 1,
				temperatureOverride: 0f,
				ct).ConfigureAwait(false);
			return content != null;
		}

		public static async Task<string> CompleteAsync(
			AinpcServerConfig config,
			IReadOnlyList<ChatMessageDto> messages,
			int? maxTokensOverride = null,
			float? temperatureOverride = null,
			CancellationToken ct = default) {
			if (!IsConfigured(config)) {
				return null;
			}

			return await CompleteWithCredentialsAsync(
				config,
				messages,
				maxTokensOverride,
				temperatureOverride,
				ct).ConfigureAwait(false);
		}

		private static async Task<string> CompleteWithCredentialsAsync(
			AinpcServerConfig config,
			IReadOnlyList<ChatMessageDto> messages,
			int? maxTokensOverride,
			float? temperatureOverride,
			CancellationToken ct) {
			string apiKey = ResolveApiKey(config);
			string model = ResolveModel(config);
			string baseUrl = AiProviderEndpoints.GetBaseUrl(config.Provider, config).TrimEnd('/');
			if (string.IsNullOrWhiteSpace(baseUrl)) {
				return null;
			}

			string url = baseUrl + "/chat/completions";
			int maxTokens = maxTokensOverride ?? config.AiMaxTokens;

			var payload = new Dictionary<string, object> {
				["model"] = model,
				["temperature"] = temperatureOverride ?? AiProviderEndpoints.DefaultTemperature,
				["messages"] = BuildMessages(messages)
			};
			// OpenAI-compatible default: max_tokens. MiniMax uses max_completion_tokens.
			if (config.Provider == AiProvider.MiniMax) {
				payload["max_completion_tokens"] = maxTokens;
			}
			else {
				payload["max_tokens"] = maxTokens;
			}

			string json = JsonSerializer.Serialize(payload);
			await Concurrency.WaitAsync(ct).ConfigureAwait(false);
			try {
				using var req = new HttpRequestMessage(HttpMethod.Post, url);
				req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
				req.Content = new StringContent(json, Encoding.UTF8, "application/json");

				using HttpResponseMessage resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
				string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
				if (!resp.IsSuccessStatusCode) {
					string snippet = body == null
						? ""
						: body.Length <= 200 ? body : body.Substring(0, 200);
					ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
						$"AI chat HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} provider={config.Provider} model={model}: {snippet}");
					return null;
				}

				string content = ExtractContent(body);
				if (content == null) {
					// Probe / some providers return 200 with empty content; treat HTTP OK as usable.
					if (maxTokensOverride == 1) {
						return "";
					}

					ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
						$"AI chat empty/unparseable content provider={config.Provider} model={model}");
				}

				return content;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
					$"AI chat request failed provider={config.Provider} model={model}: {ex.GetBaseException().Message}");
				return null;
			}
			finally {
				Concurrency.Release();
			}
		}

		private static string ResolveApiKey(AinpcServerConfig config) {
			return config.Provider switch {
				AiProvider.VolcArk => config.VolcArkApiKey?.Trim(),
				AiProvider.Custom => config.CustomApiKey?.Trim(),
				_ => config.MiniMaxApiKey?.Trim()
			};
		}

		private static string ResolveModel(AinpcServerConfig config) {
			return config.Provider switch {
				AiProvider.VolcArk => string.IsNullOrWhiteSpace(config.VolcArkModel)
					? AiProviderEndpoints.VolcArkModelId
					: config.VolcArkModel.Trim(),
				AiProvider.Custom => (config.CustomModel ?? "").Trim(),
				_ => string.IsNullOrWhiteSpace(config.MiniMaxModel)
					? AiProviderEndpoints.MiniMaxModelId
					: config.MiniMaxModel.Trim()
			};
		}

		private static List<Dictionary<string, string>> BuildMessages(IReadOnlyList<ChatMessageDto> messages) {
			var list = new List<Dictionary<string, string>>(messages.Count);
			foreach (ChatMessageDto m in messages) {
				if (m == null || string.IsNullOrWhiteSpace(m.Content)) {
					continue;
				}

				list.Add(new Dictionary<string, string> {
					["role"] = m.Role ?? "user",
					["content"] = m.Content
				});
			}

			return list;
		}

		private static string ExtractContent(string body) {
			try {
				using JsonDocument doc = JsonDocument.Parse(body);
				JsonElement root = doc.RootElement;
				if (!root.TryGetProperty("choices", out JsonElement choices) || choices.GetArrayLength() == 0) {
					return null;
				}

				JsonElement message = choices[0].GetProperty("message");
				string content = null;
				if (message.TryGetProperty("content", out JsonElement contentEl)) {
					content = contentEl.GetString();
				}

				if (string.IsNullOrWhiteSpace(content)) {
					return null;
				}

				content = ThinkBlock.Replace(content, string.Empty).Trim();
				content = Regex.Replace(content, @"\n{3,}", "\n\n").Trim();
				return string.IsNullOrWhiteSpace(content) ? null : content;
			}
			catch {
				return null;
			}
		}
	}
}
