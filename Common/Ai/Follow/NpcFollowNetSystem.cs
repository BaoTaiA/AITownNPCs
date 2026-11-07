using System.IO;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Configs;
using AITownNPCs.Common.Host;
using AITownNPCs.Common.Net;
using AITownNPCs.Common.Superpower;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Follow
{
	public class NpcFollowNetSystem : ModSystem
	{
		public static void RequestStart() {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				TryStartFollow(Main.myPlayer);
				return;
			}

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFollowMessageType.RequestStart);
			packet.Send();
		}

		public static void RequestStop() {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				TryStopFollow(Main.myPlayer);
				return;
			}

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFollowMessageType.RequestStop);
			packet.Send();
		}

		public static void RequestSyncAll() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFollowMessageType.RequestSyncAll);
			packet.Send();
		}

		public static void HandlePacket(NpcFollowMessageType type, BinaryReader reader, int sender) {
			switch (type) {
				case NpcFollowMessageType.RequestStart:
					if (Main.netMode == NetmodeID.Server) {
						TryStartFollow(sender);
					}
					break;
				case NpcFollowMessageType.RequestStop:
					if (Main.netMode == NetmodeID.Server) {
						TryStopFollow(sender);
					}
					break;
				case NpcFollowMessageType.SyncFollow:
					HandleSyncFollow(reader);
					break;
				case NpcFollowMessageType.RequestSyncAll:
					if (Main.netMode == NetmodeID.Server) {
						SendAllFollow(sender);
					}
					break;
				case NpcFollowMessageType.SyncAll:
					HandleSyncAll(reader);
					break;
				case NpcFollowMessageType.TeleportFx:
					HandleTeleportFx(reader);
					break;
			}
		}

		public static void BroadcastTeleportFx(int npcWhoAmI, Vector2 from, Vector2 to, int width, int height) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				FollowMovement.PlayIceMirrorFx(from, to, width, height);
				return;
			}

			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFollowMessageType.TeleportFx);
			packet.Write(npcWhoAmI);
			packet.Write(from.X);
			packet.Write(from.Y);
			packet.Write(to.X);
			packet.Write(to.Y);
			packet.Write(width);
			packet.Write(height);
			packet.Send();
		}

		private static void HandleTeleportFx(BinaryReader reader) {
			reader.ReadInt32();
			Vector2 from = new Vector2(reader.ReadSingle(), reader.ReadSingle());
			Vector2 to = new Vector2(reader.ReadSingle(), reader.ReadSingle());
			int width = reader.ReadInt32();
			int height = reader.ReadInt32();
			FollowMovement.PlayIceMirrorFx(from, to, width, height);
		}

		public static void TryStartFollow(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			Player player = Main.player[playerWhoAmI];
			if (player == null || !player.active) {
				return;
			}

			if (!(AinpcServerConfig.Instance?.FollowEnabled ?? false)) {
				NotifyPlayer(playerWhoAmI, "Mods.AITownNPCs.Follow.Disabled");
				return;
			}

			AinpcServerConfig config = AinpcServerConfig.Instance;
			float range = config.FollowTriggerDistancePixels;
			NPC best = null;
			float bestDist = range;

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!NpcHostRules.IsManagedTownNpc(npc)) {
					continue;
				}

				float dist = player.Distance(npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					best = npc;
				}
			}

			if (best == null) {
				NotifyPlayer(playerWhoAmI, "Mods.AITownNPCs.Follow.NoNpcNearby");
				return;
			}

			if (NpcVisitState.IsStaying(best.whoAmI)) {
				NotifyPlayer(playerWhoAmI, "Mods.AITownNPCs.Follow.CastingBusy");
				return;
			}

			FollowingTownNPC follow = best.GetGlobalNPC<FollowingTownNPC>();
			if (follow.IsFollowing && follow.FollowPlayerWhoAmI != playerWhoAmI) {
				NotifyPlayer(playerWhoAmI, "Mods.AITownNPCs.Follow.NpcBusy");
				return;
			}

			NpcFollowPlayer modPlayer = player.GetModPlayer<NpcFollowPlayer>();
			// Already following this NPC: dual chat+RequestStart must not re-announce.
			if (follow.IsFollowing
				&& follow.FollowPlayerWhoAmI == playerWhoAmI
				&& modPlayer.FollowingNpcWhoAmI == best.whoAmI) {
				return;
			}

			if (modPlayer.HasFollower && modPlayer.FollowingNpcWhoAmI != best.whoAmI) {
				NPC old = Main.npc[modPlayer.FollowingNpcWhoAmI];
				if (old.active) {
					StopFollow(old, announce: true);
				}
			}

			follow.FollowPlayerWhoAmI = playerWhoAmI;
			modPlayer.FollowingNpcWhoAmI = best.whoAmI;
			NpcInteractGate.DismissOpenEquip(best.whoAmI, playerWhoAmI);
			FollowingTownNPC.ApplyFollowNetFlags(best, following: true);
			SyncFollow(best.whoAmI, playerWhoAmI);
			NpcFollowEvents.RaiseFollowStarted(player, best);
			NpcFollowMessages.AnnounceStart(best, player);
		}

		public static void TryStopFollow(int playerWhoAmI) {
			if (playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
				return;
			}

			Player player = Main.player[playerWhoAmI];
			if (player == null || !player.active) {
				return;
			}

			NpcFollowPlayer modPlayer = player.GetModPlayer<NpcFollowPlayer>();
			if (!modPlayer.HasFollower) {
				NotifyPlayer(playerWhoAmI, "Mods.AITownNPCs.Follow.NoFollower");
				return;
			}

			NPC npc = Main.npc[modPlayer.FollowingNpcWhoAmI];
			StopFollow(npc, announce: true);
		}

		public static void StopFollow(NPC npc, bool announce) {
			if (npc == null || !npc.active) {
				return;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			int owner = follow.FollowPlayerWhoAmI;
			Player player = null;
			if (owner >= 0 && owner < Main.maxPlayers) {
				player = Main.player[owner];
				if (player.active) {
					var mp = player.GetModPlayer<NpcFollowPlayer>();
					if (mp.FollowingNpcWhoAmI == npc.whoAmI) {
						mp.FollowingNpcWhoAmI = -1;
					}
				}
			}

			follow.ClearFollowLocal(npc);
			FollowingTownNPC.ApplyFollowNetFlags(npc, following: false);
			npc.netUpdate = true;
			SyncFollow(npc.whoAmI, -1);

			if (player != null && player.active) {
				NpcFollowEvents.RaiseFollowStopped(player, npc);
			}

			if (announce && player != null && player.active) {
				NpcFollowMessages.AnnounceStop(npc, player);
			}
		}

		public static void SyncFollow(int npcWhoAmI, int followPlayerWhoAmI) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFollowMessageType.SyncFollow);
			packet.Write(npcWhoAmI);
			packet.Write(followPlayerWhoAmI);
			packet.Send();
		}

		private static void HandleSyncFollow(BinaryReader reader) {
			int npcWhoAmI = reader.ReadInt32();
			int followPlayer = reader.ReadInt32();
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs) {
				return;
			}

			NPC npc = Main.npc[npcWhoAmI];
			if (!npc.active) {
				return;
			}

			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();

			// Clear old player reverse link on clients.
			if (follow.FollowPlayerWhoAmI >= 0 && follow.FollowPlayerWhoAmI < Main.maxPlayers) {
				var oldMp = Main.player[follow.FollowPlayerWhoAmI].GetModPlayer<NpcFollowPlayer>();
				if (oldMp.FollowingNpcWhoAmI == npcWhoAmI) {
					oldMp.FollowingNpcWhoAmI = -1;
				}
			}

			follow.FollowPlayerWhoAmI = followPlayer;
			FollowingTownNPC.ApplyFollowNetFlags(npc, following: followPlayer >= 0);
			if (followPlayer >= 0 && followPlayer < Main.maxPlayers) {
				Main.player[followPlayer].GetModPlayer<NpcFollowPlayer>().FollowingNpcWhoAmI = npcWhoAmI;
			}
		}

		public static void SendAllFollow(int toClient) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFollowMessageType.SyncAll);

			int countPos = (int)packet.BaseStream.Position;
			packet.Write(0);
			int count = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active) {
					continue;
				}

				FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
				if (!follow.IsFollowing) {
					continue;
				}

				packet.Write(i);
				packet.Write(follow.FollowPlayerWhoAmI);
				count++;
			}

			long end = packet.BaseStream.Position;
			packet.BaseStream.Position = countPos;
			packet.Write(count);
			packet.BaseStream.Position = end;
			packet.Send(toClient);
		}

		private static void HandleSyncAll(BinaryReader reader) {
			int count = reader.ReadInt32();
			for (int i = 0; i < Main.maxPlayers; i++) {
				if (Main.player[i].active) {
					Main.player[i].GetModPlayer<NpcFollowPlayer>().FollowingNpcWhoAmI = -1;
				}
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				if (Main.npc[i].active) {
					Main.npc[i].GetGlobalNPC<FollowingTownNPC>().ClearFollowLocal(Main.npc[i]);
				}
			}

			for (int n = 0; n < count; n++) {
				int npcWhoAmI = reader.ReadInt32();
				int followPlayer = reader.ReadInt32();
				if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs || !Main.npc[npcWhoAmI].active) {
					continue;
				}

				NPC npc = Main.npc[npcWhoAmI];
				FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
				follow.FollowPlayerWhoAmI = followPlayer;
				FollowingTownNPC.ApplyFollowNetFlags(npc, following: followPlayer >= 0);
				if (followPlayer >= 0 && followPlayer < Main.maxPlayers) {
					Main.player[followPlayer].GetModPlayer<NpcFollowPlayer>().FollowingNpcWhoAmI = npcWhoAmI;
				}
			}
		}

		private static void NotifyPlayer(int playerWhoAmI, string localizationKey) {
			NpcAiSpeech.NotifyPlayerLiteral(
				playerWhoAmI,
				Language.GetTextValue(localizationKey),
				NpcFollowMessages.Pink);
		}

		public override void PostUpdateNPCs() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			// Drop follow if owner vanished.
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active) {
					continue;
				}

				FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
				if (!follow.IsFollowing) {
					continue;
				}

				Player p = Main.player[follow.FollowPlayerWhoAmI];
				if (!p.active) {
					follow.ClearFollowLocal(npc);
					npc.netUpdate = true;
					SyncFollow(npc.whoAmI, -1);
				}
			}
		}
	}
}
