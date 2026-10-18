using System;
using System.Collections.Generic;
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

		private static readonly Dictionary<string, string> BiomeAliases = new(StringComparer.OrdinalIgnoreCase) {
			["丛林"] = "丛林", ["jungle"] = "丛林",
			["蘑菇"] = "蘑菇地", ["蘑菇地"] = "蘑菇地", ["发光蘑菇"] = "蘑菇地", ["mushroom"] = "蘑菇地",
			["腐化"] = "腐化", ["corruption"] = "腐化",
			["猩红"] = "猩红", ["crimson"] = "猩红",
			["神圣"] = "神圣", ["hallow"] = "神圣", ["神圣之地"] = "神圣",
			["雪"] = "雪原", ["雪原"] = "雪原", ["雪地"] = "雪原", ["冰雪"] = "雪原", ["snow"] = "雪原",
			["沙漠"] = "沙漠", ["desert"] = "沙漠",
			["地下沙漠"] = "地下沙漠",
			["海洋"] = "海洋", ["海"] = "海洋", ["beach"] = "海洋", ["海滩"] = "海洋",
			["花岗岩"] = "花岗岩", ["花岗岩洞"] = "花岗岩", ["granite"] = "花岗岩",
			["大理石"] = "大理石", ["大理石洞"] = "大理石", ["marble"] = "大理石"
		};

		private static readonly Dictionary<string, string> StructureAliases = new(StringComparer.OrdinalIgnoreCase) {
			["空岛"] = "sky", ["天空岛"] = "sky", ["浮岛"] = "sky", ["sky"] = "sky", ["island"] = "sky",
			["地牢"] = "dungeon", ["地城"] = "dungeon", ["dungeon"] = "dungeon",
			["神庙"] = "temple", ["丛林神庙"] = "temple", ["temple"] = "temple",
			["生命树"] = "tree", ["活木"] = "tree", ["活树"] = "tree",
			["金字塔"] = "pyramid", ["pyramid"] = "pyramid",
			["蜂巢"] = "hive", ["蜂房"] = "hive", ["hive"] = "hive",
			["微光之湖"] = "shimmer", ["微光"] = "shimmer", ["以太"] = "shimmer", ["shimmer"] = "shimmer", ["aether"] = "shimmer",
			["暗影珠"] = "shadow_orb", ["暗影珠子"] = "shadow_orb",
			["猩红之心"] = "crimson_heart",
			["附魔剑祠"] = "sword", ["附魔剑"] = "sword", ["剑祠"] = "sword",
			["地狱熔炉"] = "hellforge", ["hellforge"] = "hellforge",
			["恶魔祭坛"] = "altar", ["猩红祭坛"] = "altar", ["祭坛"] = "altar"
		};

		private static readonly (string hint, int tile)[] OreHints = {
			("铜", TileID.Copper), ("锡", TileID.Tin),
			("铁", TileID.Iron), ("铅", TileID.Lead),
			("银", TileID.Silver), ("钨", TileID.Tungsten),
			("金", TileID.Gold), ("铂", TileID.Platinum), ("白金", TileID.Platinum),
			("陨石", TileID.Meteorite),
			("魔矿", TileID.Demonite), ("猩红矿", TileID.Crimtane), ("血肉", TileID.Crimtane),
			("狱石", TileID.Hellstone), ("地狱石", TileID.Hellstone),
			("钴", TileID.Cobalt), ("钯", TileID.Palladium),
			("秘银", TileID.Mythril), ("山铜", TileID.Orichalcum),
			("精金", TileID.Adamantite), ("钛", TileID.Titanium),
			("叶绿", TileID.Chlorophyte),
			("夜明", TileID.LunarOre), ("月矿", TileID.LunarOre),
			("紫晶", TileID.Amethyst), ("黄玉", TileID.Topaz),
			("蓝玉", TileID.Sapphire), ("蓝宝石", TileID.Sapphire),
			("翡翠", TileID.Emerald), ("绿宝石", TileID.Emerald),
			("红玉", TileID.Ruby), ("红宝石", TileID.Ruby),
			("钻石", TileID.Diamond),
			("琥珀", TileID.TreeAmber),
			("copper", TileID.Copper), ("iron", TileID.Iron), ("gold", TileID.Gold),
			("hellstone", TileID.Hellstone), ("chlorophyte", TileID.Chlorophyte)
		};

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
			Vector2? found = kind switch {
				WorldQueryKind.Biome => FindNearestBiome(target, playerTile, out _),
				WorldQueryKind.Structure => FindNearestStructure(target, playerTile, out _),
				WorldQueryKind.Ore => FindNearestOre(target, playerTile, out _),
				WorldQueryKind.Chest => FindNearestChestItem(target, playerTile, out _),
				WorldQueryKind.LifeCrystal => FindNearest(WorldCache.LifeCrystals, playerTile),
				WorldQueryKind.LifeFruit => FindNearest(WorldCache.LifeFruits, playerTile),
				_ => null
			};

			string label = kind switch {
				WorldQueryKind.LifeCrystal => "生命水晶",
				WorldQueryKind.LifeFruit => "生命果",
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
			string depth = DescribeLayerAt((int)Math.Round(targetTile.Y));

			if (ay < VerticalSplitTiles) {
				string dir = PrimaryDir(dx, dy);
				return $"在你{dir}大约{Math.Max(ax, ay)}格，深度{depth}。";
			}

			if (ax < VerticalSplitTiles) {
				string v = dy > 0 ? "下" : "上";
				return $"在你{v}大约{ay}格，深度{depth}。";
			}

			string h = dx >= 0 ? "右" : "左";
			string climb = dy > 0 ? "往下挖" : "往上走";
			return $"在你{h}大约{ax}格，然后{climb}大约{ay}格，深度{depth}。";
		}

		public static string PrimaryDir(float dx, float dy) {
			if (Math.Abs(dx) >= Math.Abs(dy)) {
				return dx >= 0 ? "右" : "左";
			}

			return dy >= 0 ? "下" : "上";
		}

		public static string DescribeLayerAt(int tileY) {
			if (tileY > Main.UnderworldLayer) {
				return "地狱";
			}

			if (tileY > Main.rockLayer) {
				return "洞穴";
			}

			if (tileY > Main.worldSurface) {
				return "地下";
			}

			if (tileY < Main.worldSurface * 0.35) {
				return "天空";
			}

			return "地表";
		}

		private static WorldQueryKind ParseKind(string query, string target) {
			if (query is "chest" or "宝箱" or "箱子") {
				return WorldQueryKind.Chest;
			}

			if (query is "life_crystal" or "生命水晶" || target.Contains("生命水晶") || target.Contains("心水晶")) {
				return WorldQueryKind.LifeCrystal;
			}

			if (query is "life_fruit" or "生命果" || target.Contains("生命果")) {
				return WorldQueryKind.LifeFruit;
			}

			if (StructureAliases.ContainsKey(target) || ContainsAny(target, StructureAliases.Keys)) {
				return WorldQueryKind.Structure;
			}

			if (BiomeAliases.ContainsKey(target) || ContainsAny(target, BiomeAliases.Keys)) {
				return WorldQueryKind.Biome;
			}

			if (MatchOre(target) >= 0 || query is "ore" or "矿物" or "矿") {
				return WorldQueryKind.Ore;
			}

			if (query is "structure" or "结构") {
				return WorldQueryKind.Structure;
			}

			return WorldQueryKind.Biome;
		}

		private static bool ContainsAny(string target, IEnumerable<string> keys) {
			foreach (string k in keys) {
				if (target.Contains(k, StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}

			return false;
		}

		private static Vector2? FindNearestBiome(string target, Vector2 player, out string key) {
			key = ResolveBiomeKey(target);
			if (key == null || !WorldCache.BiomeCenters.TryGetValue(key, out List<Vector2> list) || list.Count == 0) {
				return null;
			}

			return FindNearest(list, player);
		}

		private static string ResolveBiomeKey(string target) {
			if (string.IsNullOrEmpty(target)) {
				return null;
			}

			if (BiomeAliases.TryGetValue(target, out string key)) {
				return key;
			}

			foreach (KeyValuePair<string, string> kv in BiomeAliases) {
				if (target.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)) {
					return kv.Value;
				}
			}

			return null;
		}

		private static Vector2? FindNearestStructure(string target, Vector2 player, out string key) {
			key = ResolveStructureKey(target);
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

		private static string ResolveStructureKey(string target) {
			if (string.IsNullOrEmpty(target)) {
				return null;
			}

			if (StructureAliases.TryGetValue(target, out string key)) {
				return key;
			}

			foreach (KeyValuePair<string, string> kv in StructureAliases) {
				if (target.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)) {
					return kv.Value;
				}
			}

			return null;
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

			foreach ((string hint, int tile) in OreHints) {
				if (target.Contains(hint, StringComparison.OrdinalIgnoreCase)) {
					return tile;
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
