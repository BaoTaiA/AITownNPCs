using AITownNPCs.Common.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AITownNPCs.Common.Loc;

namespace AITownNPCs.Common.Superpower.Guide
{
	public sealed class GuideWorldQueryPower : ISuperpower
	{
		public string Id => SuperpowerIds.GuideWorldQuery;
		public int NpcType => NPCID.Guide;
		public int CooldownFrames => 60 * 60;
		public string DisplayName => SuperpowerLoc.DisplayName(Id);
		public string Description => SuperpowerLoc.Description(Id);
		public string RefuseStyle => SuperpowerLoc.RefuseStyle(Id);

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (!WorldCache.Ready) {
				return SuperpowerResult.Failed(Id, "not_ready", ModText.Get("Runtime.Superpower.Fixed.map_not_ready"));
			}

			string query = intent.GetParam("query", intent.GetParam("kind", "biome"));
			string target = intent.GetParam("target", intent.GetParam("name", ""));
			if (string.IsNullOrWhiteSpace(target)) {
				target = intent.GetParam("itemHint", "");
			}

			Vector2 playerTile = player.Center / 16f;
			if (!WorldQuery.TryResolve(query, target, playerTile, out WorldQueryHit hit)) {
				string label = string.IsNullOrWhiteSpace(target) ? query : target;
				return SuperpowerResult.Failed(Id, "not_found", ModText.Get("Runtime.Superpower.NotFound", label));
			}

			player.GetModPlayer<GuideGuidancePlayer>().StartGuidance(hit.TargetTile);
			string line = hit.LineTemplate + ModText.Get("Runtime.Superpower.GuidedSuffix");
			return SuperpowerResult.Ok(Id, true, line, ModText.Get("Runtime.Superpower.QueryEq", hit.Label));
		}
	}
}
