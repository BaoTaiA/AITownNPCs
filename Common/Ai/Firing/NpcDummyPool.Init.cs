using System;
using System.Collections;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Dummy player slot bootstrap: SetupPlayer, runtime arrays, biome flags, safe visuals.
	/// </summary>
	public static partial class NpcDummyPool
	{
		private static readonly MethodInfo SetupPlayerMethod = typeof(PlayerLoader).GetMethod(
			"SetupPlayer",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

		private static readonly MethodInfo BiomeSetupPlayerMethod = typeof(BiomeLoader).GetMethod(
			"SetupPlayer",
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		/// <summary>
		/// Main.player slots are often constructed before ExtraJump / Projectile loaders finish resizing.
		/// </summary>
		private static readonly FieldInfo ExtraJumpsField = typeof(Player).GetField(
			"extraJumps",
			BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly FieldInfo ModBiomeFlagsField = typeof(Player).GetField(
			"modBiomeFlags",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static readonly FieldInfo BiomeListField = FindBiomeListField();

		private static Player EnsureInitialized(int slot) {
			Player p = Main.player[slot];
			if (p == null || !PlayerKernelSized(p)) {
				p = new Player();
				p.whoAmI = slot;
				Main.player[slot] = p;
			}

			try {
				SetupPlayerMethod?.Invoke(null, new object[] { p });
			}
			catch (Exception) {
				// ItemCheck still runs; draw is skipped for dummies.
			}

			EnsureRuntimeArrays(p);
			EnsureBiomeFlags(p);

			CopySafeVisuals(p);
			if (p.inventory == null || p.inventory.Length < 58) {
				p.inventory = new Item[58];
				for (int i = 0; i < p.inventory.Length; i++) {
					p.inventory[i] = new Item();
				}
			}

			if (p.armor == null || p.armor.Length < 20) {
				p.armor = new Item[Math.Max(20, p.armor?.Length ?? 0)];
				for (int i = 0; i < p.armor.Length; i++) {
					p.armor[i] ??= new Item();
				}
			}

			return p;
		}

		/// <summary>Lightweight re-validate before each Dummy Player.Update.</summary>
		internal static void EnsureKernelReady(Player p) {
			if (p == null) {
				return;
			}

			EnsureRuntimeArrays(p);
			EnsureBiomeFlags(p);
		}

		private static bool PlayerKernelSized(Player p) {
			if (p.ownedProjectileCounts == null || p.ownedProjectileCounts.Length < ProjectileID.Count) {
				return false;
			}

			Array jumps = ExtraJumpsField?.GetValue(p) as Array;
			if (jumps == null || jumps.Length < ExtraJumpLoader.ExtraJumpCount) {
				return false;
			}

			int neededBiomes = GetModBiomeCount();
			BitArray flags = GetModBiomeFlags(p);
			if (flags == null || flags.Length != neededBiomes) {
				return false;
			}

			return true;
		}

		private static void EnsureRuntimeArrays(Player p) {
			if (p == null) {
				return;
			}

			if (p.ownedProjectileCounts == null || p.ownedProjectileCounts.Length < ProjectileID.Count) {
				p.ownedProjectileCounts = new int[ProjectileID.Count];
			}

			try {
				int needed = ExtraJumpLoader.ExtraJumpCount;
				if (needed > 0 && ExtraJumpsField != null) {
					Array jumps = ExtraJumpsField.GetValue(p) as Array;
					if (jumps == null || jumps.Length < needed) {
						Type elem = ExtraJumpsField.FieldType.GetElementType();
						ExtraJumpsField.SetValue(p, Array.CreateInstance(elem, needed));
					}
				}
			}
			catch (Exception) {
				// ExtraJump path is best-effort; draw Suspend is the hard crash guard.
			}
		}

		private static void EnsureBiomeFlags(Player p) {
			if (p == null) {
				return;
			}

			int needed = GetModBiomeCount();
			try {
				BiomeLoader biomes = LoaderManager.Get<BiomeLoader>();
				BiomeSetupPlayerMethod?.Invoke(biomes, new object[] { p });

				Player src = FindRealPlayerForVisuals();
				BitArray srcFlags = GetModBiomeFlags(src);
				if (src != null && src != p
					&& srcFlags != null
					&& srcFlags.Length == needed) {
					BiomeLoader.CopyCustomBiomesTo(src, p);
				}
			}
			catch (Exception) {
				// Length correction below is the hard guarantee.
			}

			if (needed < 0) {
				needed = 0;
			}

			BitArray flags = GetModBiomeFlags(p);
			if (flags == null || flags.Length != needed) {
				SetModBiomeFlags(p, new BitArray(needed));
			}
		}

		private static BitArray GetModBiomeFlags(Player p) {
			if (p == null || ModBiomeFlagsField == null) {
				return null;
			}

			return ModBiomeFlagsField.GetValue(p) as BitArray;
		}

		private static void SetModBiomeFlags(Player p, BitArray flags) {
			if (p == null || ModBiomeFlagsField == null) {
				return;
			}

			ModBiomeFlagsField.SetValue(p, flags);
		}

		private static int GetModBiomeCount() {
			try {
				BiomeLoader biomes = LoaderManager.Get<BiomeLoader>();
				if (BiomeListField != null) {
					object list = BiomeListField.GetValue(biomes);
					if (list is ICollection collection) {
						return collection.Count;
					}
				}
			}
			catch (Exception) {
				// Fall through.
			}

			return 0;
		}

		private static FieldInfo FindBiomeListField() {
			Type t = typeof(BiomeLoader);
			while (t != null) {
				FieldInfo f = t.GetField("list", BindingFlags.Instance | BindingFlags.NonPublic);
				if (f != null) {
					return f;
				}

				t = t.BaseType;
			}

			return null;
		}

		private static Player FindRealPlayerForVisuals() {
			if (Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers) {
				Player local = Main.player[Main.myPlayer];
				if (local != null && local.active && !DummyWho.Contains(local.whoAmI)) {
					return local;
				}
			}

			for (int i = 0; i < Main.maxPlayers; i++) {
				Player candidate = Main.player[i];
				if (candidate != null && candidate.active && !DummyWho.Contains(i)) {
					return candidate;
				}
			}

			return null;
		}

		private static void CopySafeVisuals(Player p) {
			Player src = FindRealPlayerForVisuals();
			if (src == null) {
				return;
			}

			p.hair = src.hair;
			p.skinVariant = src.skinVariant;
			p.skinColor = src.skinColor;
			p.hairColor = src.hairColor;
			p.eyeColor = src.eyeColor;
			p.shirtColor = src.shirtColor;
			p.underShirtColor = src.underShirtColor;
			p.pantsColor = src.pantsColor;
			p.shoeColor = src.shoeColor;
			p.Male = src.Male;
		}
	}
}
