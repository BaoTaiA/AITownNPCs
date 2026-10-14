using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Isolates a dummy as the local player for Update or projectile AI, without the real player's input or cursor.
	/// </summary>
	public static class NpcDummyLocal
	{
		private static readonly Dictionary<int, bool> LastWantUse = new();
		private static readonly Dictionary<int, Vector2> AimWorld = new();
		private static readonly Stack<ProjFrame> ProjFrames = new();

		/// <summary>Reused save buffers — Enter/Leave must not allocate TriggersSet per tick.</summary>
		private static readonly TriggersSet SavedCurrent = new();
		private static readonly TriggersSet SavedOld = new();
		private static readonly TriggersSet SavedJustPressed = new();
		private static readonly TriggersSet SavedJustReleased = new();

		private static bool _active;
		private static int _savedMyPlayer;
		private static int _savedMouseX;
		private static int _savedMouseY;
		private static bool _savedMouseLeft;
		private static bool _savedMouseRight;
		private static bool _savedInventory;
		private static Vector2 _savedScreen;

		public static bool IsRunning => _active;

		public static bool WasWantUse(int dummyWho) {
			return LastWantUse.TryGetValue(dummyWho, out bool use) && use;
		}

		public static void RememberAim(int dummyWho, Vector2 aimWorld) {
			if (dummyWho < 0) {
				return;
			}

			AimWorld[dummyWho] = aimWorld;
		}

		public static void Enter(Player dummy, Vector2 aimWorld, bool wantUse) {
			if (_active) {
				Leave(dummy, wantUse: false);
			}

			bool wasDown = LastWantUse.TryGetValue(dummy.whoAmI, out bool last) && last;
			RememberAim(dummy.whoAmI, aimWorld);
			LastWantUse[dummy.whoAmI] = wantUse;

			_savedMyPlayer = Main.myPlayer;
			_savedMouseX = Main.mouseX;
			_savedMouseY = Main.mouseY;
			_savedMouseLeft = Main.mouseLeft;
			_savedMouseRight = Main.mouseRight;
			_savedInventory = Main.playerInventory;
			_savedScreen = Main.screenPosition;

			CopyTriggers(SavedCurrent, PlayerInput.Triggers.Current);
			CopyTriggers(SavedOld, PlayerInput.Triggers.Old);
			CopyTriggers(SavedJustPressed, PlayerInput.Triggers.JustPressed);
			CopyTriggers(SavedJustReleased, PlayerInput.Triggers.JustReleased);

			ApplyUseTriggers(wantUse, wasDown);

			Main.myPlayer = dummy.whoAmI;
			Main.mouseLeft = wantUse;
			Main.mouseRight = false;
			Main.mouseX = (int)(aimWorld.X - Main.screenPosition.X);
			Main.mouseY = (int)(aimWorld.Y - Main.screenPosition.Y);
			dummy.controlUseItem = wantUse;
			dummy.controlUseTile = false;
			dummy.controlHook = false;
			dummy.controlJump = false;
			dummy.controlInv = false;
			dummy.controlMap = false;
			dummy.controlThrow = false;
			dummy.controlLeft = false;
			dummy.controlRight = false;
			dummy.controlUp = false;
			dummy.controlDown = false;
			_active = true;
		}

		public static void Leave(Player dummy, bool wantUse) {
			if (!_active) {
				return;
			}

			if (dummy != null) {
				dummy.controlUseItem = wantUse;
			}

			CopyTriggers(PlayerInput.Triggers.Current, SavedCurrent);
			CopyTriggers(PlayerInput.Triggers.Old, SavedOld);
			CopyTriggers(PlayerInput.Triggers.JustPressed, SavedJustPressed);
			CopyTriggers(PlayerInput.Triggers.JustReleased, SavedJustReleased);

			Main.mouseX = _savedMouseX;
			Main.mouseY = _savedMouseY;
			Main.mouseLeft = _savedMouseLeft;
			Main.mouseRight = _savedMouseRight;
			Main.playerInventory = _savedInventory;
			Main.screenPosition = _savedScreen;

			Main.myPlayer = _savedMyPlayer;
			_active = false;
		}

		public static void Forget(int dummyWho) {
			LastWantUse.Remove(dummyWho);
			AimWorld.Remove(dummyWho);
		}

		public static void PushForProjectile(int dummyWho) {
			ProjFrames.Push(new ProjFrame {
				MyPlayer = Main.myPlayer,
				MouseX = Main.mouseX,
				MouseY = Main.mouseY,
				MouseLeft = Main.mouseLeft,
				MouseRight = Main.mouseRight
			});

			Main.myPlayer = dummyWho;
			if (AimWorld.TryGetValue(dummyWho, out Vector2 aim)) {
				Main.mouseX = (int)(aim.X - Main.screenPosition.X);
				Main.mouseY = (int)(aim.Y - Main.screenPosition.Y);
			}

			bool wantUse = LastWantUse.TryGetValue(dummyWho, out bool use) && use;
			Main.mouseLeft = wantUse;
			Main.mouseRight = false;
			if (dummyWho >= 0 && dummyWho < Main.maxPlayers && Main.player[dummyWho] != null) {
				Main.player[dummyWho].controlUseItem = wantUse;
			}
		}

		public static void PopForProjectile() {
			if (ProjFrames.Count == 0) {
				return;
			}

			ProjFrame frame = ProjFrames.Pop();
			Main.myPlayer = frame.MyPlayer;
			Main.mouseX = frame.MouseX;
			Main.mouseY = frame.MouseY;
			Main.mouseLeft = frame.MouseLeft;
			Main.mouseRight = frame.MouseRight;
		}

		public static void DrainProjFrames() {
			while (ProjFrames.Count > 0) {
				PopForProjectile();
			}
		}

		private static void ApplyUseTriggers(bool wantUse, bool wasDown) {
			ClearMovementAndUse(PlayerInput.Triggers.Current);
			ClearMovementAndUse(PlayerInput.Triggers.JustPressed);
			ClearMovementAndUse(PlayerInput.Triggers.JustReleased);

			PlayerInput.Triggers.Current.MouseLeft = wantUse;
			PlayerInput.Triggers.JustPressed.MouseLeft = wantUse && !wasDown;
			PlayerInput.Triggers.JustReleased.MouseLeft = !wantUse && wasDown;
			PlayerInput.Triggers.Old.MouseLeft = wasDown;
		}

		private static void ClearMovementAndUse(TriggersSet set) {
			if (set == null) {
				return;
			}

			set.MouseLeft = false;
			set.MouseRight = false;
			set.MouseMiddle = false;
			set.Inventory = false;
			set.Grapple = false;
			set.Jump = false;
			set.QuickMount = false;
			set.QuickHeal = false;
			set.QuickMana = false;
			set.QuickBuff = false;
			set.MapStyle = false;
			set.MapFull = false;
			set.MapAlphaDown = false;
			set.MapAlphaUp = false;
			set.SmartCursor = false;
			set.SmartSelect = false;
			set.Up = false;
			set.Down = false;
			set.Left = false;
			set.Right = false;
		}

		private static void CopyTriggers(TriggersSet dest, TriggersSet src) {
			if (dest == null || src == null) {
				return;
			}

			dest.MouseLeft = src.MouseLeft;
			dest.MouseRight = src.MouseRight;
			dest.MouseMiddle = src.MouseMiddle;
			dest.Inventory = src.Inventory;
			dest.Grapple = src.Grapple;
			dest.Jump = src.Jump;
			dest.QuickMount = src.QuickMount;
			dest.QuickHeal = src.QuickHeal;
			dest.QuickMana = src.QuickMana;
			dest.QuickBuff = src.QuickBuff;
			dest.MapStyle = src.MapStyle;
			dest.MapFull = src.MapFull;
			dest.MapAlphaDown = src.MapAlphaDown;
			dest.MapAlphaUp = src.MapAlphaUp;
			dest.SmartCursor = src.SmartCursor;
			dest.SmartSelect = src.SmartSelect;
			dest.Up = src.Up;
			dest.Down = src.Down;
			dest.Left = src.Left;
			dest.Right = src.Right;
		}

		private struct ProjFrame
		{
			public int MyPlayer;
			public int MouseX;
			public int MouseY;
			public bool MouseLeft;
			public bool MouseRight;
		}
	}
}
