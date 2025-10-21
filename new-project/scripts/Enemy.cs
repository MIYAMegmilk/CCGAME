//Enemy.cs
using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	
	[Export]
	public float Speed { get; set; } = 40.0f;

	// プレイヤーを追跡中かどうかを判断するフラグ
	private bool _isChasing = false;
	
	// 追跡対象のプレイヤーを保持する変数
	private CharacterBody2D _player = null;

	// アニメーション用のノード
	private AnimatedSprite2D _animatedSprite;
	
	// 【追加】押し合いエリア用の変数
	private Area2D _pushArea;

	// 【追加】押し合いの力の強さ
	[Export]
	public float PushForce { get; set; } = 150.0f;

	public override void _Ready()
	{
		// 子ノードを取得
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		Area2D detectionArea = GetNode<Area2D>("DetectionArea");
		_pushArea = GetNode<Area2D>("PushArea");

		// シグナルを接続
		// プレイヤーが検出範囲に入った時の処理
		detectionArea.BodyEntered += OnPlayerEntered;
		// プレイヤーが検出範囲から出た時の処理
		detectionArea.BodyExited += OnPlayerExited;
	}

	public override void _PhysicsProcess(double delta)
	{
		// 現在の速度を一時変数に保持
		Vector2 currentVelocity = Velocity;

		if (_isChasing && _player != null)
		{
			// プレイヤーの方向へ移動
			Vector2 direction = (_player.GlobalPosition - this.GlobalPosition).Normalized();
			currentVelocity = direction * Speed;
		}
		else
		{
			// 【修正点】追跡していない場合は停止（ではなく減速）
			currentVelocity = currentVelocity.MoveToward(Vector2.Zero, Speed * (float)delta * 5.0f);
		}

		// Velocity プロパティを更新
		Velocity = currentVelocity;
		
		// 押し合い処理 (MoveAndSlide の前にあるのでOK)
		HandlePushing((float)delta);

		MoveAndSlide();
		UpdateAnimation();
	}

	private void UpdateAnimation()
	{
		if (_isChasing)
		{
			_animatedSprite.Play("walk");
			// プレイヤーの方向に応じてスプライトを反転させる
			_animatedSprite.FlipH = Velocity.X < 0;
		}
		else
		{
			_animatedSprite.Play("idle");
		}
	}

	// -- シグナルハンドラ --

	private void OnPlayerEntered(Node2D body)
	{
		// 範囲に入ってきたのがPlayerクラスを持つノードか確認
		if (body is Player)
		{
			GD.Print("プレイヤーを検出！");
			_player = body as CharacterBody2D;
			_isChasing = true;
		}
	}

	private void OnPlayerExited(Node2D body)
	{
		if (body is Player)
		{
			GD.Print("プレイヤーを見失った...");
			_player = null;
			_isChasing = false;
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
	
}
