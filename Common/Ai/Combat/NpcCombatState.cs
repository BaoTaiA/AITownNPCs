using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Ai.Combat
{
	/// <summary>
	/// Per-NPC combat runtime: mode, sticky target, aim mirror for ExtraAI.
	/// </summary>
	public class NpcCombatState : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public int ItemAnimation;
		public int ItemAnimationMax;
		public float ItemRotation;
		public bool IsChanneling;

		public NpcCombatMode Mode = NpcCombatMode.FollowOnly;
		public int LockedBossWhoAmI = -1;
		public int NormalClearTicks;
		public int BossClearTicks;
		public int CachedTargetWhoAmI = -1;
		public bool HasEngagedTarget;
		public bool UseCombatFlight;
		public bool CombatWantDrop;
		public int MoveNetCooldown;
		/// <summary>Frames until next full acquire scan (boss + normal).</summary>
		public int TargetRefreshTicks;

		/// <summary>True when combat owns locomotion (Boss, or Normal with a target).</summary>
		public bool CombatControlsLocomotion =>
			Mode == NpcCombatMode.Boss
			|| (Mode == NpcCombatMode.Normal && HasEngagedTarget);

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bool busy = ItemAnimation > 0 || IsChanneling;
			bitWriter.WriteBit(busy);
			if (!busy) {
				return;
			}

			binaryWriter.Write((short)ItemAnimation);
			binaryWriter.Write((short)ItemAnimationMax);
			binaryWriter.Write(ItemRotation);
			bitWriter.WriteBit(IsChanneling);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			if (!bitReader.ReadBit()) {
				ItemAnimation = 0;
				ItemAnimationMax = 0;
				ItemRotation = 0f;
				IsChanneling = false;
				return;
			}

			ItemAnimation = binaryReader.ReadInt16();
			ItemAnimationMax = binaryReader.ReadInt16();
			ItemRotation = binaryReader.ReadSingle();
			IsChanneling = bitReader.ReadBit();
		}

		public void ResetUse() {
			ItemAnimation = 0;
		}

		public void ClearNormalTarget() {
			CachedTargetWhoAmI = -1;
			HasEngagedTarget = false;
			UseCombatFlight = false;
			CombatWantDrop = false;
		}

		public void ClearBossLock() {
			LockedBossWhoAmI = -1;
			BossClearTicks = 0;
		}

		public void EnterFollowOnly() {
			Mode = NpcCombatMode.FollowOnly;
			NormalClearTicks = 0;
			TargetRefreshTicks = 0;
			ClearNormalTarget();
			ClearBossLock();
		}
	}
}
