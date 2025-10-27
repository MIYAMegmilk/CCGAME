using Godot;
using System;

[GlobalClass]
public partial class EnemyBase : Resource
{
	[ExportGroup("Basic Info")]
	[Export] public float DetectionSize {get;set;} = 60.0f;
	[Export] public float DetectionAngle { get; set; } = 60.0f;
	[Export] public string EnemyName { get; set; } = "New Enemy";
	[Export] public PackedScene EnemyScene { get; set; }

	[ExportGroup("Stats")]
	[Export] // 既存の CharacterStats リソースをここにリンク
	public CharacterStats Stats { get; set; }

	[ExportGroup("Behavior")]
	[Export(PropertyHint.File, "*.cs")] //行動パターンを書いた C# スクリプトへのパス
	public string AIScriptPath { get; set; }
	// または、AI用の Resource を別途作る場合:
	// [Export] public EnemyAIBehavior AIBehavior { get; set; }

	//[ExportGroup("Loot")]
	//[Export] //ドロップ品データを定義するリソースをここにリンク
	//public LootTableData LootTable { get; set; }

	// サウンドエフェクト用(使わなかったら消して)
	// [Export] public Faction FactionType { get; set; }
	// [Export] public AudioStream HurtSound { get; set; }
}
