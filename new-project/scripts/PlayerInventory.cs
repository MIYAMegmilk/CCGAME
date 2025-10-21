// PlayerInventory.cs
using System;
using Godot;

[GlobalClass]
public partial class PlayerInventory : Resource // これもResourceを継承
{
	// [Export]で、インスペクターからアイテムの配列（リスト）を編集できるようにします
	[Export]
	public Godot.Collections.Array<ItemData> Items { get; set; } = new();

	// インベントリのサイズ（スロット数）をインスペクターで設定できるようにします
	[Export]
	public int SlotCount { get; set; } = 12;

	// シグナルを定義。インベントリの内容が変わったときにUIに通知するために使います。
	[Signal]
	public delegate void InventoryChangedEventHandler();

	// このメソッドは、インベントリのサイズが変更されたときにスロット数を合わせるために使います
	public void InitializeSlots()
	{
		while (Items.Count < SlotCount)
		{
			Items.Add(null); // 空のスロット（null）を追加
		}
		while (Items.Count > SlotCount)
		{
			Items.RemoveAt(Items.Count - 1); // 超過分を削除
		}
		EmitSignal(SignalName.InventoryChanged); // 変更を通知
	}
	
	// TODO: ここに AddItem(ItemData item) などのロジックを追加していきます
}
