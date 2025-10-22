// InventoryUI.cs
using Godot;
using System.Collections.Generic; // List<T> を使うために必要

/// <summary>
/// インベントリ全体のUIを管理するクラス。
/// PlayerInventoryデータに基づいてスロットUIを生成し、
/// ホットバーとメインインベントリのグリッドに配置します。
/// </summary>
public partial class InventoryUI : CanvasLayer
{
	// --- インスペクターから設定する変数 ---

	[Export(PropertyHint.File, "*.tres")]
	public PlayerInventory InventoryData { get; set; } // PlayerInventory.tres (データ本体)

	[Export(PropertyHint.File, "*.tscn")]
	public PackedScene InventorySlotScene { get; set; } // InventorySlotUI.tscn (スロット1個のUI)

	// --- コンテナへの参照 (インスペクターから設定) ---

	[Export] // ← カッコと中身を削除
	private GridContainer _hotbarGrid;

	[Export] // ← カッコと中身を削除
	private Control _fullInventoryContainer;

	[Export] // ← カッコと中身を削除
	private GridContainer _mainInventoryGrid;

	// --- 内部変数 ---

	// 生成したスロットUIのインスタンスをすべて保持するリスト
	private List<InventorySlotUI> _uiSlots = new List<InventorySlotUI>();

	/// <summary>
	/// ゲーム開始時に一度だけ呼ばれる
	/// </summary>
	public override void _Ready()
	{
		// 1. 必要なリソースが設定されているか確認
		if (InventoryData == null || InventorySlotScene == null)
		{
			GD.PrintErr("InventoryUI: [Export] に InventoryData または InventorySlotScene が設定されていません。");
			return;
		}
		if (_hotbarGrid == null || _fullInventoryContainer == null || _mainInventoryGrid == null)
		{
			GD.PrintErr("InventoryUI: [Export] にグリッドの参照が設定されていません。");
			return;
		}

		// 2. UIの初期状態を設定
		_fullInventoryContainer.Visible = false;

		// 3. データのスロット数をインスペクターの設定値に合わせる
		InventoryData.InitializeSlots();

		// 4. UIスロットを生成し、グリッドに配置する
		GenerateSlotUIs();

		// 5. データが変更されたら、UIも更新するようにシグナルを接続
		InventoryData.InventoryChanged += UpdateAllSlots;
	}

	/// <summary>
	/// 毎フレーム呼ばれる
	/// </summary>
	public override void _Process(double delta)
	{
		// "inventory_toggle"キー（Iキーなど）が押されたら
		if (Input.IsActionJustPressed("inventory_toggle"))
		{
			// フルインベントリの表示/非表示を切り替える
			_fullInventoryContainer.Visible = !_fullInventoryContainer.Visible;
		}
	}

	/// <summary>
	/// 既存のUIスロットをすべて安全に削除し、
	/// PlayerInventoryのデータに基づいて再生成する
	/// </summary>
	private void GenerateSlotUIs()
	{
		// 1. 内部の参照リストをクリア
		_uiSlots.Clear();

		// 2. ホットバーの既存の子ノード（スロットUI）を安全に削除
		while (_hotbarGrid.GetChildCount() > 0)
		{
			Node child = _hotbarGrid.GetChild(0);
			_hotbarGrid.RemoveChild(child);
			child.QueueFree();
		}

		// 3. メインインベントリの既存の子ノードを安全に削除
		while (_mainInventoryGrid.GetChildCount() > 0)
		{
			Node child = _mainInventoryGrid.GetChild(0);
			_mainInventoryGrid.RemoveChild(child);
			child.QueueFree();
		}

		// 4. データに基づいて新しいスロットUIを生成
		int totalSlots = InventoryData.GetTotalSlotCount();

		for (int i = 0; i < totalSlots; i++)
		{
			// スロットUI (InventorySlotUI.tscn) をインスタンス化
			InventorySlotUI slotUI = InventorySlotScene.Instantiate<InventorySlotUI>();
			
			// 内部リストに追加
			_uiSlots.Add(slotUI);

			// 5. ホットバー用かメインインベントリ用かを判断して配置
			if (i < InventoryData.HotbarSize)
			{
				// iがホットバーのサイズ未満なら、_hotbarGrid に追加
				_hotbarGrid.AddChild(slotUI);
			}
			else
			{
				// それ以外は、_mainInventoryGrid に追加
				_mainInventoryGrid.AddChild(slotUI);
			}
		}
		
		// 6. 最後に、すべてのスロットの表示を更新
		UpdateAllSlots();
	}

	/// <summary>
	/// PlayerInventoryのデータに基づいて、すべてのUIスロットの見た目を更新する
	/// (InventoryChangedシグナルから呼ばれる)
	/// </summary>
	private void UpdateAllSlots()
	{
		int totalSlots = InventoryData.GetTotalSlotCount();

		for (int i = 0; i < totalSlots; i++)
		{
			// データとUIの数が合わない場合はエラーを防ぐために中断
			if (i >= _uiSlots.Count || i >= InventoryData.Slots.Count)
			{
				break;
			}

			// 対応するUIスロットに、対応するスロットデータを渡して更新を依頼
			_uiSlots[i].UpdateSlot(InventoryData.Slots[i]);
		}
	}
}
