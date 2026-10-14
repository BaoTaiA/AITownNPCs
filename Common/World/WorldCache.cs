using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace AITownNPCs.Common.World
{
	public static class WorldCache
	{
		public static bool Ready;

		/// <summary>Biome key → one or more region centers (tile coords).</summary>
		public static readonly Dictionary<string, List<Vector2>> BiomeCenters = new();

		public static readonly List<Vector2> SkyIslands = new();
		public static Vector2 DungeonPos = Vector2.Zero;
		public static bool HasDungeon;
		public static Vector2 TemplePos = Vector2.Zero;
		public static bool HasTemple;
		public static readonly List<Vector2> LifeTrees = new();
		public static readonly List<Vector2> Pyramids = new();
		public static readonly List<Vector2> Hives = new();

		/// <summary>Tile type → ore cluster centers (tile coords).</summary>
		public static readonly Dictionary<int, List<Vector2>> OreLocations = new();

		public static readonly List<ChestInfo> Chests = new();
		public static readonly List<Vector2> LifeCrystals = new();
		public static readonly List<Vector2> LifeFruits = new();

		public static void Clear() {
			Ready = false;
			BiomeCenters.Clear();
			SkyIslands.Clear();
			DungeonPos = Vector2.Zero;
			HasDungeon = false;
			TemplePos = Vector2.Zero;
			HasTemple = false;
			LifeTrees.Clear();
			Pyramids.Clear();
			Hives.Clear();
			OreLocations.Clear();
			Chests.Clear();
			LifeCrystals.Clear();
			LifeFruits.Clear();
		}
	}
}
