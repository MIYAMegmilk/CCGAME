// WorldManager.cs
using Godot;

public partial class WorldManager : Node2D
{
	// --- インスペクターから設定 ---
	[Export] public TileMapLayer TerrainLayer { get; set; } // 壁を配置するレイヤー
	[Export] public TileMapLayer WallLayer { get; set; }    // 床（背景壁）を配置するレイヤー
	[Export] public int TileSourceId { get; set; } = 0;

	// ★ 変数名を変更 & 値を逆に設定
	[Export] public Vector2I WallTileAtlasCoords { get; set; } = new Vector2I(0, 0); // 以前のStoneTile (壁用)
	[Export] public Vector2I FloorTileAtlasCoords { get; set; } = new Vector2I(1, 0); // 以前のWallTile (床用)
	// ----------------------------

	[Export] public ulong WorldSeed { get; set; } = 12345;

	private const int MAP_WIDTH = 256;
	private const int MAP_HEIGHT = 256;

	public override void _Ready()
	{
		// ... (MapGeneratorの呼び出しは同じ) ...
		GD.Print($"マップ生成を開始します... Seed: {WorldSeed}");
		MapGenerator generator = new MapGenerator(MAP_WIDTH, MAP_HEIGHT, WorldSeed);
		int[,] mapData = generator.GenerateMap();
		DrawMap(mapData);
		GD.Print("マップ生成が完了しました。");
	}

	private void DrawMap(int[,] mapData)
	{
		if (TerrainLayer == null || WallLayer == null)
		{
			GD.PrintErr("TileMapLayerがインスペクターで設定されていません。");
			return;
		}
		TerrainLayer.Clear();
		WallLayer.Clear();
		GD.Print("タイルを配置中...");
		int offsetX = MAP_WIDTH / 2;
		int offsetY = MAP_HEIGHT / 2;

		for (int x = 0; x < MAP_WIDTH; x++)
		{
			for (int y = 0; y < MAP_HEIGHT; y++)
			{
				Vector2I tileMapCoords = new Vector2I(x - offsetX, y - offsetY);

				// ★ 割り当てを逆にする
				if (mapData[x, y] == 1) // 1 = 壁
				{
					// TerrainLayerに壁タイルを配置
					TerrainLayer.SetCell(tileMapCoords, TileSourceId, WallTileAtlasCoords);
				}
				else // 0 = 空間
				{
					// WallLayerに床タイルを配置
					WallLayer.SetCell(tileMapCoords, TileSourceId, FloorTileAtlasCoords);
				}
			}
		}
		GD.Print("タイル配置完了。");
	}
}
