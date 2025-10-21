// InventoryUI.cs
using Godot;
using System;

public partial class InventoryUI : Control
{
	[Export] // インスペクターからプレイヤーのインベントリデータを割り当てる
	public PlayerInventory InventoryData { get; set; }

	[Export] // インスペクターからスロットのシーン（.tscn）を割り当てる
	public PackedScene InventorySlotScene { get; set; }

	private GridContainer _gridContainer;
	private bool _isOpen = false;

	public override void _Ready()
	{
		_gridContainer = GetNode<GridContainer>("NinePatchRect/GridContainer");

		// インベントリの開閉状態を初期化
		_isOpen = false;
		Visible = false;
		
		// インベントリデータが設定されているか確認
		if (InventoryData != null)
		{
			// インベントリスロットを初期化（サイズを合わせる）
			InventoryData.InitializeSlots();
			// UIスロットを生成
			GenerateSlots();
			// インベントリのデータが変更されたら、UIを更新するようにシグナルを接続
			InventoryData.InventoryChanged += UpdateSlots;
		}
	}

	public override void _Process(double delta)
	{
		// "inventory_toggle"キー（Iキーなど）が押されたら
		if (Input.IsActionJustPressed("inventory_toggle"))
		{
			_isOpen = !_isOpen;
			Visible = _isOpen;
		}
	}

	// インベントリのデータに基づいてUIスロットを生成する
	private void GenerateSlots()
	{
		foreach (Node child in _gridContainer.GetChildren())
		{
			child.QueueFree(); // 既存のスロットをクリア
		}

		if (InventorySlotScene == null) return;

		for (int i = 0; i < InventoryData.SlotCount; i++)
		{
			InventorySlotUI slotUI = InventorySlotScene.Instantiate<InventorySlotUI>();
			_gridContainer.AddChild(slotUI);
		}
		UpdateSlots(); // 最初の表示を更新
	}

	// すべてのスロットの表示を更新する
	private void UpdateSlots()
	{
		for (int i = 0; i < InventoryData.Items.Count; i++)
		{
			var slotUI = _gridContainer.GetChild<InventorySlotUI>(i);
			slotUI.UpdateSlot(InventoryData.Items[i]);
		}
	}
}
