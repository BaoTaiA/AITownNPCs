namespace AITownNPCs.Common.Superpower
{
	public enum SuperpowerMessageType : byte
	{
		NurseTeleportFx = 30,
		ArmsWeaponReclaimNotify = 31,
		/// <summary>Server → client: guide guidance target + buff.</summary>
		GuidanceStart = 32,
		/// <summary>Client → server: arms loan buff ended (manual cancel); run ReclaimAll.</summary>
		RequestArmsWeaponReclaim = 33
	}
}
