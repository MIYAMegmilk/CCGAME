// blue_slime_AI.cs
using Godot;
using System;
using System.Linq; // LINQ を使うため

public partial class blue_slime_AI : CharacterBody2D
{
	// --- 状態定義 ---
	private enum EnemyState { IDLE, CHASE, ATTACK, DEATH }
	private EnemyState _currentState = EnemyState.IDLE;

	// --- 内部変数 ---
	private CharacterBody2D _targetPlayer = null; // 追跡対象プレイヤー
	private float _currentHealth;
	private Vector2 _movementVelocity = Vector2.Zero; // 移動速度
	private Vector2 _lastDirection = new Vector2(0, 1); // 最後に見ていた向き (初期値:下)

	// --- ノード参照 ---
	[Export] private AnimationPlayer _animationPlayer;
	[Export] private Sprite2D _sprite;
	[Export] private Area2D _detectionArea;
	[Export] private Area2D _pushArea;
	[Export] private Area2D _hitbox;    // Hurtbox (被ダメージ判定)
	[Export] private Area2D _attackArea;
	[Export] private NavigationAgent2D _navAgent; // 経路探索用

	// --- リソース参照 ---
	[Export] public EnemyBase Base { get; set; } // EnemyBaseリソース
	private EnemyStats Stats; // EnemyBaseから取得

	// --- 物理定数 ---
	private uint _wallCollisionMask = 1; // 壁(Terrain)のコリジョンレイヤー番号 (要確認)

	public override void _Ready()
	{
		// --- 1. 必須リソース/ノードのチェック ---
		if (Base == null || Base.Stats == null)
		{
			GD.PrintErr($"Enemy {Name}: EnemyBase または Stats リソースが設定されていません！");
			SetPhysicsProcess(false);
			return;
		}
		Stats = Base.Stats; // Stats 変数に Base から参照をコピー

		// ノード取得 (インスペクターからの割り当てを優先)
		_animationPlayer ??= GetNode<AnimationPlayer>("AnimationPlayer");
		_sprite ??= GetNode<Sprite2D>("Sprite");
		_pushArea ??= GetNode<Area2D>("PushArea");
		_hitbox ??= GetNode<Area2D>("Hitbox");
		_detectionArea ??= GetNode<Area2D>("DetectionArea");
		_attackArea ??= GetNode<Area2D>("AttackArea");
		_navAgent ??= GetNode<NavigationAgent2D>("NavigationAgent2D");

		// 主要ノードのnullチェック
		if (_animationPlayer == null || _sprite == null || _detectionArea == null || _attackArea == null || _hitbox == null || _navAgent == null)
		{
			GD.PrintErr($"Enemy {Name}: 必要なノードが見つかりません (AnimationPlayer, Sprite, DetectionArea, AttackArea, Hitbox, NavigationAgent2D)。");
			SetPhysicsProcess(false);
			return;
		}

		// --- 2. ステータス初期化 ---
		_currentHealth = Stats.MaxHealth;
		// TODO: HPバーがあれば初期化

		// --- 3. コリジョンサイズの初期設定 ---
		SetCollisionShapeRadius(_detectionArea, "CollisionShape2D", Base.DetectionSize);
		SetCollisionShapeRadius(_attackArea, "CollisionShape2D", Base.AttackRange);

		// --- 4. シグナル接続 ---
		_detectionArea.BodyEntered += OnDetectionAreaBodyEntered;
		_detectionArea.BodyExited += OnDetectionAreaBodyExited;
		_attackArea.BodyEntered += OnAttackAreaBodyEntered;
		_attackArea.BodyExited += OnAttackAreaBodyExited;
		_hitbox.AreaEntered += OnHurtboxAreaEntered; // Hurtbox のシグナル接続
		_animationPlayer.AnimationFinished += OnAnimationFinished; // アニメーション終了シグナル
		_navAgent.TargetReached += OnNavAgentTargetReached;

		GD.Print($"Enemy {Name} initialized.");
	}

	// Helper method to safely set CircleShape2D radius
	private void SetCollisionShapeRadius(Area2D area, string shapeNodeName, float radius)
	{
		if (area == null) return;
		var collisionShape = area.GetNodeOrNull<CollisionShape2D>(shapeNodeName);
		if (collisionShape?.Shape is CircleShape2D circleShape)
		{
			circleShape.Radius = radius;
		}
		else
		{
			GD.PrintErr($"{area.Name}/{shapeNodeName} が見つからないか、CircleShape2Dではありません。");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_currentState == EnemyState.DEATH) return;

		_movementVelocity = Velocity; // 現在の速度を取得

		// ステートマシン
		switch (_currentState)
		{
			case EnemyState.IDLE:
				_movementVelocity = ProcessIdleState(delta, _movementVelocity);
				break;
			case EnemyState.CHASE:
				_movementVelocity = ProcessChaseState(delta, _movementVelocity);
				break;
			case EnemyState.ATTACK:
				_movementVelocity = ProcessAttackState(delta, _movementVelocity);
				break;
		}
		
		Velocity = _movementVelocity; // 計算後の速度を適用
		MoveAndSlide();
	}

	// --- ステートごとの処理 ---
	private Vector2 ProcessIdleState(double delta, Vector2 currentVelocity)
	{
		PlayAnimation("idle");
		currentVelocity = currentVelocity.MoveToward(Vector2.Zero, Stats.Speed * (float)delta * 0.5f);
		
		// ★ プレイヤーが索敵範囲内に入ったら (nullでなくなったら)、
		// 視線と視野角のチェックを開始
		if (_targetPlayer != null)
		{
			GD.Print("★ 2. IDLE: プレイヤーをスキャン中..."); // ★追加
			bool los = IsPlayerInLineOfSight();
			bool fov = IsPlayerInFieldOfView();
			GD.Print($"  -> 視線(LOS) OK?: {los}"); // ★追加
			GD.Print($"  -> 視野角(FOV) OK?: {fov}"); // ★追加

			if (los && fov)
			{
				GD.Print("★ 3. プレイヤーを発見！ CHASE に遷移します。"); // ★追加
				TransitionToState(EnemyState.CHASE);
			}
		}
		return currentVelocity;
	}

	private Vector2 ProcessChaseState(double delta, Vector2 currentVelocity)
	{
		if (_targetPlayer == null)
		{
			TransitionToState(EnemyState.IDLE);
			return currentVelocity.MoveToward(Vector2.Zero, Stats.Speed * (float)delta);
		}

		if (!IsPlayerInLineOfSight() || !IsPlayerInFieldOfView())
		{
			TransitionToState(EnemyState.IDLE);
			return currentVelocity.MoveToward(Vector2.Zero, Stats.Speed * (float)delta);
		}

		Vector2 directionToPlayer = (_targetPlayer.GlobalPosition - GlobalPosition).Normalized();
		
		// 最後に動いた方向を更新
		_lastDirection = directionToPlayer;

		if (IsDirectPathBlocked(_targetPlayer.GlobalPosition))
		{
			// 壁がある場合: ナビ（経路探索）を使う
			_navAgent.TargetPosition = _targetPlayer.GlobalPosition;
			Vector2 nextPathPosition = _navAgent.GetNextPathPosition();
			Vector2 navDirection = (nextPathPosition - GlobalPosition).Normalized();
			currentVelocity = navDirection * Stats.Speed;
		}
		else
		{
			// 壁がない場合: 直線的に移動
			currentVelocity = directionToPlayer * Stats.Speed;
		}
		
		PlayAnimation("walk");

		return currentVelocity;
	}

	private Vector2 ProcessAttackState(double delta, Vector2 currentVelocity)
	{
		currentVelocity = Vector2.Zero; // 攻撃中は停止
		PlayAnimation("attack"); // 攻撃アニメーションを再生
		
		// 攻撃アニメーションの終了は OnAnimationFinished シグナルで検知
		return currentVelocity;
	}

	// --- 索敵ヘルパー ---
	private bool IsPlayerInFieldOfView()
	{
		// ターゲットがいない、または視野角が360度(180*2)以上なら、常に true (見える)
		if (_targetPlayer == null || Base.DetectionAngle >= 180f) return true; 
			
		Vector2 forwardVector; // 敵の現在の正面向きベクトル

		// 1. _lastDirection に基づいて「正面」を決定
		// _lastDirection がゼロ (初期状態など) の場合、デフォルトの向き (例: 下) を使う
		if (_lastDirection == Vector2.Zero)
		{
			forwardVector = Vector2.Down; // または Vector2.Right など、初期の向き
		}
		// Y方向(上下)の入力が X方向(左右) より大きいか？
		else if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X))
		{
			// 上下向き
			forwardVector = (_lastDirection.Y < 0) ? Vector2.Up : Vector2.Down;
		}
		else
		{
			// 左右向き
			forwardVector = (_lastDirection.X < 0) ? Vector2.Left : Vector2.Right;
		}

		// 2. 敵からプレイヤーへの方向ベクトル
		Vector2 directionToPlayer = (_targetPlayer.GlobalPosition - GlobalPosition).Normalized();
		
		// 3. ドット積（内積）を使って角度を比較
		//    (forwardVector と directionToPlayer がどれだけ近いかを -1.0 ～ 1.0 の値で示す)
		float dotProduct = forwardVector.Dot(directionToPlayer);
		
		// 4. 視野角 (例: 60度) のコサイン値を計算
		//    (cos(60度) = 0.5)
		float angleThreshold = Mathf.Cos(Mathf.DegToRad(Base.DetectionAngle)); 
		
		// 5. ドット積が閾値(しきいち)より大きければ、プレイヤーは視野角内にいる
		//    (例: dotProduct (0.7) > angleThreshold (0.5) -> true)
		return dotProduct > angleThreshold;
	}

	private bool IsPlayerInLineOfSight()
	{
		if (_targetPlayer == null) return false;
			
		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, _targetPlayer.GlobalPosition);
		query.Exclude = new Godot.Collections.Array<Rid> { this.GetRid() };
		query.CollisionMask = _wallCollisionMask; // 壁レイヤーのみを対象

		var result = spaceState.IntersectRay(query);
		
		return result.Count == 0; // 何にもぶつからなければ視線が通っている
	}

	private bool IsDirectPathBlocked(Vector2 targetPosition)
	{
		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, targetPosition);
		query.Exclude = new Godot.Collections.Array<Rid> { this.GetRid() };
		query.CollisionMask = _wallCollisionMask;
		
		return spaceState.IntersectRay(query).Count > 0;
	}


	// --- シグナルハンドラ ---
	private void OnDetectionAreaBodyEntered(Node2D body)
	{
		if (body is Player player)
		{
			GD.Print("★ 1. プレイヤーが索敵範囲 (DetectionArea) に入りました！"); // ★追加
			_targetPlayer = player;
			// 視線・視野角チェックは _PhysicsProcess で行うため、ここでは CHASE に遷移させない
		}
	}

	private void OnDetectionAreaBodyExited(Node2D body)
	{
		if (body == _targetPlayer)
		{
			_targetPlayer = null;
			if (_currentState == EnemyState.CHASE)
			{
				 TransitionToState(EnemyState.IDLE);
			}
		}
	}

	private void OnAttackAreaBodyEntered(Node2D body)
	{
		if (body == _targetPlayer && _currentState == EnemyState.CHASE)
		{
			TransitionToState(EnemyState.ATTACK);
		}
	}

	private void OnAttackAreaBodyExited(Node2D body)
	{
		if (body == _targetPlayer && _currentState == EnemyState.ATTACK)
		{
			// 攻撃モーションが終わっていれば追跡に戻る
			if(_animationPlayer != null && !_animationPlayer.IsPlaying()){
				TransitionToState(EnemyState.CHASE);
			}
		}
	}

	private void OnHurtboxAreaEntered(Area2D area)
	{
		if (_currentState == EnemyState.DEATH) return;
		if (area.IsInGroup("PlayerWeaponHitbox")) // Player側のHitboxにグループ設定が必要
		{
			Player player = GetTree().GetFirstNodeInGroup("player") as Player; // Playerにグループ設定が必要
			if (player == null)
			{
				TakeDamage(10, area); // Playerが見つからない場合、固定ダメージ
				return;
			}
			
			ItemData weapon = player.GetCurrentHeldItem();
			float totalDamage = 10f; // デフォルトダメージ

			if (weapon != null && weapon.IsWeapon)
			{
				float playerStrength = player.Stats?.Strength ?? 0f;
				int weaponPower = weapon.Strength_bonus;
				totalDamage = playerStrength + weaponPower;
			}
			TakeDamage(totalDamage, area);
		}
	}

	private void OnAnimationFinished(StringName animName)
	{
		string anim = animName.ToString();

		if (anim.Contains("attack"))
		{
			if (_targetPlayer != null)
			{
				// AttackArea内にまだプレイヤーがいるか再チェック
				bool playerInAttackRange = _attackArea.GetOverlappingBodies().Any(body => body == _targetPlayer);
				TransitionToState(playerInAttackRange ? EnemyState.ATTACK : EnemyState.CHASE);
			}
			else
			{
				TransitionToState(EnemyState.IDLE);
			}
		}
		else if (anim == "death")
		{
			QueueFree(); // 死亡アニメーション後に消滅
		}
	}
	
	private void OnNavAgentTargetReached()
	{
		// 経路探索のターゲットに到達
		// ProcessChaseState で毎フレーム TargetPosition を更新するため、
		// このシグナルでの特別な処理は不要な場合が多い
	}

	// --- 状態遷移ヘルパー ---
	private void TransitionToState(EnemyState newState)
	{
		if (_currentState == newState) return;
		// GD.Print($"Enemy {Name}: State changed from {_currentState} to {newState}");
		_currentState = newState;

		switch(newState)
		{
			case EnemyState.IDLE:
				_navAgent.TargetPosition = GlobalPosition; // ナビゲーション停止
				break;
			case EnemyState.CHASE:
				break;
			case EnemyState.ATTACK:
				break;
			case EnemyState.DEATH:
				Die();
				break;
		}
	}


	private void PlayAnimation(string action) // "idle", "walk", "attack", "damaged", "death" を受け取る
	{
		if (_animationPlayer == null || Base == null || _sprite == null) return;

		string animName = "";
		string tag = Base.EnemyTag ?? "Default";

		// 1. 向きが関係ないアニメーション
		if (action == "death")
		{
			animName = "death";
		}
		// 2. 向きが必要なアニメーション
		else
		{
			string directionPrefix = "front"; // デフォルトは前向き

			// _lastDirection に基づいて向きを決定
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X)) // 上下向き
			{
				directionPrefix = (_lastDirection.Y < 0) ? "back" : "front";
			}
			else // 左右向き
			{
				directionPrefix = "side";
			}
			
			// 3. アニメーション名を構築
			animName = $"{tag}_{directionPrefix}_{action}"; // 例: "blue_slime_side_walk"
		}

		// 4. "side" アニメーションの場合、スプライトの左右反転(FlipH)を更新
		if (animName.Contains("side"))
		{
			if (_lastDirection.X != 0) 
			{
				_sprite.FlipH = _lastDirection.X < 0; // Xがマイナス(左)なら反転
			}
		}
		else
		{
			_sprite.FlipH = false; // 上下向きのアニメーションでは反転しない
		}

		// 5. アニメーションが存在するか確認し、再生
		if (_animationPlayer.HasAnimation(animName))
		{
			if (_animationPlayer.CurrentAnimation != animName)
			{
				_animationPlayer.Play(animName);
			}
		}
		else
		{
			// "death" 以外で見つからなかった場合はエラーログ
			if (action != "death")
				GD.PrintErr($"Enemy {Name}: アニメーション '{animName}' が見つかりません！");
			else if (_animationPlayer.HasAnimation("death")) // "death" があればそれを再生
				_animationPlayer.Play("death");
		}
	}

	// --- ダメージ処理と死亡 ---
	public void TakeDamage(float damage, Area2D damageSource = null)
	{
		if (_currentState == EnemyState.DEATH) return;

		float defenseMultiplier = 1.0f - Mathf.Clamp(Stats.Defense / 100f, 0f, 0.9f); // 0%-90%カット
		float finalDamage = damage * defenseMultiplier;
		_currentHealth -= finalDamage;

		GD.Print($"Enemy {Name} took {finalDamage} damage. HP: {_currentHealth}/{Stats.MaxHealth}");
		// TODO: HPバー更新

		// ノックバック処理
		if (damageSource != null && Stats.KnockbackResist < 1.0f)
		{
			Vector2 knockbackDirection = (GlobalPosition - damageSource.GlobalPosition).Normalized();
			ItemData weapon = (GetTree().GetFirstNodeInGroup("player") as Player)?.GetCurrentHeldItem();
			float weaponKnockbackPower = weapon?.KnockbackPower ?? 1.0f;
			
			float knockbackStrength = weaponKnockbackPower * (1.0f - Stats.KnockbackResist) * Stats.PushForce;
			Velocity = knockbackDirection * knockbackStrength;
			// TODO: ノックバックステートを追加
		}

		if (_currentHealth <= 0)
		{
			TransitionToState(EnemyState.DEATH);
		}
		else
		{
			PlayAnimation("damaged"); // ★ 被ダメージアニメーション再生
			// TODO: 一時的な無敵時間
		}
	}

	private void Die()
	{
		GD.Print($"Enemy {Name} died.");
		PlayAnimation("death"); // 死亡アニメーション再生

		// コリジョン無効化
		GetNode<CollisionShape2D>("CollisionShape").SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_hitbox.GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_detectionArea.GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_attackArea.GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		CollisionLayer = 0; CollisionMask = 0;
		// _PhysicsProcess はステートがDEATHになることで実質停止する

		// TODO: ドロップアイテム生成 (Base.LootTable を使う)

		// OnAnimationFinished で QueueFree() が呼ばれる
	}
}
