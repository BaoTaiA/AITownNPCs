using System;
using System.Collections.Generic;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Follow
{
	/// <summary>
	/// Wing-slot flight. While flying, both axes chase the air slot with the same inertia.
	/// Wing time stays full. Ascent still flaps; descent does not hold hover.
	/// </summary>
	public static class NpcWingFlight
	{
		private const float PlayerGravity = 0.4f;
		private const float MaxFallSpeed = 10f;
		private const float ChaseSettle = 12f;
		private const int DetachedWhoAmI = 255;

		private static Player _probe;
		private static bool _drawLayerBroken;
		private static readonly List<DrawData> DrawCache = new();
		private static readonly List<int> DustCache = new();
		private static readonly List<int> GoreCache = new();

		public static bool HasWings(NPC npc) {
			return TryGetWing(npc, out _);
		}

		public static bool TryGetWing(NPC npc, out Item wing) {
			wing = null;
			if (npc == null) {
				return false;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			if (equip?.Slots == null) {
				return false;
			}

			int index = NpcEquipProfileRegistry.Get(npc.type).FindWingSlotIndex();
			if (index < 0 || index >= equip.Slots.Length) {
				return false;
			}

			Item item = equip.Slots[index];
			if (item == null || item.IsAir || item.wingSlot <= 0) {
				return false;
			}

			wing = item;
			return true;
		}

		public static void TickAnimation(NPC npc, FollowingTownNPC follow, bool flapping) {
			if (follow == null) {
				return;
			}

			if (!TryGetWing(npc, out Item wing)) {
				follow.ResetWings();
				return;
			}

			Player probe = Prepare(npc, wing);
			bool always = false;
			try {
				always = probe.ShouldDrawWingsThatAreAlwaysAnimated();
			}
			catch (Exception) {
				always = false;
			}

			bool grounded = FollowMovement.IsOnGroundOrPlatform(npc.position, npc.width, npc.height);
			bool animate = !grounded || always;
			if (flapping && !grounded) {
				animate = true;
			}
			probe.controlJump = animate;
			probe.velocity.Y = animate ? -1f : 0f;
			if (!animate) {
				probe.wingFrame = 0;
				probe.wingFrameCounter = 0;
			}
			else {
				probe.wingFrame = follow.WingFrame;
				probe.wingFrameCounter = follow.WingFrameCounter;
				try {
					probe.WingFrame(true, false);
				}
				catch (Exception) {
					// Detached player has no slot. Keep the last frame.
				}
			}

			follow.WingFrame = probe.wingFrame;
			follow.WingFrameCounter = probe.wingFrameCounter;
		}

		/// <summary>Dust and wing sounds at the NPC. Does not write velocity back.</summary>
		public static void PlayVisuals(NPC npc) {
			if (Main.dedServ || !TryGetWing(npc, out Item wing)) {
				return;
			}

			Player probe = Prepare(npc, wing);
			probe.velocity = npc.velocity;
			probe.controlJump = true;
			probe.wingTime = probe.wingTimeMax > 0 ? probe.wingTimeMax : 60f;
			try {
				ItemLoader.WingUpdate(probe, true);
			}
			catch (Exception) {
				// Mod wings that expect a live player slot must not break the NPC.
			}

			probe.wingTime = probe.wingTimeMax > 0 ? probe.wingTimeMax : 60f;
		}

		public static void Apply(NPC npc, Vector2 goal, ref int stuckTimer) {
			npc.rotation = 0f;
			if (!TryGetWing(npc, out Item wing)) {
				npc.noGravity = false;
				return;
			}

			SteerAxis(npc, goal.X - npc.Center.X, vertical: false);
			SteerAxis(npc, goal.Y - npc.Center.Y, vertical: true);

			bool wantAscend = goal.Y < npc.Center.Y - ChaseSettle;
			if (!Main.dedServ) {
				Player probe = Prepare(npc, wing);
				probe.velocity = npc.velocity;
				probe.controlJump = wantAscend;
				probe.controlUp = wantAscend;
				probe.controlDown = false;
				probe.jump = 0;
				probe.wingTime = probe.wingTimeMax > 0 ? probe.wingTimeMax : 60f;
				try {
					ItemLoader.WingUpdate(probe, wantAscend || Math.Abs(npc.velocity.Y) > 0.05f);
				}
				catch (Exception) {
					// Visual-only hook.
				}

				probe.wingTime = probe.wingTimeMax > 0 ? probe.wingTimeMax : 60f;
			}

			npc.noGravity = true;
			npc.rotation = 0f;

			if (Math.Abs(npc.velocity.X) > 0.15f) {
				npc.direction = npc.velocity.X > 0f ? 1 : -1;
				npc.spriteDirection = npc.direction;
			}

			bool blocked = Collision.SolidCollision(npc.position + npc.velocity, npc.width, npc.height);
			stuckTimer = blocked ? stuckTimer + 1 : 0;
		}

		public static void Draw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, int wingFrame) {
			if (Main.dedServ || spriteBatch == null || !TryGetWing(npc, out Item wing)) {
				return;
			}

			if (!_drawLayerBroken && DrawWithPlayerLayer(npc, spriteBatch, wing, wingFrame)) {
				return;
			}

			DrawTextureFallback(npc, spriteBatch, screenPos, drawColor, wing, wingFrame);
		}

		private static void SteerAxis(NPC npc, float delta, bool vertical) {
			float abs = Math.Abs(delta);
			if (abs < ChaseSettle) {
				if (vertical) {
					npc.velocity.Y *= 0.9f;
				}
				else {
					npc.velocity.X *= 0.9f;
				}

				return;
			}

			float speed = MathHelper.Clamp(abs / 80f * FollowMovement.MaxSpeedX, 3f, FollowMovement.MaxSpeedX);
			float desired = Math.Sign(delta) * speed;
			const float inertia = 12f;
			if (vertical) {
				npc.velocity.Y = (npc.velocity.Y * inertia + desired) / (inertia + 1f);
			}
			else {
				npc.velocity.X = (npc.velocity.X * inertia + desired) / (inertia + 1f);
			}
		}

		private static bool DrawWithPlayerLayer(NPC npc, SpriteBatch spriteBatch, Item wing, int wingFrame) {
			try {
				Player probe = Prepare(npc, wing);
				probe.wingFrame = wingFrame;
				probe.fullRotation = 0f;
				probe.velocity = Vector2.Zero;
				if (!Main.dedServ) {
					Main.instance?.LoadWings(wing.wingSlot);
				}

				DrawCache.Clear();
				DustCache.Clear();
				GoreCache.Clear();
				PlayerDrawSet drawSet = default;
				drawSet.BoringSetup(probe, DrawCache, DustCache, GoreCache, probe.position, 0f, 0f, Vector2.Zero);
				PlayerDrawLayers.DrawPlayer_09_Wings(ref drawSet);
				if (drawSet.DrawDataCache == null || drawSet.DrawDataCache.Count == 0) {
					return false;
				}

				for (int i = 0; i < drawSet.DrawDataCache.Count; i++) {
					DrawData data = drawSet.DrawDataCache[i];
					if (data.texture == null) {
						continue;
					}

					spriteBatch.Draw(
						data.texture,
						data.position,
						data.sourceRect,
						data.color,
						0f,
						data.origin,
						data.scale,
						data.effect,
						0f);
				}

				return true;
			}
			catch (Exception ex) {
				if (!_drawLayerBroken) {
					_drawLayerBroken = true;
					ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
						$"Wing draw layer failed, using the wing texture instead: {ex.GetBaseException().Message}");
				}

				return false;
			}
		}

		private static void DrawTextureFallback(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, Item wing, int wingFrame) {
			int slot = wing.wingSlot;
			if (slot <= 0) {
				return;
			}

			Main.instance?.LoadWings(slot);
			if (TextureAssets.Wings[slot] == null || !TextureAssets.Wings[slot].IsLoaded) {
				return;
			}

			Texture2D texture = TextureAssets.Wings[slot].Value;
			int frames = FrameCount(texture.Height);
			int frameHeight = texture.Height / frames;
			int frame = wingFrame % frames;
			if (frame < 0) {
				frame = 0;
			}

			Rectangle source = new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);
			Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
			SpriteEffects effects = npc.spriteDirection == 1
				? SpriteEffects.FlipHorizontally
				: SpriteEffects.None;
			Vector2 anchor = new Vector2(
				npc.position.X + npc.width * 0.5f - npc.spriteDirection * 4f,
				npc.position.Y + npc.height * 0.32f + npc.gfxOffY);
			spriteBatch.Draw(texture, anchor - screenPos, source, npc.GetAlpha(drawColor), 0f, origin, 1f, effects, 0f);
		}

		private static int FrameCount(int textureHeight) {
			int[] candidates = { 4, 3, 5, 6, 8, 1 };
			for (int i = 0; i < candidates.Length; i++) {
				int frames = candidates[i];
				if (frames > 0 && textureHeight % frames == 0 && textureHeight / frames >= 8) {
					return frames;
				}
			}

			return 1;
		}

		private static Player Prepare(NPC npc, Item wing) {
			Player probe = EnsureProbe();
			probe.whoAmI = DetachedWhoAmI;
			probe.active = true;
			probe.dead = false;
			probe.ghost = false;
			probe.gravDir = 1f;
			probe.gravity = PlayerGravity;
			probe.maxFallSpeed = MaxFallSpeed;
			probe.direction = npc.spriteDirection == 0 ? npc.direction : npc.spriteDirection;
			probe.position = npc.position + new Vector2(-probe.direction * 2f, -4f);
			probe.width = npc.width;
			probe.height = npc.height;
			probe.gfxOffY = npc.gfxOffY;
			probe.fullRotation = 0f;
			probe.velocity = Vector2.Zero;
			probe.oldVelocity = Vector2.Zero;
			probe.controlJump = false;
			probe.controlUp = false;
			probe.controlDown = false;
			probe.controlLeft = false;
			probe.controlRight = false;
			probe.releaseJump = false;
			probe.jump = 0;
			probe.rocketDelay2 = 0;
			probe.rocketTime = 0;
			probe.pulley = false;
			if (probe.grappling != null && probe.grappling.Length > 0) {
				probe.grappling[0] = -1;
			}

			int frameWidth = Math.Max(npc.width, 1);
			int frameHeight = Math.Max(npc.height, 1);
			probe.bodyFrame = new Rectangle(0, 0, frameWidth, frameHeight);
			probe.legFrame = probe.bodyFrame;

			EquipWing(probe, wing);
			int slot = wing.wingSlot;
			probe.wings = slot;
			probe.wingsLogic = slot;
			WingStats stats = probe.GetWingStats(slot);
			probe.wingTimeMax = stats.FlyTime > 0 ? stats.FlyTime : 60;
			probe.wingTime = probe.wingTimeMax;
			return probe;
		}

		private static void EquipWing(Player probe, Item wing) {
			if (probe.armor == null || probe.armor.Length < 20) {
				probe.armor = new Item[20];
			}

			for (int i = 0; i < probe.armor.Length; i++) {
				probe.armor[i] ??= new Item();
				if (i != 3 && !probe.armor[i].IsAir) {
					probe.armor[i].TurnToAir();
				}
			}

			Item slot = probe.armor[3];
			if (slot.type != wing.type || slot.prefix != wing.prefix) {
				slot.SetDefaults(wing.type);
				if (wing.prefix != 0) {
					slot.Prefix(wing.prefix);
				}
			}
		}

		private static Player EnsureProbe() {
			if (_probe != null) {
				return _probe;
			}

			_probe = new Player();
			_probe.whoAmI = DetachedWhoAmI;
			return _probe;
		}
	}
}
