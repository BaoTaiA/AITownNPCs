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
