using AITownNPCs.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Demolitionist
{
	public sealed class DemolitionistBlastPower : ISuperpower
	{
		public string Id => SuperpowerIds.DemoBlast;
		public int NpcType => NPCID.Demolitionist;
		public int CooldownFrames => 60 * 60;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			float dir = player.direction;
			Vector2 basePos = player.Center + new Vector2(dir * 16f * 3f, -8f);
			int spawned = 0;
			for (int i = 0; i < 3; i++) {
				Vector2 spawn = basePos + new Vector2(dir * (i - 1) * 10f, (i - 1) * 8f);
				int id = NpcProjectileSpawn.SpawnServerOwned(
					player.GetSource_FromThis(),
					spawn,
					new Vector2(dir * 0.05f, 0.35f),
					ProjectileID.StickyDynamite,
					250,
					8f,
					p => {
						p.friendly = true;
						p.hostile = false;
						p.trap = false;
					});
				if (id >= 0) {
					spawned++;
				}
			}

			if (spawned <= 0) {
				return SuperpowerResult.Failed(Id, "spawn_fail", ModText.Get("Runtime.Superpower.Fixed.spawn_fail"));
			}

			return SuperpowerResult.Ok(Id, true, ModText.Get("Runtime.Superpower.Fixed.demo_blast_ok"), ModText.Get("Runtime.Superpower.BlastCount", spawned));
		}
	}
}
