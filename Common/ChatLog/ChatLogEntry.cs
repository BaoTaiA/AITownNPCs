using Microsoft.Xna.Framework;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.ChatLog
{
	public sealed class ChatLogEntry
	{
		public long Id;
		public long TimestampUtc;
		public byte R = 255;
		public byte G = 255;
		public byte B = 255;
		public string Text = string.Empty;

		/// <summary>Client tick when this line was shown live. 0 means history, not a fresh line.</summary>
		public uint ClientShowTick;

		public Color Color => new(R, G, B);

		public TagCompound ToTag() => new() {
			["id"] = Id,
			["t"] = TimestampUtc,
			["r"] = (int)R,
			["g"] = (int)G,
			["b"] = (int)B,
			["text"] = Text ?? string.Empty
		};

		public static ChatLogEntry FromTag(TagCompound tag) {
			return new ChatLogEntry {
				Id = tag.GetLong("id"),
				TimestampUtc = tag.GetLong("t"),
				R = (byte)tag.GetInt("r"),
				G = (byte)tag.GetInt("g"),
				B = (byte)tag.GetInt("b"),
				Text = tag.GetString("text")
			};
		}
	}
}
