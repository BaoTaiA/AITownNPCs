using System.Collections.Generic;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.Nurse
{
	public sealed class NurseEmergencySession
	{
		public int NpcWhoAmI;
		public int PlayerWhoAmI;
		public Vector2 HomePos;
		public Vector2 StayPos;
		public int SavedHomeX;
		public int SavedHomeY;
		public bool SavedHomeless;
		public int ReturnTimer;
		public bool Active;
	}

	public class NurseEmergencyHealState : ModSystem
	{
		private const float SnapBackDist = 16f * 4f;

		private static readonly Dictionary<int, NurseEmergencySession> ByNpc = new();

		public static bool IsBusy(int npcWhoAmI) =>
			ByNpc.TryGetValue(npcWhoAmI, out NurseEmergencySession s) && s.Active;

		public static void ClearAll() => ByNpc.Clear();

		public static bool Start(NPC npc, Player player) {
			if (npc == null || player == null) {
				return false;
			}

			var session = new NurseEmergencySession {
				NpcWhoAmI = npc.whoAmI,
				PlayerWhoAmI = player.whoAmI,
				HomePos = npc.Center,
				SavedHomeX = npc.homeTileX,
				SavedHomeY = npc.homeTileY,
				SavedHomeless = npc.homeless,
				ReturnTimer = 60 * 5,
				Active = true
			};

			TeleportNearPlayer(npc, player);
			session.StayPos = npc.Center;
			// Suppress vanilla "go home" like follow AI does.
			npc.homeless = true;
			npc.velocity = Vector2.Zero;
			SpawnTeleportDust(npc.Center);
			HealPlayer(player);
			ByNpc[npc.whoAmI] = session;
			return true;
		}

		public static void HealPlayer(Player player) {
			ServerPlayerNet.FullHealSynced(player);
		}

		private static void TeleportNearPlayer(NPC npc, Player player) {
			float dir = player.direction != 0 ? player.direction : 1f;
			float[] xOffsets = {
				dir * 56f, -dir * 56f,
				dir * 96f, -dir * 96f,
				dir * 32f, -dir * 32f,
				0f
			};

			bool placed = false;
			for (int i = 0; i < xOffsets.Length; i++) {
				if (TryStandNear(npc, player.Center + new Vector2(xOffsets[i], 0f))) {
					placed = true;
					break;
				}
			}

			if (!placed) {
				// Last resort: beside player feet, then lift until clear of solids.
				npc.position = player.Bottom - new Vector2(npc.width * 0.5f + dir * 48f, npc.height);
				npc.velocity = Vector2.Zero;
				for (int n = 0; n < 24 && Collision.SolidCollision(npc.position, npc.width, npc.height); n++) {
					npc.position.Y -= 8f;
				}
			}

			npc.velocity = Vector2.Zero;
			npc.direction = player.direction >= 0 ? -1 : 1;
			npc.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) {
				NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
			}
		}

		/// <summary>Find solid ground under approx and seat NPC so feet rest on it without body in solids.</summary>
		private static bool TryStandNear(NPC npc, Vector2 approx) {
			int tileX = (int)(approx.X / 16f);
			int startY = (int)(approx.Y / 16f) - 2;
			if (tileX < 2 || tileX >= Main.maxTilesX - 2) {
				return false;
			}

			startY = Utils.Clamp(startY, 2, Main.maxTilesY - 4);

			for (int dy = 0; dy < 24; dy++) {
				int groundY = startY + dy;
				if (groundY >= Main.maxTilesY - 2) {
					break;
				}

				if (!WorldGen.SolidTile(tileX, groundY)) {
					continue;
				}

				// Need clear air for NPC body above the ground tile.
				int topTile = groundY - 1 - (npc.height + 15) / 16;
				if (topTile < 1) {
					continue;
				}

				bool blocked = false;
				for (int y = topTile; y < groundY; y++) {
					if (WorldGen.SolidTile(tileX, y) || WorldGen.SolidTile(tileX - 1, y) || WorldGen.SolidTile(tileX + 1, y)) {
						blocked = true;
						break;
					}
				}

				if (blocked) {
					continue;
				}

				float feetY = groundY * 16f;
				npc.position = new Vector2(tileX * 16f + 8f - npc.width * 0.5f, feetY - npc.height);
				npc.velocity = Vector2.Zero;
				if (Collision.SolidCollision(npc.position, npc.width, npc.height)) {
					continue;
				}

				return true;
			}

			return false;
		}

		private static void SpawnTeleportDust(Vector2 pos) {
			for (int i = 0; i < 24; i++) {
				Dust.NewDust(pos - new Vector2(8f, 8f), 16, 32, DustID.Teleporter);
			}

			if (Main.netMode == NetmodeID.Server) {
				SuperpowerNetSystem.SendNurseTeleportFx(pos);
			}
		}

		private static void RestoreHomeAndReturn(NPC npc, NurseEmergencySession s) {
			npc.homeless = s.SavedHomeless;
			npc.homeTileX = s.SavedHomeX;
			npc.homeTileY = s.SavedHomeY;
			npc.Center = s.HomePos;
			npc.velocity = Vector2.Zero;
			npc.netUpdate = true;
			SpawnTeleportDust(npc.Center);
			if (Main.netMode == NetmodeID.Server) {
				NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
			}
		}

		public override void PostUpdateNPCs() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			var done = new List<int>();
			foreach (var kv in ByNpc) {
				NurseEmergencySession s = kv.Value;
				if (!s.Active || s.NpcWhoAmI < 0 || s.NpcWhoAmI >= Main.maxNPCs) {
					done.Add(kv.Key);
					continue;
				}

				NPC npc = Main.npc[s.NpcWhoAmI];
				if (!npc.active || npc.type != NPCID.Nurse) {
					done.Add(kv.Key);
					continue;
				}

				npc.homeless = true;
				if (Vector2.DistanceSquared(npc.Center, s.StayPos) > SnapBackDist * SnapBackDist) {
					npc.Center = s.StayPos;
					npc.velocity = Vector2.Zero;
					npc.netUpdate = true;
				}

				s.ReturnTimer--;
				if (s.ReturnTimer > 0) {
					continue;
				}

				RestoreHomeAndReturn(npc, s);
				s.Active = false;
				done.Add(kv.Key);
			}

			foreach (int key in done) {
				ByNpc.Remove(key);
			}
		}
	}

	/// <summary>Skip vanilla town AI while emergency stay is active.</summary>
	public class NurseEmergencyHealGlobal : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
			entity.type == NPCID.Nurse;

		public override bool PreAI(NPC npc) {
			if (NurseEmergencyHealState.IsBusy(npc.whoAmI)) {
				npc.velocity = Vector2.Zero;
				return false;
			}

			return true;
		}
	}
}
