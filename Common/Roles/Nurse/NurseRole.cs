using System.IO;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Superpower.Nurse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Roles.Nurse
{
	public class NurseRole : INpcRole
	{
		public int NpcType => NPCID.Nurse;
		public NpcEquipProfile EquipProfile { get; } = RoleEquipProfiles.CreateNursePotions();

		public void Update(NPC npc) {
			NurseHealAI.Update(npc, npc.GetGlobalNPC<NurseRoleGlobal>().State);
		}

		public bool IsBusy(NPC npc) {
			return NurseEmergencyHealState.IsBusy(npc.whoAmI)
			       || NurseHealAI.IsBusy(npc.GetGlobalNPC<NurseRoleGlobal>().State);
		}
	}

	public class NurseRoleGlobal : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public NurseRoleState State { get; } = new();

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
			return entity.type == NPCID.Nurse;
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bitWriter.WriteBit(State.ThrowTimer > 0);
			if (State.ThrowTimer > 0) {
				binaryWriter.Write((byte)State.ThrowTimer);
				bitWriter.WriteBit(State.TargetIsPlayer);
				binaryWriter.Write((byte)System.Math.Clamp(State.TargetIndex, 0, 255));
				binaryWriter.Write(State.PendingPotionType);
			}
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			if (!bitReader.ReadBit()) {
				State.ThrowTimer = 0;
				State.PendingPotionType = 0;
				return;
			}

			State.ThrowTimer = binaryReader.ReadByte();
			State.TargetIsPlayer = bitReader.ReadBit();
			State.TargetIndex = binaryReader.ReadByte();
			State.PendingPotionType = binaryReader.ReadInt32();
		}

		public override void FindFrame(NPC npc, int frameHeight) {
			if (State.ThrowTimer <= 0 || npc.GetGlobalNPC<FollowingTownNPC>().IsFollowing) {
				return;
			}

			int attackCount = NPCID.Sets.AttackFrameCount[npc.type];
			if (attackCount <= 0) {
				attackCount = 4;
			}

			int extra = NPCID.Sets.ExtraFramesCount[npc.type];
			int total = Main.npcFrameCount[npc.type];
			int baseFrame = total - extra - attackCount;
			if (baseFrame < 0) {
				baseFrame = System.Math.Max(0, total - attackCount);
			}

			float progress = 1f - State.ThrowTimer / (float)NurseHealAI.ThrowAnim;
			int idx = (int)(progress * attackCount);
			idx = Utils.Clamp(idx, 0, attackCount - 1);
			npc.frame.Y = (baseFrame + idx) * frameHeight;
			npc.frameCounter = 0;
		}

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
			if (State.ThrowTimer <= 0 || State.PendingPotionType <= 0) {
				return;
			}

			int itemType = State.PendingPotionType;
			Main.instance.LoadItem(itemType);
			Texture2D texture = TextureAssets.Item[itemType].Value;
			Rectangle frame = texture.Frame();
			Vector2 origin = frame.Size() / 2f;

			Vector2 aimDir = new Vector2(npc.direction, 0f);
			Vector2? target = NurseHealAI.GetTargetCenterPublic(State);
			float rotation = 0f;
			if (target.HasValue) {
				aimDir = target.Value - npc.Center;
				if (aimDir.LengthSquared() > 0.01f) {
					aimDir.Normalize();
					rotation = aimDir.ToRotation();
				}
			}
			else {
				aimDir = new Vector2(npc.direction, -0.2f);
				aimDir.Normalize();
				rotation = aimDir.ToRotation();
			}

			// Raise arm during wind-up, lower toward release.
			float raise = State.ThrowTimer > NurseHealAI.ThrowAnim / 2
				? 0.6f
				: 0.25f;
			Vector2 drawPos = npc.Center + aimDir * 18f + new Vector2(0f, -8f * raise) - screenPos;
			SpriteEffects effects = aimDir.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
			spriteBatch.Draw(texture, drawPos, frame, npc.GetAlpha(drawColor), rotation, origin, 0.85f, effects, 0f);
		}
	}
}
