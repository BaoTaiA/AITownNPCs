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

		private const float ShimmerClusterDist = 100f;
		private const float FurnitureClusterDist = 24f;
		/// <summary>LargePiles2 style whose top-left frame is the enchanted sword shrine.</summary>
		private const int SwordShrineFrameX = 918;
		/// <summary>Shadow Orb tile is one sheet: style 0 is the orb, the next style is the crimson heart.</summary>
		private const int ShadowOrbStyleWidth = 36;

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
			TileID.LunarOre,
			TileID.Amethyst, TileID.Topaz, TileID.Sapphire,
			TileID.Emerald, TileID.Ruby, TileID.Diamond,
			TileID.TreeAmber
		};

		public static ScanSession Begin() {
			int w = Main.maxTilesX;
			int h = Main.maxTilesY;
			if (w <= 0 || h <= 0) {
				return null;
			}

			return new ScanSession(w, h);
		}

		public sealed class ScanSession
		{
			public readonly ScanBuffer Buffer = new();
			private readonly int _w;
			private readonly int _h;
			private readonly int _cellsX;
			private readonly int _cellsY;
			private readonly int _oceanLeft;
			private readonly int _oceanRight;
			private readonly int _surfaceBandTop;
			private readonly int _surfaceBandBot;
			private readonly int[] _surfaceSandPerColumn;
			private readonly Dictionary<string, int[,]> _biomeCounts = new();
			private readonly Dictionary<string, Dictionary<long, int>> _structureCounts = new();
			private readonly Dictionary<string, Dictionary<long, long>> _structureSumX = new();
			private readonly Dictionary<string, Dictionary<long, long>> _structureSumY = new();
			private int _x;

			internal ScanSession(int w, int h) {
				_w = w;
				_h = h;
				foreach (int ore in OreTypes) {
					Buffer.OreLocations[ore] = new List<Vector2>();
				}

				_cellsX = (w + BiomeCell - 1) / BiomeCell;
				_cellsY = (h + BiomeCell - 1) / BiomeCell;
				_oceanLeft = (int)(w * OceanEdgeFraction);
				_oceanRight = w - _oceanLeft;
				_surfaceSandPerColumn = new int[w];
				_surfaceBandTop = Math.Max(0, (int)Main.worldSurface - 80);
				_surfaceBandBot = Math.Min(h - 1, (int)Main.worldSurface + 40);
			}

			/// <summary>Scan the next columns. True when the buffer is complete.</summary>
			public bool Step(int columns) {
				if (columns < 1) {
					columns = 1;
				}

				int end = Math.Min(_x + columns, _w);
				for (int x = _x; x < end; x++) {
					for (int y = 0; y < _h; y++) {
						Visit(x, y);
					}
				}

				_x = end;
				if (_x < _w) {
					return false;
				}

				Finish();
				return true;
			}

			private void Visit(int x, int y) {
				Tile tile = Main.tile[x, y];
				if (tile == null) {
					return;
				}

				if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Shimmer) {
					AddCluster(Buffer.Shimmer, x, y, ShimmerClusterDist);
				}

				if (!tile.HasTile) {
					return;
				}

				ushort type = tile.TileType;
				string biome = BiomeKeyFromTile(type);
				if (biome != null) {
					AddBiome(biome, x, y);
				}

				if (type == TileID.Sand && x >= _oceanLeft && x < _oceanRight
				    && y >= _surfaceBandTop && y <= _surfaceBandBot) {
					_surfaceSandPerColumn[x]++;
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

				if (Buffer.OreLocations.TryGetValue(type, out List<Vector2> oreList)) {
					AddCluster(oreList, x, y, OreClusterDist);
				}

				if (type == TileID.Heart) {
					AddCluster(Buffer.LifeCrystals, x, y, LifeClusterDist);
				}
				else if (type == TileID.LifeFruit) {
					AddCluster(Buffer.LifeFruits, x, y, LifeClusterDist);
				}
				else if (type == TileID.ShadowOrbs) {
					List<Vector2> list = tile.TileFrameX < ShadowOrbStyleWidth
						? Buffer.ShadowOrbs
						: Buffer.CrimsonHearts;
					AddCluster(list, x, y, LifeClusterDist);
				}
				else if (type == TileID.LargePiles2 && tile.TileFrameX == SwordShrineFrameX && tile.TileFrameY == 0) {
					AddCluster(Buffer.SwordShrines, x, y, LifeClusterDist);
				}
				else if (type == TileID.Hellforge) {
					AddCluster(Buffer.Hellforges, x, y, FurnitureClusterDist);
				}
				else if (type == TileID.DemonAltar) {
					AddCluster(Buffer.Altars, x, y, FurnitureClusterDist);
				}
			}

			private void Finish() {
				FinalizeBiomes(Buffer, _biomeCounts, _cellsX, _cellsY);
				FinalizeDesert(Buffer, _surfaceSandPerColumn, _oceanLeft, _oceanRight, _surfaceBandTop, _surfaceBandBot);
				FinalizeOcean(Buffer, _oceanLeft, _oceanRight);
				FinalizeStructures(Buffer, _structureCounts, _structureSumX, _structureSumY);

				if (Main.dungeonX > 0 && Main.dungeonY > 0) {
					Buffer.DungeonPos = new Vector2(Main.dungeonX, Main.dungeonY);
					Buffer.HasDungeon = true;
				}

				Rectangle ud = GenVars.UndergroundDesertLocation;
				if (ud.Width > 10 && ud.Height > 10) {
					AddBiomeCenter(Buffer, "地下沙漠", new Vector2(ud.Center.X, ud.Center.Y));
				}

				ScanChests(Buffer);
			}

			private void TouchStructure(string key, int x, int y) {
				if (!_structureCounts.TryGetValue(key, out Dictionary<long, int> counts)) {
					counts = new Dictionary<long, int>();
					_structureCounts[key] = counts;
					_structureSumX[key] = new Dictionary<long, long>();
					_structureSumY[key] = new Dictionary<long, long>();
				}

				int gx = x / StructureGrid;
				int gy = y / StructureGrid;
				long gkey = ((long)gx << 32) | (uint)gy;
				counts.TryGetValue(gkey, out int c);
				counts[gkey] = c + 1;
				_structureSumX[key].TryGetValue(gkey, out long sx);
				_structureSumY[key].TryGetValue(gkey, out long sy);
				_structureSumX[key][gkey] = sx + x;
				_structureSumY[key][gkey] = sy + y;
			}

			private void AddBiome(string key, int x, int y) {
				if (!_biomeCounts.TryGetValue(key, out int[,] grid)) {
					grid = new int[_cellsX, _cellsY];
					_biomeCounts[key] = grid;
				}

				int cx = Math.Clamp(x / BiomeCell, 0, _cellsX - 1);
				int cy = Math.Clamp(y / BiomeCell, 0, _cellsY - 1);
				grid[cx, cy]++;
			}
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

			if (type == TileID.Granite) {
				return "花岗岩";
			}

			if (type == TileID.Marble) {
				return "大理石";
			}

			return null;
		}

		private static void FinalizeBiomes(ScanBuffer dest, Dictionary<string, int[,]> biomeCounts, int cellsX, int cellsY) {
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

					AddBiomeCenter(dest, kvp.Key, new Vector2(r.sumX / (float)r.count, r.sumY / (float)r.count));
				}
			}
		}

		private static void FinalizeDesert(ScanBuffer dest, int[] sandPerCol, int oceanLeft, int oceanRight, int bandTop, int bandBot) {
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
			AddBiomeCenter(dest, "沙漠", new Vector2(mid, y));
		}

		private static void FinalizeOcean(ScanBuffer dest, int oceanLeft, int oceanRight) {
			float y = (float)Main.worldSurface;
			AddBiomeCenter(dest, "海洋", new Vector2(oceanLeft / 2f, y));
			AddBiomeCenter(dest, "海洋", new Vector2((Main.maxTilesX + oceanRight) / 2f, y));
		}

		private static void FinalizeStructures(
			ScanBuffer dest,
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
							dest.SkyIslands.Add(pos);
							break;
						case "dungeon":
							if (!dest.HasDungeon) {
								dest.DungeonPos = pos;
								dest.HasDungeon = true;
							}

							break;
						case "temple":
							dest.TemplePos = pos;
							dest.HasTemple = true;
							break;
						case "tree":
							dest.LifeTrees.Add(pos);
							break;
						case "pyramid":
							dest.Pyramids.Add(pos);
							break;
						case "hive":
							dest.Hives.Add(pos);
							break;
					}
				}
			}
		}

		private static void AddBiomeCenter(ScanBuffer dest, string key, Vector2 pos) {
			if (!dest.BiomeCenters.TryGetValue(key, out List<Vector2> list)) {
				list = new List<Vector2>();
				dest.BiomeCenters[key] = list;
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

		private static void ScanChests(ScanBuffer dest) {
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

				dest.Chests.Add(info);
			}
		}
	}
}
