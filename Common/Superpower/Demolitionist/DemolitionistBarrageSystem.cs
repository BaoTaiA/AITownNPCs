using System.Collections.Generic;
using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.Demolitionist
{
	public sealed class DemolitionistBarrageSession
	{
		public int PlayerWhoAmI;
		public int RemainingFrames;
		public int CooldownFrames;
	}

	public class DemolitionistBarrageSystem : ModSystem
	{
		private static readonly List<DemolitionistBarrageSession> Sessions = new();
		public const int DurationFrames = 60 * 60;
		public const float Radius = 400f;
		public const int FireInterval = 4;

		public static void ClearAll() => Sessions.Clear();

		public static void Start(int playerWhoAmI) {
			for (int i = Sessions.Count - 1; i >= 0; i--) {
				if (Sessions[i].PlayerWhoAmI == playerWhoAmI) {
					Sessions.RemoveAt(i);
				}
			}

			Sessions.Add(new DemolitionistBarrageSession {
				PlayerWhoAmI = playerWhoAmI,
				RemainingFrames = DurationFrames,
				CooldownFrames = 0
			});
		}

		public override void PostUpdatePlayers() {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return;
			}

			for (int i = Sessions.Count - 1; i >= 0; i--) {
				DemolitionistBarrageSession s = Sessions[i];
				if (s.PlayerWhoAmI < 0 || s.PlayerWhoAmI >= Main.maxPlayers) {
					Sessions.RemoveAt(i);
					continue;
				}

				Player player = Main.player[s.PlayerWhoAmI];
				if (player == null || !player.active || player.dead) {
					Sessions.RemoveAt(i);
					continue;
				}

				s.RemainingFrames--;
				if (s.RemainingFrames <= 0) {
					Sessions.RemoveAt(i);
					continue;
				}

				s.CooldownFrames--;
				if (s.CooldownFrames > 0) {
					continue;
				}

				s.CooldownFrames = FireInterval;
				FireOne(player);
			}
		}

		private static void FireOne(Player player) {
			float angle = Main.rand.NextFloat(MathHelper.TwoPi);
			float dist = Main.rand.NextFloat(0f, Radius);
			Vector2 target = player.Center + angle.ToRotationVector2() * dist;
			Vector2 spawn = target + new Vector2(Main.rand.NextFloat(-40f, 40f), -600f - Main.rand.NextFloat(0f, 200f));
			Vector2 velocity = (target - spawn).SafeNormalize(Vector2.UnitY) * 12f;
			int damage = 40 + player.statDefense / 2;
			NpcProjectileSpawn.SpawnServerOwned(
				player.GetSource_FromThis(),
				spawn,
				velocity,
				ProjectileID.Grenade,
				damage,
				4f,
				p => {
					p.friendly = true;
					p.hostile = false;
					p.trap = false;
				});
		}
	}
}
