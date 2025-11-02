// WorldManager.cs
using Godot;

public partial class WorldManager : Node2D
{
	// ★ 変数名を分かりやすく変更
	[Export] public TileMapLayer FloorLayer { get; set; } // 床 (Z=-1, ナビメッシュ用)
	[Export] public TileMapLayer WallLayer { get; set; }  // 壁 (Z=0, コリジョン用)
	
	[Export] public int TileSourceId { get; set; } = 0;
	
	// アトラス座標 (tileset.png の例)
	[Export] public Vector2I WallTileAtlasCoords { get; set; } = new Vector2I(3, 0); // stone.png (壁)
	[Export] public Vector2I FloorTileAtlasCoords { get; set; } = new Vector2I(0, 0); // dirt.png (床)

	[Export] public ulong WorldSeed { get; set; } = 12345;

	private const int MAP_WIDTH = 64;
	private const int MAP_HEIGHT = 64;

	public override void _Ready()
	{
		GD.Print($"マップ生成を開始します... Seed: {WorldSeed}");
		MapGenerator generator = new MapGenerator(MAP_WIDTH, MAP_HEIGHT, WorldSeed);
		int[,] mapData = generator.GenerateMap();
		DrawMap(mapData);
		GD.Print("マップ生成が完了しました。");
	}

	private void DrawMap(int[,] mapData)
	{
		// ★ 変数名を変更
		if (WallLayer == null || FloorLayer == null)
		{
			GD.PrintErr("WallLayer または FloorLayer がインスペクターで設定されていません。");
			return;
		}
		WallLayer.Clear();
		FloorLayer.Clear();

		GD.Print("タイルを配置中...");
		int offsetX = MAP_WIDTH / 2;
		int offsetY = MAP_HEIGHT / 2;

		for (int x = 0; x < MAP_WIDTH; x++)
		{
			for (int y = 0; y < MAP_HEIGHT; y++)
			{
				Vector2I tileMapCoords = new Vector2I(x - offsetX, y - offsetY);

				if (mapData[x, y] == 1) // 1 = 壁
				{
					// ★ WallLayer に壁タイルを配置
					WallLayer.SetCell(tileMapCoords, TileSourceId, WallTileAtlasCoords);
				}
				else // 0 = 空間 (床)
				{
					// ★ FloorLayer に床タイルを配置
					FloorLayer.SetCell(tileMapCoords, TileSourceId, FloorTileAtlasCoords);
				}
			}
		}
		GD.Print("タイル配置完了。");
	}
}
