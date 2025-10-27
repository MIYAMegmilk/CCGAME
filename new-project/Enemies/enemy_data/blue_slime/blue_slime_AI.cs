using Godot;
using System;

public partial class blue_slime_AI : CharacterBody2D{
	
	public override void _Ready()
	{
		_Sprite ??= GetNode<Sprite2D>("Sprite");
		
		
	}
	public override void _PhysicsProcess(double delta){
		
	}
}
