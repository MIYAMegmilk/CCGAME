using Godot;

public class MapImageGenerator
{
	public static Image CreateImageFromMap(int[,] map)
	{
		int width = map.GetLength(0);
		int height = map.GetLength(1);
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

		Color wallColor = new Color(0, 0, 0); // 壁 = 黒
		Color airColor = new Color(1, 1, 1);  // 空間 = 白

		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				image.SetPixel(x, y, (map[x, y] == 1) ? wallColor : airColor);
			}
		}
		return image;
	}

	public static void SaveMapImageExample(int[,] map)
	{
		Image mapImage = CreateImageFromMap(map);
		mapImage.SavePng("res://generated_map.png");
		GD.Print("マップ画像を res://generated_map.png に保存しました。");
	}
}
