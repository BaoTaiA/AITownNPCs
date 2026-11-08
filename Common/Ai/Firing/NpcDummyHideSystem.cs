using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	public class NpcDummyModPlayer : ModPlayer
	{
		public override bool OnPickup(Item item) {
			return !NpcDummyPool.IsDummy(Player.whoAmI);
		}

		public override void ResetEffects() {
			if (!NpcDummyPool.IsDummy(Player.whoAmI)) {
				return;
			}

			Player.gravity = 0f;
			Player.maxFallSpeed = 0f;
			NpcDummyPool.FillMana(Player);
			NpcDummyPool.KeepAlive(Player);
		}

		public override void PreUpdateMovement() {
			if (!NpcDummyPool.IsDummy(Player.whoAmI)) {
				return;
			}

			Player.gravity = 0f;
			Player.maxFallSpeed = 0f;
			Player.velocity = Vector2.Zero;
			Player.controlUseTile = false;
			Player.controlHook = false;
			Player.controlJump = false;
			Player.controlLeft = false;
			Player.controlRight = false;
			Player.controlUp = false;
			Player.controlDown = false;
		}

		public override void PostUpdate() {
			if (!NpcDummyPool.IsDummy(Player.whoAmI)) {
				return;
			}

			if (!NpcDummyPool.TryGetNpcWho(Player.whoAmI, out int npcWho)
				|| npcWho < 0
				|| npcWho >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWho];
			if (npc == null || !npc.active) {
				return;
			}

			NpcDummyPool.LockPose(Player, npc);
		}

		public override bool PreItemCheck() {
			if (!NpcDummyPool.IsDummy(Player.whoAmI)) {
				return true;
			}

			Player.wet = false;
			Player.lavaWet = false;
			Player.honeyWet = false;
			Player.shimmerWet = false;
			return true;
		}

		public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) {
			return !NpcDummyPool.IsDummy(Player.whoAmI);
		}

		public override bool CanBeHitByProjectile(Projectile proj) {
			return !NpcDummyPool.IsDummy(Player.whoAmI);
		}

		public override void ModifyHitNPCWithItem(Item item, NPC npc, ref NPC.HitModifiers modifiers) {
			if (!NpcDummyPool.IsDummy(Player.whoAmI)) {
				return;
			}

			if (IsSpectatorDummy(Player.whoAmI)) {
				// Numbers/life sync come from server SendStrikeNPC; do not invent local CombatText.
				modifiers.FinalDamage *= 0f;
				modifiers.Knockback *= 0f;
				modifiers.DisableCrit();
				return;
			}

			if (NpcDummyPool.TryGetNpcWho(Player.whoAmI, out int npcWho)
				&& npcWho >= 0
				&& npcWho < Main.maxNPCs) {
				NPC town = Main.npc[npcWho];
				if (town != null && town.active) {
					NpcDamageMods.ApplyToHitModifiers(town, ref modifiers);
				}
			}
		}

		public override void ModifyHitNPCWithProj(Projectile proj, NPC npc, ref NPC.HitModifiers modifiers) {
			// SpectatorVisual projectiles are handled in NpcWeaponProjectile.ModifyHitNPC.
			if (proj != null && NpcWeaponProjectile.IsSpectatorVisual(proj)) {
				return;
			}

			if (!IsSpectatorDummy(Player.whoAmI)) {
				return;
			}

			modifiers.FinalDamage *= 0f;
			modifiers.Knockback *= 0f;
			modifiers.DisableCrit();
		}

		private static bool IsSpectatorDummy(int dummyWho) {
			if (!NpcDummyPool.IsDummy(dummyWho)) {
				return false;
			}

			if (!NpcDummyPool.TryGetNpcWho(dummyWho, out int npcWho)
				|| npcWho < 0
				|| npcWho >= Main.maxNPCs) {
				return false;
			}

			NPC npc = Main.npc[npcWho];
			return NpcFiringState.IsSpectatorClient(npc);
		}

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) {
			return NpcDummyPool.IsDummy(Player.whoAmI);
		}

		public override bool FreeDodge(Player.HurtInfo info) {
			return NpcDummyPool.IsDummy(Player.whoAmI);
		}

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust,
			ref PlayerDeathReason damageSource) {
			return !NpcDummyPool.IsDummy(Player.whoAmI);
		}

		public override void HideDrawLayers(PlayerDrawSet drawInfo) {
			if (!NpcDummyPool.IsDummy(Player.whoAmI)) {
				return;
			}

			bool swinging = Player.itemAnimation > 0 || Player.channel || Player.heldProj >= 0;
			foreach (PlayerDrawLayer layer in PlayerDrawLayerLoader.Layers) {
				if (swinging && IsHeldVisual(layer)) {
					continue;
				}

				layer.Hide();
			}
		}

		private static bool IsHeldVisual(PlayerDrawLayer layer) {
			string name = layer.Name;
			return layer == PlayerDrawLayers.HeldItem
				|| layer == PlayerDrawLayers.ArmOverItem
				|| name.Contains("Held")
				|| name.Contains("Projectile");
		}
	}

	public class NpcDummyHideSystem : ModSystem
	{
		public override void Load() {
			On_NPC.FindClosestPlayer += FindClosestPlayer_NoDummy;
			On_NPC.TargetClosest += TargetClosest_NoDummy;
			On_Player.FindClosest += PlayerFindClosest_NoDummy;
			On_Player.Update += Update_SkipVanillaDummy;
			On_Player.UpdateBiomes += UpdateBiomes_SkipDummy;
			On_Player.ManageSpecialBiomeVisuals += ManageSpecialBiomeVisuals_SkipDummy;
			On_Player.ItemCheck_PlayInstruments += ItemCheck_PlayInstruments_SkipDummy;
			On_Player.SmartSelectLookup += SmartSelect_NoDummy;
			On_Player.SmartInteractLookup += SmartInteract_NoDummy;
			On_SmartCursorHelper.SmartCursorLookup += SmartCursor_NoDummy;
			On_Main.DrawMouseOver += DrawMouseOver_NoDummy;
			On_Main.DrawInterface_14_EntityHealthBars += DrawHealthBars_NoDummy;
			// Calamity (and similar) hooks DrawPlayer and assumes a fully-sized real player.
			On_Main.DrawPlayers_AfterProjectiles += DrawPlayers_NoDummy;
			On_Main.DrawPlayers_BehindNPCs += DrawPlayersBehind_NoDummy;
			// Dedicated has no achievements instance; Dummy Player.Update hits NRE in HandleRunning.
			On_AchievementsHelper.HandleRunning += HandleRunning_SkipDedServ;
		}

		public override void PostUpdateEverything() {
			NpcDummyPool.LockAllToNpcs();
			NpcDummyLocal.DrainProjFrames();
			// After every dummy/projectile borrow, recompute from the real mouse.
			NpcDummyLocal.RefreshLocalCursorIfNeeded();
		}

		private static void Update_SkipVanillaDummy(On_Player.orig_Update orig, Player self, int i) {
			if (self != null && NpcDummyPool.IsDummy(self.whoAmI) && !NpcDummyLocal.IsRunning) {
				return;
			}

			orig(self, i);
		}

		/// <summary>Dummy does not need biomes / special visuals (NRE on dedicated).</summary>
		private static void UpdateBiomes_SkipDummy(On_Player.orig_UpdateBiomes orig, Player self) {
			if (self != null && NpcDummyPool.IsDummy(self.whoAmI)) {
				return;
			}

			orig(self);
		}

		private static void ManageSpecialBiomeVisuals_SkipDummy(
			On_Player.orig_ManageSpecialBiomeVisuals orig,
			Player self,
			string biomeName,
			bool inZone,
			Vector2 activationSource) {
			if (self != null && NpcDummyPool.IsDummy(self.whoAmI)) {
				return;
			}

			orig(self, biomeName, inZone, activationSource);
		}

		private static void ItemCheck_PlayInstruments_SkipDummy(
			On_Player.orig_ItemCheck_PlayInstruments orig,
			Player self,
			Item sItem) {
			if (self != null && NpcDummyPool.IsDummy(self.whoAmI)) {
				return;
			}

			orig(self, sItem);
		}

		private static void HandleRunning_SkipDedServ(On_AchievementsHelper.orig_HandleRunning orig, float pixelsMoved) {
			if (Main.dedServ) {
				return;
			}

			try {
				orig(pixelsMoved);
			}
			catch (NullReferenceException) {
				// Listen-server / headless edge: achievements subsystem missing.
			}
		}

		private static int FindClosestPlayer_NoDummy(On_NPC.orig_FindClosestPlayer orig, NPC self) {
			return WithDummiesHidden(() => orig(self));
		}

		private static void TargetClosest_NoDummy(On_NPC.orig_TargetClosest orig, NPC self, bool faceTarget) {
			WithDummiesHidden(() => orig(self, faceTarget));
		}

		private static byte PlayerFindClosest_NoDummy(On_Player.orig_FindClosest orig, Vector2 position, int width, int height) {
			return WithDummiesHidden(() => orig(position, width, height));
		}

		private static void SmartSelect_NoDummy(On_Player.orig_SmartSelectLookup orig, Player self) {
			WithDummiesHidden(() => orig(self));
		}

		/// <summary>Borrowed dummy Update must not run the player's smart cursor.</summary>
		private static void SmartCursor_NoDummy(On_SmartCursorHelper.orig_SmartCursorLookup orig, Player player) {
			if (player != null && NpcDummyPool.IsDummy(player.whoAmI)) {
				return;
			}

			orig(player);
		}

		/// <summary>Right-click smart interact is global; dummy aim must not rewrite it.</summary>
		private static void SmartInteract_NoDummy(On_Player.orig_SmartInteractLookup orig, Player self) {
			if (self != null && NpcDummyPool.IsDummy(self.whoAmI)) {
				return;
			}

			orig(self);
		}

		private static void DrawMouseOver_NoDummy(On_Main.orig_DrawMouseOver orig, Main self) {
			WithDummiesHidden(() => orig(self));
		}

		private static void DrawHealthBars_NoDummy(On_Main.orig_DrawInterface_14_EntityHealthBars orig, Main self) {
			WithDummiesHidden(() => orig(self));
		}

		/// <summary>
		/// Let dummies draw HeldItem/ArmOverItem (body layers stay hidden via HideDrawLayers).
		/// Still suspend dummies for targeting/UI searches elsewhere.
		/// </summary>
		private static void DrawPlayers_NoDummy(On_Main.orig_DrawPlayers_AfterProjectiles orig, Main self) {
			orig(self);
		}

		private static void DrawPlayersBehind_NoDummy(On_Main.orig_DrawPlayers_BehindNPCs orig, Main self) {
			orig(self);
		}

		private static void WithDummiesHidden(Action action) {
			NpcDummyPool.SuspendForPlayerSearch();
			try {
				action();
			}
			finally {
				NpcDummyPool.ResumeAfterPlayerSearch();
			}
		}

		private static T WithDummiesHidden<T>(Func<T> action) {
			NpcDummyPool.SuspendForPlayerSearch();
			try {
				return action();
			}
			finally {
				NpcDummyPool.ResumeAfterPlayerSearch();
			}
		}
	}
}
