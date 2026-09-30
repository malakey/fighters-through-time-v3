extends SceneTree
## Asset pipeline for production-style placeholder art (Package 10 pass 1).
##
## Runs headless:
##   Godot_console --headless --path <repo> --script res://tools/generate_assets.gd -- group=menu
##
## Job groups live in res://tools/asset_src/jobs.json. Two job families:
##   "svg"        - rasterize an authored SVG through the same ThorVG build the
##                  game imports with, supersampled then Lanczos-downscaled, so a
##                  preview PNG shows exactly what Godot will render.
##   "atmosphere" - deterministic procedural texture: vertical gradient +
##                  FastNoiseLite cloud layers + particle field (stars/embers) +
##                  vignette. Seeded; same JSON in, same PNG out.
##   "blend"      - alpha-composite one generated PNG over another.
##   "svg_atlas"  - rasterize one SVG per cell and pack a fixed-grid sprite atlas
##                  (each cell downscaled on its own, so nothing bleeds across cells).
##
## SVG sources sit under res://tools/asset_src/ behind a .gdignore so the editor
## never imports them; only the PNGs this script writes into assets/ ship.

const JOBS_PATH := "res://tools/asset_src/jobs.json"


func _initialize() -> void:
	var groups := _requested_groups()
	if groups.is_empty():
		push_error("Usage: ... --script res://tools/generate_assets.gd -- group=<name>[,<name>] | group=all")
		quit(1)
		return

	var manifest := _load_jobs()
	if manifest.is_empty():
		quit(1)
		return

	var ran := 0
	var failed := 0
	for job_variant in manifest:
		var job: Dictionary = job_variant
		var group := str(job.get("group", ""))
		if not ("all" in groups or group in groups):
			continue
		ran += 1
		var err := _run_job(job)
		if err != OK:
			failed += 1
			push_error("Job failed (%s): %s" % [error_string(err), JSON.stringify(job)])
	print("generate_assets: %d jobs run, %d failed (groups: %s)" % [ran, failed, ",".join(groups)])
	quit(0 if failed == 0 else 1)


func _requested_groups() -> PackedStringArray:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("group="):
			return arg.substr(6).split(",", false)
	return PackedStringArray()


func _load_jobs() -> Array:
	var text := FileAccess.get_file_as_string(JOBS_PATH)
	if text.is_empty():
		push_error("Cannot read " + JOBS_PATH)
		return []
	var parsed: Variant = JSON.parse_string(text)
	if parsed == null or not (parsed is Dictionary) or not parsed.has("jobs"):
		push_error("jobs.json must be an object with a \"jobs\" array")
		return []
	return parsed["jobs"]


func _run_job(job: Dictionary) -> Error:
	match str(job.get("type", "")):
		"svg":
			return _job_svg(job)
		"svg_dir":
			return _job_svg_dir(job)
		"svg_atlas":
			return _job_svg_atlas(job)
		"atmosphere":
			return _job_atmosphere(job)
		"blend":
			return _job_blend(job)
		_:
			push_error("Unknown job type: " + str(job.get("type")))
			return ERR_INVALID_PARAMETER


# --- svg -------------------------------------------------------------------

func _job_svg(job: Dictionary) -> Error:
	var src := str(job.get("src", ""))
	var out := str(job.get("out", ""))
	var width := int(job.get("width", 0))
	var height := int(job.get("height", 0))
	var supersample := float(job.get("supersample", 2.0))

	var svg_text := FileAccess.get_file_as_string(src)
	if svg_text.is_empty():
		push_error("Cannot read SVG " + src)
		return ERR_FILE_NOT_FOUND

	var image := Image.new()
	var err := image.load_svg_from_string(svg_text, supersample)
	if err != OK:
		return err
	if width > 0 and height > 0 and (image.get_width() != width or image.get_height() != height):
		image.resize(width, height, Image.INTERPOLATE_LANCZOS)
	return _save(image, out)


## Rasterizes every .svg in src_dir to out_dir at a fixed size, and optionally
## writes a contact-sheet montage of all frames for quick visual review.
func _job_svg_dir(job: Dictionary) -> Error:
	var src_dir := str(job.get("src_dir", ""))
	var out_dir := str(job.get("out_dir", ""))
	var width := int(job.get("width", 96))
	var height := int(job.get("height", 128))
	var supersample := float(job.get("supersample", 4.0))

	var dir := DirAccess.open(src_dir)
	if dir == null:
		push_error("Cannot open " + src_dir)
		return ERR_FILE_NOT_FOUND
	var names: Array[String] = []
	for file in dir.get_files():
		if file.ends_with(".svg"):
			names.append(file)
	names.sort()
	if names.is_empty():
		push_error("No SVGs in " + src_dir)
		return ERR_FILE_NOT_FOUND

	var frames: Array[Image] = []
	for file in names:
		var image := Image.new()
		var err := image.load_svg_from_string(
			FileAccess.get_file_as_string(src_dir.path_join(file)), supersample)
		if err != OK:
			push_error("SVG failed: " + file)
			return err
		image.resize(width, height, Image.INTERPOLATE_LANCZOS)
		frames.append(image)
		var save_err := _save(image, out_dir.path_join(file.get_basename() + ".png"))
		if save_err != OK:
			return save_err

	var sheet_out := str(job.get("sheet_out", ""))
	if not sheet_out.is_empty():
		var columns := int(job.get("sheet_columns", 10))
		var rows := int(ceil(float(frames.size()) / columns))
		var sheet := Image.create_empty(columns * width, rows * height, false, Image.FORMAT_RGBA8)
		sheet.fill(Color(0.08, 0.09, 0.16, 1.0))
		for i in frames.size():
			sheet.blend_rect(frames[i], Rect2i(0, 0, width, height),
				Vector2i((i % columns) * width, (i / columns) * height))
		return _save(sheet, sheet_out)
	return OK


## Rasterizes one SVG per atlas cell and packs them into a fixed grid, so no
## cell's antialiasing or downscale can bleed into its neighbour. Cell files are
## <cell_dir>/<prefix><row>_<column>.svg; "rows" lists the row names top to
## bottom (the order the SpriteFrames builder reads them).
func _job_svg_atlas(job: Dictionary) -> Error:
	var cell_dir := str(job.get("cell_dir", ""))
	var prefix := str(job.get("prefix", ""))
	var rows: Array = job.get("rows", [])
	var columns := int(job.get("columns", 3))
	var width := int(job.get("cell_width", 96))
	var height := int(job.get("cell_height", 128))
	var supersample := float(job.get("supersample", 4.0))
	if rows.is_empty():
		push_error("svg_atlas needs a non-empty \"rows\" list")
		return ERR_INVALID_PARAMETER

	var atlas := Image.create_empty(columns * width, rows.size() * height, false, Image.FORMAT_RGBA8)
	atlas.fill(Color(0, 0, 0, 0))
	for row in rows.size():
		for column in columns:
			var path := cell_dir.path_join("%s%s_%d.svg" % [prefix, str(rows[row]), column])
			var svg_text := FileAccess.get_file_as_string(path)
			if svg_text.is_empty():
				push_error("Cannot read SVG " + path)
				return ERR_FILE_NOT_FOUND
			var image := Image.new()
			var err := image.load_svg_from_string(svg_text, supersample)
			if err != OK:
				push_error("SVG failed: " + path)
				return err
			image.resize(width, height, Image.INTERPOLATE_LANCZOS)
			image.convert(Image.FORMAT_RGBA8)
			atlas.blit_rect(image, Rect2i(0, 0, width, height), Vector2i(column * width, row * height))
	return _save(atlas, str(job.get("out", "")))


# --- atmosphere ------------------------------------------------------------

func _job_atmosphere(job: Dictionary) -> Error:
	var width := int(job.get("width", 1920))
	var height := int(job.get("height", 1080))
	var image := Image.create_empty(width, height, false, Image.FORMAT_RGBA8)

	_paint_gradient(image, job.get("gradient", []))
	for layer_variant in job.get("noise_layers", []):
		_paint_noise_layer(image, layer_variant, int(job.get("seed", 1)))
	for field_variant in job.get("particles", []):
		_paint_particles(image, field_variant, int(job.get("seed", 1)))
	if job.has("vignette"):
		_paint_vignette(image, job["vignette"])
	return _save(image, str(job.get("out", "")))


func _paint_gradient(image: Image, stops_variant: Variant) -> void:
	var stops: Array = stops_variant
	if stops.is_empty():
		return
	var height := image.get_height()
	var width := image.get_width()
	for y in height:
		var t := float(y) / float(height - 1)
		var color := _gradient_sample(stops, t)
		for x in width:
			image.set_pixel(x, y, color)


func _gradient_sample(stops: Array, t: float) -> Color:
	var previous: Array = stops[0]
	for stop_variant in stops:
		var stop: Array = stop_variant
		if t <= float(stop[0]):
			var span := float(stop[0]) - float(previous[0])
			var local := 0.0 if span <= 0.0 else (t - float(previous[0])) / span
			return Color.html(str(previous[1])).lerp(Color.html(str(stop[1])), local)
		previous = stop
	return Color.html(str(previous[1]))


func _paint_noise_layer(image: Image, layer_variant: Variant, base_seed: int) -> void:
	var layer: Dictionary = layer_variant
	var noise := FastNoiseLite.new()
	noise.noise_type = FastNoiseLite.TYPE_PERLIN
	noise.fractal_type = FastNoiseLite.FRACTAL_FBM
	noise.fractal_octaves = int(layer.get("octaves", 4))
	noise.frequency = float(layer.get("frequency", 0.002))
	noise.seed = base_seed + int(layer.get("seed_offset", 0))

	var color := Color.html(str(layer.get("color", "#404060")))
	var threshold := float(layer.get("threshold", 0.45))
	var curve := float(layer.get("curve", 2.0))
	var alpha := float(layer.get("alpha", 0.35))
	var stretch_x := float(layer.get("stretch_x", 1.0))

	for y in image.get_height():
		for x in image.get_width():
			var n := noise.get_noise_2d(x * stretch_x, y) * 0.5 + 0.5
			if n <= threshold:
				continue
			var strength: float = pow((n - threshold) / (1.0 - threshold), curve) * alpha
			var base := image.get_pixel(x, y)
			image.set_pixel(x, y, base.lerp(color, strength))


func _paint_particles(image: Image, field_variant: Variant, base_seed: int) -> void:
	var field: Dictionary = field_variant
	var rng := RandomNumberGenerator.new()
	rng.seed = base_seed + int(field.get("seed_offset", 100))

	var count := int(field.get("count", 300))
	var colors: Array = field.get("colors", ["#ffffff"])
	var size_min := float(field.get("size_min", 0.6))
	var size_max := float(field.get("size_max", 2.2))
	var streak := float(field.get("streak", 1.0))  # >1 elongates along angle
	var angle := deg_to_rad(float(field.get("angle_degrees", 0.0)))
	var band_top := float(field.get("band_top", 0.0))
	var band_bottom := float(field.get("band_bottom", 1.0))
	var glow := float(field.get("glow", 2.5))
	var margin_x := float(field.get("margin_x", 0.0))  # keep clear of edges so tiles repeat cleanly

	var width := image.get_width()
	var height := image.get_height()
	var cos_a := cos(angle)
	var sin_a := sin(angle)

	for i in count:
		var cx := margin_x + rng.randf() * (width - margin_x * 2.0)
		var cy := lerpf(band_top, band_bottom, rng.randf()) * height
		var size := rng.randf_range(size_min, size_max)
		var color := Color.html(str(colors[rng.randi() % colors.size()]))
		var brightness := rng.randf_range(0.35, 1.0)
		var reach := size * glow * maxf(streak, 1.0)

		for py in range(maxi(0, int(cy - reach)), mini(height, int(cy + reach) + 1)):
			for px in range(maxi(0, int(cx - reach)), mini(width, int(cx + reach) + 1)):
				var dx := px - cx
				var dy := py - cy
				# Rotate into streak space; divide the along-axis by streak.
				var along := (dx * cos_a + dy * sin_a) / maxf(streak, 1.0)
				var across := -dx * sin_a + dy * cos_a
				var dist := sqrt(along * along + across * across) / size
				if dist > glow:
					continue
				var falloff: float = brightness * exp(-dist * dist * 1.35)
				if falloff < 0.012:
					continue
				var base := image.get_pixel(px, py)
				image.set_pixel(px, py, Color(
					minf(base.r + color.r * falloff, 1.0),
					minf(base.g + color.g * falloff, 1.0),
					minf(base.b + color.b * falloff, 1.0),
					maxf(base.a, minf(falloff * 2.0, 1.0))))


func _paint_vignette(image: Image, vignette_variant: Variant) -> void:
	var vignette: Dictionary = vignette_variant
	var strength := float(vignette.get("strength", 0.35))
	var power := float(vignette.get("power", 2.2))
	var width := image.get_width()
	var height := image.get_height()
	var center := Vector2(width, height) * 0.5
	var max_dist := center.length()
	for y in height:
		for x in width:
			var dist := Vector2(x - center.x, y - center.y).length() / max_dist
			var factor: float = 1.0 - strength * pow(dist, power)
			var base := image.get_pixel(x, y)
			image.set_pixel(x, y, Color(base.r * factor, base.g * factor, base.b * factor, base.a))


# --- blend -----------------------------------------------------------------

func _job_blend(job: Dictionary) -> Error:
	var base_image := Image.load_from_file(ProjectSettings.globalize_path(str(job.get("base", ""))))
	var overlay := Image.load_from_file(ProjectSettings.globalize_path(str(job.get("overlay", ""))))
	if base_image == null or overlay == null:
		return ERR_FILE_NOT_FOUND
	base_image.convert(Image.FORMAT_RGBA8)
	overlay.convert(Image.FORMAT_RGBA8)
	base_image.blend_rect(overlay, Rect2i(0, 0, overlay.get_width(), overlay.get_height()),
		Vector2i(int(job.get("x", 0)), int(job.get("y", 0))))
	return _save(base_image, str(job.get("out", "")))


# --- shared ----------------------------------------------------------------

func _save(image: Image, out: String) -> Error:
	if out.is_empty():
		return ERR_INVALID_PARAMETER
	var absolute := ProjectSettings.globalize_path(out)
	DirAccess.make_dir_recursive_absolute(absolute.get_base_dir())
	var err := image.save_png(absolute)
	if err == OK:
		print("  wrote %s (%dx%d)" % [out, image.get_width(), image.get_height()])
	return err
