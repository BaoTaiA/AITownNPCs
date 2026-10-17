using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower
{
	public sealed class NpcVisitSession
	{
		public int NpcWhoAmI;
		public int NpcType;
		public Vector2 HomePos;
		public Vector2 StayPos;
		public int SavedHomeX;
		public int SavedHomeY;
		public bool SavedHomeless;
		public int ReturnTimer;
		public bool Active;
	}

	/// <summary>
	/// Teleport an NPC beside a player, hold them there, then send them back.
	/// Nurse emergency heal and the dryad group ward share this stay.
	/// </summary>
	public class NpcVisitState : ModSystem
	{
		public const int StayTicks = 60 * 8;
		private const float SnapBackDist = 16f * 4f;

		private static readonly Dictionary<int, NpcVisitSession> ByNpc = new();

		public static bool IsStaying(int npcWhoAmI) =>
			ByNpc.TryGetValue(npcWhoAmI, out NpcVisitSession s) && s.Active;

		public static void ClearAll() => ByNpc.Clear();

		/// <summary>False when this NPC is already standing through a visit.</summary>
		public static bool TryBegin(NPC npc, Player player, int stayTicks) {
			if (npc == null || player == null || stayTicks <= 0) {
				return false;
			}

			if (IsStaying(npc.whoAmI)) {
				return false;
			}

			var session = new NpcVisitSession {
				NpcWhoAmI = npc.whoAmI,
				NpcType = npc.type,
				HomePos = npc.Center,
				SavedHomeX = npc.homeTileX,
				SavedHomeY = npc.homeTileY,
				SavedHomeless = npc.homeless,
				ReturnTimer = stayTicks,
				Active = true
			};

			TeleportNearPlayer(npc, player);
			session.StayPos = npc.Center;
			npc.homeless = true;
			npc.velocity = Vector2.Zero;
			SpawnTeleportDust(npc.Center);
			ByNpc[npc.whoAmI] = session;
			return true;
		}

		public static void Cancel(int npcWhoAmI) {
			if (!ByNpc.TryGetValue(npcWhoAmI, out NpcVisitSession s) || !s.Active) {
				ByNpc.Remove(npcWhoAmI);
				return;
			}

			if (npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs) {
				NPC npc = Main.npc[npcWhoAmI];
				if (npc.active && npc.type == s.NpcType) {
					RestoreHomeAndReturn(npc, s);
				}
			}

			s.Active = false;
			ByNpc.Remove(npcWhoAmI);
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

		private static void RestoreHomeAndReturn(NPC npc, NpcVisitSession s) {
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
				NpcVisitSession s = kv.Value;
				if (!s.Active || s.NpcWhoAmI < 0 || s.NpcWhoAmI >= Main.maxNPCs) {
					done.Add(kv.Key);
					continue;
				}

				NPC npc = Main.npc[s.NpcWhoAmI];
				if (!npc.active || npc.type != s.NpcType) {
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

	/// <summary>Skip vanilla town AI while a visit stay is holding this NPC.</summary>
	public class NpcVisitGlobal : GlobalNPC
	{
		public override bool PreAI(NPC npc) {
			if (!NpcVisitState.IsStaying(npc.whoAmI)) {
				return true;
			}

			npc.velocity = Vector2.Zero;
			return false;
		}
	}
}
