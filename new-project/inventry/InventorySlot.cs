// InventorySlot.cs
//それぞれのインベントリがどのアイテムを何個持っているのか管理
using Godot;

[GlobalClass]
public partial class InventorySlot : Resource
{
	[Export] public ItemData Item { get; set; }
	[Export] public int Quantity { get; set; }

	// このスロットが空かどうかを判断するヘルパー
	public bool IsEmpty()
	{
		return Item == null || Quantity <= 0;
	}
}
