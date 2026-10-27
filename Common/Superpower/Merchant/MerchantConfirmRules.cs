using System;
using System.Collections.Generic;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Merchant
{
	public static class MerchantConfirmRules
	{
		private static HashSet<string> _confirm;
		private static HashSet<string> _cancel;

		private static HashSet<string> Confirm =>
			_confirm ??= new HashSet<string>(AliasLists.Get("MerchantConfirm"), StringComparer.OrdinalIgnoreCase);

		private static HashSet<string> Cancel =>
			_cancel ??= new HashSet<string>(AliasLists.Get("MerchantCancel"), StringComparer.OrdinalIgnoreCase);

		public static bool IsConfirm(string trimmed) =>
			!string.IsNullOrEmpty(trimmed) && Confirm.Contains(trimmed);

		public static bool IsCancel(string trimmed) =>
			!string.IsNullOrEmpty(trimmed) && Cancel.Contains(trimmed);
	}
}
