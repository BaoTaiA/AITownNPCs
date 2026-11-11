using System;
using System.Reflection;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Host;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Redirects Thorium's inspiration wheel from LocalPlayer onto the combat dummy
	/// locked to a followed town NPC that is playing a bard instrument.
	/// When the dummy is released on ceasefire, suppress the leftover Fade on the human head.
	/// </summary>
	public sealed class NpcThoriumInspirationUi : ModSystem
	{
		/// <summary>Keep suppressing LocalPlayer inspiration draw after dummy release (Thorium Fade).</summary>
		private const uint CeasefireSuppressFrames = 90;

		private static Type _bardItemType;
		private static PropertyInfo _bardWheelFadeTime;
		private static Hook _layerDrawHook;
		private static bool _drawing;
		private static bool _thoriumPresent;
		private static uint _suppressUntil;
		private static int _fadeNpcWho = -1;

		private delegate bool LayerDrawOrig(GameInterfaceLayer self);

		private enum DrawMode : byte
		{
			Normal,
			RedirectDummy,
			Suppress
		}

		public override void PostSetupContent() {
			UnloadHooks();
			_thoriumPresent = ModLoader.TryGetMod("ThoriumMod", out Mod thorium) && thorium.Code != null;
			if (!_thoriumPresent) {
				return;
			}

			_bardItemType = thorium.Code.GetType("ThoriumMod.Items.BardItems.BardItem")
				?? FindTypeByName(thorium.Code, "BardItem");

			// BardWheel only lights when LocalPlayer inspiration is below max. NPC spend
			// used to trip that by accident via the stuck LocalPlayer Thorium cache; after
			// the cache fix we must raise FadeTime ourselves when redirecting onto a dummy.
			Type bardWheel = thorium.Code.GetType("ThoriumMod.UI.ResourceBars.BardWheel")
				?? FindTypeByName(thorium.Code, "BardWheel");
			_bardWheelFadeTime = bardWheel?.GetProperty(
				"BardWheelFadeTime",
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

			MethodInfo draw = typeof(GameInterfaceLayer).GetMethod(
				"Draw",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				binder: null,
				types: Type.EmptyTypes,
				modifiers: null);
			if (draw == null) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
					"GameInterfaceLayer.Draw not found; NPC inspiration UI redirect disabled.");
				return;
			}

			try {
				_layerDrawHook = new Hook(draw, OnLayerDraw);
			}
			catch (Exception ex) {
				ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
					$"Inspiration UI layer hook failed: {ex.Message}");
				_layerDrawHook = null;
			}
		}

		public override void Unload() {
			UnloadHooks();
		}

		private static void UnloadHooks() {
			_layerDrawHook?.Dispose();
			_layerDrawHook = null;
			_bardItemType = null;
			_bardWheelFadeTime = null;
			_thoriumPresent = false;
			_drawing = false;
			_suppressUntil = 0;
			_fadeNpcWho = -1;
		}

		private static bool OnLayerDraw(LayerDrawOrig orig, GameInterfaceLayer self) {
			if (!_thoriumPresent || self == null || !IsInspirationLayerName(self.Name)) {
				return orig(self);
			}

			if (_drawing || NpcDummyLocal.IsRunning || Main.dedServ) {
				return orig(self);
			}

			Player local = Main.LocalPlayer;
			if (local == null || !local.active) {
				return orig(self);
			}

			DrawMode mode = ResolveDrawMode(local, out Player dummy);
			if (mode == DrawMode.Suppress) {
				return true;
			}

			if (mode == DrawMode.RedirectDummy && dummy != null) {
				PulseBardWheelFade();
				using (new MyPlayerScope(dummy.whoAmI)) {
					return orig(self);
				}
			}

			return orig(self);
		}

		/// <summary>Thorium skips DrawSelf when BardWheelFadeTime is 0.</summary>
		private static void PulseBardWheelFade() {
			if (_bardWheelFadeTime == null || !_bardWheelFadeTime.CanWrite) {
				return;
			}

			try {
				_bardWheelFadeTime.SetValue(null, 40);
			}
			catch {
				// ignore
			}
		}

		private static bool IsInspirationLayerName(string name) {
			if (string.IsNullOrEmpty(name)) {
				return false;
			}

			if (name.IndexOf("Inspiration", StringComparison.OrdinalIgnoreCase) >= 0) {
				return true;
			}

			if (name.IndexOf("BardWheel", StringComparison.OrdinalIgnoreCase) >= 0) {
				return true;
			}

			bool bard = name.IndexOf("Bard", StringComparison.OrdinalIgnoreCase) >= 0;
			bool wheel = name.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0;
			return bard && wheel;
		}

		private static Type FindTypeByName(Assembly asm, string simpleName) {
			foreach (Type t in asm.GetTypes()) {
				if (t.Name == simpleName) {
					return t;
				}
			}

			return null;
		}

		private static DrawMode ResolveDrawMode(Player local, out Player dummy) {
			dummy = null;
			if (IsBardUser(local)) {
				_suppressUntil = 0;
				_fadeNpcWho = -1;
				return DrawMode.Normal;
			}

			Player spending = null;
			int spendingNpc = -1;
			float bestDist = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!NpcHostRules.IsManagedTownNpc(npc)) {
					continue;
				}

				FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
				if (!follow.IsFollowing || follow.FollowPlayerWhoAmI != local.whoAmI) {
					continue;
				}

				EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
				if (!IsBardItem(equip.Weapon)) {
					continue;
				}

				if (!NpcDummyPool.TryGetDummy(npc.whoAmI, out Player found) || found == null || !found.active) {
					continue;
				}

				if (!IsSpendingInspiration(found)) {
					continue;
				}

				float dist = Vector2.DistanceSquared(local.Center, npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					spending = found;
					spendingNpc = npc.whoAmI;
				}
			}

			if (spending != null) {
				dummy = spending;
				_fadeNpcWho = spendingNpc;
				_suppressUntil = Main.GameUpdateCount + CeasefireSuppressFrames;
				return DrawMode.RedirectDummy;
			}

			// Thorium's fade is one global layer. Keep it on the NPC that just spent, then hide it.
			// Another followed bard NPC must not steal the layer, and it must not fall back onto this player.
			if (TryFadeDummy(local, out Player fadeDummy)) {
				dummy = fadeDummy;
				return DrawMode.RedirectDummy;
			}

			_fadeNpcWho = -1;
			return DrawMode.Suppress;
		}

		private static bool IsSpendingInspiration(Player dummy) {
			return dummy.itemAnimation > 0 || dummy.itemTime > 0 || dummy.channel;
		}

		private static bool TryFadeDummy(Player local, out Player dummy) {
			dummy = null;
			if (_fadeNpcWho < 0 || Main.GameUpdateCount >= _suppressUntil) {
				return false;
			}

			if (_fadeNpcWho >= Main.maxNPCs) {
				return false;
			}

			NPC npc = Main.npc[_fadeNpcWho];
			if (!NpcHostRules.IsManagedTownNpc(npc)) {
				return false;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing || follow.FollowPlayerWhoAmI != local.whoAmI) {
				return false;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			if (!IsBardItem(equip.Weapon)) {
				return false;
			}

			if (!NpcDummyPool.TryGetDummy(npc.whoAmI, out Player found) || found == null || !found.active) {
				return false;
			}

			dummy = found;
			return true;
		}

		private static bool IsBardUser(Player player) {
			if (player == null || !player.active) {
				return false;
			}

			if (IsBardItem(player.HeldItem)) {
				return true;
			}

			Item selected = player.inventory != null
				&& player.selectedItem >= 0
				&& player.selectedItem < player.inventory.Length
					? player.inventory[player.selectedItem]
					: null;
			return IsBardItem(selected) && (player.itemAnimation > 0 || player.channel);
		}

		private static bool IsBardItem(Item item) {
			if (item == null || item.IsAir || item.ModItem == null) {
				return false;
			}

			if (_bardItemType != null) {
				return _bardItemType.IsInstanceOfType(item.ModItem);
			}

			string full = item.ModItem.GetType().FullName;
			return full != null
				&& full.IndexOf("BardItem", StringComparison.Ordinal) >= 0
				&& item.ModItem.Mod?.Name == "ThoriumMod";
		}

		private sealed class MyPlayerScope : IDisposable
		{
			private readonly int _saved;
			private readonly bool _active;

			public MyPlayerScope(int dummyWho) {
				_saved = Main.myPlayer;
				if (dummyWho < 0 || dummyWho >= Main.maxPlayers || dummyWho == _saved) {
					_active = false;
					return;
				}

				_drawing = true;
				Main.myPlayer = dummyWho;
				_active = true;
			}

			public void Dispose() {
				if (!_active) {
					return;
				}

				Main.myPlayer = _saved;
				_drawing = false;
			}
		}
	}
}
