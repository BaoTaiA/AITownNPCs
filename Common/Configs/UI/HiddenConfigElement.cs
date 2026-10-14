using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;

namespace AITownNPCs.Common.Configs.UI
{
	/// <summary>Keeps a field in JSON/config but occupies no UI space.</summary>
	public sealed class HiddenConfigElement : ConfigElement
	{
		public override void OnBind() {
			base.OnBind();
			Height.Set(0f, 0f);
			Width.Set(0f, 0f);
			IgnoresMouseInteraction = true;
		}

		public override void Draw(SpriteBatch spriteBatch) {
			// intentionally empty
		}
	}
}
