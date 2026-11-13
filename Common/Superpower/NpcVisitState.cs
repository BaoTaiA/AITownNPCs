using System.Collections.Generic;
using System.IO;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Host;
using AITownNPCs.Common.Roles.Dryad;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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
		/// <summary>Dryad ward projectile. While it lives, the wait timer does not run.</summary>
		public int WardProjectile = -1;
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

		public static void BindWard(int npcWhoAmI, int projectileWho) {
			if (ByNpc.TryGetValue(npcWhoAmI, out NpcVisitSession session)) {
				session.WardProjectile = projectileWho;
			}
		}

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

			Vector2 from = npc.position;
			TeleportNearPlayer(npc, player);
			session.StayPos = npc.Center;
			npc.homeless = true;
			npc.velocity = Vector2.Zero;
			npc.noGravity = true;
			NpcFollowNetSystem.BroadcastTeleportFx(npc.whoAmI, from, npc.position, npc.width, npc.height);
			ByNpc[npc.whoAmI] = session;
			return true;
		}

		public static void Cancel(int npcWhoAmI) {
			if (!ByNpc.TryGetValue(npcWhoAmI, out NpcVisitSession s) || !s.Active) {
				ByNpc.Remove(npcWhoAmI);
				return;
			}

			// Clear before SyncNPC so ExtraAI does not still advertise stay.
			s.Active = false;
			if (npcWhoAmI >= 0 && npcWhoAmI < Main.maxNPCs) {
				NPC npc = Main.npc[npcWhoAmI];
				if (npc.active && npc.type == s.NpcType) {
					RestoreHomeAndReturn(npc, s);
				}
			}

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

		private static bool IsStandableFloor(int x, int y) {
			if (!WorldGen.InWorld(x, y)) {
				return false;
			}

			if (WorldGen.SolidTile(x, y)) {
				return true;
			}

			Tile tile = Framing.GetTileSafely(x, y);
			return tile.HasUnactuatedTile && Main.tileSolidTop[tile.TileType];
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

				if (!IsStandableFloor(tileX, groundY)) {
					continue;
				}

				int topTile = groundY - 1 - (npc.height + 15) / 16;
				if (topTile < 1) {
					continue;
				}

				// Headroom: full solids only — platform tops must not block standing on a platform.
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

		private static void RestoreHomeAndReturn(NPC npc, NpcVisitSession s) {
			Vector2 from = npc.position;
			npc.homeless = s.SavedHomeless;
			npc.homeTileX = s.SavedHomeX;
			npc.homeTileY = s.SavedHomeY;
			npc.Center = s.HomePos;
			npc.velocity = Vector2.Zero;
			npc.noGravity = false;
			npc.netUpdate = true;
			NpcFollowNetSystem.BroadcastTeleportFx(npc.whoAmI, from, npc.position, npc.width, npc.height);
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

				if (s.WardProjectile >= 0) {
					int ward = s.WardProjectile;
					bool wardAlive = ward >= 0 && ward < Main.maxProjectiles
						&& Main.projectile[ward].active
						&& Main.projectile[ward].type == ProjectileID.DryadsWardCircle;
					if (wardAlive) {
						s.ReturnTimer = StayTicks + 1;
						npc.ai[0] = DryadWardBind.CastAi;
					}
					else {
						s.WardProjectile = -1;
						s.ReturnTimer = StayTicks;
						if (npc.ai[0] == DryadWardBind.CastAi) {
							npc.ai[0] = 0f;
							npc.netUpdate = true;
						}
					}
				}

				s.ReturnTimer--;
				if (s.ReturnTimer > 0) {
					continue;
				}

				s.Active = false;
				RestoreHomeAndReturn(npc, s);
				done.Add(kv.Key);
			}

			foreach (int key in done) {
				ByNpc.Remove(key);
			}
		}
	}

	/// <summary>Skip vanilla town AI while a visit stay is holding this NPC. Town NPCs only.</summary>
	public class NpcVisitGlobal : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
			NpcHostRules.AppliesToManagedTownNpc(entity);

		/// <summary>Client copy of visit-stay; server uses <see cref="NpcVisitState.IsStaying"/>.</summary>
		public bool ClientStay;

		public bool IsVisitHolding(NPC npc) =>
			NpcVisitState.IsStaying(npc.whoAmI) || ClientStay;

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bitWriter.WriteBit(NpcVisitState.IsStaying(npc.whoAmI));
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			ClientStay = bitReader.ReadBit();
		}

		public override bool PreAI(NPC npc) {
			if (!IsVisitHolding(npc)) {
				return true;
			}

			npc.noGravity = true;
			npc.velocity = Vector2.Zero;
			return false;
		}

		public override bool? CanFallThroughPlatforms(NPC npc) {
			if (!IsVisitHolding(npc)) {
				return null;
			}

			return false;
		}
	}
}
