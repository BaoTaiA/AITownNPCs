using System;
using AITownNPCs.Common.Ai.Combat.Boss;
using AITownNPCs.Common.Ai.Combat.Stance;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.Ai.Combat.Normal
{
	/// <summary>Port of Palico CompanionTargeting for town NPC followers.</summary>
	public sealed class NpcCombatTargeting
	{
		NPC current;
		int currentType;
		int currentWho = -1;
		ulong lastSeen;

		public const int SightGrace = 45;

		public bool Obscured { get; private set; }

		bool SameTarget =>
			current is { active: true }
			&& current.type == currentType
			&& current.whoAmI == currentWho;

		public bool Holding => Obscured && SameTarget && Main.GameUpdateCount - lastSeen < SightGrace;

		public NPC VisibleTarget => SameTarget && !Obscured ? current : null;

		public NPC Current => SameTarget ? current : null;

		public void Clear() {
			current = null;
			currentType = 0;
			currentWho = -1;
			Obscured = false;
		}

		NPC Remember(NPC target) {
			current = target;
			currentType = target?.type ?? 0;
			currentWho = target?.whoAmI ?? -1;
			Obscured = false;
			if (target != null) {
				lastSeen = Main.GameUpdateCount;
			}

			return target;
		}

		public static bool Destructible(NPC npc) =>
			npc is { active: true, friendly: false, dontTakeDamage: false, immortal: false }
			&& npc.type >= NPCID.None
			&& NPCID.Sets.ProjectileNPC[npc.type];

		public static bool Incoming(NPC npc, Vector2 point) {
			if (!Destructible(npc) || npc.velocity.LengthSquared() < 0.1f) {
				return false;
			}

			float t = Math.Clamp(
				Vector2.Dot(point - npc.Center, npc.velocity) / npc.velocity.LengthSquared(),
				0f,
				18f);
			return t > 0f
				&& Vector2.DistanceSquared(npc.Center + npc.velocity * t, point) < 48f * 48f;
		}

		static bool InReach(Player owner, Vector2 cat, NPC npc, float range) =>
			npc is { active: true }
			&& (npc.CanBeChasedBy() || Destructible(npc))
			&& !NpcBossCombat.IsBossEncounterNpc(npc)
			&& Vector2.DistanceSquared(npc.Center, owner.Center) < range * range
			&& Vector2.DistanceSquared(npc.Center, cat) < range * range;

		public static bool Valid(Player owner, Vector2 cat, NPC npc) =>
			Valid(owner, cat, npc, NpcWeaponReach.SearchRange);

		public static bool Valid(Player owner, Vector2 cat, NPC npc, float range) =>
			InReach(owner, cat, npc, range) && NpcWeaponReach.Clear(cat, npc.Center);

		public static NPC Marked(Player owner, Vector2 cat) {
			int id = owner.MinionAttackTargetNPC;
			return id >= 0 && id < Main.maxNPCs
				&& Valid(owner, cat, Main.npc[id], NpcWeaponReach.TrackingRange)
					? Main.npc[id]
					: null;
		}

		/// <summary>Active combat select (Palico non-passive, non-cooperative). working=false.</summary>
		public NPC Select(Player owner, Vector2 cat) {
			if (Marked(owner, cat) is { } marked) {
				return Remember(marked);
			}

			bool retained = SameTarget;

			bool Intercept(NPC n) => Incoming(n, owner.Center) || Incoming(n, cat);

			bool Eligible(NPC n) =>
				Valid(owner, cat, n, retained && n == current ? NpcWeaponReach.TrackingRange : NpcWeaponReach.SearchRange);

			bool Protect(NPC n) =>
				Vector2.DistanceSquared(n.Center, owner.Center) < 160 * 160
				&& NpcWeaponReach.Clear(owner.Center, n.Center);

			float Distance(NPC n) => Vector2.Distance(n.Center, Protect(n) ? owner.Center : cat);

			NPC best = null;
			foreach (NPC n in Main.npc) {
				if (!Eligible(n)) {
					continue;
				}

				if (best == null
					|| Intercept(n) && !Intercept(best)
					|| Intercept(n) == Intercept(best)
						&& (Protect(n) && !Protect(best)
							|| Protect(n) == Protect(best) && Distance(n) < Distance(best))) {
					best = n;
				}
			}

			if (retained
				&& Eligible(current)
				&& best != null
				&& Intercept(current) == Intercept(best)
				&& Protect(current) == Protect(best)
				&& Distance(current) <= Distance(best) + 48f) {
				best = current;
			}

			if (best == null
				&& retained
				&& InReach(owner, cat, current, NpcWeaponReach.TrackingRange)
				&& !NpcWeaponReach.Clear(cat, current.Center)
				&& Main.GameUpdateCount - lastSeen < SightGrace) {
				Obscured = true;
				return current;
			}

			return Remember(best);
		}

		public bool InTrackingReach(Player owner, Vector2 cat) =>
			SameTarget && InReach(owner, cat, current, NpcWeaponReach.TrackingRange);
	}
}
