using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AITownNPCs.Items
{
	public class ReinforcementHeart : ModItem
	{
		public const int MaxEquipStack = 20;
		public const int LifePerStack = 500;

		public override void SetStaticDefaults() {
			Main.RegisterItemAnimation(Type, new DrawAnimationVertical(6, 6));
		}

		public override void SetDefaults() {
			Item.width = 40;
			Item.height = 37;
			Item.maxStack = MaxEquipStack;
			Item.value = Item.sellPrice(gold: 1);
			Item.rare = ItemRarityID.Pink;
		}

		public override void AddRecipes() {
			CreateRecipe()
				.AddIngredient(ItemID.LifeCrystal, 5)
				.AddIngredient(ItemID.HallowedBar, 5)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
