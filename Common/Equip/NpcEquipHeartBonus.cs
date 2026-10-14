using System;
using AITownNPCs.Common.Host;
using AITownNPCs.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Equip
{
	/// <summary>
	/// Applies Reinforcement Heart slot bonus: +300 lifeMax per stack (max 20).
	/// Only NPCs whose profile exposes the Heart slot (any index).
	/// </summary>
	public class NpcEquipHeartBonus : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		private int _baseLifeMax;
		private bool _hasBase;
		private int _appliedBonus;

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
			return entity.townNPC;
		}

		/// <summary>Current heart bonus currently baked into lifeMax (0 if none).</summary>
		public int AppliedBonus => _appliedBonus;

		public static int GetHeartBonus(NPC npc) {
			if (npc == null || !npc.active || !npc.townNPC) {
				return 0;
			}

			NpcEquipProfile profile = NpcEquipProfileRegistry.Get(npc.type);
			int heartIndex = profile.FindHeartSlotIndex();
			if (heartIndex < 0) {
				return 0;
			}

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			Item heart = equip.GetSlot((NpcEquipSlotId)heartIndex);
			if (heart.IsAir || heart.type != ModContent.ItemType<ReinforcementHeart>()) {
				return 0;
			}

			return Math.Clamp(heart.stack, 0, ReinforcementHeart.MaxEquipStack) * ReinforcementHeart.LifePerStack;
		}

		public static bool ProfileHasHeartSlot(int npcType) {
			return NpcEquipProfileRegistry.Get(npcType).FindHeartSlotIndex() >= 0;
		}

		public override void PostAI(NPC npc) {
			if (!NpcHostRules.IsManagedTownNpc(npc) || !ProfileHasHeartSlot(npc.type)) {
				if (_appliedBonus != 0) {
					ClearBonus(npc);
				}

				return;
			}

			int bonus = GetHeartBonus(npc);
			if (!_hasBase) {
				_baseLifeMax = npc.lifeMax - _appliedBonus;
				if (_baseLifeMax < 1) {
					_baseLifeMax = npc.lifeMax;
				}

				_hasBase = true;
			}
			else {
				// Peel our previous bonus so base tracks Lifeforce / other mods.
				int peeled = npc.lifeMax - _appliedBonus;
				if (peeled > 0) {
					_baseLifeMax = peeled;
				}
			}

			int target = _baseLifeMax + bonus;
			if (npc.lifeMax == target && _appliedBonus == bonus) {
				return;
			}

			int oldMax = npc.lifeMax;
			int delta = target - oldMax;
			npc.lifeMax = target;
			_appliedBonus = bonus;

			if (delta > 0) {
				npc.life = Math.Min(npc.life + delta, npc.lifeMax);
			}
			else if (npc.life > npc.lifeMax) {
				npc.life = npc.lifeMax;
			}

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				npc.netUpdate = true;
			}
		}

		/// <summary>
		/// Used by TownNpcAuraBuffs: strip heart bonus from a lifeMax sample
		/// so Lifeforce base stays heart-free.
		/// </summary>
		public static int StripHeartBonus(NPC npc, int lifeMaxSample) {
			if (npc == null) {
				return lifeMaxSample;
			}

			int applied = npc.GetGlobalNPC<NpcEquipHeartBonus>()._appliedBonus;
			int stripped = lifeMaxSample - applied;
			return stripped > 0 ? stripped : lifeMaxSample;
		}

		/// <summary>Re-apply heart on top of current lifeMax (peels any previous heart first).</summary>
		public static void ReapplyOnTop(NPC npc) {
			if (npc == null || !npc.active) {
				return;
			}

			NpcEquipHeartBonus self = npc.GetGlobalNPC<NpcEquipHeartBonus>();
			if (!ProfileHasHeartSlot(npc.type)) {
				return;
			}

			int bonus = GetHeartBonus(npc);
			int baseLife = npc.lifeMax - self._appliedBonus;
			if (baseLife < 1) {
				baseLife = npc.lifeMax;
			}

			self._baseLifeMax = baseLife;
			self._hasBase = true;
			int target = baseLife + bonus;
			if (npc.lifeMax != target) {
				int oldMax = npc.lifeMax;
				npc.lifeMax = target;
				if (npc.life == oldMax) {
					npc.life = target;
				}
				else if (npc.life > npc.lifeMax) {
					npc.life = npc.lifeMax;
				}
			}

			self._appliedBonus = bonus;
		}

		private void ClearBonus(NPC npc) {
			if (_appliedBonus == 0) {
				return;
			}

			npc.lifeMax -= _appliedBonus;
			if (npc.lifeMax < 1) {
				npc.lifeMax = 1;
			}

			if (npc.life > npc.lifeMax) {
				npc.life = npc.lifeMax;
			}

			_appliedBonus = 0;
			_hasBase = false;
			_baseLifeMax = 0;
		}
	}
}
