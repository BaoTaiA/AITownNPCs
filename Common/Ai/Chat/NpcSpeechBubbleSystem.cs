using System;
using System.IO;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace AITownNPCs.Common.Ai.Chat
{
	/// <summary>
	/// NPC overhead speech drawn in the same pass as vanilla player chat bubbles.
	/// </summary>
	public class NpcSpeechBubbleSystem : ModSystem
	{
		private static readonly Player.OverheadMessage[] Bubbles = new Player.OverheadMessage[Main.maxNPCs];

		public override void Load() {
			On_Main.DrawPlayerChatBubbles += OnDrawPlayerChatBubbles;
		}

		public override void Unload() {
			On_Main.DrawPlayerChatBubbles -= OnDrawPlayerChatBubbles;
		}

		public static void ShowLocal(int npcWhoAmI, string text) {
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs || string.IsNullOrWhiteSpace(text)) {
				return;
			}

			string trimmed = text.Trim();
			Bubbles[npcWhoAmI].NewMessage(trimmed, Main.PlayerOverheadChatMessageDisplayTime);
			// NewMessage parses snippets as white; re-parse so overhead matches chat-box SpeechColor.
			Bubbles[npcWhoAmI].color = NpcAiSpeech.SpeechColor;
			Bubbles[npcWhoAmI].snippets = ChatManager.ParseMessage(trimmed, NpcAiSpeech.SpeechColor).ToArray();
			Bubbles[npcWhoAmI].messageSize = ChatManager.GetStringSize(
				FontAssets.MouseText.Value,
				Bubbles[npcWhoAmI].snippets,
				Vector2.One);
		}

		public static void Broadcast(int npcWhoAmI, string text) {
			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcChatMessageType.SpeechBubble);
			packet.Write(npcWhoAmI);
			packet.Write(text ?? string.Empty);
			packet.Send();
		}

		public static void HandlePacket(BinaryReader reader) {
			int npcWhoAmI = reader.ReadInt32();
			string text = reader.ReadString();
			ShowLocal(npcWhoAmI, text);
		}

		public override void PostUpdateNPCs() {
			for (int i = 0; i < Main.maxNPCs; i++) {
				if (Bubbles[i].timeLeft > 0) {
					Bubbles[i].timeLeft--;
				}
			}
		}

		private static void OnDrawPlayerChatBubbles(On_Main.orig_DrawPlayerChatBubbles orig, Main self) {
			orig(self);

			if (Main.dedServ) {
				return;
			}

			SpriteBatch spriteBatch = Main.spriteBatch;
			for (int i = 0; i < Main.maxNPCs; i++) {
				if (Bubbles[i].timeLeft <= 0) {
					continue;
				}

				NPC npc = Main.npc[i];
				if (!npc.active) {
					continue;
				}

				DrawLikePlayerChatBubble(spriteBatch, npc, ref Bubbles[i]);
			}
		}

		/// <summary>
		/// Mirrors Main.DrawPlayerChatBubbles positioning for a single entity top.
		/// </summary>
		private static void DrawLikePlayerChatBubble(SpriteBatch spriteBatch, NPC npc, ref Player.OverheadMessage msg) {
			Vector2 messageSize = msg.messageSize;
			Vector2 pos = npc.Top.ToScreenPosition();
			pos.Y -= messageSize.Y + 2f;
			if (Main.LocalPlayer.gravDir == -1f) {
				pos.Y += 2f;
			}

			pos.X -= messageSize.X / 2f;
			pos.Y += npc.gfxOffY;
			// Treat NPCs like other players: flip when local gravity is inverted.
			if (Main.LocalPlayer.gravDir == -1f) {
				pos.Y = Main.screenHeight - pos.Y - messageSize.Y;
			}

			pos = pos.Floor();
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				FontAssets.MouseText.Value,
				msg.snippets,
				pos,
				0f,
				msg.color,
				Vector2.Zero,
				Vector2.One,
				out _);
		}
	}
}
