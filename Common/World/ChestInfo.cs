using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace AITownNPCs.Common.World
{
	public sealed class ChestInfo
	{
		public Vector2 Position;
		public HashSet<int> ItemTypes = new();
	}
}
