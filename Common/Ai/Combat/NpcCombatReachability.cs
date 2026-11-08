using Microsoft.Xna.Framework;
using Terraria;

namespace AITownNPCs.Common.Ai.Combat
{
	/// <summary>Line of sight for town-NPC combat — vanilla Collision.CanHit.</summary>
	public static class NpcCombatReachability
	{
		public static bool Clear(Vector2 from, Vector2 to, float maximumDistance = 600f) {
			if (!float.IsFinite(from.X) || !float.IsFinite(from.Y) || !float.IsFinite(to.X) || !float.IsFinite(to.Y)) {
				return false;
			}

			if (Vector2.Distance(from, to) > maximumDistance) {
				return false;
			}

			return Collision.CanHit(from, 1, 1, to, 1, 1);
		}
	}
}
