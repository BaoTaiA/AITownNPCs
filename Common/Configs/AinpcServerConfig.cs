using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Configs.UI;
using AITownNPCs.Common.Loc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace AITownNPCs.Common.Configs
{
	public enum AiProvider
	{
		MiniMax,
		VolcArk,
		Custom
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

	/// <summary>
	/// Soft recall: if farther than this for ~5s, teleport near the owner.
	/// Independent of <see cref="FollowChatDistance"/> (chat / start-follow range).
	/// </summary>
	public enum FollowLostTeleportDistance
	{
		D400 = 400,
		D800 = 800,
		D1200 = 1200,
		D1600 = 1600,
		D2000 = 2000
	}

	public class AinpcServerConfig : ModConfig
	{
		public const string ProviderLabelMiniMax = "MiniMax";
		public const string ProviderLabelVolcArk = "Volcengine Ark";
		public const string ProviderLabelCustom = "Custom";

		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static AinpcServerConfig Instance => ModContent.GetInstance<AinpcServerConfig>();

		[Header("$Mods.AITownNPCs.Config.Headers.Follow")]
		[DefaultValue(FollowChatDistance.D200)]
		[DrawTicks]
		public FollowChatDistance FollowTriggerDistance = FollowChatDistance.D200;

		[JsonIgnore]
		public float FollowTriggerDistancePixels => (float)(int)FollowTriggerDistance;

		/// <summary>
		/// Soft anti-lost teleport radius (pixels). Default 800 matches the old
		/// "FollowTriggerDistance 200 × 4" behavior without coupling chat range.
		/// Hard snap at FollowMovement.TeleportDistance (2000) is unchanged.
		/// </summary>
		[DefaultValue(FollowLostTeleportDistance.D800)]
		[DrawTicks]
		public FollowLostTeleportDistance FollowLostTeleportDistance = FollowLostTeleportDistance.D800;

		[JsonIgnore]
		public float FollowLostTeleportDistancePixels => (float)(int)FollowLostTeleportDistance;

		/// <summary>Empty = use game-language default from localization.</summary>
		[DefaultValue("")]
		public string FollowStartKeyword = "";

		/// <summary>Empty = use game-language default from localization.</summary>
		[DefaultValue("")]
		public string FollowStopKeyword = "";

		[JsonIgnore]
		public string EffectiveFollowStartKeyword {
			get {
				string custom = (FollowStartKeyword ?? "").Trim();
				return string.IsNullOrEmpty(custom)
					? ModText.Get("Config.Defaults.FollowStartKeyword").Trim()
					: custom;
			}
		}

		[JsonIgnore]
		public string EffectiveFollowStopKeyword {
			get {
				string custom = (FollowStopKeyword ?? "").Trim();
				return string.IsNullOrEmpty(custom)
					? ModText.Get("Config.Defaults.FollowStopKeyword").Trim()
					: custom;
			}
		}

		/// <summary>Dev/test: join red team ~5s after entering a world. Default off.</summary>
		[DefaultValue(false)]
		public bool AutoJoinRedTeam = false;

		[Header("$Mods.AITownNPCs.Config.Headers.Ai")]
		[DefaultValue(true)]
		public bool AiEnabled = true;

		/// <summary>Block click-to-select provider (not slider).</summary>
		[DefaultValue(ProviderLabelMiniMax)]
		[CustomModConfigItem(typeof(ProviderSelectElement))]
		public string ProviderId = ProviderLabelMiniMax;

		/// <summary>Resolved provider for chat client code.</summary>
		[JsonIgnore]
		public AiProvider Provider {
			get {
				string id = ProviderId ?? "";
				if (string.Equals(id, ProviderLabelCustom, StringComparison.Ordinal)
				    || id.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0
				    || id.IndexOf("\u81ea\u5b9a\u4e49", StringComparison.Ordinal) >= 0) {
					return AiProvider.Custom;
				}

				if (string.Equals(id, ProviderLabelVolcArk, StringComparison.Ordinal)
				    || id.IndexOf("Volc", StringComparison.OrdinalIgnoreCase) >= 0
				    || id.IndexOf("ark", StringComparison.OrdinalIgnoreCase) >= 0
				    || id.IndexOf("\u65b9\u821f", StringComparison.Ordinal) >= 0) {
					return AiProvider.VolcArk;
				}

				return AiProvider.MiniMax;
			}
		}

		[DefaultValue("")]
		[JsonIgnore]
		[ShowDespiteJsonIgnore]
		[CustomModConfigItem(typeof(BaseUrlInputElement))]
		public string BaseUrl = "";

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

		[DefaultValue("")]
		[CustomModConfigItem(typeof(HiddenConfigElement))]
		public string CustomApiKey = "";

		[DefaultValue("")]
		[CustomModConfigItem(typeof(HiddenConfigElement))]
		public string CustomModel = "";

		[DefaultValue("")]
		[CustomModConfigItem(typeof(HiddenConfigElement))]
		public string CustomBaseUrl = "";

		[JsonIgnore]
		private AiProvider _uiBoundProvider = AiProvider.MiniMax;

		[JsonIgnore]
		private bool _uiHydrated;

		/// <summary>Provider whose Model/ApiKey/BaseUrl are currently shown in the UI.</summary>
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
			WriteStore(_uiBoundProvider, Model, ApiKey, BaseUrl);
			NormalizeStoredModels();
			NpcAiChatSystem.OnConfigChangedRestartProbe();
		}

		/// <summary>
		/// Switch Model/ApiKey/BaseUrl to the selected provider's saved values.
		/// Safe to call repeatedly; only flushes when a real switch is pending.
		/// </summary>
		public void SyncUiForProvider() {
			EnsureUiHydrated();
			ApplyProviderSwitch();
		}

		public string StoredModel(AiProvider provider) {
			return provider switch {
				AiProvider.VolcArk => string.IsNullOrWhiteSpace(VolcArkModel)
					? AiProviderEndpoints.VolcArkModelId
					: VolcArkModel.Trim(),
				AiProvider.Custom => (CustomModel ?? "").Trim(),
				_ => string.IsNullOrWhiteSpace(MiniMaxModel)
					? AiProviderEndpoints.MiniMaxModelId
					: MiniMaxModel.Trim()
			};
		}

		public string StoredApiKey(AiProvider provider) {
			return provider switch {
				AiProvider.VolcArk => VolcArkApiKey ?? "",
				AiProvider.Custom => CustomApiKey ?? "",
				_ => MiniMaxApiKey ?? ""
			};
		}

		public string StoredBaseUrl(AiProvider provider) {
			return provider switch {
				AiProvider.VolcArk => AiProviderEndpoints.VolcArkCodingBaseUrl,
				AiProvider.Custom => AiProviderEndpoints.NormalizeBaseUrl(CustomBaseUrl),
				_ => AiProviderEndpoints.MiniMaxBaseUrl
			};
		}

		public void WriteStore(AiProvider provider, string model, string apiKey, string baseUrl = null) {
			switch (provider) {
				case AiProvider.VolcArk:
					VolcArkApiKey = apiKey ?? "";
					VolcArkModel = string.IsNullOrWhiteSpace(model)
						? AiProviderEndpoints.VolcArkModelId
						: model.Trim();
					break;
				case AiProvider.Custom:
					CustomApiKey = apiKey ?? "";
					CustomModel = (model ?? "").Trim();
					if (baseUrl != null) {
						CustomBaseUrl = AiProviderEndpoints.NormalizeBaseUrl(baseUrl);
					}

					break;
				default:
					MiniMaxApiKey = apiKey ?? "";
					MiniMaxModel = string.IsNullOrWhiteSpace(model)
						? AiProviderEndpoints.MiniMaxModelId
						: model.Trim();
					break;
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

			WriteStore(_uiBoundProvider, Model, ApiKey, BaseUrl);
			_uiBoundProvider = Provider;
			LoadUiFromStore(Provider);
		}

		private void LoadUiFromStore(AiProvider provider) {
			ApiKey = StoredApiKey(provider);
			Model = StoredModel(provider);
			BaseUrl = StoredBaseUrl(provider);
		}

		private void NormalizeStoredModels() {
			if (string.IsNullOrWhiteSpace(MiniMaxModel)) {
				MiniMaxModel = AiProviderEndpoints.MiniMaxModelId;
			}

			if (string.IsNullOrWhiteSpace(VolcArkModel)) {
				VolcArkModel = AiProviderEndpoints.VolcArkModelId;
			}

			CustomBaseUrl = AiProviderEndpoints.NormalizeBaseUrl(CustomBaseUrl);
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
							: panel.Provider == AiProvider.Custom
								? ProviderLabelCustom
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
				int value = token.Value<int>();
				if (value == (int)AiProvider.VolcArk) {
					return ProviderLabelVolcArk;
				}

				if (value == (int)AiProvider.Custom) {
					return ProviderLabelCustom;
				}

				return ProviderLabelMiniMax;
			}

			string text = token.ToString() ?? "";
			if (text.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0
			    || text.IndexOf("\u81ea\u5b9a\u4e49", StringComparison.Ordinal) >= 0
			    || text == "2") {
				return ProviderLabelCustom;
			}

			// Legacy saved configs may still use the old Chinese provider label.
			if (text.IndexOf("Volc", StringComparison.OrdinalIgnoreCase) >= 0
			    || text.IndexOf("ark", StringComparison.OrdinalIgnoreCase) >= 0
			    || text.IndexOf("\u65b9\u821f", StringComparison.Ordinal) >= 0
			    || text == "1") {
				return ProviderLabelVolcArk;
			}

			return ProviderLabelMiniMax;
		}
	}
}
