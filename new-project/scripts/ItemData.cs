using Godot;
using System;

[GlobalClass] // Godotエディタでこのクラスをリソースとして作成できるようにします
public partial class ItemData : Resource // GodotのResourceを継承
{
	// [Export]を付けると、インスペクターで編集可能になります
	[Export] public string Name { get; set; } = "New Item";
	[Export] public Texture2D Texture { get; set; }
	
	// 他にも「説明文」「最大スタック数」などをここに追加できます
	[Export] public string Description { get; set; } = "";
	[Export] public int MaxStackSize { get; set; } = 99;
}
