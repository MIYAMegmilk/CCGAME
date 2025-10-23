using Godot;

public class MapGenerator
{
	private int _width;
	private int _height;
	private int[,] _map; // 0 = 空間, 1 = 壁
	private ulong _seed; // シード値を保持する変数 (ulong型推奨)

	[Export] public float FillPercent { get; set; } = 0.45f;
	[Export] public int SmoothIterations { get; set; } = 5;

	// コンストラクタでシード値を受け取るように変更
	public MapGenerator(int width, int height, ulong seed)
	{
		_width = width;
		_height = height;
		_map = new int[_width, _height];
		_seed = seed;
	}

	/// <summary>
	/// マップ生成のメイン関数
	/// </summary>
	public int[,] GenerateMap()
	{
		// ★ シード値を設定
		GD.Seed(_seed);
		
		InitializeMap();
		for (int i = 0; i < SmoothIterations; i++)
		{
			SmoothMap();
		}
		return _map;
	}

	/// <summary>
	/// ステップ1: マップをランダムなノイズで初期化
	/// </summary>
	private void InitializeMap()
	{
		for (int x = 0; x < _width; x++)
		{
			for (int y = 0; y < _height; y++)
			{
				if (x == 0 || x == _width - 1 || y == 0 || y == _height - 1)
				{
					_map[x, y] = 1; // フチは壁
				}
				else
				{
					// GD.Seed()が設定されているので、同じシードなら同じ乱数列が生成される
					_map[x, y] = (GD.Randf() < FillPercent) ? 1 : 0;
				}
			}
		}
	}

	// SmoothMap と CountNeighborWalls は変更なし
	private void SmoothMap() 
	{ 
		// ... (以前のコードと同じ) ... 
		int[,] newMap = new int[_width, _height];
		for (int x = 0; x < _width; x++)
		{
			for (int y = 0; y < _height; y++)
			{
				int neighborWallCount = CountNeighborWalls(x, y);

				if (neighborWallCount > 4)
					newMap[x, y] = 1; // 壁になる
				else if (neighborWallCount < 4)
					newMap[x, y] = 0; // 空間になる
				else
					newMap[x, y] = _map[x, y]; // 維持
			}
		}
		_map = newMap;
	}
	private int CountNeighborWalls(int gridX, int gridY) 
	{ 
		// ... (以前のコードと同じ) ... 
		int wallCount = 0;
		for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
		{
			for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
			{
				if (neighborX >= 0 && neighborX < _width && neighborY >= 0 && neighborY < _height)
				{
					if (neighborX != gridX || neighborY != gridY)
					{
						wallCount += _map[neighborX, neighborY];
					}
				}
				else
				{
					wallCount++; // フチは壁としてカウント
				}
			}
		}
		return wallCount;
	}
}
