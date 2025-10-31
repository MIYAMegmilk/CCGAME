// LootTable.cs
using Godot;
// using Godot.Collections; // ←フルネームで書くので、この行はあってもなくてもOK

[GlobalClass]
public partial class LootTable : Resource
{
	// 【修正】 Array<LootEntry> -> Godot.Collections.Array<LootEntry> に変更
	[Export] 
	public Godot.Collections.Array<LootEntry> Entries { get; set; } = new Godot.Collections.Array<LootEntry>();
}
