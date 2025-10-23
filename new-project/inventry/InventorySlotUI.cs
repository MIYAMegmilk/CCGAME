// InventorySlotUI.cs
using Godot;

// ルートノードの型に合わせて変更 (TextureRect推奨)
public partial class InventorySlotUI : TextureRect
{
	private TextureRect _itemTexture;
	private Label _quantityLabel;
	private Panel _highlightPanel; // 選択ハイライト用

	public override void _Ready()
	{
		// 子ノードを取得 (パスは実際のシーン構成に合わせる)
		_itemTexture = GetNode<TextureRect>("ItemTexture");
		_quantityLabel = GetNode<Label>("QuantityLabel");
		_highlightPanel = GetNode<Panel>("Highlight"); // シーンにHighlight Panelを追加しておく

		// 初期状態
		_highlightPanel.Visible = false;
		UpdateSlot(null);
	}

	/// <summary>
	/// スロットのデータを受け取って見た目を更新する
	/// </summary>
	public void UpdateSlot(InventorySlot slotData)
	{
		if (slotData == null || slotData.IsEmpty())
		{
			_itemTexture.Visible = false;
			_quantityLabel.Visible = false;
		}
		else
		{
			_itemTexture.Visible = true;
			_itemTexture.Texture = slotData.Item.Texture;
			_quantityLabel.Visible = slotData.Quantity > 1;
			_quantityLabel.Text = slotData.Quantity.ToString();
		}
	}

	/// <summary>
	/// 選択ハイライトの表示/非表示を設定する
	/// </summary>
	public void SetHighlight(bool highlighted)
	{
		_highlightPanel.Visible = highlighted;
	}
}
