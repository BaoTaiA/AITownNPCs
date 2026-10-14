using System.Collections.Generic;
using System.IO;
using AITownNPCs.Common.Ai.Combat;
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
		private int _syncTimer;

		/// <summary>Aggregated outgoing damage mult from aura potions (Wrath/MagicPower/Archery).</summary>
		public float OutgoingDamageMult { get; private set; } = 1f;

		/// <summary>Aggregated crit chance add (Rage).</summary>
		public float CritChanceAdd { get; private set; }

		public override void Load() {
			NpcCombatHooks.RegisterOutgoingDamageMult(ReadOutgoingMult);
			NpcCombatHooks.RegisterCritChanceAdd(ReadCritChanceAdd);
		}

		public override void Unload() {
			NpcCombatHooks.UnregisterOutgoingDamageMult(ReadOutgoingMult);
			NpcCombatHooks.UnregisterCritChanceAdd(ReadCritChanceAdd);
		}

		private static float ReadOutgoingMult(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 1f;
			}

			return npc.GetGlobalNPC<TownNpcAuraBuffs>().OutgoingDamageMult;
		}

		private static float ReadCritChanceAdd(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 0f;
			}

			return npc.GetGlobalNPC<TownNpcAuraBuffs>().CritChanceAdd;
		}

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

				OutgoingDamageMult = 1f;
				CritChanceAdd = 0f;
				ThornsFraction = 0f;
				_syncTimer = 0;
				return;
			}

			// Server/singleplayer own the countdown; clients apply effects from synced ExtraAI.
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				TickTimers(npc);
				if (_buffTime.Count == 0) {
					npc.netUpdate = true;
					OutgoingDamageMult = 1f;
					CritChanceAdd = 0f;
					ThornsFraction = 0f;
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

		public override void ResetEffects(NPC npc) {
			if (_buffTime.Count == 0) {
				return;
			}

			int defense = 0;
			foreach (var kv in _buffTime) {
				if (PotionBuffNpcEffectTable.TryGet(kv.Key, out PotionBuffNpcEffect fx)) {
					defense += fx.DefenseAdd;
				}
			}

			npc.defense += defense;
		}

		public override void UpdateLifeRegen(NPC npc, ref int damage) {
			int regen = 0;
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
			TryReflectThorns(npc, player, damageDone);
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
			if (projectile == null || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
				return;
			}

			TryReflectThorns(npc, Main.player[projectile.owner], damageDone);
		}

		private void TryReflectThorns(NPC npc, Player attacker, int damageDone) {
			if (ThornsFraction <= 0f || attacker == null || !attacker.active || attacker.dead || damageDone <= 0) {
				return;
			}

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			int reflect = System.Math.Max(1, (int)(damageDone * ThornsFraction));
			attacker.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), reflect, -attacker.direction);
		}

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
			if (_buffTime.Count == 0) {
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

		private void ApplyUnifiedEffects(NPC npc) {
			OutgoingDamageMult = 1f;
			CritChanceAdd = 0f;
			ThornsFraction = 0f;
			float moveSpeedMult = 1f;
			Vector3 light = Vector3.Zero;
			bool wantLifeforce = false;

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

				if (fx.OutgoingDamageMult > 1f) {
					OutgoingDamageMult *= fx.OutgoingDamageMult;
				}

				CritChanceAdd += fx.CritChanceAdd;
				if (fx.ThornsFraction > ThornsFraction) {
					ThornsFraction = fx.ThornsFraction;
				}

				if (fx.MoveSpeedMult > 1f) {
					moveSpeedMult *= fx.MoveSpeedMult;
				}

				if (fx.LavaImmune) {
					npc.lavaImmune = true;
				}
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

			if (moveSpeedMult > 1f
				&& System.Math.Abs(npc.velocity.X) > 0.05f
				&& System.Math.Abs(npc.velocity.X) < 5f) {
				npc.velocity.X *= moveSpeedMult;
				if (npc.velocity.X > 5f) {
					npc.velocity.X = 5f;
				}
				else if (npc.velocity.X < -5f) {
					npc.velocity.X = -5f;
				}
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
}
