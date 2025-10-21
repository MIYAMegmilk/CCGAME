// HPBar.cs
using Godot;

// TextureProgressBarノードにアタッチするので、TextureProgressBarを継承します
public partial class HPBar : TextureProgressBar
{
	public override void _Ready()
	{
		// 1. Playerノードを見つける
		// パスはあなたのシーンツリー構造に合わせてください
		// (例: Mainシーンの直下にPlayerがいる場合)
		Player player = GetNode<Player>("/root/Main/player");

		if (player != null)
		{
			// 2. Playerの "HealthChanged" シグナルに接続する
			// player.HealthChanged += OnHealthChanged;
			// ↑ C# 10以降の書き方。Godot 4.xはこちらを推奨

			// player.Connect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnHealthChanged)));
			// ↑ 従来のConnectメソッドを使った書き方 (どちらでも動作します)

			// 3. Player の HealthChanged シグナルが発行されたら、
			//    このスクリプトの OnHealthChanged メソッドを実行するように予約
			player.HealthChanged += OnHealthChanged;

			// 4. (初回用) 現在のHPでバーの表示を初期化
			MaxValue = player.MaxHealth;
			Value = player.Health;
		}
		else
		{
			GD.PrintErr("HPBarスクリプトがPlayerノードを見つけられません。GetNodeのパスを確認してください。");
		}
	}

	// 3. シグナルを受け取ったときに実行されるメソッド
	private void OnHealthChanged(float currentHealth, float maxHealth)
	{
		// 受け取った値でHPバーの最大値と現在の値を更新
		MaxValue = maxHealth;
		Value = currentHealth;
	}
}
