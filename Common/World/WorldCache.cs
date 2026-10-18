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
		public static readonly List<Vector2> Shimmer = new();
		public static readonly List<Vector2> ShadowOrbs = new();
		public static readonly List<Vector2> CrimsonHearts = new();
		public static readonly List<Vector2> SwordShrines = new();
		public static readonly List<Vector2> Hellforges = new();
		public static readonly List<Vector2> Altars = new();

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
			Shimmer.Clear();
			ShadowOrbs.Clear();
			CrimsonHearts.Clear();
			SwordShrines.Clear();
			Hellforges.Clear();
			Altars.Clear();
		}

		/// <summary>Swap in a finished scan. The previous lists stay readable until this returns.</summary>
		public static void Apply(ScanBuffer src) {
			CopyDict(BiomeCenters, src.BiomeCenters);
			CopyList(SkyIslands, src.SkyIslands);
			DungeonPos = src.DungeonPos;
			HasDungeon = src.HasDungeon;
			TemplePos = src.TemplePos;
			HasTemple = src.HasTemple;
			CopyList(LifeTrees, src.LifeTrees);
			CopyList(Pyramids, src.Pyramids);
			CopyList(Hives, src.Hives);
			OreLocations.Clear();
			foreach (KeyValuePair<int, List<Vector2>> kv in src.OreLocations) {
				OreLocations[kv.Key] = new List<Vector2>(kv.Value);
			}

			Chests.Clear();
			Chests.AddRange(src.Chests);
			CopyList(LifeCrystals, src.LifeCrystals);
			CopyList(LifeFruits, src.LifeFruits);
			CopyList(Shimmer, src.Shimmer);
			CopyList(ShadowOrbs, src.ShadowOrbs);
			CopyList(CrimsonHearts, src.CrimsonHearts);
			CopyList(SwordShrines, src.SwordShrines);
			CopyList(Hellforges, src.Hellforges);
			CopyList(Altars, src.Altars);
			Ready = true;
		}

		private static void CopyList(List<Vector2> dest, List<Vector2> src) {
			dest.Clear();
			dest.AddRange(src);
		}

		private static void CopyDict(Dictionary<string, List<Vector2>> dest, Dictionary<string, List<Vector2>> src) {
			dest.Clear();
			foreach (KeyValuePair<string, List<Vector2>> kv in src) {
				dest[kv.Key] = new List<Vector2>(kv.Value);
			}
		}
	}

	/// <summary>Tile points gathered by one scan. Not visible to queries until <see cref="WorldCache.Apply"/>.</summary>
	public sealed class ScanBuffer
	{
		public readonly Dictionary<string, List<Vector2>> BiomeCenters = new();
		public readonly List<Vector2> SkyIslands = new();
		public Vector2 DungeonPos = Vector2.Zero;
		public bool HasDungeon;
		public Vector2 TemplePos = Vector2.Zero;
		public bool HasTemple;
		public readonly List<Vector2> LifeTrees = new();
		public readonly List<Vector2> Pyramids = new();
		public readonly List<Vector2> Hives = new();
		public readonly Dictionary<int, List<Vector2>> OreLocations = new();
		public readonly List<ChestInfo> Chests = new();
		public readonly List<Vector2> LifeCrystals = new();
		public readonly List<Vector2> LifeFruits = new();
		public readonly List<Vector2> Shimmer = new();
		public readonly List<Vector2> ShadowOrbs = new();
		public readonly List<Vector2> CrimsonHearts = new();
		public readonly List<Vector2> SwordShrines = new();
		public readonly List<Vector2> Hellforges = new();
		public readonly List<Vector2> Altars = new();
	}
}
