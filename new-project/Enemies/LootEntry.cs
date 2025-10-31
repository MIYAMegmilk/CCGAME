// LootEntry.cs
using Godot;

// 【確認】 この [GlobalClass] が絶対に必要です！
[GlobalClass]
public partial class LootEntry : Resource
{
	[Export] 
	public ItemData Item { get; set; }
	
	[Export(PropertyHint.Range, "0.0, 1.0, 0.01")] 
	public float DropChance { get; set; } = 1.0f;
	
	[Export(PropertyHint.Range, "1, 999, 1")] 
	public int MinAmount { get; set; } = 1;
	
	[Export(PropertyHint.Range, "1, 999, 1")] 
	public int MaxAmount { get; set; } = 1;
}
