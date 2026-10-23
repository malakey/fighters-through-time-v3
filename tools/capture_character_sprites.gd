extends SceneTree
## Renders every frame of the roster's character SpriteFrames the way the game
## draws them (factory base scale, texel scale, RetroSpriteScaleNormalizer feet
## anchoring, mipmapped filtering for 2x art) at a chosen camera zoom, and saves
## contact pages with a floor line and the 40 x 64 px Story hurtbox for scale.
##
## Needs a real window (rendering does not run headless):
##   Godot_console --path <repo> --script res://tools/capture_character_sprites.gd -- \
##       out=D:/tmp/captures chars=joan,einstein zoom=1.5
## Pages are written as <out>/<character>_page_<n>.png at 1920 x 1080.

const WORLD_SCALE := 0.845            # CharacterFactory.CharacterSpriteWorldScale
const NORMALIZER := "res://scripts/Combat/RetroSpriteScaleNormalizer.cs"
const ANIMATIONS := [
	"idle", "run", "jump", "fall", "skid", "crouch",
	"roll_startup", "roll", "roll_recovery", "ledge_hang", "ledge_pull_up", "ledge_drop",
	"basic_attack_1", "basic_attack_2", "basic_attack_3", "up_attack", "down_air", "grab", "throw",
	"block", "hitstun", "dazed", "death", "respawn", "victory", "defeat",
	"special_1", "special_2", "movement_ability", "ultimate",
]
const CELL := Vector2(236, 300)
const COLUMNS := 8
const ROWS := 3
## legacy_dir=<path>: build each character's frames from pre-v2 96x128 atlases
## at <path>/<id>/<id>_<sheet>_atlas.png (the old fixed grid, three frames per
## row), for before/after captures without importing anything.
const LEGACY_ROWS := {
	"idle": ["locomotion", 0], "run": ["locomotion", 1], "jump": ["locomotion", 2],
	"fall": ["locomotion", 3], "skid": ["locomotion", 4], "crouch": ["locomotion", 5],
	"roll_startup": ["traversal", 0], "roll": ["traversal", 1], "roll_recovery": ["traversal", 2],
	"ledge_hang": ["traversal", 3], "ledge_pull_up": ["traversal", 4], "ledge_drop": ["traversal", 5],
	"basic_attack_1": ["combat", 0], "basic_attack_2": ["combat", 1], "basic_attack_3": ["combat", 2],
	"block": ["combat", 3], "hitstun": ["combat", 4], "grab": ["combat", 0], "throw": ["combat", 2],
	"up_attack": ["directional_attacks", 0], "down_air": ["directional_attacks", 1],
	"dazed": ["states", 0], "death": ["states", 1], "respawn": ["states", 2],
	"victory": ["states", 3], "defeat": ["states", 4],
	"special_1": ["abilities", 0], "special_2": ["abilities", 1],
	"movement_ability": ["abilities", 2], "ultimate": ["abilities", 3],
}


func _legacy_frames(dir: String, character: String) -> SpriteFrames:
	var frames := SpriteFrames.new()
	var sheets := {}
	for animation in LEGACY_ROWS:
		var sheet: String = LEGACY_ROWS[animation][0]
		if not sheets.has(sheet):
			var image := Image.load_from_file("%s/%s/%s_%s_atlas.png" % [dir, character, character, sheet])
			if image == null:
				continue
			sheets[sheet] = ImageTexture.create_from_image(image)
		frames.add_animation(animation)
		for column in 3:
			var region := AtlasTexture.new()
			region.atlas = sheets[sheet]
			region.region = Rect2(column * 96, LEGACY_ROWS[animation][1] * 128, 96, 128)
			frames.add_frame(animation, region)
	return frames


func _init() -> void:
	var options := {"out": "user://captures", "chars": "joan", "zoom": "1.5"}
	for arg in OS.get_cmdline_user_args():
		var parts: PackedStringArray = arg.split("=", true, 1)
		if parts.size() == 2:
			options[parts[0]] = parts[1]
	DisplayServer.window_set_size(Vector2i(1920, 1080))
	root.size = Vector2i(1920, 1080)
	DirAccess.make_dir_recursive_absolute(options["out"])
	_run(options)


func _run(options: Dictionary) -> void:
	await process_frame
	var zoom := float(options["zoom"])
	for character in String(options["chars"]).split(","):
		var frames: SpriteFrames
		if options.has("legacy_dir"):
			frames = _legacy_frames(options["legacy_dir"], character)
		else:
			frames = load("res://resources/SpriteFrames/%s_frames.tres" % character)
		if frames == null:
			push_error("missing frames for %s" % character)
			continue
		var texel := float(frames.get_meta("retro_texel_scale", 1.0))
		var cells: Array = []
		for animation in ANIMATIONS:
			if not frames.has_animation(animation):
				continue
			for index in frames.get_frame_count(animation):
				cells.append([animation, index])
		var per_page := COLUMNS * ROWS
		var page := 0
		while page * per_page < cells.size():
			var holder := Node2D.new()
			root.add_child(holder)
			var background := ColorRect.new()
			background.color = Color(0.17, 0.19, 0.24)
			background.size = Vector2(1920, 1080)
			holder.add_child(background)
			var slice: Array = cells.slice(page * per_page, (page + 1) * per_page)
			for i in slice.size():
				var origin := Vector2(40 + (i % COLUMNS) * CELL.x + CELL.x * 0.5,
					60 + int(i / COLUMNS) * CELL.y + CELL.y - 40)
				_add_cell(holder, frames, texel, zoom, slice[i][0], slice[i][1], origin)
			var title := Label.new()
			title.text = "%s  texel %.1f  zoom %.2f  page %d" % [character, texel, zoom, page + 1]
			title.position = Vector2(16, 8)
			holder.add_child(title)
			for _wait in 4:
				await process_frame
			var image := root.get_texture().get_image()
			image.save_png("%s/%s_page_%d.png" % [options["out"], character, page + 1])
			holder.queue_free()
			await process_frame
			page += 1
	quit()


func _add_cell(holder: Node2D, frames: SpriteFrames, texel: float, zoom: float,
		animation: String, index: int, origin: Vector2) -> void:
	var anchor := Node2D.new()
	anchor.position = origin
	holder.add_child(anchor)
	var floor_line := Line2D.new()
	floor_line.points = PackedVector2Array([Vector2(-CELL.x * 0.45, 0), Vector2(CELL.x * 0.45, 0)])
	floor_line.width = 2.0
	floor_line.default_color = Color(0.35, 0.8, 0.45)
	anchor.add_child(floor_line)
	# The universal Story hurtbox (40 x 64 px), feet on the origin.
	var hurtbox := ReferenceRect.new()
	hurtbox.border_color = Color(1, 0.35, 0.35, 0.8)
	hurtbox.editor_only = false
	hurtbox.size = Vector2(40, 64) * zoom
	hurtbox.position = Vector2(-20, -64) * zoom
	anchor.add_child(hurtbox)
	var sprite := AnimatedSprite2D.new()
	sprite.sprite_frames = frames
	sprite.animation = animation
	sprite.scale = Vector2.ONE * WORLD_SCALE / texel * zoom
	sprite.position = Vector2(0, -54.08 * zoom)
	if texel > 1.0:
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	anchor.add_child(sprite)
	var normalizer = load(NORMALIZER).new()
	sprite.add_child(normalizer)
	sprite.frame = index
	sprite.pause()
	var label := Label.new()
	label.text = "%s %d" % [animation, index]
	label.position = Vector2(-CELL.x * 0.45, 4)
	label.add_theme_font_size_override("font_size", 13)
	anchor.add_child(label)
