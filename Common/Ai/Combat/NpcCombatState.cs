using System.IO;
using AITownNPCs.Common.Ai.Combat.Normal;
using AITownNPCs.Common.Host;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Ai.Combat
{
	/// <summary>
	/// Per-NPC combat runtime: mode, sticky target, aim mirror for ExtraAI.
	/// Scoped to managed town NPCs only (never enemies).
	/// </summary>
	public class NpcCombatState : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
			NpcHostRules.AppliesToManagedTownNpc(entity);

		public int ItemAnimation;
		public int ItemAnimationMax;
		public float ItemRotation;
		public bool IsChanneling;

		public NpcCombatMode Mode = NpcCombatMode.FollowOnly;
		public int LockedBossWhoAmI = -1;
		public int BossClearTicks;
		public int CachedTargetWhoAmI = -1;
		public bool HasEngagedTarget;
		/// <summary>Frames until next full acquire scan (boss + normal).</summary>
		public int TargetRefreshTicks;

		/// <summary>Palico-style normal targeting state for this follower.</summary>
		public NpcCombatTargeting Targeting { get; } = new();

		/// <summary>True while Boss or Normal has an engaged target (chat / perception).</summary>
		public bool CombatControlsLocomotion =>
			HasEngagedTarget && Mode != NpcCombatMode.FollowOnly;

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bool busy = ItemAnimation > 0 || IsChanneling;
			bitWriter.WriteBit(busy);
			if (busy) {
				binaryWriter.Write((short)ItemAnimation);
				binaryWriter.Write((short)ItemAnimationMax);
				binaryWriter.Write(ItemRotation);
				bitWriter.WriteBit(IsChanneling);
			}

			WriteChaseTarget(bitWriter, binaryWriter);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			if (!bitReader.ReadBit()) {
				ItemAnimation = 0;
				ItemAnimationMax = 0;
				ItemRotation = 0f;
				IsChanneling = false;
			}
			else {
				ItemAnimation = binaryReader.ReadInt16();
				ItemAnimationMax = binaryReader.ReadInt16();
				ItemRotation = binaryReader.ReadSingle();
				IsChanneling = bitReader.ReadBit();
			}

			ReadChaseTarget(bitReader, binaryReader);
		}

		private void WriteChaseTarget(BitWriter bitWriter, BinaryWriter binaryWriter) {
			bool chase = Mode == NpcCombatMode.Normal
				&& HasEngagedTarget
				&& CachedTargetWhoAmI >= 0
				&& CachedTargetWhoAmI < Main.maxNPCs;
			bitWriter.WriteBit(chase);
			if (chase) {
				binaryWriter.Write((short)CachedTargetWhoAmI);
			}
		}

		private void ReadChaseTarget(BitReader bitReader, BinaryReader binaryReader) {
			if (!bitReader.ReadBit()) {
				if (Mode == NpcCombatMode.Normal) {
					ClearNormalTarget();
					Mode = NpcCombatMode.FollowOnly;
				}

				return;
			}

			int who = binaryReader.ReadInt16();
			if (Mode == NpcCombatMode.Boss) {
				return;
			}

			Mode = NpcCombatMode.Normal;
			HasEngagedTarget = true;
			CachedTargetWhoAmI = who;
		}

		public void ResetUse() {
			ItemAnimation = 0;
		}

		public void ClearNormalTarget() {
			CachedTargetWhoAmI = -1;
			HasEngagedTarget = false;
			Targeting.Clear();
		}

		public void ClearBossLock() {
			LockedBossWhoAmI = -1;
			BossClearTicks = 0;
		}

		public void EnterFollowOnly() {
			Mode = NpcCombatMode.FollowOnly;
			TargetRefreshTicks = 0;
			ClearNormalTarget();
			ClearBossLock();
		}
	}
}
