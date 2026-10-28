using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;

namespace AITownNPCs.Common.Configs.UI
{
	/// <summary>Model id on its own row (full width, wraps) under the label.</summary>
	public sealed class ModelInputElement : ConfigElement<string>
	{
		private const float LabelRow = 30f;
		private const float BoxHeight = 36f;
		private const float LineHeight = 18f;

		private WrappedFocusInputTextField _field;
		private AiProvider _seenProvider;

		public override void OnBind() {
			base.OnBind();
			Height.Set(LabelRow + BoxHeight + 6f, 0f);
			_seenProvider = (Item as AinpcServerConfig)?.Provider ?? AiProvider.MiniMax;

			var background = new UIPanel();
			background.SetPadding(8f);
			background.Top.Set(LabelRow, 0f);
			background.Left.Set(8f, 0f);
			background.Width.Set(-16f, 1f);
			background.Height.Set(BoxHeight, 0f);
			Append(background);

			_field = new WrappedFocusInputTextField(Language.GetTextValue("tModLoader.ModConfigTypeHere"));
			_field.SetText(Value ?? "");
			_field.Width.Set(0f, 1f);
			_field.Height.Set(0f, 1f);
			_field.OnTextChange += (_, _) => {
				Value = _field.CurrentString;
				if (Item is AinpcServerConfig cfg) {
					cfg.WriteStore(cfg.Provider, Value, cfg.ApiKey, cfg.BaseUrl);
				}
			};
			background.Append(_field);
		}

		public override void Update(GameTime gameTime) {
			base.Update(gameTime);
			if (_field == null || Item is not AinpcServerConfig cfg) {
				return;
			}

			if (_seenProvider != cfg.Provider) {
				if (cfg.UiBoundProvider == _seenProvider) {
					Value = _field.CurrentString ?? "";
					cfg.SyncUiForProvider();
				}

				_seenProvider = cfg.Provider;
				Value = cfg.Model;
				_field.SetText(Value ?? "");
				return;
			}

			if (_field.Focused) {
				return;
			}

			string current = Value ?? "";
			if (_field.CurrentString != current) {
				_field.SetText(current);
			}
		}

		private sealed class WrappedFocusInputTextField : UIElement
		{
			internal string CurrentString = "";
			internal bool Focused => _focused;
			private readonly string _hintText;
			private bool _focused;
			private int _blinkCount;
			private int _blinkState;

			public event EventHandler OnTextChange;

			public WrappedFocusInputTextField(string hintText) {
				_hintText = hintText ?? "";
			}

			public void SetText(string text) {
				CurrentString = text ?? "";
			}

			public override void LeftClick(UIMouseEvent evt) {
				Main.clrInput();
				_focused = true;
			}

			public override void Update(GameTime gameTime) {
				var mouse = new Vector2(Main.mouseX, Main.mouseY);
				if (_focused && !ContainsPoint(mouse) && Main.mouseLeft) {
					_focused = false;
				}

				base.Update(gameTime);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch) {
				if (_focused) {
					PlayerInput.WritingText = true;
					Main.instance.HandleIME();
					string next = Main.GetInputText(CurrentString);
					if (Main.inputTextEscape) {
						Main.inputTextEscape = false;
						_focused = false;
					}

					if (JustPressed(Keys.Enter) || JustPressed(Keys.Tab)) {
						_focused = false;
					}

					if (next != CurrentString) {
						CurrentString = next ?? "";
						OnTextChange?.Invoke(this, EventArgs.Empty);
					}

					if (++_blinkCount >= 20) {
						_blinkState = (_blinkState + 1) % 2;
						_blinkCount = 0;
					}
				}

				CalculatedStyle space = GetDimensions();
				if (CurrentString.Length == 0 && !_focused) {
					Utils.DrawBorderString(spriteBatch, _hintText, new Vector2(space.X, space.Y), Color.Gray);
					return;
				}

				string display = CurrentString;
				if (_focused && _blinkState == 1) {
					display += "|";
				}

				IReadOnlyList<string> lines = WrapByWidth(display, space.Width);
				float y = space.Y;
				int maxLines = Math.Max(1, (int)(space.Height / LineHeight));
				for (int i = 0; i < lines.Count && i < maxLines; i++) {
					Utils.DrawBorderString(spriteBatch, lines[i], new Vector2(space.X, y), Color.White);
					y += LineHeight;
				}
			}

			private static bool JustPressed(Keys key) {
				return Main.inputText.IsKeyDown(key) && !Main.oldInputText.IsKeyDown(key);
			}

			private static List<string> WrapByWidth(string text, float maxWidth) {
				var lines = new List<string>();
				if (string.IsNullOrEmpty(text) || maxWidth <= 8f) {
					lines.Add(text ?? "");
					return lines;
				}

				var font = FontAssets.MouseText.Value;
				var current = new StringBuilder();
				foreach (char c in text) {
					current.Append(c);
					if (font.MeasureString(current.ToString()).X > maxWidth && current.Length > 1) {
						current.Length--;
						lines.Add(current.ToString());
						current.Clear();
						current.Append(c);
					}
				}

				if (current.Length > 0) {
					lines.Add(current.ToString());
				}

				if (lines.Count == 0) {
					lines.Add("");
				}

				return lines;
			}
		}
	}
}
