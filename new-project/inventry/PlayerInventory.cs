// PlayerInventory.cs
//インベントリ全体のデータ管理
using Godot;
using System.Linq; // FirstOrDefault を使うため

[GlobalClass]
public partial class PlayerInventory : Resource
{
	[Signal]
	public delegate void InventoryChangedEventHandler();

	// --- インスペクターで設定 ---
	[Export]
	public int HotbarSize { get; set; } = 9;

	[Export]
	public int MainInventorySize { get; set; } = 18;
	// ----------------------------

	[Export]
	public Godot.Collections.Array<InventorySlot> Slots { get; set; } = new();

	// 現在の総スロット数を計算
	public int GetTotalSlotCount()
	{
		return HotbarSize + MainInventorySize;
	}

	// ゲーム開始時やインスペクターでのサイズ変更時に呼ばれ、
	// データのスロット数を正しいサイズに調整します。
	public void InitializeSlots()
	{
		int totalSlots = GetTotalSlotCount();
		while (Slots.Count < totalSlots)
		{
			Slots.Add(new InventorySlot());
		}
		while (Slots.Count > totalSlots)
		{
			Slots.RemoveAt(Slots.Count - 1);
		}
		EmitSignal(SignalName.InventoryChanged);
	}

	// アイテムを追加するロジック
	public bool AddItem(ItemData item, int quantity = 1)
	{
		// 1. 既存のスタックを探す
		var stackableSlot = Slots.FirstOrDefault(s => 
			!s.IsEmpty() && 
			s.Item == item && 
			s.Quantity < item.MaxStackSize
		);

		if (stackableSlot != null)
		{
			int canAdd = item.MaxStackSize - stackableSlot.Quantity;
			int toAdd = Mathf.Min(quantity, canAdd);
			stackableSlot.Quantity += toAdd;
			quantity -= toAdd;
		}

		// 2. 残りを空きスロットに追加
		while (quantity > 0)
		{
			var emptySlot = Slots.FirstOrDefault(s => s.IsEmpty());
			if (emptySlot == null)
			{
				GD.Print("インベントリが満タンです！");
				EmitSignal(SignalName.InventoryChanged); // 変更をUIに通知
				return false; // 追加しきれなかった
			}

			int toAdd = Mathf.Min(quantity, item.MaxStackSize);
			emptySlot.Item = item;
			emptySlot.Quantity = toAdd;
			quantity -= toAdd;
		}

		EmitSignal(SignalName.InventoryChanged); // 変更をUIに通知
		return true;
	}
}
