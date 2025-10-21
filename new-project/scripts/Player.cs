//Player.cs
using Godot;
using System;

public partial class Player : CharacterBody2D
{
	// [Export]を付けるとGodotエディタのインスペクターから値を変更できるようになります
	[ExportGroup("Stats")]
	[Export]
	public float Speed { get; set; } = 100.0f;
	[Export]
	public float MaxHealth {get; set;} = 50.0f;
	[Export]
	public float Health {get; set;} = 10.0f;
	[Export]
	public float HPRegene {get;set;} = 1.0f;//毎秒の回復寮
	[Export]
	public float MaxMana {get; set;} = 30.0f;
	[Export]
	public float Mana {get; set;} = 0.0f;
	[Export]
	public float ManaRegene {get;set;} = 0.5f;//毎秒の回復寮
	
	[Signal]
	public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal]
	public delegate void ManaChangedEventHandler(float currentMana, float maxMana);
	

	// アニメーション用のノードを保持する変数
	private AnimatedSprite2D _animatedSprite;
	
	// プレイヤーが停止したときに、最後に向いていた方向を記憶するための変数
	private Vector2 _lastDirection = new Vector2(0, 1); // 初期値は下向き
	
	private Area2D _pushArea;
	
	[Export]
	public float PushForce { get; set; } = 300.0f;

	// ゲーム開始時に一度だけ呼ばれるメソッド
	public override void _Ready()
	{
		// 子ノードであるAnimatedSprite2Dを取得して変数に保存しておく
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_pushArea = GetNode<Area2D>("PushArea");
		
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
	}

	// 物理演算フレームごとに呼ばれるメソッド
	public override void _PhysicsProcess(double delta)
	{
	
		// 1. 入力を取得
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Vector2 currentVelocity = Velocity;

		// 2. 速度を計算
		if (direction != Vector2.Zero)
		{
			// 入力がある場合、その方向に移動
			currentVelocity = direction * Speed;
			_lastDirection = direction; // 最後に向いていた方向を更新
		}
		else
		{
			// 【修正点】入力がない場合、速度を0に「近づける」（減速）
			// これにより、他の力（押し合い）が Velocity に影響できるようにします。
			// `5.0f` の部分を調整すると、滑り具合（慣性）が変わります。
			currentVelocity = currentVelocity.MoveToward(Vector2.Zero, Speed * (float)delta * 5.0f);
		}

		// 3. 移動と衝突処理を実行
		
		
		Velocity = currentVelocity;


		// 3. 【修正点】押し合い処理を "MoveAndSlide" の "前" に移動
		HandlePushing((float)delta);

		// 4. 移動と衝突処理を実行
		// HandlePushing で変更された最終的な Velocity を使って移動する
		MoveAndSlide();
		

		
		// 5. 状態に合わせてアニメーションを更新 (direction は元の入力を使う)
		UpdateAnimation(direction);
	}

	private void UpdateAnimation(Vector2 direction)
	{
		if (_animatedSprite == null) return;
		
		bool isMoving = direction != Vector2.Zero;

		if (isMoving)
		{
			// ---- 移動中のアニメーション ----
			
			// Mathf.Abs() で方向の絶対値（強さ）を取得して比較する
			if (Mathf.Abs(direction.Y) > Mathf.Abs(direction.X))
			{
				// Y軸（上下）の入力がX軸（左右）より強い場合
				if (direction.Y < 0)
				{
					_animatedSprite.Play("back_walk");
				}
				else
				{
					_animatedSprite.Play("front_walk");
				}
			}
			else
			{
				// X軸（左右）の入力がY軸より強い（または等しい）場合
				_animatedSprite.Play("side_walk");
			}

			// 左右の向きに応じてスプライトを反転
			if (direction.X != 0)
			{
				_animatedSprite.FlipH = direction.X < 0; 
			}
		}
		else
		{
			// ---- 停止中（アイドル）のアニメーション ----
			// 最後に移動していた方向に応じてアイドルアニメーションを再生
			// (こちらのロジックも、移動中と合わせるため Abs で比較するように変更)
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X))
			{
				 if (_lastDirection.Y < 0)
				{
					_animatedSprite.Play("back_idle");
				}
				else
				{
					_animatedSprite.Play("front_idle");
				}
			}
			else
			{
				_animatedSprite.Play("side_idle");

				// アイドル時も左右の向きを反映する
				if (_lastDirection.X != 0)
				{
					_animatedSprite.FlipH = _lastDirection.X < 0;
				}
			}
		}
	}
	
	private void HandlePushing(float delta)
	{
		// "PushArea" に重なっている "Body" (CharacterBody2D や RigidBody2D) をすべて取得
		var overlappingBodies = _pushArea.GetOverlappingBodies();
		
		foreach (Node2D body in overlappingBodies)
		{
			// 重なっているのが CharacterBody2D か確認 (Enemy など)
			if (body is CharacterBody2D otherCharacter)
			{
				// 相手から自分への方向ベクトルを計算 (正規化)
				// (GlobalPosition を使って正確な方向を計算)
				Vector2 pushDirection = (GlobalPosition - otherCharacter.GlobalPosition).Normalized();

				// 自分の Velocity に押し出す力を加える
				// (delta を掛けてフレームレートに依存しないようにする)
				Velocity += pushDirection * PushForce * delta;
			}
		}
	}
	public void Damaged(int amount)
	{
		Health -= amount;
		if (Health < 0){
			Health = 0;
			Death();
		}
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

		// --- HP回復処理 ---
		if(Health < MaxHealth)
		{
			// Mathf.Clampを使うと、最大値を超えないように制限するのが簡単です
			Health = Mathf.Clamp(Health + HPRegene, 0, MaxHealth);
			healthWasChanged = true;
		}
		
		// --- Mana回復処理 ---
		if(Mana < MaxMana)
		{
			Mana = Mathf.Clamp(Mana + ManaRegene, 0, MaxMana);
			manaWasChanged = true;
		}

		// --- シグナルの発行 ---
		// 実際に変更があった場合のみ、シグナルを発行する
		if (healthWasChanged)
		{
			EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		}
		if (manaWasChanged)
		{
			EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
		}
	}
}
