using AITownNPCs.Common.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Superpower.Guide
{
	public sealed class GuideWorldQueryPower : ISuperpower
	{
		public string Id => SuperpowerIds.GuideWorldQuery;
		public int NpcType => NPCID.Guide;
		public int CooldownFrames => 60 * 60;
		public string DisplayNameZh => "世界指引";
		public string DescriptionZh => "根据地图记忆指路：群系、结构、矿物、宝箱、生命水晶等";
		public string RefuseStyleZh => "温和";

		public SuperpowerResult Execute(Player player, NPC npc, SuperpowerIntent intent) {
			if (!WorldCache.Ready) {
				return SuperpowerResult.Failed(Id, "not_ready", "地图记忆还没理清");
			}

			string query = intent.GetParam("query", intent.GetParam("kind", "biome"));
			string target = intent.GetParam("target", intent.GetParam("name", ""));
			if (string.IsNullOrWhiteSpace(target)) {
				target = intent.GetParam("itemHint", "");
			}

			Vector2 playerTile = player.Center / 16f;
			if (!WorldQuery.TryResolve(query, target, playerTile, out WorldQueryHit hit)) {
				string label = string.IsNullOrWhiteSpace(target) ? query : target;
				return SuperpowerResult.Failed(Id, "not_found", "没找到可靠的" + label);
			}

			player.GetModPlayer<GuideGuidancePlayer>().StartGuidance(hit.TargetTile);
			string line = hit.LineTemplate + "给你指引了。";
			return SuperpowerResult.Ok(Id, true, line, "查询=" + hit.Label);
		}
	}
}
