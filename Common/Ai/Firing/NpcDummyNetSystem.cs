using System.IO;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	public static class NpcDummyNetSystem
	{
		private const uint PoseMinInterval = 5;

		/// <summary>
		/// Pose sync after the dummy has used the weapon this tick.
		/// Swinging, channeling, or a held projectile sends every tick so clients see the swing.
		/// Idle poses stay on a short interval.
		/// </summary>
		public static void SendPoseThrottled(NPC npc, Player dummy, Vector2 mouseWorld) {
			if (Main.netMode != NetmodeID.Server || npc == null || !npc.active || dummy == null) {
				return;
			}

			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
			int weaponType = dummy.inventory[dummy.selectedItem]?.type ?? 0;
			bool busy = dummy.itemAnimation > 0 || dummy.channel || dummy.heldProj >= 0;
			bool changed = firing.LastPoseChannel != dummy.channel
				|| firing.LastPoseWeaponType != weaponType
				|| firing.LastPoseDirection != dummy.direction
				|| firing.LastPoseItemAnimation != dummy.itemAnimation
				|| firing.LastPoseHeldProj != dummy.heldProj;

			uint now = Main.GameUpdateCount;
			if (!busy && !changed && now - firing.LastPoseSendTick < PoseMinInterval) {
				return;
			}

			firing.LastPoseSendTick = now;
			firing.LastPoseItemAnimation = dummy.itemAnimation;
			firing.LastPoseChannel = dummy.channel;
			firing.LastPoseWeaponType = weaponType;
			firing.LastPoseDirection = dummy.direction;
			firing.LastPoseHeldProj = dummy.heldProj;
			SendPose(npc.whoAmI, dummy, mouseWorld);
		}

		public static void SendPose(int npcWhoAmI, Player dummy, Vector2 mouseWorld, int toClient = -1, int ignoreClient = -1) {
			if (Main.netMode != NetmodeID.Server || dummy == null) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFiringMessageType.DummyPose);
			packet.Write(npcWhoAmI);
			packet.Write(dummy.whoAmI);
			packet.Write(dummy.position.X);
			packet.Write(dummy.position.Y);
			packet.Write((sbyte)dummy.direction);
			packet.Write(dummy.itemRotation);
			packet.Write((short)dummy.itemAnimation);
			packet.Write((short)dummy.itemAnimationMax);
			packet.Write(dummy.channel);
			packet.Write((short)dummy.heldProj);
			packet.Write(mouseWorld.X);
			packet.Write(mouseWorld.Y);
			packet.Write(dummy.inventory[dummy.selectedItem]?.type ?? 0);
			packet.Write((byte)(dummy.inventory[dummy.selectedItem]?.prefix ?? 0));
			packet.Send(toClient, ignoreClient);
		}

		public static void SendRelease(int npcWhoAmI, int dummyWho, int toClient = -1, int ignoreClient = -1) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFiringMessageType.DummyRelease);
			packet.Write(npcWhoAmI);
			packet.Write(dummyWho);
			packet.Send(toClient, ignoreClient);
		}

		public static void HandlePose(BinaryReader reader) {
			int npcWhoAmI = reader.ReadInt32();
			int dummyWho = reader.ReadInt32();
			float x = reader.ReadSingle();
			float y = reader.ReadSingle();
			int direction = reader.ReadSByte();
			float itemRotation = reader.ReadSingle();
			int itemAnimation = reader.ReadInt16();
			int itemAnimationMax = reader.ReadInt16();
			bool channel = reader.ReadBoolean();
			int heldProj = reader.ReadInt16();
			float mouseX = reader.ReadSingle();
			float mouseY = reader.ReadSingle();
			int weaponType = reader.ReadInt32();
			byte prefix = reader.ReadByte();

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			NpcDummyPool.BindClient(npcWhoAmI, dummyWho);
			if (!NpcDummyPool.TryGetDummy(npcWhoAmI, out Player dummy) || dummy == null) {
				return;
			}

			dummy.position = new Vector2(x, y);
			dummy.direction = direction;
			dummy.itemRotation = itemRotation;
			dummy.itemAnimation = itemAnimation;
			dummy.itemAnimationMax = itemAnimationMax;
			dummy.channel = channel;
			dummy.heldProj = heldProj;
			dummy.selectedItem = 0;
			NpcDummyPool.KeepAlive(dummy);
			dummy.aggro = -1000;
			dummy.hostile = false;
			NpcDummyLocal.RememberAim(dummy.whoAmI, new Vector2(mouseX, mouseY));

			if (weaponType > 0) {
				if (dummy.inventory[0].type != weaponType || dummy.inventory[0].prefix != prefix) {
					dummy.inventory[0].SetDefaults(weaponType);
					dummy.inventory[0].Prefix(prefix);
				}
			}
			else {
				dummy.inventory[0].TurnToAir();
			}

			if (npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs) {
				NPC npc = Main.npc[npcWhoAmI];
				if (npc.active) {
					NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
					combat.ItemAnimation = itemAnimation;
					combat.ItemAnimationMax = itemAnimationMax;
					combat.IsChanneling = channel;
					combat.ItemRotation = itemRotation;
					npc.direction = direction;
					npc.spriteDirection = direction;
				}
			}
		}

		public static void HandleRelease(BinaryReader reader) {
			int npcWhoAmI = reader.ReadInt32();
			int dummyWho = reader.ReadInt32();
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			NpcDummyPool.ForceRelease(npcWhoAmI);
			if (dummyWho >= 0 && dummyWho < Main.maxPlayers && Main.player[dummyWho] != null) {
				Main.player[dummyWho].active = false;
			}
		}
	}
}
