using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Net
{
	/// <summary>
	/// Server-authoritative projectile spawn for NPC/world effects.
	/// Fill ai/localAI via <paramref name="configure"/> before sync.
	/// <para>
	/// Policy: NPC superpowers, barrage rain, and role throws use
	/// <see cref="SpawnServerOwned"/> (owner 255) so dedicated fuse/AI matches singleplayer.
	/// <see cref="SpawnForPlayer"/> is only for paths that must credit a real player and
	/// have been verified to run AI on dedicated.
	/// </para>
	/// </summary>
	public static class NpcProjectileSpawn
	{
		/// <summary>Server-owned projectile (dedicated-safe). Owner is always 255 on server.</summary>
		public static int SpawnServerOwned(
			IEntitySource source,
			Microsoft.Xna.Framework.Vector2 position,
			Microsoft.Xna.Framework.Vector2 velocity,
			int type,
			int damage,
			float knockBack,
			Action<Projectile> configure = null) {
			int owner = Main.netMode == NetmodeID.MultiplayerClient ? Main.myPlayer : 255;
			return Spawn(source, position, velocity, type, damage, knockBack, owner, configure);
		}

		/// <summary>
		/// Projectile attributed to a real player. Prefer <see cref="SpawnServerOwned"/> for
		/// NPC/world effects on dedicated; use this only when player ownership is required.
		/// </summary>
		public static int SpawnForPlayer(
			IEntitySource source,
			Microsoft.Xna.Framework.Vector2 position,
			Microsoft.Xna.Framework.Vector2 velocity,
			int type,
			int damage,
			float knockBack,
			int playerWhoAmI,
			Action<Projectile> configure = null) {
			int owner = playerWhoAmI;
			if (owner < 0 || owner >= Main.maxPlayers) {
				owner = 255;
			}

			return Spawn(source, position, velocity, type, damage, knockBack, owner, configure);
		}

		public static int Spawn(
			IEntitySource source,
			Microsoft.Xna.Framework.Vector2 position,
			Microsoft.Xna.Framework.Vector2 velocity,
			int type,
			int damage,
			float knockBack,
			int owner,
			Action<Projectile> configure = null) {
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return -1;
			}

			int id = Projectile.NewProjectile(source, position, velocity, type, damage, knockBack, owner);
			if (id < 0 || id >= Main.maxProjectiles) {
				return -1;
			}

			Projectile proj = Main.projectile[id];
			configure?.Invoke(proj);
			proj.netImportant = true;
			proj.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) {
				NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, id);
			}

			return id;
		}
	}
}
