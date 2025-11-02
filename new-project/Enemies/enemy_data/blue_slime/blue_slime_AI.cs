using Godot;
using System;
using System.Linq; // GetOverlappingBodies().Any() で使う

public partial class blue_slime_AI : CharacterBody2D
{
	// --- 敵の状態定義 ---
	private enum EnemyState { IDLE, CHASE, ATTACK, KNOCKBACK, DEATH }
	private EnemyState _currentState = EnemyState.IDLE;

	// --- 内部変数 ---
	private CharacterBody2D _targetPlayer = null; // 追いかける相手
	private float _currentHealth;
	private Vector2 _lastDirection = new Vector2(0, 1); // 最後に見ていた向き (アニメーション用)
	private Timer _knockbackTimer; // ノックバック時間タイマー
	private Vector2 _lastKnownPlayerPosition; // プレイヤーを最後に見失った場所

	// --- ノード参照 (インスペクターから設定) ---
	[Export] private AnimationPlayer _animationPlayer;
	[Export] private Sprite2D _sprite;
	[Export] private Area2D _detectionArea; // 索敵範囲 (広範囲)
	[Export] private Area2D _pushArea;      // 押し合い判定
	[Export] private Area2D _hitbox;        // 被ダメージ判定 (Hurtbox)
	[Export] private Area2D _attackArea;    // 攻撃開始範囲
	[Export] private NavigationAgent2D _navAgent; // 経路探索エージェント
	[Export] private Polygon2D _fovDebugDisplay;  // 視界デバッグ表示
	[Export] private Line2D _pathDebugDisplay;   // 経路デバッグ表示

	// --- リソース参照 (インスペクターから設定) ---
	[Export] public EnemyBase Base { get; set; } // 敵の基本設定リソース
	private EnemyStats Stats; // 敵のステータス (Baseから取得)

	// --- 物理定数 ---
	private uint _wallCollisionMask = 1; // 壁(TerrainLayer)のコリジョンレイヤー番号

	public override void _Ready()
	{
		// --- 1. リソース/ノードのチェック ---
		if (Base == null || Base.Stats == null)
		{
			GD.PrintErr($"Enemy {Name}: EnemyBase または Stats が未設定！");
			SetPhysicsProcess(false);
			return;
		}
		Stats = Base.Stats; // ステータスを変数にコピー

		// ノード取得 (インスペクターで設定されてない場合の予備)
		_animationPlayer ??= GetNode<AnimationPlayer>("AnimationPlayer");
		_sprite ??= GetNode<Sprite2D>("Sprite");
		_pushArea ??= GetNode<Area2D>("PushArea");
		_hitbox ??= GetNode<Area2D>("Hitbox");
		_detectionArea ??= GetNode<Area2D>("DetectionArea");
		_attackArea ??= GetNode<Area2D>("AttackArea");
		_navAgent ??= GetNode<NavigationAgent2D>("NavigationAgent2D");
		_fovDebugDisplay ??= GetNode<Polygon2D>("FOVDebugDisplay");
		_pathDebugDisplay ??= GetNode<Line2D>("PathDebugDisplay");

		// 主要ノードのnullチェック
		if (_animationPlayer == null || _sprite == null || _detectionArea == null || _attackArea == null || _hitbox == null || _navAgent == null || _pushArea == null || _pathDebugDisplay == null)
		{
			GD.PrintErr($"Enemy {Name}: 必要なノードが割り当てられていません。");
			SetPhysicsProcess(false);
			return;
		}
		
		_lastKnownPlayerPosition = GlobalPosition; // 最後の位置を初期化

		// Line2D (経路表示) の設定
		if (_pathDebugDisplay.TopLevel)
		{
			_pathDebugDisplay.GlobalPosition = Vector2.Zero;
		}
		
		// 視界 (扇形) の生成
		if (_fovDebugDisplay != null)
		{
			GenerateFOVDebugShape();
		}

		// --- 2. ステータス初期化 ---
		_currentHealth = Stats.MaxHealth;
		// TODO: HPバー初期化

		// --- 3. コリジョンサイズの初期設定 ---
		SetCollisionShapeRadius(_detectionArea, "CollisionShape2D", Base.DetectionSize);
		SetCollisionShapeRadius(_attackArea, "CollisionShape2D", Base.AttackRange);
		
		// --- 4. ノックバック用タイマー作成 ---
		_knockbackTimer = new Timer();
		_knockbackTimer.Name = "KnockbackTimer";
		_knockbackTimer.WaitTime = 0.2; // ノックバック硬直時間
		_knockbackTimer.OneShot = true;
		_knockbackTimer.Timeout += OnKnockbackTimerTimeout;
		AddChild(_knockbackTimer);

		// --- 5. シグナル接続 ---
		_detectionArea.BodyEntered += OnDetectionAreaBodyEntered;
		_detectionArea.BodyExited += OnDetectionAreaBodyExited;
		_attackArea.BodyEntered += OnAttackAreaBodyEntered;
		_attackArea.BodyExited += OnAttackAreaBodyExited;
		_hitbox.AreaEntered += OnHurtboxAreaEntered;
		_animationPlayer.AnimationFinished += OnAnimationFinished;
		_navAgent.TargetReached += OnNavAgentTargetReached;

		GD.Print($"Enemy {Name} initialized.");
	}

	// Area2Dの半径をリソース(Base)の値で設定する
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

		Vector2 currentVelocity = Velocity;

		// ステートマシン (状態ごとに行動を切り替え)
		switch (_currentState)
		{
			case EnemyState.IDLE:
				currentVelocity = ProcessIdleState(delta, currentVelocity);
				break;
			case EnemyState.CHASE:
				currentVelocity = ProcessChaseState(delta, currentVelocity);
				break;
			case EnemyState.ATTACK:
				currentVelocity = ProcessAttackState(delta, currentVelocity);
				break;
			case EnemyState.KNOCKBACK:
				currentVelocity = ProcessKnockbackState(delta, currentVelocity);
				break;
		}
		
		Velocity = currentVelocity; // ステートでの計算結果を速度に反映
		HandlePushing((float)delta); // 押し合い処理
		MoveAndSlide();              // 移動と壁衝突
		UpdateFOVDebugRotation();    // 視界デバッグの向き更新
	}
	
	// 押し合い処理
	private void HandlePushing(float delta)
	{
		if (_pushArea == null || _currentState == EnemyState.DEATH) return;
		
		var overlappingBodies = _pushArea.GetOverlappingBodies();
		
		foreach (Node2D body in overlappingBodies)
		{
			if (body is CharacterBody2D otherCharacter && body != this)
			{
				Vector2 pushDirection = (GlobalPosition - otherCharacter.GlobalPosition).Normalized();
				float pushForce = Stats?.PushForce ?? 1000.0f;
				// Velocity に直接押し出す力を加える
				Velocity += pushDirection * pushForce * delta;
			}
		}
	}

	// --- ステートごとの処理 ---

	// 待機状態: プレイヤー索敵
	private Vector2 ProcessIdleState(double delta, Vector2 currentVelocity)
	{
		PlayAnimation("idle");
		currentVelocity = currentVelocity.MoveToward(Vector2.Zero, 1000.0f * (float)delta); // 摩擦で停止
		
		// プレイヤーが索敵範囲内にいるか？
		if (_targetPlayer != null)
		{
			// 視野角 (FOV) チェック
			if (IsPlayerInFieldOfView())
			{
				// 壁越し感知 (Perceptual) or 近距離感知 (SpiritArea) が有効か？
				bool canSeeThroughWalls = Base.Perceptual || IsPlayerInSpiritArea();
				// 視線 (LOS) が通っているか？
				bool inLOS = IsPlayerInLineOfSight();

				// (視線が通ってる) or (壁越しに感知できる) なら追跡開始
				if (inLOS || canSeeThroughWalls)
				{
					GD.Print("★ 3. プレイヤーを発見！ CHASE に遷移します。");
					TransitionToState(EnemyState.CHASE);
				}
			}
		}
		return currentVelocity;
	}

	// 追跡状態: プレイヤーを追いかける
	private Vector2 ProcessChaseState(double delta, Vector2 currentVelocity)
	{
		// 1. ターゲットが索敵範囲から消えたら IDLE へ
		if (_targetPlayer == null)
		{
			TransitionToState(EnemyState.IDLE);
			if (_pathDebugDisplay != null) _pathDebugDisplay.ClearPoints();
			return currentVelocity.MoveToward(Vector2.Zero, Stats.Speed * (float)delta);
		}

		// 2. プレイヤーが見えるか判定
		bool isPlayerVisible = IsPlayerInFieldOfView() && (IsPlayerInLineOfSight() || Base.Perceptual || IsPlayerInSpiritArea());

		if (isPlayerVisible)
		{
			// 見える: 最後の位置を更新し、ナビの目標をプレイヤーに設定
			_lastKnownPlayerPosition = _targetPlayer.GlobalPosition;
			_navAgent.TargetPosition = _targetPlayer.GlobalPosition;
		}
		else
		{
			// 見えない: 「最後に見た場所」を目標にする
			_navAgent.TargetPosition = _lastKnownPlayerPosition;
		}

		// 3. 「最後に見た場所」に着いてもプレイヤーが見えなければ IDLE へ
		float distanceToLastKnownPos = GlobalPosition.DistanceTo(_lastKnownPlayerPosition);
		if (distanceToLastKnownPos < 10.0f && !isPlayerVisible) 
		{
			TransitionToState(EnemyState.IDLE);
			if (_pathDebugDisplay != null) _pathDebugDisplay.ClearPoints();
			return currentVelocity.MoveToward(Vector2.Zero, Stats.Speed * (float)delta);
		}

		// 4. ナビゲーションに従って移動
		Vector2 nextPathPosition = _navAgent.GetNextPathPosition();
		Vector2 navDirection = (nextPathPosition - GlobalPosition).Normalized();
		currentVelocity = navDirection * Stats.Speed;
		_lastDirection = navDirection; // アニメーション用に記憶
		
		// 5. 経路デバッグ表示
		if (_pathDebugDisplay != null)
		{
			Vector2[] fullPath = NavigationServer2D.Singleton.MapGetPath(
				_navAgent.GetNavigationMap(), GlobalPosition, _navAgent.TargetPosition, true);
			_pathDebugDisplay.Points = fullPath;
		}
		
		PlayAnimation("walk");
		return currentVelocity;
	}

	// 攻撃状態: 停止して攻撃アニメーション
	private Vector2 ProcessAttackState(double delta, Vector2 currentVelocity)
	{
		currentVelocity = Vector2.Zero; // 攻撃中は移動停止
		if (_targetPlayer != null)
		{
			_lastDirection = (_targetPlayer.GlobalPosition - GlobalPosition).Normalized(); // プレイヤーの方を向く
		}
		PlayAnimation("attack");
		return currentVelocity;
	}

	// ノックバック状態: 吹き飛んで減速
	private Vector2 ProcessKnockbackState(double delta, Vector2 currentVelocity)
	{
		PlayAnimation("damaged"); // 被ダメージアニメーション
		currentVelocity = currentVelocity.MoveToward(Vector2.Zero, 1000 * (float)delta); // 減衰
		return currentVelocity;
	}

	// --- 索敵ヘルパー ---

	// プレイヤーが近距離感知 (SpiritArea) 範囲内にいるか
	private bool IsPlayerInSpiritArea()
	{
		if (_targetPlayer == null || Base == null) return false;
		return GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition) <= Base.SpiritArea;
	}
	
	// プレイヤーが視野角 (FOV) 内にいるか
	private bool IsPlayerInFieldOfView()
	{
		// ターゲットがいない、または視野角が360度(180*2)以上なら、常に true (見える)
		if (_targetPlayer == null || Base.DetectionAngle >= 180f) return true; 
			
		Vector2 forwardVector; // 敵の現在の正面向きベクトル

		// ★ 修正点:
		// _lastDirection を丸めずに、そのまま正面ベクトルとして使う
		if (_lastDirection != Vector2.Zero)
		{
			forwardVector = _lastDirection.Normalized();
		}
		else
		{
			forwardVector = Vector2.Down; // 初期状態は下向き
		}

		// 2. 敵からプレイヤーへの方向ベクトル (変更なし)
		Vector2 directionToPlayer = (_targetPlayer.GlobalPosition - GlobalPosition).Normalized();
		
		// 3. ドット積（内積）を使って角度を比較 (変更なし)
		float dotProduct = forwardVector.Dot(directionToPlayer);
		
		// 4. 視野角 (例: 60度) のコサイン値を計算 (変更なし)
		float angleThreshold = Mathf.Cos(Mathf.DegToRad(Base.DetectionAngle)); 
		
		// 5. 比較 (変更なし)
		return dotProduct > angleThreshold;
	}

	// プレイヤーとの間に壁があるか (視線)
	private bool IsPlayerInLineOfSight()
	{
		if (_targetPlayer == null) return false;
		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, _targetPlayer.GlobalPosition);
		query.Exclude = new Godot.Collections.Array<Rid> { this.GetRid() }; // 自分を除外
		query.CollisionMask = _wallCollisionMask; // 壁レイヤーのみ
		var result = spaceState.IntersectRay(query);
		return result.Count == 0; // 何もなければ視線が通っている
	}

	// 目的地まで壁があるか (経路探索用)
	private bool IsDirectPathBlocked(Vector2 targetPosition)
	{
		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, targetPosition);
		query.Exclude = new Godot.Collections.Array<Rid> { this.GetRid() };
		query.CollisionMask = _wallCollisionMask;
		return spaceState.IntersectRay(query).Count > 0; // 何かあればブロックされている
	}


	// --- シグナルハンドラ ---
	private void OnDetectionAreaBodyEntered(Node2D body)
	{
		if (body is Player player)
		{
			_targetPlayer = player;
			_lastKnownPlayerPosition = player.GlobalPosition;
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
			if(_animationPlayer != null && !_animationPlayer.IsPlaying()){
				TransitionToState(EnemyState.CHASE);
			}
		}
	}

	private void OnHurtboxAreaEntered(Area2D area)
	{
		if (_currentState == EnemyState.DEATH || _currentState == EnemyState.KNOCKBACK) return;

		if (area.IsInGroup("PlayerWeaponHitbox"))
		{
			Player player = GetTree().GetFirstNodeInGroup("player") as Player;
			if (player == null) { TakeDamage(10, area); return; }
			
			ItemData weapon = player.GetCurrentHeldItem();
			float totalDamage = 10f;

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
			// 攻撃後、ターゲットがまだいるか？
			if (_targetPlayer != null)
			{
				// ターゲットがまだ攻撃範囲にいるか？
				bool playerInAttackRange = _attackArea.GetOverlappingBodies().Any(body => body == _targetPlayer);
				TransitionToState(playerInAttackRange ? EnemyState.ATTACK : EnemyState.CHASE);
			}
			else
			{
				TransitionToState(EnemyState.IDLE);
			}
		}
		else if (anim.Contains("death")) // "death" アニメーション名が EnemyTag を含む場合も考慮
		{
			QueueFree(); // 死亡アニメーション後に消滅
		}
	}
	
	private void OnNavAgentTargetReached()
	{
		// ターゲット地点に到達 (ほぼ毎フレーム呼ばれる)
	}

	private void OnKnockbackTimerTimeout()
	{
		// ノックバック硬直終了
		TransitionToState(_targetPlayer != null ? EnemyState.CHASE : EnemyState.IDLE);
	}


	// --- 状態遷移ヘルパー ---
	private void TransitionToState(EnemyState newState)
	{
		if (_currentState == newState) return;
		
		// 古い状態を出るときの処理
		switch(_currentState)
		{
			case EnemyState.ATTACK:
				if (_animationPlayer.IsPlaying() && _animationPlayer.CurrentAnimation.Contains("attack"))
				{
					_animationPlayer.Stop();
				}
				break;
		}

		// GD.Print($"Enemy {Name}: State changed from {_currentState} to {newState}");
		_currentState = newState;

		// 新しい状態に入るときの初期化処理
		switch(newState)
		{
			case EnemyState.IDLE:
				_navAgent.TargetPosition = GlobalPosition; // ナビ停止
				if (_pathDebugDisplay != null) _pathDebugDisplay.ClearPoints(); // 経路表示クリア
				break;
			case EnemyState.CHASE:
				break;
			case EnemyState.ATTACK:
				break;
			case EnemyState.KNOCKBACK:
				_knockbackTimer.Start(); // ノックバックタイマー開始
				if (_pathDebugDisplay != null) _pathDebugDisplay.ClearPoints(); // 経路表示クリア
				break;
			case EnemyState.DEATH:
				Die();
				break;
		}
	}

	// --- アニメーション再生ヘルパー ---
	private void PlayAnimation(string action)
	{
		if (_animationPlayer == null || Base == null || _sprite == null) return;
		if (_currentState == EnemyState.DEATH && action != "death") return;

		string animName = "";
		string tag = Base.EnemyTag ?? "Default";

		if (action == "death") { animName = "death"; }
		else
		{
			string directionPrefix = "front";
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X)) { directionPrefix = (_lastDirection.Y < 0) ? "back" : "front"; }
			else { directionPrefix = "side"; }
			animName = $"{tag}_{directionPrefix}_{action}";
		}

		if (animName.Contains("side"))
		{
			if (_lastDirection.X != 0) { _sprite.FlipH = _lastDirection.X < 0; }
		} else { _sprite.FlipH = false; }

		if (_animationPlayer.HasAnimation(animName))
		{
			if (_animationPlayer.CurrentAnimation != animName) { _animationPlayer.Play(animName); }
		}
		else
		{
			if (action == "death" && _animationPlayer.HasAnimation("death"))
				_animationPlayer.Play("death");
			else if (action != "death")
				GD.PrintErr($"Enemy {Name}: アニメーション '{animName}' が見つかりません！");
		}
	}

	// --- ダメージ処理と死亡 ---
	public void TakeDamage(float damage, Area2D damageSource = null)
	{
		if (_currentState == EnemyState.DEATH || _currentState == EnemyState.KNOCKBACK) return;

		float defenseMultiplier = 1.0f - Mathf.Clamp(Stats.Defense / 100f, 0f, 0.9f);
		float finalDamage = damage * defenseMultiplier;
		_currentHealth -= finalDamage;

		GD.Print($"Enemy {Name} took {finalDamage} damage. HP: {_currentHealth}/{Stats.MaxHealth}");

		if (_currentHealth <= 0)
		{
			TransitionToState(EnemyState.DEATH);
			return; // 死亡
		}
		
		// ノックバック処理
		if (damageSource != null && Stats.KnockbackResist < 1.0f)
		{
			Vector2 knockbackDirection = (GlobalPosition - damageSource.GlobalPosition).Normalized();
			Player player = GetTree().GetFirstNodeInGroup("player") as Player;
			ItemData weapon = player?.GetCurrentHeldItem();
			float weaponKnockbackPower = weapon?.KnockbackPower ?? 1.0f;
			float knockbackStrength = weaponKnockbackPower * (1.0f - Stats.KnockbackResist) * Stats.PushForce;
			
			Velocity = knockbackDirection * knockbackStrength;
			TransitionToState(EnemyState.KNOCKBACK); // ノックバック状態へ
		}
		else
		{
			PlayAnimation("damaged"); // 被ダメージアニメーション
		}
	}
	
	// 視界デバッグ用の扇形を生成
	private void GenerateFOVDebugShape()
	{
		if (Base == null || _fovDebugDisplay == null) return;
		float angleDeg = Base.DetectionAngle;
		float radius = Base.DetectionSize;
		int resolution = 10; // 扇形の滑らかさ

		Vector2[] points = new Vector2[resolution + 1];
		points[0] = Vector2.Zero; // 原点
		float startAngleRad = Mathf.DegToRad(-angleDeg);
		float endAngleRad = Mathf.DegToRad(angleDeg);

		for (int i = 0; i < resolution; i++)
		{
			float t = (float)i / (resolution - 1);
			float currentAngle = Mathf.Lerp(startAngleRad, endAngleRad, t);
			// Vector2.Right (0度) を基準に回転
			points[i + 1] = Vector2.Right.Rotated(currentAngle) * radius;
		}
		_fovDebugDisplay.Polygon = points;
	}
	
	// 視界デバッグ用の扇形の向きを更新
	private void UpdateFOVDebugRotation()
	{
		if (_fovDebugDisplay == null) return;

		// ★ 修正点: _lastDirection が (0,0) でない限り、
		// そのベクトルの角度をそのまま Rotation に設定する
		if (_lastDirection != Vector2.Zero)
		{
			// .Angle() は、Vector2.Right (0度) からの角度(ラジアン)を返します。
			// GenerateFOVDebugShape が Vector2.Right を基準に扇形を作ったため、
			// これで向きがピッタリ合います。
			_fovDebugDisplay.Rotation = _lastDirection.Angle();
		}
		else
		{
			// 初期状態 (動いていない) 場合はデフォルトの向き
			_fovDebugDisplay.Rotation = Vector2.Down.Angle(); // (例: 下向き)
		}
	}

	private void Die()
	{
		GD.Print($"Enemy {Name} died.");
		PlayAnimation("death"); // 死亡アニメーション再生

		// 全てのコリジョンを安全に無効化
		GetNode<CollisionShape2D>("CollisionShape")?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_hitbox?.GetNode<CollisionShape2D>("CollisionShape2D")?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_detectionArea?.GetNode<CollisionShape2D>("CollisionShape2D")?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_attackArea?.GetNode<CollisionShape2D>("CollisionShape2D")?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_pushArea?.GetNode<CollisionShape2D>("CollisionShape2D")?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		CollisionLayer = 0; CollisionMask = 0; // 他と衝突しないように

		// TODO: ドロップアイテム生成 (Base.LootTable を使う)
		// OnAnimationFinished で QueueFree() が呼ばれる
	}
}
