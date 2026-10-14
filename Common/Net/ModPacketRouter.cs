using System.IO;
using AITownNPCs.Common.Ai.Chat;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Ai.Firing;
using AITownNPCs.Common.ChatLog;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Superpower;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Net
{
	public static class ModPacketRouter
	{
		public static void Handle(BinaryReader reader, int whoAmI) {
			byte type = reader.ReadByte();
			if (type >= PacketRanges.PlayerStateMin) {
				ServerPlayerNet.HandlePacket(type, reader);
			}
			else if (type >= PacketRanges.SuperpowerMin) {
				SuperpowerNetSystem.HandlePacket(type, reader, whoAmI);
			}
			else if (type >= PacketRanges.AiMin) {
				if (type == (byte)NpcChatMessageType.SpeechBubble) {
					NpcSpeechBubbleSystem.HandlePacket(reader);
				}
				else if (type == (byte)NpcChatMessageType.PlayerChatHidden) {
					NpcFollowChatHooks.HandleHiddenChatPacket(reader, whoAmI);
				}
				else if (type == (byte)NpcChatMessageType.PlayerChatLine) {
					NpcFollowChatHooks.HandlePlayerChatLinePacket(reader, whoAmI);
				}
				else if (type == (byte)NpcChatMessageType.PlayerOverhead) {
					NpcFollowChatHooks.HandlePlayerOverheadPacket(reader);
				}
				else if (type == (byte)NpcChatMessageType.ChatHistoryRequest
				         || type == (byte)NpcChatMessageType.ChatHistoryChunk
				         || type == (byte)NpcChatMessageType.FollowChatLine) {
					ChatLogWorldSystem.HandlePacket(type, reader, whoAmI);
				}
				else if (type == (byte)NpcFiringMessageType.DummyPose) {
					NpcDummyNetSystem.HandlePose(reader);
				}
				else if (type == (byte)NpcFiringMessageType.DummyRelease) {
					NpcDummyNetSystem.HandleRelease(reader);
				}
			}
			else if (type >= PacketRanges.FollowMin) {
				NpcFollowNetSystem.HandlePacket((NpcFollowMessageType)type, reader, whoAmI);
			}
			else {
				NpcEquipNetSystem.HandlePacket((NpcEquipMessageType)type, reader, whoAmI);
			}
		}
	}
}
