using System;
using System.Collections.Generic;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Firing;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Roles.Dryad;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AITownNPCs.Common.UI
{
	/// <summary>Read-only stats rows + vanilla-style buff icons for a town NPC.</summary>
	public static class NpcStatsPanel
	{
		public const int Width = 380;
		public const int BodyTop = 52;
		private const float Scale = 0.85f;
		private const int RowH = 26;
		private const int BuffIcon = 32;
		private const int BuffGap = 6;
		private const int MaxBuffIcons = 20;
		private const int EyeSize = 22;

		private static readonly List<(int type, int time)> BuffScratch = new();

		public static int MeasureHeight(NPC npc) {
			int rows = 7;
			int buffRows = 1;
			CollectBuffs(npc, BuffScratch);
			int count = Math.Min(BuffScratch.Count, MaxBuffIcons);
			int perRow = Math.Max(1, (Width - 32) / (BuffIcon + BuffGap));
			buffRows = count <= 0 ? 1 : (count + perRow - 1) / perRow;

			return BodyTop + 8 + rows * RowH + 28 + buffRows * (BuffIcon + 18) + 16;
		}

		public static void Draw(SpriteBatch batch, NPC npc, Rectangle area, Point mouse, bool clicked) {
			if (npc == null || !npc.active) {
				return;
			}

			float labelW = MeasureLabelColumn();
			float valueX = area.X + labelW + 16f;
			int y = area.Y;

			void Row(string key, string value) {
				Utils.DrawBorderString(batch, L(key), new Vector2(area.X, y), Color.LightSteelBlue, Scale);
				Utils.DrawBorderString(batch, value, new Vector2(valueX, y), Color.White, Scale);
				y += RowH;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			Item weapon = equip?.Weapon;
			int flat = NpcLifeStatScale.GetAttackBonus(npc);
			bool hasWeapon = weapon != null && !weapon.IsAir;
			bool usedDummyPreview = false;
			int attack = flat;
			float crit = NpcLifeStatScale.GetCritChanceAdd(npc);

			if (hasWeapon && NpcDummyPool.TryGetDummy(npc.whoAmI, out Player dummy) && dummy != null && dummy.active) {
				Item held = dummy.inventory[dummy.selectedItem];
				Item sample = held != null && !held.IsAir ? held : weapon;
				attack = dummy.GetWeaponDamage(sample) + flat;
				crit = dummy.GetWeaponCrit(sample) + NpcLifeStatScale.GetCritChanceAdd(npc);
				usedDummyPreview = true;
			}
			else if (hasWeapon) {
				attack = weapon.damage + flat;
			}

			float regenPerSec = npc.lifeRegen / 2f;

			Row("Life", $"{npc.life} / {npc.lifeMax}");
			Row("Regen", $"{regenPerSec:0.##} / s");
			Row("Defense", $"{TownNpcAuraBuffs.GetEffectiveDefense(npc)}");
			string attackText = hasWeapon ? $"{attack}" : (flat > 0 ? $"+{flat}" : "—");
			int attackRowY = y;
			Row("Attack", attackText);
			if (!usedDummyPreview && hasWeapon) {
				var tipRect = new Rectangle(area.X, attackRowY - 2, area.Width, RowH);
				if (tipRect.Contains(mouse)) {
					Main.LocalPlayer.mouseInterface = true;
					Main.instance.MouseText(Language.GetTextValue("Mods.AITownNPCs.UI.StatsAttackPotionHint"));
				}
			}

			Row("Crit", $"{crit:0.#}%");
			int minions = NpcLifeStatScale.GetMaxMinions(npc) + TownNpcAuraBuffs.GetMinionSlotBonus(npc);
			Row("Minions", $"{minions}");
			Row("Sentries", $"{NpcLifeStatScale.GetMaxTurrets(npc)}");

			y += 6;
			TownNpcAuraBuffs aura = npc.GetGlobalNPC<TownNpcAuraBuffs>();
			Utils.DrawBorderString(batch, L("Buffs"), new Vector2(area.X, y), Color.Gold, Scale);
			DrawOverheadEyeToggle(batch, area, y, mouse, clicked, aura);
			y += 24;

			CollectBuffs(npc, BuffScratch);
			if (BuffScratch.Count == 0) {
				Utils.DrawBorderString(batch, L("BuffsNone"), new Vector2(area.X, y), Color.Gray, Scale);
				return;
			}

			int x = area.X;
			int rowStartX = area.X;
			int drawn = 0;
			foreach (var (type, time) in BuffScratch) {
				if (drawn >= MaxBuffIcons) {
					break;
				}

				if (type <= 0 || type >= TextureAssets.Buff.Length) {
					continue;
				}

				if (x + BuffIcon > area.Right) {
					x = rowStartX;
					y += BuffIcon + 18;
				}

				Texture2D tex = TextureAssets.Buff[type].Value;
				Rectangle icon = new(x, y, BuffIcon, BuffIcon);
				batch.Draw(tex, icon, Color.White);

				bool hideTime = time <= 0
					|| (type < Main.buffNoTimeDisplay.Length && Main.buffNoTimeDisplay[type]);
				if (!hideTime) {
					string timeText = Lang.LocalizedDuration(
						TimeSpan.FromSeconds(Math.Max(0, time / 60)),
						abbreviated: true,
						showAllAvailableUnits: false);
					Utils.DrawBorderString(batch, timeText, new Vector2(x, y + BuffIcon), Color.White, 0.7f);
				}

				if (icon.Contains(mouse) || new Rectangle(x, y, BuffIcon, BuffIcon + 14).Contains(mouse)) {
					Main.LocalPlayer.mouseInterface = true;
					Main.instance.MouseText(Lang.GetBuffName(type) + "\n" + Lang.GetBuffDescription(type));
				}

				x += BuffIcon + BuffGap;
				drawn++;
			}
		}

		/// <summary>Dryad aura buffs plus minion icons from the NPC combat dummy.</summary>
		private static void CollectBuffs(NPC npc, List<(int type, int time)> dest) {
			dest.Clear();
			if (npc == null || !npc.active) {
				return;
			}

			var seen = new HashSet<int>();
			npc.GetGlobalNPC<TownNpcAuraBuffs>().CopyBuffs(dest);
			for (int i = 0; i < dest.Count; i++) {
				seen.Add(dest[i].type);
			}

			AppendMinionBuffs(npc, dest, seen);
		}

		private static void AppendMinionBuffs(NPC npc, List<(int type, int time)> dest, HashSet<int> seen) {
			if (!NpcDummyPool.TryGetDummy(npc.whoAmI, out Player dummy) || dummy == null) {
				return;
			}

			// Vanilla minion icons use buffNoTimeDisplay; read them from the combat dummy.
			for (int i = 0; i < dummy.buffType.Length; i++) {
				int type = dummy.buffType[i];
				int time = dummy.buffTime[i];
				if (type <= 0 || time <= 0 || seen.Contains(type)) {
					continue;
				}

				if (type >= Main.buffNoTimeDisplay.Length || !Main.buffNoTimeDisplay[type]) {
					continue;
				}

				seen.Add(type);
				dest.Add((type, 0));
			}
		}

		private static void DrawOverheadEyeToggle(
			SpriteBatch batch,
			Rectangle area,
			int titleY,
			Point mouse,
			bool clicked,
			TownNpcAuraBuffs aura) {
			var eye = new Rectangle(area.Right - EyeSize, titleY - 2, EyeSize, EyeSize);
			bool on = aura.ShowOverheadIcons;
			Texture2D tex = (on ? TextureAssets.InventoryTickOn : TextureAssets.InventoryTickOff).Value;
			batch.Draw(tex, eye, Color.White);

			if (!eye.Contains(mouse)) {
				return;
			}

			Main.LocalPlayer.mouseInterface = true;
			Main.instance.MouseText(Language.GetTextValue(
				on ? "Mods.AITownNPCs.UI.StatsOverheadBuffsOn" : "Mods.AITownNPCs.UI.StatsOverheadBuffsOff"));
			if (clicked) {
				aura.ShowOverheadIcons = !on;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		private static float MeasureLabelColumn() {
			string[] keys = { "Life", "Regen", "Defense", "Attack", "Crit", "Minions", "Sentries", "Buffs" };
			float w = 0f;
			foreach (string key in keys) {
				w = Math.Max(w, FontAssets.MouseText.Value.MeasureString(L(key)).X * Scale);
			}

			return w;
		}

		private static string L(string key) => Language.GetTextValue("Mods.AITownNPCs.UI.Stats" + key);
	}
}
