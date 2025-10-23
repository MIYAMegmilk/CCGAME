extends Node2D



func create_sprite(texture_path:String, position: Vector2) -> Sprite2D:
	# 1. 新しいSprite2Dノードを作成する
	var new_sprite = Sprite2D.new()

	# 2. テクスチャをロードして割り当てる
	new_sprite.texture = load(texture_path) 

	# 3. 位置を設定する
	new_sprite.position = position

	# 4. シーンツリーに追加する
	add_child(new_sprite)

	return new_sprite
