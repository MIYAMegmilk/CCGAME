using Godot;
using System;

public partial class Player : CharacterBody2D
{
	// --- インスペクター設定 ---
	[ExportGroup("Stats")]
	[Export] public float Speed { get; set; } = 100.0f;
	[Export] public float MaxHealth { get; set; } = 50.0f;
	[Export] public float Health { get; set; } = 10.0f;
	[Export] public float HPRegene { get; set; } = 1.0f; //毎秒の回復量
	[Export] public float MaxMana { get; set; } = 30.0f;
	[Export] public float Mana { get; set; } = 0.0f;
	[Export] public float ManaRegene { get; set; } = 0.5f; //毎秒の回復量
	[Export] public float PushForce { get; set; } = 1000.0f;

	[ExportGroup("Inventory")]
	[Export] public PlayerInventory InventoryData { get; set; } // ★ インベントリデータ (player_inventory.tres)
	[Export] private Sprite2D _heldItemSprite;     // ★ 手元アイテム表示用
	[Export] private InventoryUI _inventoryUI;     // ★ Playerの子にあるInventoryUI

	// ---- シグナル ----
	[Signal] public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal] public delegate void ManaChangedEventHandler(float currentMana, float maxMana);
	[Signal] public delegate void HotbarSelectionChangedEventHandler(int newIndex); // ★ ホットバー選択変更

	// ---- 内部変数 ----
	private AnimatedSprite2D _animatedSprite;
	private Area2D _pushArea;
	private Vector2 _lastDirection = new Vector2(0, 1); // 初期値は下向き
	private int _selectedHotbarIndex = 0; // ★ 現在選択中のホットバースロット (0から始まる)

	// --- Godotメソッド ---
	public override void _Ready()
	{
		// 子ノードを取得
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_pushArea = GetNode<Area2D>("PushArea");
		_heldItemSprite = GetNode<Sprite2D>("HeldItemSprite"); // Sprite2Dを取得
		_inventoryUI = GetNode<InventoryUI>("InventoryUI"); // パスが正しいか確認

		// インベントリ関連の初期化
		if (InventoryData != null)
		{
			InventoryData.InitializeSlots();
			InventoryData.InventoryChanged += UpdateHeldItemDisplay; // データ変更時も更新
		}
		else
		{
			 GD.PrintErr("Player: InventoryDataが設定されていません！");
		}

		// InventoryUI自体は常に表示されているべきなので、Player側で非表示にしない
		// if (_inventoryUI != null)
		// {
		//	 _inventoryUI.Visible = false; // ← この行は削除またはコメントアウト
		// }
		// else
		// {
		//	 GD.PrintErr("Player: InventoryUIが設定されていません！");
		// }

		UpdateHeldItemDisplay(); // 手元アイテムの初期表示
		EmitSignal(SignalName.HotbarSelectionChanged, _selectedHotbarIndex); // UIに初期選択を通知

		// HP/Manaの初期シグナル発行
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
	}

	public override void _Input(InputEvent @event)
	{
		// マウスホイール処理
		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.IsPressed())
		{
			HandleMouseWheel(mouseButtonEvent);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// 入力取得
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Vector2 currentVelocity = Velocity;

		// 速度計算
		if (direction != Vector2.Zero)
		{
			currentVelocity = direction * Speed;
			_lastDirection = direction;
		}
		else
		{
			currentVelocity = currentVelocity.MoveToward(Vector2.Zero, Speed * (float)delta * 5.0f);
		}
		Velocity = currentVelocity;

		// 押し合い処理
		HandlePushing((float)delta);
		// 移動と衝突
		MoveAndSlide();
		// アニメーション更新
		UpdateAnimation(direction);

		// --- フレーム処理に追加 ---
		// HandleInventoryInput();  // ★ インベントリ開閉処理は削除
		HandleHotbarNumberKeys(); // 数字キー選択
		UpdateHeldItemPosition(); // 手元アイテム位置調整 (オプション)
		// -------------------------
	}

	// --- 入力処理 (ホットバー) ---
	private void HandleMouseWheel(InputEventMouseButton mouseButtonEvent)
	{
		int previousIndex = _selectedHotbarIndex;
		int hotbarSize = InventoryData?.HotbarSize ?? 0;
		if (hotbarSize <= 0) return;

		if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp)
		{
			_selectedHotbarIndex = (_selectedHotbarIndex - 1 + hotbarSize) % hotbarSize;
		}
		else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown)
		{
			_selectedHotbarIndex = (_selectedHotbarIndex + 1) % hotbarSize;
		}

		if (previousIndex != _selectedHotbarIndex)
		{
			UpdateHeldItemDisplay();
			EmitSignal(SignalName.HotbarSelectionChanged, _selectedHotbarIndex);
		}
	}

	private void HandleHotbarNumberKeys()
	{
		int previousIndex = _selectedHotbarIndex;
		int hotbarSize = InventoryData?.HotbarSize ?? 0;
		if (hotbarSize <= 0) return;

		for (int i = 0; i < hotbarSize; i++)
		{
			if (Input.IsActionJustPressed($"slot_{i + 1}"))
			{
				_selectedHotbarIndex = i;
				break;
			}
		}

		if (previousIndex != _selectedHotbarIndex)
		{
			UpdateHeldItemDisplay();
			EmitSignal(SignalName.HotbarSelectionChanged, _selectedHotbarIndex);
		}
	}

	// ★ HandleInventoryInput メソッドは削除 (InventoryUI.cs で処理するため)
	// private void HandleInventoryInput()
	// {
	//	 if (Input.IsActionJustPressed("inventory_toggle") && _inventoryUI != null)
	//	 {
	//		 // _inventoryUI.Visible = !_inventoryUI.Visible; // ← この行を削除
	//	 }
	// }

	// --- アイテム表示 ---
	private void UpdateHeldItemDisplay()
	{
		if (InventoryData == null || _heldItemSprite == null ||
			InventoryData.Slots == null || InventoryData.Slots.Count <= _selectedHotbarIndex)
		{
			if (_heldItemSprite != null) _heldItemSprite.Visible = false;
			return;
		}

		InventorySlot selectedSlot = InventoryData.Slots[_selectedHotbarIndex];

		if (selectedSlot == null || selectedSlot.IsEmpty())
		{
			_heldItemSprite.Visible = false;
		}
		else
		{
			_heldItemSprite.Visible = true;
			_heldItemSprite.Texture = selectedSlot.Item.Texture;
		}
	}

	private void UpdateHeldItemPosition()
	{
		if (_heldItemSprite == null || _animatedSprite == null) return;
		_heldItemSprite.Scale = new Vector2(_animatedSprite.FlipH ? -1 : 1, 1);
	}

	// --- 既存のメソッド ---
	private void UpdateAnimation(Vector2 direction)
	{
		if (_animatedSprite == null) return;
		bool isMoving = direction != Vector2.Zero;
		if (isMoving)
		{
			if (Mathf.Abs(direction.Y) > Mathf.Abs(direction.X))
			{
				if (direction.Y < 0){_animatedSprite.Play("back_walk");}
				else{_animatedSprite.Play("front_walk");}
			}
			else
			{
				_animatedSprite.Play("side_walk");
			}
			if (direction.X != 0){_animatedSprite.FlipH = direction.X < 0;}
		}
		else
		{
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X))
			{
				 if (_lastDirection.Y < 0){_animatedSprite.Play("back_idle");}
				 else{_animatedSprite.Play("front_idle");}
			}
			else
			{
				_animatedSprite.Play("side_idle");
				if (_lastDirection.X != 0){_animatedSprite.FlipH = _lastDirection.X < 0;}
			}
		}
	}

	private void HandlePushing(float delta)
	{
		if (_pushArea == null) return;
		var overlappingBodies = _pushArea.GetOverlappingBodies();
		foreach (Node2D body in overlappingBodies)
		{
			if (body is CharacterBody2D otherCharacter)
			{
				Vector2 pushDirection = (GlobalPosition - otherCharacter.GlobalPosition).Normalized();
				Velocity += pushDirection * PushForce * delta;
			}
		}
	}

	public void Damaged(int amount)
	{
		Health -= amount;
		if (Health < 0){ Health = 0; Death();}
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	public void Death()
	{
		GD.Print("Death");
		_animatedSprite.Play("death");
	}

	private void _on_state_update_timeout()
	{
		bool healthWasChanged = false;
		bool manaWasChanged = false;
		if(Health < MaxHealth)
		{
			Health = Mathf.Clamp(Health + HPRegene, 0, MaxHealth);
			healthWasChanged = true;
		}
		if(Mana < MaxMana)
		{
			Mana = Mathf.Clamp(Mana + ManaRegene, 0, MaxMana);
			manaWasChanged = true;
		}
		if (healthWasChanged){EmitSignal(SignalName.HealthChanged, Health, MaxHealth);}
		if (manaWasChanged){EmitSignal(SignalName.ManaChanged, Mana, MaxMana);}
	}

	// --- 外部アクセス用 ---
	public int GetSelectedHotbarIndex()
	{
		return _selectedHotbarIndex;
	}
}
