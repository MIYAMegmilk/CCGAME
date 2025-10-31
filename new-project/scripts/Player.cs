// Player.cs
using Godot;
using System;
using System.Collections.Generic; // List<T> を使うため

public partial class Player : CharacterBody2D
{
	// --- Inspector Settings ---
	[Export] public CharacterStats Stats {get; set;}
	
	[ExportGroup("Stats")]
	[Export] public float Speed { get; set; } = 100.0f;
	[Export] public float MaxHealth { get; set; } = 50.0f;
	[Export] public float Health { get; set; } = 10.0f;
	[Export] public float HPRegene { get; set; } = 1.0f; // Per second
	[Export] public float MaxMana { get; set; } = 30.0f;
	[Export] public float Mana { get; set; } = 0.0f;
	[Export] public float ManaRegene { get; set; } = 0.5f; // Per second
	[Export] public float PushForce { get; set; } = 1000.0f;

	[ExportGroup("Inventory")]
	[Export] public PlayerInventory InventoryData { get; set; } // Assign player_inventory.tres
	[Export] private Sprite2D _heldItemSprite;     // Assign HeldItemSprite node
	[Export] private InventoryUI _inventoryUI;     // Assign InventoryUI node

	[ExportGroup("Held Item Offsets")]
	[Export] private Vector2 _offsetDown = new Vector2(8, 8);
	[Export] private Vector2 _offsetLeft = new Vector2(-10, 2);
	[Export] private Vector2 _offsetRight = new Vector2(10, 2);
	[Export] private Vector2 _offsetUp = new Vector2(-6, -8);

	[ExportGroup("Held Item Angles")]
	[Export] private float _angleDown = 0f;
	[Export] private float _angleLeftRight = 0f;
	[Export] private float _angleUp = 0f;

	[ExportGroup("Combat Nodes")]
	[Export] private Area2D _weaponHitbox;       // Assign WeaponHitbox node
	[Export] private AnimationPlayer _animationPlayer; // Assign AnimationPlayer node

	// --- Signals ---
	[Signal] public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal] public delegate void ManaChangedEventHandler(float currentMana, float maxMana);
	[Signal] public delegate void HotbarSelectionChangedEventHandler(int newIndex);

	// --- Internal Variables ---
	private Sprite2D _sprite; // Player's main sprite
	private Area2D _pushArea;
	private Vector2 _lastDirection = new Vector2(0, 1); // Default facing down
	private int _selectedHotbarIndex = 0; // Current hotbar slot index (0-based)
	private Vector2 _initialHeldItemScale = Vector2.One; // Base scale from inspector

	private ItemData _currentHeldItem = null; // Currently held item data
	private List<Node> _hitEnemiesThisSwing = new List<Node>(); // Enemies hit during the current swing
	private bool _isAttacking = false; // Is attack animation playing?

	// --- Godot Methods ---
	public override void _Ready()
	{
		// Get nodes
		_sprite ??= GetNode<Sprite2D>("Sprite2D");
		_pushArea ??= GetNode<Area2D>("PushArea");
		_heldItemSprite ??= GetNode<Sprite2D>("HeldItemSprite");
		_inventoryUI ??= GetNode<InventoryUI>("InventoryUI");
		_weaponHitbox ??= GetNode<Area2D>("WeaponHitbox");
		_animationPlayer ??= GetNode<AnimationPlayer>("AnimationPlayer");

		// Connect signals
		if (_weaponHitbox != null) { _weaponHitbox.BodyEntered += OnWeaponHitboxBodyEntered; }
		else { GD.PrintErr("Player: WeaponHitbox not assigned!"); }

		if (_animationPlayer != null) { _animationPlayer.AnimationFinished += OnAnimationFinished; }
		else { GD.PrintErr("Player: AnimationPlayer not assigned!"); }

		// Store initial scale
		if (_heldItemSprite != null) { _initialHeldItemScale = _heldItemSprite.Scale; }

		// Initialize inventory
		if (InventoryData != null)
		{
			InventoryData.InitializeSlots();
			InventoryData.InventoryChanged += UpdateHeldItemDisplay;
		} else { GD.PrintErr("Player: InventoryData not assigned!"); }

		UpdateHeldItemDisplay();
		EmitSignal(SignalName.HotbarSelectionChanged, _selectedHotbarIndex);
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
	}

	public override void _Input(InputEvent @event)
	{
		HandleMouseWheel(@event as InputEventMouseButton);
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 direction = _isAttacking ? Vector2.Zero : Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Vector2 currentVelocity = Velocity;

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

		HandlePushing((float)delta);
		MoveAndSlide();
		UpdateAnimation(direction); // Player walk/idle animation

		// Non-physics updates
		HandleAttackInput(); // Attack logic
		HandleHotbarNumberKeys();
		UpdateHeldItemPosition(); // Held item visuals (non-attack)
	}

	// --- Input Handling ---
	private void HandleMouseWheel(InputEventMouseButton mouseButtonEvent)
	{
		if (mouseButtonEvent == null || !mouseButtonEvent.IsPressed()) return;
		int previousIndex = _selectedHotbarIndex;
		int hotbarSize = InventoryData?.HotbarSize ?? 0;
		if (hotbarSize <= 0) return;

		if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp)
		{ _selectedHotbarIndex = (_selectedHotbarIndex - 1 + hotbarSize) % hotbarSize; }
		else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown)
		{ _selectedHotbarIndex = (_selectedHotbarIndex + 1) % hotbarSize; }

		if (previousIndex != _selectedHotbarIndex)
		{ UpdateHeldItemDisplay(); EmitSignal(SignalName.HotbarSelectionChanged, _selectedHotbarIndex); }
	}

	private void HandleHotbarNumberKeys()
	{
		int previousIndex = _selectedHotbarIndex;
		int hotbarSize = InventoryData?.HotbarSize ?? 0;
		if (hotbarSize <= 0) return;

		for (int i = 0; i < hotbarSize; i++)
		{ if (Input.IsActionJustPressed($"slot_{i + 1}")) { _selectedHotbarIndex = i; break; } }

		if (previousIndex != _selectedHotbarIndex)
		{ UpdateHeldItemDisplay(); EmitSignal(SignalName.HotbarSelectionChanged, _selectedHotbarIndex); }
	}

	private void HandleAttackInput()
	{	

		// 1. 前提条件チェック: 攻撃中でないか、必要なノードが存在するか、武器を持っているか
		if (_isAttacking || _animationPlayer == null || 
			_currentHeldItem == null || !_currentHeldItem.IsWeapon ||
			_sprite == null || _weaponHitbox == null) // 安全のためnullチェックを追加
		{
			return; // 条件を満たさなければ処理を中断
		}

		// 2. 攻撃アクションが押された瞬間かチェック
		if (Input.IsActionJustPressed("attack"))
		{
			_isAttacking = true; // 攻撃状態にする
			if (_heldItemSprite != null) _heldItemSprite.Visible = false; // 手持ちアイテムを隠す

			// 3. プレイヤーの向きに応じたアニメーション名を決定
			string directionPrefix;
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X)) // 上下向き優先
			{
				directionPrefix = (_lastDirection.Y < 0) ? "back" : "front";
			}
			else // 左右向き優先
			{
				directionPrefix = "side";
				// 横攻撃のためにスプライトの向きを確定させる
				if (_lastDirection.X != 0) _sprite.FlipH = _lastDirection.X < 0;
			}

			// アニメーション名を生成 (例: "front_attack_Sword")
			// ItemData.Name が武器タイプ名であることを想定。専用プロパティ推奨
			string ShapeName = _currentHeldItem.HitboxShapeName ?? "Default";
			string animationName = $"{directionPrefix}_attack_{ShapeName}";

			// 4. ★ プレイヤーが左向きなら Hitbox の Scale.X を -1 にする
			_weaponHitbox.Scale = new Vector2(_sprite.FlipH ? -1 : 1, 1);

			// 5. アニメーションが存在するか確認して再生
			if (_animationPlayer.HasAnimation(animationName))
			{
				// この振りで当てた敵リストをクリア (AnimationPlayerからも呼べる)
				ClearHitList();

				// アニメーション再生 (武器ごとの速度スケールを適用)
				_animationPlayer.Play(animationName, customSpeed: _currentHeldItem.AnimationSpeedScale);

				// 注意: CollisionShapeの有効/無効化は AnimationPlayer 内で
				//       'disabled' プロパティをキーフレーム設定して行う
			}
			else // アニメーションが見つからない場合
			{
				GD.PrintErr($"Animation not found: {animationName}");
				_isAttacking = false; // 攻撃失敗
				_weaponHitbox.Scale = Vector2.One; // スケールを元に戻す
				if (_heldItemSprite != null) _heldItemSprite.Visible = true; // 手持ちアイテムを再表示
			}
		}
	}


	// --- Item Display & Held Item ---
	private void UpdateHeldItemDisplay()
	{
		if (InventoryData == null || InventoryData.Slots == null || InventoryData.Slots.Count <= _selectedHotbarIndex)
		{
			if (_heldItemSprite != null) _heldItemSprite.Visible = false;
			_currentHeldItem = null;
			return;
		}
		InventorySlot selectedSlot = InventoryData.Slots[_selectedHotbarIndex];

		// Only show held item if not currently attacking
		bool shouldShowHeldItem = !_isAttacking && selectedSlot != null && !selectedSlot.IsEmpty();

		if (_heldItemSprite != null)
		{
			_heldItemSprite.Visible = shouldShowHeldItem;
			if (shouldShowHeldItem)
			{
				_heldItemSprite.Texture = selectedSlot.Item.Texture;
			}
		}
		_currentHeldItem = (selectedSlot == null || selectedSlot.IsEmpty()) ? null : selectedSlot.Item;
	}


	private void UpdateHeldItemPosition()
	{
		if (_heldItemSprite == null || _sprite == null) return;
		if (!_heldItemSprite.Visible) return; // Don't update if hidden

		Vector2 targetOffset; float targetAngle; int targetZIndex;
		bool isFacingLeft = _sprite.FlipH;

		if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X)){
			if (_lastDirection.Y > 0) { targetOffset = _offsetDown; targetAngle = _angleDown; targetZIndex = 1; }
			else { targetOffset = _offsetUp; targetAngle = _angleUp; targetZIndex = -1; }
		} else {
			targetOffset = isFacingLeft ? _offsetLeft : _offsetRight;
			targetAngle = _angleLeftRight; targetZIndex = 1;
		}
		_heldItemSprite.Position = targetOffset;
		_heldItemSprite.RotationDegrees = targetAngle;
		_heldItemSprite.ZIndex = targetZIndex;
		Vector2 targetScale = _initialHeldItemScale;
		if (isFacingLeft) { targetScale.X = -_initialHeldItemScale.X; }
		_heldItemSprite.Scale = targetScale;
	}

	// --- Animation ---
	private void UpdateAnimation(Vector2 direction)
	{
		// Don't play walk/idle if attacking
		if (_isAttacking || _animationPlayer == null || _sprite == null) return;

		bool isMoving = direction != Vector2.Zero;
		string targetAnimation;

		if (isMoving){
			if (Mathf.Abs(direction.Y) > Mathf.Abs(direction.X))
			{ targetAnimation = (direction.Y < 0) ? "back_walk" : "front_walk"; }
			else { targetAnimation = "side_walk"; }
			if (direction.X != 0) { _sprite.FlipH = direction.X < 0; }
		} else {
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X))
			{ targetAnimation = (_lastDirection.Y < 0) ? "back_idle" : "front_idle"; }
			else {
				targetAnimation = "side_idle";
				if (_lastDirection.X != 0) { _sprite.FlipH = _lastDirection.X < 0; }
			}
		}

		if (_animationPlayer.CurrentAnimation != targetAnimation)
		{ _animationPlayer.Play(targetAnimation); }
	}

	// ★ Called when any animation finishes
	private void OnAnimationFinished(StringName animName)
	{
		// If an attack animation finished, reset state
		if (animName.ToString().Contains("attack"))
		{
			_isAttacking = false;
			UpdateHeldItemDisplay(); // Re-show non-attacking item if applicable
			// Ensure hitbox is disabled (can also be done on last frame of animation)
			DisableAllHitboxes();
		}
	}

	// --- Physics & Combat ---
	private void HandlePushing(float delta)
	{
		if (_pushArea == null) return;
		var overlappingBodies = _pushArea.GetOverlappingBodies();
		foreach (Node2D body in overlappingBodies) {
			if (body is CharacterBody2D otherCharacter) {
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
		GD.Print("Player Died");
		// _animationPlayer?.Play("death"); // If death animation exists
		_sprite?.Hide();
		SetPhysicsProcess(false);
	}

	// ★ Simplified function, can be called by AnimationPlayer or HandleAttackInput
	public void ClearHitList()
	{
		_hitEnemiesThisSwing.Clear();
		// GD.Print("Hit list cleared."); // Optional debug
	}

	// ★ Helper to disable all weapon hitboxes (called on animation finish for safety)
	private void DisableAllHitboxes()
	{
		if (_weaponHitbox == null) return;
		foreach (Node child in _weaponHitbox.GetChildren()){
			if (child is CollisionShape2D shape) { shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true); }
			else if (child is CollisionPolygon2D poly) { poly.SetDeferred(CollisionPolygon2D.PropertyName.Disabled, true); }
		}
	}

	// ★ Connected to _weaponHitbox's body_entered signal
	private void OnWeaponHitboxBodyEntered(Node2D body)
	{
		// Check if attacking and list exists
		if (!_isAttacking || _hitEnemiesThisSwing == null) return;

		// Check if body is an Enemy and not already hit in this swing
		if (body is Enemy enemy && !_hitEnemiesThisSwing.Contains(body)) // Assuming Enemy class exists
		{
			GD.Print($"Hit Enemy: {enemy.Name}");
			_hitEnemiesThisSwing.Add(body); // Add to hit list for this swing

			// Apply damage (Assuming Enemy has a TakeDamage method)
			// enemy.TakeDamage(CalculateDamage(_currentHeldItem)); // Damage calculation needed
		}
	}

	// --- State Update (Timer) ---
	private void _on_state_update_timeout()
	{
		bool healthChanged = false; bool manaChanged = false;
		if(Health < MaxHealth) { Health = Mathf.Clamp(Health + HPRegene, 0, MaxHealth); healthChanged = true; }
		if(Mana < MaxMana) { Mana = Mathf.Clamp(Mana + ManaRegene, 0, MaxMana); manaChanged = true; }
		if (healthChanged){ EmitSignal(SignalName.HealthChanged, Health, MaxHealth); }
		if (manaChanged){ EmitSignal(SignalName.ManaChanged, Mana, MaxMana); }
	}

	// --- External Access ---
	public int GetSelectedHotbarIndex()
	{
		return _selectedHotbarIndex;
	}
	
	public ItemData GetCurrentHeldItem()
	{
		// _currentHeldItem は Player.cs 内で
		// UpdateHeldItemDisplay() によって更新されているはずの変数
		return _currentHeldItem;
	}
}
