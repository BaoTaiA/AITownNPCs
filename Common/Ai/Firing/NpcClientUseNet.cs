using System.Collections.Generic;
using System.IO;
using AITownNPCs.Common.Ai.Combat;
using AITownNPCs.Common.Ai.Follow;
using AITownNPCs.Common.Equip;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Firing
{
	/// <summary>
	/// Server broadcasts fire intent to every client.
	/// The followed client deals real damage; others simulate locally for visuals only.
	/// Hit numbers for spectators come from the follower's reported strike (vanilla-like).
	/// </summary>
	public static class NpcClientUseNet
	{
		private const byte Intent = 1;
		private const byte Report = 2;
		private const byte Visual = 3;
		private const byte NpcHit = 4;
		private const byte HitVisual = 5;
		private const uint StackBroadcastInterval = 10;

		public static Projectile RoutingProjectile;

		private static readonly List<QueuedHit> Hits = new();

		private struct QueuedHit
		{
			public int SourceNpcWho;
			public int TargetNpcWho;
			public int Damage;
			public float Knockback;
			public int Direction;
			public bool Crit;
		}

		public static void QueueHit(int sourceNpcWho, NPC target, NPC.HitInfo hit) {
			if (target == null || hit.Damage <= 0 || sourceNpcWho < 0 || sourceNpcWho >= Main.maxNPCs) {
				return;
			}

			NPC source = Main.npc[sourceNpcWho];
			if (source == null || !source.active) {
				return;
			}

			NpcFiringState firing = source.GetGlobalNPC<NpcFiringState>();
			if (firing.SpectatorClient) {
				return;
			}

			Hits.Add(new QueuedHit {
				SourceNpcWho = sourceNpcWho,
				TargetNpcWho = target.whoAmI,
				Damage = hit.Damage,
				Knockback = hit.Knockback,
				Direction = hit.HitDirection,
				Crit = hit.Crit
			});
		}

		public static void PlaySyncedHitFeedback(NPC target, int damage, bool crit, int hitDirection) {
			if (target == null || !target.active || damage <= 0) {
				return;
			}

			Color color = crit ? CombatText.DamagedHostileCrit : CombatText.DamagedHostile;
			CombatText.NewText(target.getRect(), color, damage, crit);
			target.HitEffect(hitDirection, damage);
		}

		public static void SendIntent(NPC npc, EquippedTownNPC equip, NpcCombatState combat, NPC target, bool allowFire) {
			if (Main.netMode != NetmodeID.Server || npc == null || !npc.active || equip == null || combat == null) {
				return;
			}

			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing) {
				firing.IntentOpen = false;
				combat.ItemAnimation = 0;
				combat.ItemAnimationMax = 0;
				combat.IsChanneling = false;
				return;
			}

			if (!allowFire) {
				if (!firing.IntentOpen) {
					return;
				}

				firing.IntentOpen = false;
			}
			else {
				firing.IntentOpen = true;
			}

			Vector2 aim = target != null && target.active
				? target.Center
				: npc.Center + new Vector2(npc.direction * 320f, 0f);

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFiringMessageType.ClientUse);
			packet.Write(Intent);
			packet.Write(npc.whoAmI);
			packet.Write(allowFire);
			packet.WriteVector2(aim);
			WriteItem(packet, equip.Weapon);
			WriteItem(packet, equip.Ammo);
			packet.Send(-1);
		}

		public static void Handle(BinaryReader reader, int fromWho) {
			byte sub = reader.ReadByte();
			if (sub == Intent) {
				HandleIntent(reader);
			}
			else if (sub == Report) {
				HandleReport(reader, fromWho);
			}
			else if (sub == Visual) {
				DrainVisual(reader);
			}
			else if (sub == NpcHit) {
				HandleNpcHit(reader, fromWho);
			}
			else if (sub == HitVisual) {
				HandleHitVisual(reader);
			}
		}

		public static void ApplyPending() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active) {
					continue;
				}

				NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
				if (!firing.ClientHoldingUse) {
					continue;
				}

				FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
				if (!follow.IsFollowing) {
					firing.ClientHoldingUse = false;
					firing.SpectatorClient = false;
					continue;
				}

				// Freeze role before any dummy myPlayer swap.
				firing.SpectatorClient = follow.FollowPlayerWhoAmI != Main.myPlayer;

				EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
				if (firing.HasPendingIntent) {
					firing.HasPendingIntent = false;
					ApplyStack(equip.Weapon, firing.PendingWeaponType, firing.PendingWeaponPrefix, firing.PendingWeaponStack);
					ApplyStack(equip.Ammo, firing.PendingAmmoType, firing.PendingAmmoPrefix, firing.PendingAmmoStack);
				}

				bool pressed = NpcWeaponChannel.Apply(npc, equip, firing.PendingWantUse);
				NpcDummyActor.RunFromIntent(npc, equip, firing.PendingAim, pressed);
			}
		}

		public static void FlushReports() {
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active) {
					continue;
				}

				NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
				if (!firing.NeedsReport) {
					continue;
				}

				firing.NeedsReport = false;
				if (firing.SpectatorClient) {
					continue;
				}

				FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
				if (!follow.IsFollowing || follow.FollowPlayerWhoAmI != Main.myPlayer) {
					continue;
				}

				SendReport(npc);
			}

			for (int i = 0; i < Hits.Count; i++) {
				SendHit(Hits[i]);
			}

			Hits.Clear();
		}

		private static void HandleIntent(BinaryReader reader) {
			int npcWho = reader.ReadInt32();
			bool wantUse = reader.ReadBoolean();
			Vector2 aim = reader.ReadVector2();
			int weaponType = reader.ReadInt32();
			byte weaponPrefix = reader.ReadByte();
			short weaponStack = reader.ReadInt16();
			int ammoType = reader.ReadInt32();
			byte ammoPrefix = reader.ReadByte();
			short ammoStack = reader.ReadInt16();

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			if (npcWho < 0 || npcWho >= Main.maxNPCs || !Main.npc[npcWho].active) {
				return;
			}

			NpcFiringState firing = Main.npc[npcWho].GetGlobalNPC<NpcFiringState>();
			firing.HasPendingIntent = true;
			firing.ClientHoldingUse = true;
			firing.PendingWantUse = wantUse;
			firing.PendingAim = aim;
			firing.PendingWeaponType = weaponType;
			firing.PendingWeaponPrefix = weaponPrefix;
			firing.PendingWeaponStack = weaponStack;
			firing.PendingAmmoType = ammoType;
			firing.PendingAmmoPrefix = ammoPrefix;
			firing.PendingAmmoStack = ammoStack;
		}

		private static void SendReport(NPC npc) {
			NpcDummyPool.TryGetDummy(npc.whoAmI, out Player dummy);
			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			int direction = dummy != null ? dummy.direction : npc.direction;
			float rotation = dummy != null ? dummy.itemRotation : combat.ItemRotation;
			int held = dummy != null ? dummy.heldProj : -1;

			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFiringMessageType.ClientUse);
			packet.Write(Report);
			packet.Write(npc.whoAmI);
			packet.Write((short)combat.ItemAnimation);
			packet.Write((short)combat.ItemAnimationMax);
			packet.Write(combat.IsChanneling);
			packet.Write(rotation);
			packet.Write(held);
			packet.Write((sbyte)direction);
			WriteItem(packet, equip.Weapon);
			WriteItem(packet, equip.Ammo);
			packet.Write((byte)0);
			packet.Send();
		}

		private static void HandleReport(BinaryReader reader, int fromWho) {
			int npcWho = reader.ReadInt32();
			int anim = reader.ReadInt16();
			int animMax = reader.ReadInt16();
			bool channel = reader.ReadBoolean();
			float rotation = reader.ReadSingle();
			int held = reader.ReadInt32();
			int direction = reader.ReadSByte();
			int weaponType = reader.ReadInt32();
			byte weaponPrefix = reader.ReadByte();
			short weaponStack = reader.ReadInt16();
			int ammoType = reader.ReadInt32();
			byte ammoPrefix = reader.ReadByte();
			short ammoStack = reader.ReadInt16();
			int projCount = reader.ReadByte();

			if (Main.netMode != NetmodeID.Server
				|| npcWho < 0
				|| npcWho >= Main.maxNPCs
				|| !Main.npc[npcWho].active) {
				SkipProjectiles(reader, projCount);
				return;
			}

			NPC npc = Main.npc[npcWho];
			FollowingTownNPC follow = npc.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing || follow.FollowPlayerWhoAmI != fromWho) {
				SkipProjectiles(reader, projCount);
				return;
			}

			NpcCombatState combat = npc.GetGlobalNPC<NpcCombatState>();
			combat.ItemAnimation = anim;
			combat.ItemAnimationMax = animMax;
			combat.IsChanneling = channel;
			combat.ItemRotation = rotation;
			npc.direction = direction;
			npc.spriteDirection = direction;

			NpcFiringState firing = npc.GetGlobalNPC<NpcFiringState>();
			firing.RemoteBusy = anim > 0 || channel || held >= 0;

			EquippedTownNPC equip = npc.GetGlobalNPC<EquippedTownNPC>();
			ApplyReportedItem(npcWho, equip.Weapon, NpcEquipSlotId.Weapon, weaponType, weaponPrefix, weaponStack, ref firing.LastWeaponSyncTick);
			ApplyReportedItem(npcWho, equip.Ammo, NpcEquipSlotId.Ammo, ammoType, ammoPrefix, ammoStack, ref firing.LastAmmoSyncTick);

			SkipProjectiles(reader, projCount);
		}

		private static void DrainVisual(BinaryReader reader) {
			reader.ReadInt32();
			reader.ReadInt16();
			reader.ReadInt16();
			reader.ReadBoolean();
			reader.ReadSingle();
			reader.ReadInt32();
			reader.ReadSByte();
			int projCount = reader.ReadByte();
			SkipProjectiles(reader, projCount);
		}

		private static void HandleNpcHit(BinaryReader reader, int fromWho) {
			int sourceNpcWho = reader.ReadInt32();
			int targetNpcWho = reader.ReadInt32();
			int damage = reader.ReadInt32();
			float knockback = reader.ReadSingle();
			int direction = reader.ReadInt32();
			bool crit = reader.ReadBoolean();

			if (Main.netMode != NetmodeID.Server) {
				return;
			}

			if (sourceNpcWho < 0 || sourceNpcWho >= Main.maxNPCs
				|| targetNpcWho < 0 || targetNpcWho >= Main.maxNPCs) {
				return;
			}

			NPC source = Main.npc[sourceNpcWho];
			if (!source.active) {
				return;
			}

			FollowingTownNPC follow = source.GetGlobalNPC<FollowingTownNPC>();
			if (!follow.IsFollowing || follow.FollowPlayerWhoAmI != fromWho) {
				return;
			}

			NPC target = Main.npc[targetNpcWho];
			if (!target.active || target.dontTakeDamage || damage <= 0) {
				return;
			}

			var hit = new NPC.HitInfo {
				Damage = damage,
				Knockback = knockback,
				HitDirection = direction,
				Crit = crit
			};
			target.StrikeNPC(hit, fromNet: true);
			target.netUpdate = true;

			ModPacket visual = ModContent.GetInstance<AITownNPCs>().GetPacket();
			visual.Write((byte)NpcFiringMessageType.ClientUse);
			visual.Write(HitVisual);
			visual.Write(targetNpcWho);
			visual.Write(damage);
			visual.Write(direction);
			visual.Write(crit);
			visual.Send(-1, fromWho);
		}

		private static void HandleHitVisual(BinaryReader reader) {
			int targetNpcWho = reader.ReadInt32();
			int damage = reader.ReadInt32();
			int direction = reader.ReadInt32();
			bool crit = reader.ReadBoolean();

			if (Main.netMode != NetmodeID.MultiplayerClient) {
				return;
			}

			if (targetNpcWho < 0 || targetNpcWho >= Main.maxNPCs) {
				return;
			}

			PlaySyncedHitFeedback(Main.npc[targetNpcWho], damage, crit, direction);
		}

		private static void SendHit(QueuedHit hit) {
			ModPacket packet = ModContent.GetInstance<AITownNPCs>().GetPacket();
			packet.Write((byte)NpcFiringMessageType.ClientUse);
			packet.Write(NpcHit);
			packet.Write(hit.SourceNpcWho);
			packet.Write(hit.TargetNpcWho);
			packet.Write(hit.Damage);
			packet.Write(hit.Knockback);
			packet.Write(hit.Direction);
			packet.Write(hit.Crit);
			packet.Send();
		}

		private static void WriteItem(ModPacket packet, Item item) {
			if (item == null || item.IsAir) {
				packet.Write(0);
				packet.Write((byte)0);
				packet.Write((short)0);
				return;
			}

			packet.Write(item.type);
			packet.Write((byte)item.prefix);
			packet.Write((short)System.Math.Clamp(item.stack, 0, short.MaxValue));
		}

		private static void ApplyStack(Item item, int type, byte prefix, short stack) {
			if (item == null) {
				return;
			}

			if (type <= 0 || stack <= 0) {
				item.TurnToAir();
				return;
			}

			if (item.type != type || item.prefix != prefix) {
				item.SetDefaults(type);
				item.Prefix(prefix);
			}

			item.stack = stack;
		}

		private static void ApplyReportedItem(
			int npcWho,
			Item item,
			NpcEquipSlotId slot,
			int type,
			byte prefix,
			short stack,
			ref uint lastTick) {
			bool typeChanged = item.type != type || item.prefix != prefix;
			int currentStack = item.IsAir ? 0 : item.stack;
			if (!typeChanged && currentStack == stack) {
				return;
			}

			ApplyStack(item, type, prefix, stack);
			uint now = Main.GameUpdateCount;
			if (typeChanged || stack <= 0 || now - lastTick >= StackBroadcastInterval) {
				lastTick = now;
				NpcEquipNetSystem.BroadcastSlot(npcWho, slot, item);
			}
		}

		private static void SkipProjectiles(BinaryReader reader, int count) {
			for (int n = 0; n < count; n++) {
				reader.ReadInt16();
				reader.ReadInt16();
				reader.ReadVector2();
				reader.ReadVector2();
				for (int i = 0; i < Projectile.maxAI; i++) {
					reader.ReadSingle();
				}

				reader.ReadSingle();
				reader.ReadSingle();
				reader.ReadSByte();
				reader.ReadInt16();
				reader.ReadInt32();
				reader.ReadInt16();
				reader.ReadBoolean();
			}
		}
	}

	public class NpcClientUseSystem : ModSystem
	{
		public override void PreUpdateProjectiles() {
			NpcClientUseNet.ApplyPending();
		}

		public override void PostUpdateProjectiles() {
			NpcClientUseNet.FlushReports();
		}
	}
}
