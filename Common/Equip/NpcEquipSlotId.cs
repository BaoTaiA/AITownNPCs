namespace AITownNPCs.Common.Equip
{
	/// <summary>Slot indices 0–9. Weapon/Ammo/Heal* aliases keep combat/nurse code readable.</summary>
	public enum NpcEquipSlotId : byte
	{
		Slot0 = 0,
		Slot1 = 1,
		Slot2 = 2,
		Slot3 = 3,
		Slot4 = 4,
		Slot5 = 5,
		Slot6 = 6,
		Slot7 = 7,
		Slot8 = 8,
		Slot9 = 9,

		Weapon = Slot0,
		Ammo = Slot1,
		HealLife = Slot0,
		HealMana = Slot1,
		Heart = Slot2
	}
}
