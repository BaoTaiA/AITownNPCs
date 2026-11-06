using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace AITownNPCs.Common.Ai.Combat
{
	/// <summary>Port of Palico Reachability — bounded point ray on solid tiles.</summary>
	public static class NpcCombatReachability
	{
		public static bool Clear(Vector2 from, Vector2 to, float maximumDistance = 600f) {
			if (!float.IsFinite(from.X) || !float.IsFinite(from.Y) || !float.IsFinite(to.X) || !float.IsFinite(to.Y)) {
				return false;
			}

			float distance = Vector2.Distance(from, to);
			if (distance > maximumDistance) {
				return false;
			}

			int steps = Math.Max(1, (int)Math.Ceiling(distance / 4));
			for (int i = 0; i <= steps; i++) {
				Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
				int x = (int)(point.X / 16);
				int y = (int)(point.Y / 16);
				if (!WorldGen.InWorld(x, y, 1) || WorldGen.SolidTile(x, y)) {
					return false;
				}
			}

			return true;
		}
	}
}
