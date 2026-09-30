extends SceneTree
## Cell-bounds audit for the generated Tubman atlases (Package 13 W5).
##
##   Godot_console --headless --path <repo> --script res://tools/asset_src/tubman/check_tubman_cells.gd
##
## Reports every cell's opaque bounding box and flags art that touches a cell
## edge (and so bleeds into a neighbouring cell once the atlas is sliced), or a
## cell with no opaque art at all. Exit code 1 on any flag. It lives under the
## .gdignore'd asset_src tree, so the editor never imports it; --script reads it
## straight from disk.

const ALPHA_FLOOR := 0.04

const SHEETS := {
	"res://assets/sprites/characters/tubman/retro/tubman_locomotion_atlas.png": Vector2i(96, 128),
	"res://assets/sprites/characters/tubman/retro/tubman_traversal_atlas.png": Vector2i(96, 128),
	"res://assets/sprites/characters/tubman/retro/tubman_combat_atlas.png": Vector2i(96, 128),
	"res://assets/sprites/characters/tubman/retro/tubman_states_atlas.png": Vector2i(96, 128),
	"res://assets/sprites/characters/tubman/retro/tubman_abilities_atlas.png": Vector2i(96, 128),
	"res://assets/sprites/characters/tubman/retro/tubman_directional_attacks_atlas.png": Vector2i(96, 128),
	"res://assets/sprites/vfx/tubman/tubman_ability_vfx_atlas.png": Vector2i(192, 192),
}


func _initialize() -> void:
	var flags := 0
	for path in SHEETS:
		var image := Image.load_from_file(ProjectSettings.globalize_path(path))
		if image == null:
			push_error("cannot read " + path)
			flags += 1
			continue
		var cell: Vector2i = SHEETS[path]
		var cols := image.get_width() / cell.x
		var rows := image.get_height() / cell.y
		print("%s  %dx%d  (%d cols x %d rows)" % [path.get_file(), image.get_width(), image.get_height(), cols, rows])
		for row in rows:
			var line := "  row %d:" % row
			for col in cols:
				var sub := image.get_region(Rect2i(col * cell.x, row * cell.y, cell.x, cell.y))
				var used := _visible_rect(sub)
				var note := ""
				if used.size == Vector2i.ZERO:
					note = " EMPTY"
				elif used.position.x <= 0 or used.position.y <= 0 \
						or used.end.x >= cell.x or used.end.y >= cell.y:
					note = " EDGE"
				if note != "":
					flags += 1
				line += "  [%d,%d %dx%d h=%d%s]" % [used.position.x, used.position.y, used.size.x, used.size.y, used.size.y, note]
			print(line)
	print("check_tubman_cells: %d flag(s)" % flags)
	quit(0 if flags == 0 else 1)


## Bounding box of pixels above a small alpha floor, so the Lanczos downscale's
## near-invisible ringing (alpha of a few 1/255ths) does not count as art.
func _visible_rect(image: Image) -> Rect2i:
	var min_x := image.get_width()
	var min_y := image.get_height()
	var max_x := -1
	var max_y := -1
	for y in image.get_height():
		for x in image.get_width():
			if image.get_pixel(x, y).a > ALPHA_FLOOR:
				min_x = mini(min_x, x)
				min_y = mini(min_y, y)
				max_x = maxi(max_x, x)
				max_y = maxi(max_y, y)
	if max_x < 0:
		return Rect2i()
	return Rect2i(min_x, min_y, max_x - min_x + 1, max_y - min_y + 1)
