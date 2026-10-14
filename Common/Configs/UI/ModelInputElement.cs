using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;

namespace AITownNPCs.Common.Configs.UI
{
	public sealed class ModelInputElement : ConfigElement<string>
	{
		private SingleLineFocusField _field;
		private AiProvider _seenProvider;

		public override void OnBind() {
			base.OnBind();
			Height.Set(30f, 0f);
			_seenProvider = (Item as AinpcServerConfig)?.Provider ?? AiProvider.MiniMax;

			var textBoxBackground = new UIPanel();
			textBoxBackground.SetPadding(0);
			textBoxBackground.Top.Set(0f, 0f);
			textBoxBackground.Left.Set(-190f, 1f);
			textBoxBackground.Width.Set(180f, 0f);
			textBoxBackground.Height.Set(30f, 0f);
			Append(textBoxBackground);

			_field = new SingleLineFocusField(Language.GetTextValue("tModLoader.ModConfigTypeHere"));
			_field.SetText(Value ?? "");
			_field.Top.Set(5f, 0f);
			_field.Left.Set(10f, 0f);
			_field.Width.Set(-20f, 1f);
			_field.Height.Set(20f, 0f);
			_field.OnTextChange += (_, _) => {
				Value = _field.CurrentString;
				if (Item is AinpcServerConfig cfg) {
					cfg.WriteStore(cfg.Provider, Value, cfg.ApiKey);
				}
			};
			textBoxBackground.Append(_field);
		}

		public override void Update(GameTime gameTime) {
			base.Update(gameTime);
			if (_field == null || Item is not AinpcServerConfig cfg) {
				return;
			}

			if (_seenProvider != cfg.Provider) {
				// Only commit stale UI if config has not already switched stores.
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

		private sealed class SingleLineFocusField : UIElement
		{
			internal string CurrentString = "";
			internal bool Focused => _focused;
			private readonly string _hintText;
			private bool _focused;
			private int _blinkCount;
			private int _blinkState;

			public event EventHandler OnTextChange;

			public SingleLineFocusField(string hintText) {
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

					if (Main.inputText.IsKeyDown(Keys.Enter) && !Main.oldInputText.IsKeyDown(Keys.Enter)) {
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

				Utils.DrawBorderString(spriteBatch, display, new Vector2(space.X, space.Y), Color.White);
			}
		}
	}
}
