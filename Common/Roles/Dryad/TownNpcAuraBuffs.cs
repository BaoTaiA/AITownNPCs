using System.Collections.Generic;
using System.IO;
using AITownNPCs.Common.Equip;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AITownNPCs.Common.Roles.Dryad
{
	/// <summary>
	/// Town-NPC-side potion buffs from Dryad aura pulses.
	/// Stores timers, keeps buffs stuck despite ImmuneToRegularBuffs, draws icons,
	/// and applies player-parity effects via PotionBuffNpcEffectTable.
	/// </summary>
	public class TownNpcAuraBuffs : GlobalNPC
	{
		public const int MaxTracked = 10;
		/// <summary>Server periodic ExtraAI refresh so client timers do not drift.</summary>
		public const int SyncInterval = 180;

		public override bool InstancePerEntity => true;

		private readonly Dictionary<int, int> _buffTime = new();
		private int _baseLifeMax;
		private bool _hasBaseLifeMax;
		private bool _baseLavaImmune;
		private bool _hasBaseLavaImmune;
		private bool _baseOnFireImmune;
		private bool _hasBaseOnFireImmune;
		private int _syncTimer;

		/// <summary>Swiftness move-speed scale for follow/combat desired speeds (not velocity multiply).</summary>
		public float MoveSpeedMult { get; private set; } = 1f;

		/// <summary>Local UI: draw overhead aura buff icons for this NPC. Not saved.</summary>
		public bool ShowOverheadIcons = true;

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
			return entity.townNPC;
		}

		public void Apply(NPC npc, int buffType, int durationTicks) {
			if (npc == null || buffType <= 0 || durationTicks <= 0) {
				return;
			}

			if (buffType == BuffID.Lifeforce && !_hasBaseLifeMax) {
				_baseLifeMax = NpcEquipHeartBonus.StripHeartBonus(npc, npc.lifeMax);
				_hasBaseLifeMax = true;
			}

			bool firstLifeforce = buffType == BuffID.Lifeforce && !Has(BuffID.Lifeforce);
			_buffTime[buffType] = durationTicks;

			if (firstLifeforce) {
				int boosted = PotionBuffNpcEffectTable.ApplyLifeforceLifeMax(_baseLifeMax);
				int gain = boosted - NpcEquipHeartBonus.StripHeartBonus(npc, npc.lifeMax);
				npc.lifeMax = boosted;
				NpcEquipHeartBonus.ReapplyOnTop(npc);
				if (gain > 0) {
					npc.life += gain;
					if (npc.life > npc.lifeMax) {
						npc.life = npc.lifeMax;
					}
				}
			}
		}

		public bool Has(int buffType) => _buffTime.ContainsKey(buffType);

		/// <summary>Thorns-style reflect fraction (1 = full hit damage back to attacker).</summary>
		public float ThornsFraction { get; private set; }

		public override void PostAI(NPC npc) {
			if (_buffTime.Count == 0) {
				if (_hasBaseLifeMax) {
					RestoreLifeMax(npc);
				}

				RestoreLavaImmune(npc);
				ResetCombatAggregates();
				_syncTimer = 0;
				return;
			}

			// Server/singleplayer own the countdown; clients apply effects from synced ExtraAI.
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				TickTimers(npc);
				if (_buffTime.Count == 0) {
					npc.netUpdate = true;
					RestoreLavaImmune(npc);
					ResetCombatAggregates();
					_syncTimer = 0;
					return;
				}

				_syncTimer++;
				if (_syncTimer >= SyncInterval) {
					_syncTimer = 0;
					npc.netUpdate = true;
				}
			}

			StickVanillaBuffs(npc);
			ApplyUnifiedEffects(npc);
		}

		/// <summary>Read-only snapshot of tracked aura buffs for the stats panel.</summary>
		public void CopyBuffs(List<(int type, int time)> dest) {
			dest?.Clear();
			if (dest == null || _buffTime.Count == 0) {
				return;
			}

			foreach (var kv in _buffTime) {
				dest.Add((kv.Key, kv.Value));
			}
		}

		public static int GetMinionSlotBonus(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 0;
			}

			int add = 0;
			foreach (var kv in npc.GetGlobalNPC<TownNpcAuraBuffs>()._buffTime) {
				if (PotionBuffNpcEffectTable.TryGet(kv.Key, out PotionBuffNpcEffect fx)) {
					add += fx.MinionSlotAdd;
				}
			}

			return add;
		}

		/// <summary>Ironskin etc. defense from tracked aura buffs (does not write npc.defense).</summary>
		public static int GetPotionDefenseAdd(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 0;
			}

			int add = 0;
			foreach (var kv in npc.GetGlobalNPC<TownNpcAuraBuffs>()._buffTime) {
				if (PotionBuffNpcEffectTable.TryGet(kv.Key, out PotionBuffNpcEffect fx)) {
					add += fx.DefenseAdd;
				}
			}

			return add;
		}

		/// <summary>Regeneration potion lifeRegen units from tracked aura buffs.</summary>
		public static int GetPotionLifeRegenAdd(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 0;
			}

			int add = 0;
			foreach (var kv in npc.GetGlobalNPC<TownNpcAuraBuffs>()._buffTime) {
				if (PotionBuffNpcEffectTable.TryGet(kv.Key, out PotionBuffNpcEffect fx)) {
					add += fx.LifeRegenAdd;
				}
			}

			return add;
		}

		/// <summary>Vanilla npc.defense + potion defense + lifeMax growth defense.</summary>
		public static int GetEffectiveDefense(NPC npc) {
			if (npc == null || !npc.active) {
				return 0;
			}

			return npc.defense + GetPotionDefenseAdd(npc) + NpcLifeStatScale.GetDefenseBonus(npc);
		}

		/// <summary>Add synced aura buffs onto the dummy (caller clears DummySyncedBuffTypes first).</summary>
		public void SyncBuffsToDummy(Player dummy) {
			if (dummy == null) {
				return;
			}

			foreach (var kv in _buffTime) {
				if (!PotionBuffNpcEffectTable.ShouldSyncToDummy(kv.Key)) {
					continue;
				}

				dummy.AddBuff(kv.Key, System.Math.Max(2, kv.Value), quiet: true);
			}
		}

		public override void UpdateLifeRegen(NPC npc, ref int damage) {
			int regen = NpcLifeStatScale.GetLifeRegenBonus(npc);
			foreach (var kv in _buffTime) {
				if (PotionBuffNpcEffectTable.TryGet(kv.Key, out PotionBuffNpcEffect fx)) {
					regen += fx.LifeRegenAdd;
				}
			}

			if (regen <= 0) {
				return;
			}

			npc.lifeRegen += regen;
			if (npc.life < npc.lifeMax && npc.lifeRegen > 0 && damage > 0) {
				damage = 0;
			}
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
			int defenseAdd = GetPotionDefenseAdd(npc) + NpcLifeStatScale.GetDefenseBonus(npc);
			if (defenseAdd != 0) {
				modifiers.Defense.Flat += defenseAdd;
			}

			float taken = 1f;
			foreach (var kv in _buffTime) {
				if (!PotionBuffNpcEffectTable.TryGet(kv.Key, out PotionBuffNpcEffect fx)) {
					continue;
				}

				if (fx.DamageTakenMult > 0f && fx.DamageTakenMult != 1f) {
					taken *= fx.DamageTakenMult;
				}
			}

			if (taken != 1f) {
				modifiers.FinalDamage *= taken;
			}
		}

		public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
			TryReflectThornsToPlayer(npc, player, damageDone);
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
			if (projectile == null || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
				return;
			}

			TryReflectThornsToPlayer(npc, Main.player[projectile.owner], damageDone);
		}

		private void TryReflectThornsToPlayer(NPC npc, Player attacker, int damageDone) {
			if (!CanReflect(damageDone) || attacker == null || !attacker.active || attacker.dead) {
				return;
			}

			int reflect = System.Math.Max(1, (int)(damageDone * ThornsFraction));
			attacker.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), reflect, -attacker.direction);
		}

		/// <summary>
		/// Reflect contact damage from a hostile NPC. tML has no GlobalNPC.OnHitByNPC;
		/// <see cref="TownNpcThornsContactReflect"/> calls this from the attacker's OnHitNPC.
		/// </summary>
		internal void ReflectContactFromNpc(NPC townNpc, NPC attacker, int damageDone) {
			if (!CanReflect(damageDone)
				|| attacker == null
				|| !attacker.active
				|| attacker.friendly
				|| attacker.townNPC
				|| attacker.life <= 0) {
				return;
			}

			int reflect = System.Math.Max(1, (int)(damageDone * ThornsFraction));
			int hitDir = attacker.Center.X >= townNpc.Center.X ? 1 : -1;
			attacker.SimpleStrikeNPC(reflect, hitDir, crit: false, noPlayerInteraction: true);
		}

		private bool CanReflect(int damageDone) {
			return ThornsFraction > 0f
				&& damageDone > 0
				&& Main.netMode != NetmodeID.MultiplayerClient;
		}

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
			if (_buffTime.Count == 0) {
				return;
			}

			if (!ShowOverheadIcons) {
				return;
			}

			const float iconSize = 22f;
			const float gap = 2f;
			int count = System.Math.Min(_buffTime.Count, MaxTracked);
			float totalWidth = count * iconSize + (count - 1) * gap;
			Vector2 origin = npc.Top - screenPos + new Vector2(-totalWidth * 0.5f, -28f);

			int drawn = 0;
			foreach (var kv in _buffTime) {
				if (drawn >= count) {
					break;
				}

				int buffType = kv.Key;
				if (buffType <= 0 || buffType >= TextureAssets.Buff.Length) {
					continue;
				}

				Texture2D texture = TextureAssets.Buff[buffType].Value;
				Vector2 pos = origin + new Vector2(drawn * (iconSize + gap), 0f);
				spriteBatch.Draw(texture, new Rectangle((int)pos.X, (int)pos.Y, (int)iconSize, (int)iconSize), Color.White);
				drawn++;
			}
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			int count = System.Math.Min(_buffTime.Count, MaxTracked);
			binaryWriter.Write((byte)count);
			int written = 0;
			foreach (var kv in _buffTime) {
				if (written >= count) {
					break;
				}

				binaryWriter.Write(kv.Key);
				binaryWriter.Write(kv.Value);
				written++;
			}

			binaryWriter.Write(_hasBaseLifeMax);
			if (_hasBaseLifeMax) {
				binaryWriter.Write(_baseLifeMax);
			}
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			_buffTime.Clear();
			int count = binaryReader.ReadByte();
			for (int i = 0; i < count; i++) {
				int type = binaryReader.ReadInt32();
				int time = binaryReader.ReadInt32();
				if (type > 0 && time > 0) {
					_buffTime[type] = time;
				}
			}

			_hasBaseLifeMax = binaryReader.ReadBoolean();
			_baseLifeMax = _hasBaseLifeMax ? binaryReader.ReadInt32() : 0;
		}

		private void TickTimers(NPC npc) {
			var keys = new List<int>(_buffTime.Keys);
			bool lostLifeforce = false;
			for (int i = 0; i < keys.Count; i++) {
				int type = keys[i];
				int left = _buffTime[type] - 1;
				if (left <= 0) {
					if (type == BuffID.Lifeforce) {
						lostLifeforce = true;
					}

					_buffTime.Remove(type);
					ClearVanillaBuffSlot(npc, type);
				}
				else {
					_buffTime[type] = left;
				}
			}

			if (lostLifeforce) {
				RestoreLifeMax(npc);
			}
		}

		private void StickVanillaBuffs(NPC npc) {
			foreach (var kv in _buffTime) {
				int buffType = kv.Key;
				if (buffType < 0 || buffType >= npc.buffImmune.Length) {
					continue;
				}

				npc.buffImmune[buffType] = false;
				npc.AddBuff(buffType, System.Math.Max(2, kv.Value), quiet: true);
			}
		}

		private void ResetCombatAggregates() {
			MoveSpeedMult = 1f;
			ThornsFraction = 0f;
		}

		private void ApplyUnifiedEffects(NPC npc) {
			ResetCombatAggregates();
			Vector3 light = Vector3.Zero;
			bool wantLifeforce = false;
			bool wantObsidian = false;

			foreach (var kv in _buffTime) {
				if (!PotionBuffNpcEffectTable.TryGet(kv.Key, out PotionBuffNpcEffect fx)) {
					continue;
				}

				if (fx.Lifeforce) {
					wantLifeforce = true;
				}

				if (fx.LightRgb != Vector3.Zero) {
					light += fx.LightRgb;
				}

				if (fx.ThornsFraction > ThornsFraction) {
					ThornsFraction = fx.ThornsFraction;
				}

				if (fx.MoveSpeedMult > 1f) {
					MoveSpeedMult *= fx.MoveSpeedMult;
				}

				if (fx.LavaImmune) {
					wantObsidian = true;
				}
			}

			EnsureBaseLavaImmune(npc);
			npc.lavaImmune = wantObsidian || _baseLavaImmune;
			EnsureBaseOnFireImmune(npc);
			npc.buffImmune[BuffID.OnFire] = wantObsidian || _baseOnFireImmune;
			if (wantObsidian) {
				ClearVanillaBuffSlot(npc, BuffID.OnFire);
			}

			if (wantLifeforce) {
				if (!_hasBaseLifeMax) {
					_baseLifeMax = NpcEquipHeartBonus.StripHeartBonus(npc, npc.lifeMax);
					_hasBaseLifeMax = true;
				}

				int boosted = PotionBuffNpcEffectTable.ApplyLifeforceLifeMax(_baseLifeMax);
				int currentWithoutHeart = NpcEquipHeartBonus.StripHeartBonus(npc, npc.lifeMax);
				if (currentWithoutHeart != boosted) {
					int oldMax = npc.lifeMax;
					npc.lifeMax = boosted;
					NpcEquipHeartBonus.ReapplyOnTop(npc);
					if (npc.life == oldMax) {
						npc.life = npc.lifeMax;
					}
					else if (npc.life > npc.lifeMax) {
						npc.life = npc.lifeMax;
					}
				}
				else {
					NpcEquipHeartBonus.ReapplyOnTop(npc);
				}
			}
			else if (_hasBaseLifeMax) {
				RestoreLifeMax(npc);
			}

			if (light != Vector3.Zero) {
				Lighting.AddLight(npc.Center, light.X, light.Y, light.Z);
			}
		}

		private void RestoreLifeMax(NPC npc) {
			if (!_hasBaseLifeMax || npc == null) {
				return;
			}

			npc.lifeMax = _baseLifeMax;
			NpcEquipHeartBonus.ReapplyOnTop(npc);
			if (npc.life > npc.lifeMax) {
				npc.life = npc.lifeMax;
			}

			if (!Has(BuffID.Lifeforce)) {
				_hasBaseLifeMax = false;
				_baseLifeMax = 0;
			}
		}

		private void EnsureBaseLavaImmune(NPC npc) {
			if (_hasBaseLavaImmune || npc == null) {
				return;
			}

			_baseLavaImmune = npc.lavaImmune;
			_hasBaseLavaImmune = true;
		}

		private void EnsureBaseOnFireImmune(NPC npc) {
			if (_hasBaseOnFireImmune || npc == null) {
				return;
			}

			_baseOnFireImmune = npc.buffImmune[BuffID.OnFire];
			_hasBaseOnFireImmune = true;
		}

		private void RestoreLavaImmune(NPC npc) {
			if (npc == null) {
				return;
			}

			if (_hasBaseLavaImmune) {
				npc.lavaImmune = _baseLavaImmune;
			}

			if (_hasBaseOnFireImmune) {
				npc.buffImmune[BuffID.OnFire] = _baseOnFireImmune;
			}
		}

		private static void ClearVanillaBuffSlot(NPC npc, int buffType) {
			if (npc == null || buffType <= 0) {
				return;
			}

			for (int b = 0; b < NPC.maxBuffs; b++) {
				if (npc.buffType[b] == buffType && npc.buffTime[b] > 0) {
					npc.DelBuff(b);
					b--;
				}
			}
		}
	}

	/// <summary>
	/// tML GlobalNPC has OnHitByItem/Projectile but no OnHitByNPC. Reflect thorns when a
	/// hostile NPC successfully hits a town NPC (attacker hook OnHitNPC).
	/// </summary>
	public sealed class TownNpcThornsContactReflect : GlobalNPC
	{
		public override void OnHitNPC(NPC npc, NPC target, NPC.HitInfo hit) {
			if (target == null || !target.active || !target.townNPC || hit.Damage <= 0) {
				return;
			}

			target.GetGlobalNPC<TownNpcAuraBuffs>().ReflectContactFromNpc(target, npc, hit.Damage);
		}
	}
}
