// InventoryUI.cs
using Godot;
using System.Collections.Generic;

public partial class InventoryUI : CanvasLayer
{
	// --- インスペクター設定 ---
	[Export(PropertyHint.File, "*.tres")]
	public PlayerInventory InventoryData { get; set; }

	[Export(PropertyHint.File, "*.tscn")]
	public PackedScene InventorySlotScene { get; set; }

	[Export] private GridContainer _hotbarGrid; // Columns = 9
	[Export] private Control _fullInventoryContainer;
	[Export] private GridContainer _mainInventoryGrid; // Columns = 9

	// --- 内部変数 ---
	private List<InventorySlotUI> _uiSlots = new List<InventorySlotUI>();
	private int _currentlySelectedHotbarIndex = -1;

	public override void _Ready()
	{
		// 必須項目のチェック
		if (InventoryData == null || InventorySlotScene == null ||
			_hotbarGrid == null || _fullInventoryContainer == null || _mainInventoryGrid == null)
		{
			GD.PrintErr("InventoryUI: [Export] に必要なノードまたはリソースが設定されていません。");
			SetProcess(false); // エラー時は以降の処理を停止
			SetPhysicsProcess(false);
			return;
		}

		_fullInventoryContainer.Visible = false; // 初期は非表示

		InventoryData.InitializeSlots();
		GenerateSlotUIs();

		// Playerからのシグナル接続
		// Playerノードのパスは環境に合わせて変更してください (例: GetNode<Player>("/root/World/Player"))
		Player player = GetParent<Player>(); // "player"グループを使う場合
		// Player player = GetNode<Player>("../Player"); // Playerが親の場合
		if (player != null)
		{
			player.HotbarSelectionChanged += OnHotbarSelectionChanged;
			OnHotbarSelectionChanged(player.GetSelectedHotbarIndex()); // 初期選択を反映
		}
		else
		{
			GD.PrintErr("InventoryUI: Playerノードが見つかりません。シグナル接続に失敗しました。");
		}

		// データ変更シグナル接続
		if (InventoryData != null) // nullチェックを追加
		{
			InventoryData.InventoryChanged += UpdateAllSlots;
		}
	}

	public override void _Process(double delta)
	{
		// インベントリ開閉
		if (Input.IsActionJustPressed("inventory_toggle"))
		{
			_fullInventoryContainer.Visible = !_fullInventoryContainer.Visible;
			// ホットバーはフル表示中は隠す場合
			// _hotbarContainer.Visible = !_fullInventoryContainer.Visible;
		}
	}

	/// <summary>
	/// UIスロットを生成・再生成する
	/// </summary>
	private void GenerateSlotUIs()
	{
		_uiSlots.Clear();
		// 安全な削除
		while (_hotbarGrid.GetChildCount() > 0) { Node c = _hotbarGrid.GetChild(0); _hotbarGrid.RemoveChild(c); c.QueueFree(); }
		while (_mainInventoryGrid.GetChildCount() > 0) { Node c = _mainInventoryGrid.GetChild(0); _mainInventoryGrid.RemoveChild(c); c.QueueFree(); }

		int totalSlots = InventoryData.GetTotalSlotCount();
		for (int i = 0; i < totalSlots; i++)
		{
			InventorySlotUI slotUI = InventorySlotScene.Instantiate<InventorySlotUI>();
			_uiSlots.Add(slotUI);

			if (i < InventoryData.HotbarSize)
			{
				_hotbarGrid.AddChild(slotUI);
			}
			else
			{
				_mainInventoryGrid.AddChild(slotUI);
			}
		}
		UpdateAllSlots(); // 表示を更新
	}

	/// <summary>
	/// 全てのUIスロットの表示を更新する
	/// </summary>
	private void UpdateAllSlots()
	{
		int totalSlots = InventoryData.GetTotalSlotCount();
		for (int i = 0; i < totalSlots; i++)
		{
			if (i >= _uiSlots.Count || i >= InventoryData.Slots.Count) break;
			_uiSlots[i].UpdateSlot(InventoryData.Slots[i]);
		}
		// ハイライトも再適用
		OnHotbarSelectionChanged(_currentlySelectedHotbarIndex);
	}

	/// <summary>
	/// Playerからホットバー選択変更の通知を受け取る
	/// </summary>
	private void OnHotbarSelectionChanged(int newIndex)
	{
		// 以前の選択を解除
		if (_currentlySelectedHotbarIndex >= 0 && _currentlySelectedHotbarIndex < _uiSlots.Count)
		{
			 // ホットバーの範囲内のみハイライト解除
			 if (_currentlySelectedHotbarIndex < InventoryData.HotbarSize)
			 {
				 _uiSlots[_currentlySelectedHotbarIndex].SetHighlight(false);
			 }
		}

		// 新しいスロットをハイライト (ホットバーの範囲内のみ)
		if (newIndex >= 0 && newIndex < InventoryData.HotbarSize && newIndex < _uiSlots.Count)
		{
			_uiSlots[newIndex].SetHighlight(true);
			_currentlySelectedHotbarIndex = newIndex;
		}
		else
		{
			_currentlySelectedHotbarIndex = -1; // 無効な場合は選択なし状態に
		}
	}
}
