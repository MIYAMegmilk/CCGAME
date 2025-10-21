// InventorySlotUI.cs
using Godot;
using System;

public partial class InventorySlotUI : Panel
{
	private Sprite2D _itemDisplay;

	public override void _Ready()
	{
		_itemDisplay = GetNode<Sprite2D>("ItemDisplay");
		// 初期状態ではアイテムを非表示にする
		_itemDisplay.Visible = false;
	}

	// アイテムデータを受け取って、スロットの見た目を更新するメソッド
	public void UpdateSlot(ItemData item)
	{
		if (item != null)
		{
			_itemDisplay.Visible = true;
			_itemDisplay.Texture = item.Texture;
		}
		else
		{
			_itemDisplay.Visible = false;
		}
	}
}
