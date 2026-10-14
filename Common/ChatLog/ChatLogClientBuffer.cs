using System.Collections.Generic;

namespace AITownNPCs.Common.ChatLog
{
	/// <summary>Client-side follow-channel lines and unread state for the chat tabs.</summary>
	public static class ChatLogClientBuffer
	{
		public const int MaxFollowLocal = 500;

		private static readonly List<ChatLogEntry> Follow = new();
		private static readonly HashSet<long> SeenIds = new();
		private static readonly Queue<string> RecentFingerprints = new();
		private static readonly HashSet<string> RecentFingerprintSet = new();

		public static bool HasUnread;
		public static int ScrollOffset;

		public static IReadOnlyList<ChatLogEntry> FollowEntries => Follow;

		public static void Clear() {
			Follow.Clear();
			SeenIds.Clear();
			RecentFingerprints.Clear();
			RecentFingerprintSet.Clear();
			HasUnread = false;
			ScrollOffset = 0;
		}

		public static void ReplaceFollowHistory(IEnumerable<ChatLogEntry> entries) {
			Follow.Clear();
			SeenIds.Clear();
			RecentFingerprints.Clear();
			RecentFingerprintSet.Clear();
			ScrollOffset = 0;
			foreach (ChatLogEntry entry in entries) {
				AddInternal(entry, markUnread: false);
			}
		}

		public static void AddFollowLine(ChatLogEntry entry, bool markUnreadIfHidden) {
			if (entry == null || string.IsNullOrEmpty(entry.Text)) {
				return;
			}

			if (entry.Id != 0 && SeenIds.Contains(entry.Id)) {
				return;
			}

			string fp = Fingerprint(entry);
			if (entry.Id == 0 && RecentFingerprintSet.Contains(fp)) {
				return;
			}

			// Server echo of an optimistic local line: upgrade id if text matches a zero-id tail entry.
			if (entry.Id != 0) {
				for (int i = Follow.Count - 1; i >= 0 && i >= Follow.Count - 8; i--) {
					if (Follow[i].Id == 0 && Follow[i].Text == entry.Text) {
						Follow[i].Id = entry.Id;
						SeenIds.Add(entry.Id);
						return;
					}
				}
			}

			AddInternal(entry, markUnreadIfHidden);
		}

		private static void AddInternal(ChatLogEntry entry, bool markUnread) {
			if (entry.Id != 0) {
				SeenIds.Add(entry.Id);
			}

			string fp = Fingerprint(entry);
			RecentFingerprints.Enqueue(fp);
			RecentFingerprintSet.Add(fp);
			while (RecentFingerprints.Count > 64) {
				string old = RecentFingerprints.Dequeue();
				RecentFingerprintSet.Remove(old);
			}

			Follow.Add(entry);
			while (Follow.Count > MaxFollowLocal) {
				ChatLogEntry removed = Follow[0];
				Follow.RemoveAt(0);
				if (removed.Id != 0) {
					SeenIds.Remove(removed.Id);
				}
			}

			if (markUnread) {
				HasUnread = true;
			}

			ScrollOffset = 0;
		}

		private static string Fingerprint(ChatLogEntry entry) =>
			$"{entry.R},{entry.G},{entry.B}:{entry.Text}";
	}
}
