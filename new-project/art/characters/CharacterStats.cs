using Godot;
using System;

[GlobalClass]
public partial class CharacterStats : Resource
{
	[Export] public float Speed { get; set; } = 100.0f;
	[Export] public float MaxHealth { get; set; } = 50.0f;
	[Export] public float Health { get; set; } = 50.0f;
	[Export] public float HPRegene { get; set; } = 1.0f; // Per second
	
	[Export] public bool HavaMana {get; set;} = true;
	[Export] public float Mana { get; set; } = 0.0f;
	[Export] public float MaxMana { get; set; } = 0.0f;
	[Export] public float ManaRegene { get; set; } = 0.5f; // Per second
	
	[Export] public float Strength { get; set; } = 0.0f;//物理攻撃へのボーナス
	[Export] public float Defense { get; set; } = 0.0f;//物理攻撃からのダメージ減少ボーナス
	[Export] public float MagicPower { get; set; } = 0.0f;//魔法攻撃へのボーナス
	[Export] public float MagicDefense { get; set; } = 0.0f;//魔法攻撃からのダメージ減少ボーナス
	[Export] public float Dexterity { get; set; } = 0.0f;//器用さ attack_speedやクリティカル率に影響
	[Export] public float Luck { get; set; } = 0.0f;//ドロップ量やドロップ率を上昇

	[Export] public float PushForce { get; set; } = 1000.0f;
}
