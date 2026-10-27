using AITownNPCs.Common.Net;
using AITownNPCs.Common.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.Guide
{
	public sealed class GuideGuidanceBuff : ModBuff
	{
		public const int DurationTicks = 60 * 60 * 10;

		public override string Texture => "AITownNPCs/Content/Buffs/GuideGuidance";

		public override void SetStaticDefaults() {
			Main.buffNoSave[Type] = true;
			Main.debuff[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex) {
			player.GetModPlayer<GuideGuidancePlayer>().HasGuidance = true;
		}
	}

	public sealed class GuideGuidancePlayer : ModPlayer
	{
		public bool HasGuidance;
		public bool HasTarget;
		public Vector2 TargetWorld;

		public override void ResetEffects() {
			HasGuidance = false;
		}

		public override void PostUpdate() {
			if (!HasGuidance || !Player.HasBuff(ModContent.BuffType<GuideGuidanceBuff>())) {
				HasTarget = false;
			}
		}

		/// <summary>Server/SP entry: apply locally and sync target + buff to the owning client.</summary>
		public void StartGuidance(Vector2 targetTile) {
			ApplyGuidanceLocal(targetTile, GuideGuidanceBuff.DurationTicks);
			if (Main.netMode == NetmodeID.Server) {
				SuperpowerNetSystem.SendGuidanceStart(Player.whoAmI, targetTile, GuideGuidanceBuff.DurationTicks);
				ServerPlayerNet.AddBuffSynced(Player, ModContent.BuffType<GuideGuidanceBuff>(), GuideGuidanceBuff.DurationTicks);
			}
			else {
				ServerPlayerNet.AddBuffSynced(Player, ModContent.BuffType<GuideGuidanceBuff>(), GuideGuidanceBuff.DurationTicks);
			}
		}

		/// <summary>Client: GuidanceStart packet.</summary>
		public void ApplyGuidanceFromServer(Vector2 targetTile, int durationTicks) {
			ApplyGuidanceLocal(targetTile, durationTicks);
			Player.AddBuff(ModContent.BuffType<GuideGuidanceBuff>(), durationTicks, quiet: true);
		}

		private void ApplyGuidanceLocal(Vector2 targetTile, int durationTicks) {
			_ = durationTicks;
			TargetWorld = targetTile * 16f + new Vector2(8f, 8f);
			HasTarget = true;
		}
	}

	public sealed class GuideGuidanceArrowLayer : PlayerDrawLayer
	{
		public const int FrameCount = 4;
		public const int FrameWidth = 16;
		public const int FrameHeight = 24;
		public const int TicksPerFrame = 6;

		private static Asset<Texture2D> _sheet;

		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
			Player player = drawInfo.drawPlayer;
			if (player == null || player.dead || player.invis) {
				return false;
			}

			var mp = player.GetModPlayer<GuideGuidancePlayer>();
			return mp.HasGuidance && mp.HasTarget && player.HasBuff(ModContent.BuffType<GuideGuidanceBuff>());
		}

		protected override void Draw(ref PlayerDrawSet drawInfo) {
			_sheet ??= ModContent.Request<Texture2D>(
				"AITownNPCs/Content/Textures/GuideGuidance/Arrow",
				AssetRequestMode.AsyncLoad);
			Texture2D tex = _sheet.Value;
			if (tex == null) {
				return;
			}

			Player player = drawInfo.drawPlayer;
			var mp = player.GetModPlayer<GuideGuidancePlayer>();
			Vector2 delta = mp.TargetWorld - player.Center;
			float rotation = DirectionToRotation(delta);

			int frame = (int)(Main.GameUpdateCount / TicksPerFrame) % FrameCount;
			var src = new Rectangle(0, frame * FrameHeight, FrameWidth, FrameHeight);
			Vector2 origin = new(FrameWidth / 2f, FrameHeight / 2f);
			const float drawScale = 1.75f;
			float lift = 36f + FrameHeight * drawScale * 0.35f;
			Vector2 pos = player.MountedCenter - Main.screenPosition + new Vector2(0f, player.gfxOffY - lift);

			drawInfo.DrawDataCache.Add(new DrawData(
				tex,
				pos,
				src,
				Color.White,
				rotation,
				origin,
				drawScale,
				SpriteEffects.None
			));
		}

		private static float DirectionToRotation(Vector2 delta) {
			string dir = WorldQuery.PrimaryDir(delta.X, delta.Y);
			return dir switch {
				"右" => MathHelper.PiOver2,
				"左" => -MathHelper.PiOver2,
				"下" => MathHelper.Pi,
				_ => 0f
			};
		}
	}
}
