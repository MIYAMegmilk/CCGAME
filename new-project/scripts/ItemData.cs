// ItemData.cs
using Godot;

[GlobalClass]
public partial class ItemData : Resource
{
	[Export] public string Name { get; set; } = "New Item";
	[Export] public Texture2D Texture { get; set; }
	[Export] public int MaxStackSize { get; set; } = 99;
}
