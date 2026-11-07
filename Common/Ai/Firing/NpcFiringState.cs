using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Per-NPC firing runtime: channel hold cycle, intent throttle, equip stack sync.
	/// Combat strategy fields live on NpcCombatState.
	/// HARD RULE: set <see cref="SpectatorClient"/> before any dummy Main.myPlayer swap;
	/// during the swap never re-derive authority from Main.myPlayer — only read this flag
	/// (or SpectatorVisual stamped on the projectile at spawn).
	/// </summary>
	public class NpcFiringState : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		/// <summary>Hold-release: frames held this cycle before release.</summary>
		public int ChannelHoldTicks;

		/// <summary>Hold-release: true after windup; keep released until projectiles finish.</summary>
		public bool ChannelThrowing;

		/// <summary>Server: the follower client still has a swing, channel, or projectile in flight.</summary>
		public bool RemoteBusy;

		/// <summary>Server: keep sending aim until the follower reports the use has ended.</summary>
		public bool IntentOpen;

		public bool HasPendingIntent;

		/// <summary>Any client: keep running the dummy until it returns to idle on its own.</summary>
		public bool ClientHoldingUse;

		/// <summary>Multiplayer: this client is simulating for visuals only (not the followed player).</summary>
		public bool SpectatorClient;

		public bool PendingWantUse;
		public Vector2 PendingAim;
		public int PendingWeaponType;
		public byte PendingWeaponPrefix;
		public short PendingWeaponStack;
		public int PendingAmmoType;
		public byte PendingAmmoPrefix;
		public short PendingAmmoStack;
		public bool NeedsReport;

		/// <summary>Server Intent throttle: last broadcast aim / stacks / tick.</summary>
		public uint LastIntentSendTick;
		public Vector2 LastIntentAim;
		public int LastIntentWeaponType = -1;
		public byte LastIntentWeaponPrefix;
		public short LastIntentWeaponStack = -1;
		public int LastIntentAmmoType = -1;
		public byte LastIntentAmmoPrefix;
		public short LastIntentAmmoStack = -1;

		/// <summary>Do not start another summon or sentry cast before this tick.</summary>
		public uint SummonRetryUntil;

		/// <summary>Last offhand summon/sentry type. -1 means none observed yet.</summary>
		public int TrackedSummonType = -1;

		public int TrackedSummonPrefix;

		/// <summary>Equip slot stack sync throttle while firing.</summary>
		public uint LastAmmoSyncTick;
		public uint LastWeaponSyncTick;
		public bool PendingAmmoSync;
		public bool PendingWeaponSync;

		/// <summary>Read only the frozen spectator flag — never re-derive from Main.myPlayer during a dummy swap.</summary>
		public static bool IsSpectatorClient(NPC npc) {
			return npc != null && npc.active && npc.GetGlobalNPC<NpcFiringState>().SpectatorClient;
		}

		/// <summary>Inverse of <see cref="IsSpectatorClient"/> for the frozen flag.</summary>
		public static bool IsAuthorityClient(NPC npc) {
			return npc != null && npc.active && !npc.GetGlobalNPC<NpcFiringState>().SpectatorClient;
		}

		public void ResetChannelCycle() {
			ChannelHoldTicks = 0;
			ChannelThrowing = false;
		}

		public void ResetUseCycle() {
			ResetChannelCycle();
		}

		public void ResetIntentThrottle() {
			LastIntentSendTick = 0;
			LastIntentAim = Vector2.Zero;
			LastIntentWeaponType = -1;
			LastIntentWeaponPrefix = 0;
			LastIntentWeaponStack = -1;
			LastIntentAmmoType = -1;
			LastIntentAmmoPrefix = 0;
			LastIntentAmmoStack = -1;
		}
	}
}
