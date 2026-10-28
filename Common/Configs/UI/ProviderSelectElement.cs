using AITownNPCs.Common.Loc;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;

namespace AITownNPCs.Common.Configs.UI
{
	/// <summary>Block-style provider picker (click to select, no slider).</summary>
	public sealed class ProviderSelectElement : ConfigElement<string>
	{
		private const float LabelRow = 30f;
		private const float BlockHeight = 36f;
		private const float Gap = 8f;

		private static readonly Color SelectedBg = new Color(90, 140, 210);
		private static readonly Color IdleBg = new Color(44, 58, 105);
		private static readonly Color SelectedBorder = new Color(255, 231, 69);
		private static readonly Color IdleBorder = new Color(89, 116, 213);

		private UITextPanel<string> _miniMax;
		private UITextPanel<string> _volcArk;
		private UITextPanel<string> _custom;
		private string _seenValue;

		public override void OnBind() {
			base.OnBind();
			Height.Set(LabelRow + BlockHeight * 2f + Gap + 8f, 0f);
			_seenValue = Value ?? AinpcServerConfig.ProviderLabelMiniMax;

			float half = -Gap * 0.5f;

			_miniMax = CreateBlock(
				DisplayLabel(AinpcServerConfig.ProviderLabelMiniMax),
				AinpcServerConfig.ProviderLabelMiniMax,
				leftPercent: 0f,
				widthOffset: half - 8f,
				topRow: 0);
			_volcArk = CreateBlock(
				DisplayLabel(AinpcServerConfig.ProviderLabelVolcArk),
				AinpcServerConfig.ProviderLabelVolcArk,
				leftPercent: 0.5f,
				widthOffset: half - 8f,
				topRow: 0,
				leftExtra: Gap * 0.5f);
			_custom = CreateBlock(
				DisplayLabel(AinpcServerConfig.ProviderLabelCustom),
				AinpcServerConfig.ProviderLabelCustom,
				leftPercent: 0f,
				widthOffset: -16f,
				topRow: 1,
				widthPercent: 1f);

			Append(_miniMax);
			Append(_volcArk);
			Append(_custom);
			RefreshBlockStyles();
		}

		private static string DisplayLabel(string providerId) {
			if (string.Equals(providerId, AinpcServerConfig.ProviderLabelCustom, System.StringComparison.Ordinal)) {
				string loc = ModText.Get("Configs.AiProvider.Custom.Label");
				return string.IsNullOrWhiteSpace(loc) || loc.StartsWith("Mods.")
					? "Custom (OpenAI-compatible)"
					: loc;
			}

			if (string.Equals(providerId, AinpcServerConfig.ProviderLabelVolcArk, System.StringComparison.Ordinal)) {
				string loc = ModText.Get("Configs.AiProvider.VolcArk.Label");
				return string.IsNullOrWhiteSpace(loc) || loc.StartsWith("Mods.")
					? AinpcServerConfig.ProviderLabelVolcArk
					: loc;
			}

			return AinpcServerConfig.ProviderLabelMiniMax;
		}

		private UITextPanel<string> CreateBlock(
			string display,
			string value,
			float leftPercent,
			float widthOffset,
			int topRow,
			float leftExtra = 0f,
			float widthPercent = 0.5f) {
			var block = new UITextPanel<string>(display, 0.9f, large: false) {
				TextHAlign = 0.5f
			};
			block.SetPadding(8f);
			block.Top.Set(LabelRow + topRow * (BlockHeight + Gap), 0f);
			block.Left.Set(8f + leftExtra, leftPercent);
			block.Width.Set(widthOffset, widthPercent);
			block.Height.Set(BlockHeight, 0f);
			block.OnLeftClick += (_, _) => SelectProvider(value);
			return block;
		}

		private void SelectProvider(string label) {
			if (string.Equals(Value, label, System.StringComparison.Ordinal)) {
				return;
			}

			Value = label;
			_seenValue = label;
			if (Item is AinpcServerConfig cfg) {
				cfg.SyncUiForProvider();
			}

			RefreshBlockStyles();
		}

		public override void Update(GameTime gameTime) {
			base.Update(gameTime);
			string current = Value ?? AinpcServerConfig.ProviderLabelMiniMax;
			if (current != _seenValue) {
				_seenValue = current;
				RefreshBlockStyles();
			}
		}

		private void RefreshBlockStyles() {
			ApplyStyle(_miniMax, AinpcServerConfig.ProviderLabelMiniMax);
			ApplyStyle(_volcArk, AinpcServerConfig.ProviderLabelVolcArk);
			ApplyStyle(_custom, AinpcServerConfig.ProviderLabelCustom);
		}

		private void ApplyStyle(UITextPanel<string> block, string label) {
			if (block == null) {
				return;
			}

			bool selected = string.Equals(Value, label, System.StringComparison.Ordinal);
			block.BackgroundColor = selected ? SelectedBg : IdleBg;
			block.BorderColor = selected ? SelectedBorder : IdleBorder;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch) {
			base.DrawSelf(spriteBatch);
		}
	}
}
