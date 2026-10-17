using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Per-NPC firing runtime: channel hold cycle, pose net throttle, equip stack sync.
	/// Combat strategy fields live on NpcCombatState.
	/// </summary>
	public class NpcFiringState : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		/// <summary>Hold-release: frames held this cycle before release.</summary>
		public int ChannelHoldTicks;

		/// <summary>Hold-release: true after windup; keep released until projectiles finish.</summary>
		public bool ChannelThrowing;

		/// <summary>DummyPose send throttle.</summary>
		public uint LastPoseSendTick;
		public int LastPoseItemAnimation = int.MinValue;
		public bool LastPoseChannel;
		public int LastPoseWeaponType = -1;
		public int LastPoseDirection = int.MinValue;
		public int LastPoseHeldProj = int.MinValue;

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

		/// <summary>Equip slot stack sync throttle while firing.</summary>
		public uint LastAmmoSyncTick;
		public uint LastWeaponSyncTick;
		public bool PendingAmmoSync;
		public bool PendingWeaponSync;

		public void ResetChannelCycle() {
			ChannelHoldTicks = 0;
			ChannelThrowing = false;
		}

		public void ResetUseCycle() {
			ResetChannelCycle();
		}
	}
}
