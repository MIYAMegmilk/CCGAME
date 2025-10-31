// ItemData.cs
using Godot;

[GlobalClass]
public partial class ItemData : Resource
{
	[Export] public string Name { get; set; } = "New Item";
	[Export] public Texture2D Texture { get; set; }
	[Export] public int MaxStackSize { get; set; } = 999;

	
	
	
	[ExportGroup("Weapon Stats")] // インスペクターで見やすくグループ化
	[Export] public bool IsWeapon { get; set; } = false; // これが武器かどうか
	[Export] public int Strength_bonus { get; set; } = 0;
	[Export] public float KnockbackPower { get; set; } = 1.0f;//1が通常
	// 武器ごとの当たり判定 CollisionShape2D の名前を指定 ("SwordShape", "AxeShape")
	[Export(PropertyHint.Enum,"sword,axe,spear,default")] public string HitboxShapeName { get; set; }
	// 攻撃アニメーションの再生速度 (1.0 が標準)
	[Export(PropertyHint.Range, "0.1, 5.0, 0.1")] // 0.1倍速から5倍速まで
	public float AnimationSpeedScale { get; set; } = 1.0f;
}
