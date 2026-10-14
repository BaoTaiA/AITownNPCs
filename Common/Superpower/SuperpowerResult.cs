using System.Collections.Generic;
using System.Text;

namespace AITownNPCs.Common.Superpower
{
	public enum SuperpowerOutcome
	{
		Ok,
		Quoted,
		Refused,
		Failed,
		Info
	}

	public sealed class SuperpowerResult
	{
		public SuperpowerOutcome Outcome { get; init; }
		public string AbilityId { get; init; }
		public bool StartCooldown { get; init; }
		public string ReasonCode { get; init; }
		/// <summary>Canonical result phrase from code; reply LLM must reflect this.</summary>
		public string FixedResultZh { get; init; }
		public List<string> Facts { get; init; } = new();
		public string ReplyHint { get; init; }

		public static SuperpowerResult Ok(string abilityId, bool startCooldown, string fixedResultZh, params string[] extraFacts) =>
			new() {
				Outcome = SuperpowerOutcome.Ok,
				AbilityId = abilityId,
				StartCooldown = startCooldown,
				FixedResultZh = fixedResultZh,
				Facts = NewFacts(extraFacts)
			};

		public static SuperpowerResult Quoted(string abilityId, string fixedResultZh, params string[] extraFacts) =>
			new() {
				Outcome = SuperpowerOutcome.Quoted,
				AbilityId = abilityId,
				StartCooldown = false,
				FixedResultZh = fixedResultZh,
				Facts = NewFacts(extraFacts)
			};

		public static SuperpowerResult Refused(string abilityId, string reason, string fixedResultZh, params string[] extraFacts) =>
			new() {
				Outcome = SuperpowerOutcome.Refused,
				AbilityId = abilityId,
				StartCooldown = false,
				ReasonCode = reason,
				FixedResultZh = fixedResultZh,
				Facts = NewFacts(extraFacts)
			};

		public static SuperpowerResult Failed(string abilityId, string reason, string fixedResultZh, params string[] extraFacts) =>
			new() {
				Outcome = SuperpowerOutcome.Failed,
				AbilityId = abilityId,
				StartCooldown = false,
				ReasonCode = reason,
				FixedResultZh = fixedResultZh,
				Facts = NewFacts(extraFacts)
			};

		public static SuperpowerResult Info(string fixedResultZh, params string[] extraFacts) =>
			new() {
				Outcome = SuperpowerOutcome.Info,
				AbilityId = SuperpowerIds.ListPowers,
				StartCooldown = false,
				FixedResultZh = fixedResultZh,
				Facts = NewFacts(extraFacts)
			};

		private static List<string> NewFacts(string[] extraFacts) =>
			extraFacts == null || extraFacts.Length == 0
				? new List<string>()
				: new List<string>(extraFacts);

		public string ResolveFixedResultZh() {
			if (!string.IsNullOrWhiteSpace(FixedResultZh)) {
				return FixedResultZh.Trim();
			}

			if (Facts != null) {
				foreach (string f in Facts) {
					if (string.IsNullOrWhiteSpace(f) || f.StartsWith("拒绝风格") || f.StartsWith("台词模板：")) {
						continue;
					}

					return f.Trim();
				}
			}

			return Outcome switch {
				SuperpowerOutcome.Ok => "已完成",
				SuperpowerOutcome.Quoted => "已出报价待确认",
				SuperpowerOutcome.Refused => "未答应",
				SuperpowerOutcome.Failed => "未能完成",
				SuperpowerOutcome.Info => "已说明",
				_ => "已处理"
			};
		}

		public string BuildFactsBlock() {
			var sb = new StringBuilder();
			sb.Append("本轮执行结果词：").Append(ResolveFixedResultZh());

			if (Facts != null && Facts.Count > 0) {
				bool any = false;
				for (int i = 0; i < Facts.Count; i++) {
					string f = Facts[i];
					if (string.IsNullOrWhiteSpace(f) || f.StartsWith("台词模板：")) {
						continue;
					}

					// RefuseStyleZh only reaches the reply LLM on Refused outcomes.
					if (f.StartsWith("拒绝风格") && Outcome != SuperpowerOutcome.Refused) {
						continue;
					}

					if (f == FixedResultZh) {
						continue;
					}

					if (!any) {
						sb.Append("；补充：");
						any = true;
					}
					else {
						sb.Append('；');
					}

					sb.Append(f);
				}
			}

			if (!string.IsNullOrEmpty(ReplyHint)) {
				sb.Append("；").Append(ReplyHint);
			}

			sb.Append("。回复要求：回复中必须有结果内容");
			return sb.ToString();
		}

		/// <summary>Fallback line for list_powers when the reply LLM omits the canonical phrase.</summary>
		public string BuildInfoFallbackReply() {
			var sb = new StringBuilder();
			sb.Append(ResolveFixedResultZh());
			if (Facts == null || Facts.Count == 0) {
				return sb.ToString();
			}

			bool any = false;
			for (int i = 0; i < Facts.Count; i++) {
				string f = Facts[i];
				if (string.IsNullOrWhiteSpace(f) || f.StartsWith("拒绝风格") || f.StartsWith("台词模板：")) {
					continue;
				}

				if (!any) {
					sb.Append('：');
					any = true;
				}
				else {
					sb.Append('；');
				}

				sb.Append(f.Trim());
			}

			return sb.ToString();
		}
	}
}
