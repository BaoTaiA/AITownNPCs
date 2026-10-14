using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Configs.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace AITownNPCs.Common.Configs
{
	public enum AiProvider
	{
		MiniMax,
		VolcArk
	}

	/// <summary>Discrete follow/chat distances in pixels (no slider).</summary>
	public enum FollowChatDistance
	{
		D50 = 50,
		D100 = 100,
		D200 = 200,
		D300 = 300,
		D400 = 400
	}

	public class AinpcServerConfig : ModConfig
	{
		public const string ProviderLabelMiniMax = "MiniMax";
		public const string ProviderLabelVolcArk = "火山方舟";

		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static AinpcServerConfig Instance => ModContent.GetInstance<AinpcServerConfig>();

		[Header("$Mods.AITownNPCs.Config.Headers.Follow")]
		[DefaultValue(FollowChatDistance.D200)]
		[DrawTicks]
		public FollowChatDistance FollowTriggerDistance = FollowChatDistance.D200;

		[JsonIgnore]
		public float FollowTriggerDistancePixels => (float)(int)FollowTriggerDistance;

		[DefaultValue("跟着我")]
		public string FollowStartKeyword = "跟着我";

		[DefaultValue("你走吧")]
		public string FollowStopKeyword = "你走吧";

		[Header("$Mods.AITownNPCs.Config.Headers.Ai")]
		[DefaultValue(true)]
		public bool AiEnabled = true;

		/// <summary>Block click-to-select provider (not slider).</summary>
		[DefaultValue(ProviderLabelMiniMax)]
		[CustomModConfigItem(typeof(ProviderSelectElement))]
		public string ProviderId = ProviderLabelMiniMax;

		/// <summary>Resolved provider for chat client code.</summary>
		[JsonIgnore]
		public AiProvider Provider =>
			string.Equals(ProviderId, ProviderLabelVolcArk, StringComparison.Ordinal)
				? AiProvider.VolcArk
				: AiProvider.MiniMax;

		[DefaultValue(AiProviderEndpoints.MiniMaxModelId)]
		[JsonIgnore]
		[ShowDespiteJsonIgnore]
		[CustomModConfigItem(typeof(ModelInputElement))]
		public string Model = AiProviderEndpoints.MiniMaxModelId;

		[DefaultValue("")]
		[JsonIgnore]
		[ShowDespiteJsonIgnore]
		[CustomModConfigItem(typeof(ApiKeyInputElement))]
		public string ApiKey = "";

		[DefaultValue(256)]
		[Range(32, 2048)]
		public int AiMaxTokens = 256;

		[DefaultValue(20)]
		[Range(2, 40)]
		public int AiShortTermTurns = 20;

		[DefaultValue(true)]
		public bool AiProactiveEnabled = true;

		[Header("$Mods.AITownNPCs.Config.Headers.Superpower")]
		[DefaultValue(true)]
		public bool SuperpowerEnabled = true;

		[DefaultValue("")]
		[CustomModConfigItem(typeof(HiddenConfigElement))]
		public string MiniMaxApiKey = "";

		[DefaultValue(AiProviderEndpoints.MiniMaxModelId)]
		[CustomModConfigItem(typeof(HiddenConfigElement))]
		public string MiniMaxModel = AiProviderEndpoints.MiniMaxModelId;

		[DefaultValue("")]
		[CustomModConfigItem(typeof(HiddenConfigElement))]
		public string VolcArkApiKey = "";

		[DefaultValue(AiProviderEndpoints.VolcArkModelId)]
		[CustomModConfigItem(typeof(HiddenConfigElement))]
		public string VolcArkModel = AiProviderEndpoints.VolcArkModelId;

		[JsonIgnore]
		private AiProvider _uiBoundProvider = AiProvider.MiniMax;

		[JsonIgnore]
		private bool _uiHydrated;

		/// <summary>Provider whose Model/ApiKey are currently shown in the UI.</summary>
		[JsonIgnore]
		public AiProvider UiBoundProvider => _uiBoundProvider;

		[JsonExtensionData]
		private IDictionary<string, JToken> _additionalData = new Dictionary<string, JToken>();

		public override void OnLoaded() {
			NormalizeStoredModels();
			EnsureUiHydrated();
		}

		public override void OnChanged() {
			EnsureUiHydrated();
			ApplyProviderSwitch();
			WriteStore(_uiBoundProvider, Model, ApiKey);
			NormalizeStoredModels();
			NpcAiChatSystem.OnConfigChangedRestartProbe();
		}

		/// <summary>
		/// Switch Model/ApiKey to the selected provider's saved values.
		/// Safe to call repeatedly; only flushes when a real switch is pending.
		/// </summary>
		public void SyncUiForProvider() {
			EnsureUiHydrated();
			ApplyProviderSwitch();
		}

		public string StoredModel(AiProvider provider) {
			return provider == AiProvider.VolcArk
				? (string.IsNullOrWhiteSpace(VolcArkModel) ? AiProviderEndpoints.VolcArkModelId : VolcArkModel.Trim())
				: (string.IsNullOrWhiteSpace(MiniMaxModel) ? AiProviderEndpoints.MiniMaxModelId : MiniMaxModel.Trim());
		}

		public string StoredApiKey(AiProvider provider) {
			return provider == AiProvider.VolcArk ? (VolcArkApiKey ?? "") : (MiniMaxApiKey ?? "");
		}

		public void WriteStore(AiProvider provider, string model, string apiKey) {
			if (provider == AiProvider.VolcArk) {
				VolcArkApiKey = apiKey ?? "";
				VolcArkModel = string.IsNullOrWhiteSpace(model)
					? AiProviderEndpoints.VolcArkModelId
					: model.Trim();
			}
			else {
				MiniMaxApiKey = apiKey ?? "";
				MiniMaxModel = string.IsNullOrWhiteSpace(model)
					? AiProviderEndpoints.MiniMaxModelId
					: model.Trim();
			}
		}

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context) {
			MigrateLegacyAdditionalData();
			NormalizeStoredModels();
			_uiBoundProvider = Provider;
			LoadUiFromStore(Provider);
			_uiHydrated = true;
		}

		private void EnsureUiHydrated() {
			if (_uiHydrated) {
				return;
			}

			_uiBoundProvider = Provider;
			LoadUiFromStore(Provider);
			_uiHydrated = true;
		}

		private void ApplyProviderSwitch() {
			if (_uiBoundProvider == Provider) {
				return;
			}

			WriteStore(_uiBoundProvider, Model, ApiKey);
			_uiBoundProvider = Provider;
			LoadUiFromStore(Provider);
		}

		private void LoadUiFromStore(AiProvider provider) {
			ApiKey = StoredApiKey(provider);
			Model = StoredModel(provider);
		}

		private void NormalizeStoredModels() {
			if (string.IsNullOrWhiteSpace(MiniMaxModel)) {
				MiniMaxModel = AiProviderEndpoints.MiniMaxModelId;
			}

			if (string.IsNullOrWhiteSpace(VolcArkModel)) {
				VolcArkModel = AiProviderEndpoints.VolcArkModelId;
			}
		}

		private void MigrateLegacyAdditionalData() {
			if (_additionalData == null || _additionalData.Count == 0) {
				return;
			}

			if (_additionalData.TryGetValue("Provider", out JToken providerToken)) {
				ProviderId = ParseLegacyProvider(providerToken);
			}

			if (_additionalData.TryGetValue("ProviderPanel", out JToken panelToken)) {
				try {
					AiProviderSettings panel = panelToken.ToObject<AiProviderSettings>();
					if (panel != null) {
						ProviderId = panel.Provider == AiProvider.VolcArk
							? ProviderLabelVolcArk
							: ProviderLabelMiniMax;
						if (!string.IsNullOrEmpty(panel.MiniMaxApiKey)) {
							MiniMaxApiKey = panel.MiniMaxApiKey;
						}

						if (!string.IsNullOrEmpty(panel.VolcArkApiKey)) {
							VolcArkApiKey = panel.VolcArkApiKey;
						}
					}
				}
				catch {
					// keep current
				}
			}

			_additionalData.Clear();
		}

		private static string ParseLegacyProvider(JToken token) {
			if (token == null) {
				return ProviderLabelMiniMax;
			}

			if (token.Type == JTokenType.Integer) {
				return token.Value<int>() == (int)AiProvider.VolcArk
					? ProviderLabelVolcArk
					: ProviderLabelMiniMax;
			}

			string text = token.ToString() ?? "";
			if (text.IndexOf("Volc", StringComparison.OrdinalIgnoreCase) >= 0
			    || text.IndexOf("方舟", StringComparison.Ordinal) >= 0
			    || text == "1") {
				return ProviderLabelVolcArk;
			}

			return ProviderLabelMiniMax;
		}
	}
}
