using System;
using System.Collections.Generic;

namespace AITownNPCs.Common.Loc
{
	/// <summary>
	/// Parses bilingual comma-separated alias lists from Aliases.* keys.
	/// Both language files store identical CN+EN content so matching works regardless of ActiveCulture.
	/// </summary>
	public static class AliasLists
	{
		public static string[] Split(string raw) {
			if (string.IsNullOrWhiteSpace(raw)) {
				return Array.Empty<string>();
			}

			string[] parts = raw.Split(',');
			var list = new List<string>(parts.Length);
			for (int i = 0; i < parts.Length; i++) {
				string p = parts[i].Trim();
				if (p.Length > 0) {
					list.Add(p);
				}
			}

			return list.ToArray();
		}

		public static string[] Get(string aliasesKey) =>
			Split(ModText.Get("Aliases." + aliasesKey));

		public static bool EqualsAny(string text, string aliasesKey) {
			if (string.IsNullOrEmpty(text)) {
				return false;
			}

			foreach (string a in Get(aliasesKey)) {
				if (string.Equals(text, a, StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}

			return false;
		}

		public static bool ContainsAny(string text, string aliasesKey) {
			if (string.IsNullOrEmpty(text)) {
				return false;
			}

			foreach (string a in Get(aliasesKey)) {
				if (text.Contains(a, StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}

			return false;
		}

		/// <summary>Build alias→id map from Aliases.{group}.{id} keys (comma lists → id).</summary>
		public static Dictionary<string, string> BuildMap(string group, IEnumerable<string> ids) {
			var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (string id in ids) {
				foreach (string alias in Get(group + "." + id)) {
					if (!map.ContainsKey(alias)) {
						map[alias] = id;
					}
				}

				if (!map.ContainsKey(id)) {
					map[id] = id;
				}
			}

			return map;
		}

		public static string ResolveId(Dictionary<string, string> map, string target) {
			if (string.IsNullOrEmpty(target) || map == null) {
				return null;
			}

			if (map.TryGetValue(target, out string id)) {
				return id;
			}

			foreach (KeyValuePair<string, string> kv in map) {
				if (target.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)) {
					return kv.Value;
				}
			}

			return null;
		}
	}
}
