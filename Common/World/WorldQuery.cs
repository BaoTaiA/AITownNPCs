using System;
using System.Collections.Generic;
using AITownNPCs.Common.Loc;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace AITownNPCs.Common.World
{
	public enum WorldQueryKind
	{
		Biome,
		Structure,
		Ore,
		Chest,
		LifeCrystal,
		LifeFruit
	}

	public sealed class WorldQueryHit
	{
		public Vector2 TargetTile;
		public string Label;
		public string LineTemplate;
	}

	public static class WorldQuery
	{
		private const int VerticalSplitTiles = 40;

		private static readonly string[] BiomeIds = {
			"jungle", "mushroom", "corruption", "crimson", "hallow", "snow",
			"desert", "underground_desert", "ocean", "granite", "marble"
		};

		private static readonly string[] StructureIds = {
			"sky", "dungeon", "temple", "tree", "pyramid", "hive", "shimmer",
			"shadow_orb", "crimson_heart", "sword", "hellforge", "altar"
		};

		private static readonly (string id, int tile)[] OreIds = {
			("copper", TileID.Copper), ("tin", TileID.Tin),
			("iron", TileID.Iron), ("lead", TileID.Lead),
			("silver", TileID.Silver), ("tungsten", TileID.Tungsten),
			("gold", TileID.Gold), ("platinum", TileID.Platinum),
			("meteorite", TileID.Meteorite),
			("demonite", TileID.Demonite), ("crimtane", TileID.Crimtane),
			("hellstone", TileID.Hellstone),
			("cobalt", TileID.Cobalt), ("palladium", TileID.Palladium),
			("mythril", TileID.Mythril), ("orichalcum", TileID.Orichalcum),
			("adamantite", TileID.Adamantite), ("titanium", TileID.Titanium),
			("chlorophyte", TileID.Chlorophyte),
			("lunar", TileID.LunarOre),
			("amethyst", TileID.Amethyst), ("topaz", TileID.Topaz),
			("sapphire", TileID.Sapphire),
			("emerald", TileID.Emerald),
			("ruby", TileID.Ruby),
			("diamond", TileID.Diamond),
			("amber", TileID.TreeAmber)
		};

		private static Dictionary<string, string> _biomeAliases;
		private static Dictionary<string, string> _structureAliases;
		private static Dictionary<string, int> _oreAliasToTile;

		private static Dictionary<string, string> BiomeAliases =>
			_biomeAliases ??= AliasLists.BuildMap("Biome", BiomeIds);

		private static Dictionary<string, string> StructureAliases =>
			_structureAliases ??= AliasLists.BuildMap("Structure", StructureIds);

		private static Dictionary<string, int> OreAliasToTile {
			get {
				if (_oreAliasToTile != null) {
					return _oreAliasToTile;
				}

				_oreAliasToTile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				foreach ((string id, int tile) in OreIds) {
					_oreAliasToTile[id] = tile;
					foreach (string alias in AliasLists.Get("Ore." + id)) {
						if (!_oreAliasToTile.ContainsKey(alias)) {
							_oreAliasToTile[alias] = tile;
						}
					}
				}

				return _oreAliasToTile;
			}
		}

		public static bool TryResolve(string query, string target, Vector2 playerTile, out WorldQueryHit hit) {
			hit = null;
			if (!WorldCache.Ready) {
				return false;
			}

			query = (query ?? "").Trim().ToLowerInvariant();
			target = (target ?? "").Trim();
			if (string.IsNullOrEmpty(target) && query is "life_crystal" or "life_fruit") {
				target = query;
			}

			WorldQueryKind kind = ParseKind(query, target);
			string resolvedKey = null;
			Vector2? found = kind switch {
				WorldQueryKind.Biome => FindNearestBiome(target, playerTile, out resolvedKey),
				WorldQueryKind.Structure => FindNearestStructure(target, playerTile, out resolvedKey),
				WorldQueryKind.Ore => FindNearestOre(target, playerTile, out _),
				WorldQueryKind.Chest => FindNearestChestItem(target, playerTile, out _),
				WorldQueryKind.LifeCrystal => FindNearest(WorldCache.LifeCrystals, playerTile),
				WorldQueryKind.LifeFruit => FindNearest(WorldCache.LifeFruits, playerTile),
				_ => null
			};

			string label = kind switch {
				WorldQueryKind.LifeCrystal => ModText.Get("Runtime.World.Label.LifeCrystal"),
				WorldQueryKind.LifeFruit => ModText.Get("Runtime.World.Label.LifeFruit"),
				WorldQueryKind.Biome when !string.IsNullOrEmpty(resolvedKey) =>
					ModText.Get("Runtime.World.Biome." + resolvedKey),
				WorldQueryKind.Structure when !string.IsNullOrEmpty(resolvedKey) =>
					ModText.Get("Runtime.World.Structure." + resolvedKey),
				_ => string.IsNullOrEmpty(target) ? query : target
			};

			if (found == null) {
				return false;
			}

			hit = new WorldQueryHit {
				TargetTile = found.Value,
				Label = label,
				LineTemplate = BuildLineTemplate(playerTile, found.Value)
			};
			return true;
		}

		public static string BuildLineTemplate(Vector2 playerTile, Vector2 targetTile) {
			float dx = targetTile.X - playerTile.X;
			float dy = targetTile.Y - playerTile.Y;
			int ax = (int)Math.Round(Math.Abs(dx));
			int ay = (int)Math.Round(Math.Abs(dy));
			string depthId = DescribeLayerAt((int)Math.Round(targetTile.Y));
			string depth = ModText.Get("Runtime.World.Layer." + depthId);

			if (ay < VerticalSplitTiles) {
				string dirId = PrimaryDir(dx, dy);
				string dir = ModText.Get("Runtime.World.Dir." + dirId);
				return ModText.Get("Runtime.World.LineNear", dir, Math.Max(ax, ay), depth);
			}

			if (ax < VerticalSplitTiles) {
				string vId = dy > 0 ? "down" : "up";
				string v = ModText.Get("Runtime.World.Dir." + vId);
				return ModText.Get("Runtime.World.LineVertical", v, ay, depth);
			}

			string hId = dx >= 0 ? "right" : "left";
			string h = ModText.Get("Runtime.World.Dir." + hId);
			string climb = dy > 0
				? ModText.Get("Runtime.World.ClimbDown")
				: ModText.Get("Runtime.World.ClimbUp");
			return ModText.Get("Runtime.World.LineCorner", h, ax, climb, ay, depth);
		}

		/// <summary>English direction ids: right / left / up / down.</summary>
		public static string PrimaryDir(float dx, float dy) {
			if (Math.Abs(dx) >= Math.Abs(dy)) {
				return dx >= 0 ? "right" : "left";
			}

			return dy >= 0 ? "down" : "up";
		}

		/// <summary>English layer ids: hell / cave / underground / sky / surface.</summary>
		public static string DescribeLayerAt(int tileY) {
			if (tileY > Main.UnderworldLayer) {
				return "hell";
			}

			if (tileY > Main.rockLayer) {
				return "cave";
			}

			if (tileY > Main.worldSurface) {
				return "underground";
			}

			if (tileY < Main.worldSurface * 0.35) {
				return "sky";
			}

			return "surface";
		}

		private static WorldQueryKind ParseKind(string query, string target) {
			if (AliasLists.EqualsAny(query, "QueryKind.chest")
			    || AliasLists.ContainsAny(target, "QueryKind.chest")) {
				return WorldQueryKind.Chest;
			}

			if (AliasLists.EqualsAny(query, "QueryKind.life_crystal")
			    || AliasLists.ContainsAny(target, "QueryKind.life_crystal")) {
				return WorldQueryKind.LifeCrystal;
			}

			if (AliasLists.EqualsAny(query, "QueryKind.life_fruit")
			    || AliasLists.ContainsAny(target, "QueryKind.life_fruit")) {
				return WorldQueryKind.LifeFruit;
			}

			if (StructureAliases.ContainsKey(target) || ContainsAnyKey(target, StructureAliases.Keys)) {
				return WorldQueryKind.Structure;
			}

			if (BiomeAliases.ContainsKey(target) || ContainsAnyKey(target, BiomeAliases.Keys)) {
				return WorldQueryKind.Biome;
			}

			if (MatchOre(target) >= 0 || AliasLists.EqualsAny(query, "QueryKind.ore")) {
				return WorldQueryKind.Ore;
			}

			if (AliasLists.EqualsAny(query, "QueryKind.structure")) {
				return WorldQueryKind.Structure;
			}

			return WorldQueryKind.Biome;
		}

		private static bool ContainsAnyKey(string target, IEnumerable<string> keys) {
			foreach (string k in keys) {
				if (target.Contains(k, StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}

			return false;
		}

		private static Vector2? FindNearestBiome(string target, Vector2 player, out string key) {
			key = AliasLists.ResolveId(BiomeAliases, target);
			if (key == null || !WorldCache.BiomeCenters.TryGetValue(key, out List<Vector2> list) || list.Count == 0) {
				return null;
			}

			return FindNearest(list, player);
		}

		private static Vector2? FindNearestStructure(string target, Vector2 player, out string key) {
			key = AliasLists.ResolveId(StructureAliases, target);
			if (key == null) {
				return null;
			}

			return key switch {
				"sky" => FindNearest(WorldCache.SkyIslands, player),
				"dungeon" => WorldCache.HasDungeon ? WorldCache.DungeonPos : null,
				"temple" => WorldCache.HasTemple ? WorldCache.TemplePos : null,
				"tree" => FindNearest(WorldCache.LifeTrees, player),
				"pyramid" => FindNearest(WorldCache.Pyramids, player),
				"hive" => FindNearest(WorldCache.Hives, player),
				"shimmer" => FindNearest(WorldCache.Shimmer, player),
				"shadow_orb" => FindNearest(WorldCache.ShadowOrbs, player),
				"crimson_heart" => FindNearest(WorldCache.CrimsonHearts, player),
				"sword" => FindNearest(WorldCache.SwordShrines, player),
				"hellforge" => FindNearest(WorldCache.Hellforges, player),
				"altar" => FindNearest(WorldCache.Altars, player),
				_ => null
			};
		}

		private static Vector2? FindNearestOre(string target, Vector2 player, out int tileType) {
			tileType = MatchOre(target);
			if (tileType < 0 || !WorldCache.OreLocations.TryGetValue(tileType, out List<Vector2> list)
			    || list.Count == 0) {
				return null;
			}

			return FindNearest(list, player);
		}

		private static int MatchOre(string target) {
			if (string.IsNullOrEmpty(target)) {
				return -1;
			}

			foreach (KeyValuePair<string, int> kv in OreAliasToTile) {
				if (target.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)) {
					return kv.Value;
				}
			}

			return -1;
		}

		private static Vector2? FindNearestChestItem(string target, Vector2 player, out int itemType) {
			itemType = 0;
			if (string.IsNullOrEmpty(target) || WorldCache.Chests.Count == 0) {
				return null;
			}

			Vector2? best = null;
			float bestDist = float.MaxValue;
			foreach (ChestInfo chest in WorldCache.Chests) {
				foreach (int type in chest.ItemTypes) {
					string name = Lang.GetItemNameValue(type);
					if (string.IsNullOrEmpty(name)) {
						continue;
					}

					if (!name.Contains(target, StringComparison.OrdinalIgnoreCase)
					    && !target.Contains(name, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}

					float d = Vector2.DistanceSquared(chest.Position, player);
					if (d < bestDist) {
						bestDist = d;
						best = chest.Position;
						itemType = type;
					}
				}
			}

			return best;
		}

		private static Vector2? FindNearest(List<Vector2> list, Vector2 player) {
			if (list == null || list.Count == 0) {
				return null;
			}

			Vector2 best = list[0];
			float bestDist = Vector2.DistanceSquared(best, player);
			for (int i = 1; i < list.Count; i++) {
				float d = Vector2.DistanceSquared(list[i], player);
				if (d < bestDist) {
					bestDist = d;
					best = list[i];
				}
			}

			return best;
		}
	}
}
