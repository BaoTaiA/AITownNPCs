using System;
using System.Collections.Generic;
using Terraria;

namespace AITownNPCs.Common.Ai.Combat
{
	/// <summary>
	/// Role-agnostic combat modifiers. Roles register providers in Load; firing only reads hooks.
	/// </summary>
	public static class NpcCombatHooks
	{
		private static readonly List<Func<NPC, float>> OutgoingMultProviders = new();
		private static readonly List<Func<NPC, float>> CritChanceProviders = new();

		public static void RegisterOutgoingDamageMult(Func<NPC, float> provider) {
			if (provider != null && !OutgoingMultProviders.Contains(provider)) {
				OutgoingMultProviders.Add(provider);
			}
		}

		public static void RegisterCritChanceAdd(Func<NPC, float> provider) {
			if (provider != null && !CritChanceProviders.Contains(provider)) {
				CritChanceProviders.Add(provider);
			}
		}

		public static void UnregisterOutgoingDamageMult(Func<NPC, float> provider) {
			if (provider != null) {
				OutgoingMultProviders.Remove(provider);
			}
		}

		public static void UnregisterCritChanceAdd(Func<NPC, float> provider) {
			if (provider != null) {
				CritChanceProviders.Remove(provider);
			}
		}

		public static float GetOutgoingMult(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 1f;
			}

			float mult = 1f;
			for (int i = 0; i < OutgoingMultProviders.Count; i++) {
				float m = OutgoingMultProviders[i](npc);
				if (m > 0f && m != 1f) {
					mult *= m;
				}
			}

			return mult;
		}

		/// <summary>0–100 style chance (Rage = 10 → 10%).</summary>
		public static float GetCritChanceAdd(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 0f;
			}

			float add = 0f;
			for (int i = 0; i < CritChanceProviders.Count; i++) {
				add += CritChanceProviders[i](npc);
			}

			return add;
		}
	}
}
