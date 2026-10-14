using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace AITownNPCs.Common.UI
{
	/// <summary>
	/// Chat tag [nh:headIndex] that draws the standard town NPC head icon
	/// (same textures as the housing menu / TextureAssets.NpcHead).
	/// Vanilla's [n:] is NameTagHandler (player name), not an NPC head.
	/// </summary>
	public class NpcHeadChatTagHandler : ITagHandler
	{
		TextSnippet ITagHandler.Parse(string text, Color baseColor, string options) {
			if (!int.TryParse(text, out int headIndex)
			    || headIndex < 0
			    || headIndex >= TextureAssets.NpcHead.Length) {
				return new TextSnippet(text);
			}

			return new NpcHeadSnippet(headIndex) {
				DeleteWhole = true
			};
		}

		private class NpcHeadSnippet : TextSnippet
		{
			private readonly int _headIndex;

			public NpcHeadSnippet(int headIndex) : base("") {
				_headIndex = headIndex;
			}

			public override bool UniqueDraw(
				bool justCheckingString,
				out Vector2 size,
				SpriteBatch spriteBatch,
				Vector2 position = default,
				Color color = default,
				float scale = 1f) {
				Texture2D texture = TextureAssets.NpcHead[_headIndex].Value;
				float drawScale = scale * 0.85f;
				size = texture.Size() * drawScale;

				// Skip shadow pass (same idea as ItemTagHandler).
				if (!justCheckingString && (color.R != 0 || color.G != 0 || color.B != 0)) {
					spriteBatch.Draw(
						texture,
						position,
						null,
						Color.White,
						0f,
						Vector2.Zero,
						drawScale,
						SpriteEffects.None,
						0f);
				}

				return true;
			}

			public override float GetStringLength(DynamicSpriteFont font) {
				Texture2D texture = TextureAssets.NpcHead[_headIndex].Value;
				return texture.Width * 0.85f;
			}
		}
	}
}
