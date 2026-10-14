using System.Collections.Generic;

namespace AITownNPCs.Common.Superpower
{
	public sealed class SuperpowerIntent
	{
		public string Intent { get; set; } = SuperpowerIds.Chat;
		public float Confidence { get; set; }
		public Dictionary<string, string> Params { get; set; } = new();

		public string GetParam(string key, string fallback = "") {
			if (Params != null && Params.TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v)) {
				return v.Trim();
			}

			return fallback;
		}

		public float GetFloatParam(string key, float fallback) {
			string raw = GetParam(key, null);
			if (raw != null && float.TryParse(raw, System.Globalization.NumberStyles.Float,
				    System.Globalization.CultureInfo.InvariantCulture, out float f)) {
				return f;
			}

			return fallback;
		}
	}
}
