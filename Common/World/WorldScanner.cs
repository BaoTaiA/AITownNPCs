using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace AITownNPCs.Common.World
{
	public static class WorldScanner
	{
		private const int BiomeCell = 50;
		private const int BiomeCellMinTiles = 80;
		private const int StructureGrid = 100;
		private const int StructureMinTiles = 40;
		private const float OreClusterDist = 30f;
		private const int LifeClusterDist = 4;
		private const float OceanEdgeFraction = 0.05f;
		private const int DesertColumnMinSand = 25;
		private const int DesertMinColumns = 40;

		private static readonly int[] OreTypes = {
			TileID.Copper, TileID.Tin,
			TileID.Iron, TileID.Lead,
			TileID.Silver, TileID.Tungsten,
			TileID.Gold, TileID.Platinum,
			TileID.Meteorite,
			TileID.Demonite, TileID.Crimtane,
			TileID.Hellstone,
			TileID.Cobalt, TileID.Palladium,
			TileID.Mythril, TileID.Orichalcum,
			TileID.Adamantite, TileID.Titanium,
			TileID.Chlorophyte,
			TileID.LunarOre
		};

		public static void ScanAll() {
			WorldCache.Clear();

			int w = Main.maxTilesX;
			int h = Main.maxTilesY;
			if (w <= 0 || h <= 0) {
				return;
			}

			foreach (int ore in OreTypes) {
				WorldCache.OreLocations[ore] = new List<Vector2>();
			}

			int cellsX = (w + BiomeCell - 1) / BiomeCell;
			int cellsY = (h + BiomeCell - 1) / BiomeCell;
			var biomeCounts = new Dictionary<string, int[,]>();

			int oceanLeft = (int)(w * OceanEdgeFraction);
			int oceanRight = w - oceanLeft;
			int[] surfaceSandPerColumn = new int[w];
			int surfaceBandTop = Math.Max(0, (int)Main.worldSurface - 80);
			int surfaceBandBot = Math.Min(h - 1, (int)Main.worldSurface + 40);

			var structureCounts = new Dictionary<string, Dictionary<long, int>>();
			var structureSumX = new Dictionary<string, Dictionary<long, long>>();
			var structureSumY = new Dictionary<string, Dictionary<long, long>>();

			void TouchStructure(string key, int x, int y) {
				if (!structureCounts.TryGetValue(key, out Dictionary<long, int> counts)) {
					counts = new Dictionary<long, int>();
					structureCounts[key] = counts;
					structureSumX[key] = new Dictionary<long, long>();
					structureSumY[key] = new Dictionary<long, long>();
				}

				int gx = x / StructureGrid;
				int gy = y / StructureGrid;
				long gkey = ((long)gx << 32) | (uint)gy;
				counts.TryGetValue(gkey, out int c);
				counts[gkey] = c + 1;
				structureSumX[key].TryGetValue(gkey, out long sx);
				structureSumY[key].TryGetValue(gkey, out long sy);
				structureSumX[key][gkey] = sx + x;
				structureSumY[key][gkey] = sy + y;
			}

			void AddBiome(string key, int x, int y) {
				if (!biomeCounts.TryGetValue(key, out int[,] grid)) {
					grid = new int[cellsX, cellsY];
					biomeCounts[key] = grid;
				}

				int cx = Math.Clamp(x / BiomeCell, 0, cellsX - 1);
				int cy = Math.Clamp(y / BiomeCell, 0, cellsY - 1);
				grid[cx, cy]++;
			}

			for (int x = 0; x < w; x++) {
				for (int y = 0; y < h; y++) {
					Tile tile = Main.tile[x, y];
					if (tile == null || !tile.HasTile) {
						continue;
					}

					ushort type = tile.TileType;

					string biome = BiomeKeyFromTile(type);
					if (biome != null) {
						AddBiome(biome, x, y);
					}

					if (type == TileID.Sand && x >= oceanLeft && x < oceanRight
					    && y >= surfaceBandTop && y <= surfaceBandBot) {
						surfaceSandPerColumn[x]++;
					}

					if (type == TileID.Cloud || type == TileID.RainCloud || type == TileID.SnowCloud) {
						TouchStructure("sky", x, y);
					}
					else if (type == TileID.BlueDungeonBrick || type == TileID.GreenDungeonBrick
					         || type == TileID.PinkDungeonBrick) {
						TouchStructure("dungeon", x, y);
					}
					else if (type == TileID.LihzahrdBrick) {
						TouchStructure("temple", x, y);
					}
					else if (type == TileID.LivingWood || type == TileID.LeafBlock) {
						TouchStructure("tree", x, y);
					}
					else if (type == TileID.SandstoneBrick) {
						TouchStructure("pyramid", x, y);
					}
					else if (type == TileID.Hive) {
						TouchStructure("hive", x, y);
					}

					if (WorldCache.OreLocations.TryGetValue(type, out List<Vector2> oreList)) {
						AddCluster(oreList, x, y, OreClusterDist);
					}

					if (type == TileID.Heart) {
						AddCluster(WorldCache.LifeCrystals, x, y, LifeClusterDist);
					}
					else if (type == TileID.LifeFruit) {
						AddCluster(WorldCache.LifeFruits, x, y, LifeClusterDist);
					}
				}
			}

			FinalizeBiomes(biomeCounts, cellsX, cellsY);
			FinalizeDesert(surfaceSandPerColumn, oceanLeft, oceanRight, surfaceBandTop, surfaceBandBot);
			FinalizeOcean(oceanLeft, oceanRight);
			FinalizeStructures(structureCounts, structureSumX, structureSumY);

			if (Main.dungeonX > 0 && Main.dungeonY > 0) {
				WorldCache.DungeonPos = new Vector2(Main.dungeonX, Main.dungeonY);
				WorldCache.HasDungeon = true;
			}

			Rectangle ud = GenVars.UndergroundDesertLocation;
			if (ud.Width > 10 && ud.Height > 10) {
				AddBiomeCenter("地下沙漠", new Vector2(ud.Center.X, ud.Center.Y));
			}

			ScanChests();
			WorldCache.Ready = true;
		}

		private static string BiomeKeyFromTile(ushort type) {
			if (type == TileID.JungleGrass) {
				return "丛林";
			}

			if (type == TileID.MushroomGrass) {
				return "蘑菇地";
			}

			if (type == TileID.CorruptGrass || type == TileID.Ebonstone) {
				return "腐化";
			}

			if (type == TileID.CrimsonGrass || type == TileID.Crimstone) {
				return "猩红";
			}

			if (type == TileID.HallowedGrass || type == TileID.Pearlstone) {
				return "神圣";
			}

			if (type == TileID.SnowBlock || type == TileID.IceBlock) {
				return "雪原";
			}

			return null;
		}

		private static void FinalizeBiomes(Dictionary<string, int[,]> biomeCounts, int cellsX, int cellsY) {
			foreach (KeyValuePair<string, int[,]> kvp in biomeCounts) {
				int[,] grid = kvp.Value;
				bool[,] visited = new bool[cellsX, cellsY];
				var regions = new List<(long sumX, long sumY, int count, int tiles)>();

				for (int cx = 0; cx < cellsX; cx++) {
					for (int cy = 0; cy < cellsY; cy++) {
						if (visited[cx, cy] || grid[cx, cy] < BiomeCellMinTiles) {
							continue;
						}

						long sumX = 0;
						long sumY = 0;
						int cellCount = 0;
						int tileSum = 0;
						var stack = new Stack<(int x, int y)>();
						stack.Push((cx, cy));
						visited[cx, cy] = true;

						while (stack.Count > 0) {
							(int x, int y) = stack.Pop();
							int tiles = grid[x, y];
							tileSum += tiles;
							sumX += (long)(x * BiomeCell + BiomeCell / 2) * tiles;
							sumY += (long)(y * BiomeCell + BiomeCell / 2) * tiles;
							cellCount++;

							TryFlood(x + 1, y);
							TryFlood(x - 1, y);
							TryFlood(x, y + 1);
							TryFlood(x, y - 1);

							void TryFlood(int nx, int ny) {
								if (nx < 0 || ny < 0 || nx >= cellsX || ny >= cellsY || visited[nx, ny]) {
									return;
								}

								if (grid[nx, ny] < BiomeCellMinTiles) {
									return;
								}

								visited[nx, ny] = true;
								stack.Push((nx, ny));
							}
						}

						if (tileSum >= BiomeCellMinTiles * 2) {
							regions.Add((sumX, sumY, tileSum, tileSum));
						}
					}
				}

				regions.Sort((a, b) => b.tiles.CompareTo(a.tiles));
				int keep = Math.Min(regions.Count, 8);
				for (int i = 0; i < keep; i++) {
					var r = regions[i];
					if (r.count <= 0) {
						continue;
					}

					AddBiomeCenter(kvp.Key, new Vector2(r.sumX / (float)r.count, r.sumY / (float)r.count));
				}
			}
		}

		private static void FinalizeDesert(int[] sandPerCol, int oceanLeft, int oceanRight, int bandTop, int bandBot) {
			var desertCols = new List<int>();
			for (int x = oceanLeft; x < oceanRight; x++) {
				if (sandPerCol[x] >= DesertColumnMinSand) {
					desertCols.Add(x);
				}
			}

			if (desertCols.Count < DesertMinColumns) {
				return;
			}

			// Largest contiguous run of dense sand columns.
			int bestStart = 0;
			int bestLen = 1;
			int runStart = 0;
			int runLen = 1;
			for (int i = 1; i < desertCols.Count; i++) {
				if (desertCols[i] == desertCols[i - 1] + 1) {
					runLen++;
				}
				else {
					if (runLen > bestLen) {
						bestLen = runLen;
						bestStart = runStart;
					}

					runStart = i;
					runLen = 1;
				}
			}

			if (runLen > bestLen) {
				bestLen = runLen;
				bestStart = runStart;
			}

			if (bestLen < DesertMinColumns) {
				return;
			}

			int mid = desertCols[bestStart + bestLen / 2];
			int y = (bandTop + bandBot) / 2;
			AddBiomeCenter("沙漠", new Vector2(mid, y));
		}

		private static void FinalizeOcean(int oceanLeft, int oceanRight) {
			float y = (float)Main.worldSurface;
			AddBiomeCenter("海洋", new Vector2(oceanLeft / 2f, y));
			AddBiomeCenter("海洋", new Vector2((Main.maxTilesX + oceanRight) / 2f, y));
		}

		private static void FinalizeStructures(
			Dictionary<string, Dictionary<long, int>> counts,
			Dictionary<string, Dictionary<long, long>> sumX,
			Dictionary<string, Dictionary<long, long>> sumY) {
			foreach (KeyValuePair<string, Dictionary<long, int>> kvp in counts) {
				string key = kvp.Key;
				foreach (KeyValuePair<long, int> cell in kvp.Value) {
					if (cell.Value < StructureMinTiles) {
						continue;
					}

					float x = sumX[key][cell.Key] / (float)cell.Value;
					float y = sumY[key][cell.Key] / (float)cell.Value;
					var pos = new Vector2(x, y);
					switch (key) {
						case "sky":
							WorldCache.SkyIslands.Add(pos);
							break;
						case "dungeon":
							if (!WorldCache.HasDungeon) {
								WorldCache.DungeonPos = pos;
								WorldCache.HasDungeon = true;
							}

							break;
						case "temple":
							WorldCache.TemplePos = pos;
							WorldCache.HasTemple = true;
							break;
						case "tree":
							WorldCache.LifeTrees.Add(pos);
							break;
						case "pyramid":
							WorldCache.Pyramids.Add(pos);
							break;
						case "hive":
							WorldCache.Hives.Add(pos);
							break;
					}
				}
			}
		}

		private static void AddBiomeCenter(string key, Vector2 pos) {
			if (!WorldCache.BiomeCenters.TryGetValue(key, out List<Vector2> list)) {
				list = new List<Vector2>();
				WorldCache.BiomeCenters[key] = list;
			}

			list.Add(pos);
		}

		private static void AddCluster(List<Vector2> clusters, int x, int y, float maxDist) {
			var pos = new Vector2(x, y);
			for (int i = 0; i < clusters.Count; i++) {
				if (Vector2.Distance(clusters[i], pos) < maxDist) {
					return;
				}
			}

			clusters.Add(pos);
		}

		private static void ScanChests() {
			for (int i = 0; i < Main.maxChests; i++) {
				Chest chest = Main.chest[i];
				if (chest == null) {
					continue;
				}

				var info = new ChestInfo {
					Position = new Vector2(chest.x, chest.y)
				};
				Item[] items = chest.item;
				if (items != null) {
					for (int j = 0; j < items.Length; j++) {
						Item it = items[j];
						if (it != null && it.type > ItemID.None && it.stack > 0) {
							info.ItemTypes.Add(it.type);
						}
					}
				}

				WorldCache.Chests.Add(info);
			}
		}
	}
}
