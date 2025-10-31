//EnemyBase.cs
using Godot;
using System;

[GlobalClass]
public partial class EnemyBase : Resource
{
	[ExportGroup("Basic Info")]
	[Export] public string EnemyTag { get; set; } = "Enemy Tag";
	[Export] public float DetectionSize {get;set;} = 60.0f;
	[Export] public float DetectionAngle { get; set; } = 60.0f;
	[Export] public float AttackRange { get; set; } = 6.0f;//ピクセル単位
	
	[ExportGroup("LootTable")]
	[Export] public LootTable LootTable { get; set; }
	
	[ExportGroup("Stats")]
	[Export] // 既存の CharacterStats リソースをここにリンク
	public EnemyStats Stats { get; set; }

	[ExportGroup("Behavior")]
	[Export(PropertyHint.File, "*.cs")] //行動パターンを書いた C# スクリプトへのパス
	public string AIScriptPath { get; set; }
	// 
	// [Export] public EnemyAIBehavior AIBehavior { get; set; }

	//[ExportGroup("Loot")]
	//[Export] //ドロップ品データを定義するリソースをここにリンク
	//public LootTableData LootTable { get; set; }

	// サウンドエフェクト用(使わなかったら消して)
	// [Export] public Faction FactionType { get; set; }
	// [Export] public AudioStream HurtSound { get; set; }
}
