// InventorySlotUI.cs
using Godot;

public partial class InventorySlotUI : TextureRect
{
	private TextureRect _itemTexture;
	private Label _quantityLabel;

	public override void _Ready()
	{
		_itemTexture = GetNode<TextureRect>("ItemTexture");
		_quantityLabel = GetNode<Label>("QuantityLabel");
		UpdateSlot(null); // 初期は空にする
	}

	// スロットのデータを受け取って見た目を更新する
	public void UpdateSlot(InventorySlot slotData)
	{
		if (slotData == null || slotData.IsEmpty())
		{
			// 空の場合
			_itemTexture.Visible = false;
			_quantityLabel.Visible = false;
		}
		else
		{
			// アイテムがある場合
			_itemTexture.Visible = true;
			_itemTexture.Texture = slotData.Item.Texture;
			
			// 数量が1より大きい場合のみ数字を表示
			_quantityLabel.Visible = slotData.Quantity > 1;
			_quantityLabel.Text = slotData.Quantity.ToString();
		}
	}
}
