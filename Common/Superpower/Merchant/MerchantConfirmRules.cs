using System;
using System.Collections.Generic;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public static class MerchantConfirmRules
	{
		private static readonly HashSet<string> Confirm = new(StringComparer.Ordinal) {
			"可以", "确定", "确认", "行", "好", "要", "成交", "卖"
		};

		private static readonly HashSet<string> Cancel = new(StringComparer.Ordinal) {
			"不要", "算了", "取消", "不卖", "否"
		};

		public static bool IsConfirm(string trimmed) =>
			!string.IsNullOrEmpty(trimmed) && Confirm.Contains(trimmed);

		public static bool IsCancel(string trimmed) =>
			!string.IsNullOrEmpty(trimmed) && Cancel.Contains(trimmed);
	}
}
