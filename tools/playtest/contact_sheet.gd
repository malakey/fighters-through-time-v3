extends SceneTree
## Tiles captured playtest frames into one PNG contact sheet so a run can be
## reviewed at a glance. Pure Image work — runs headless:
##
##   Godot_console --headless --path <repo> --script res://tools/playtest/contact_sheet.gd -- \
##       --dir=<abs frames dir> --from=250 --to=330 --step=2 --cols=8 --width=320 --out=<abs png>
##
## Frames are read as f_NNNNN.png (the runner's naming). Each tile gets a thin
## marker bar whose length encodes its position in the range, plus a 5x7 pixel
## digit stamp of the frame number in the top-left corner.

const DIGITS := {
	"0": ["111", "101", "101", "101", "111"],
	"1": ["010", "110", "010", "010", "111"],
	"2": ["111", "001", "111", "100", "111"],
	"3": ["111", "001", "111", "001", "111"],
	"4": ["101", "101", "111", "001", "001"],
	"5": ["111", "100", "111", "001", "111"],
	"6": ["111", "100", "111", "101", "111"],
	"7": ["111", "001", "010", "010", "010"],
	"8": ["111", "101", "111", "101", "111"],
	"9": ["111", "101", "111", "001", "111"],
}

func _initialize() -> void:
	var args := {}
	for raw in OS.get_cmdline_user_args():
		var arg: String = raw.trim_prefix("--")
		var split := arg.find("=")
		if split > 0:
			args[arg.substr(0, split)] = arg.substr(split + 1)
	var dir: String = args.get("dir", "")
	var out: String = args.get("out", "")
	var from_frame := int(args.get("from", "0"))
	var to_frame := int(args.get("to", "99999"))
	var step := maxi(1, int(args.get("step", "1")))
	var cols := maxi(1, int(args.get("cols", "8")))
	var tile_w := int(args.get("width", "320"))
	if dir == "" or out == "":
		push_error("contact_sheet: --dir and --out are required")
		quit(2)
		return

	var frames: Array[int] = []
	var listing := DirAccess.get_files_at(dir)
	for file_name in listing:
		if file_name.begins_with("f_") and file_name.ends_with(".png"):
			var n := int(file_name.substr(2, file_name.length() - 6))
			if n >= from_frame and n <= to_frame and (n - from_frame) % step == 0:
				frames.append(n)
	frames.sort()
	if frames.is_empty():
		push_error("contact_sheet: no frames in range")
		quit(3)
		return

	var first := Image.load_from_file(dir.path_join("f_%05d.png" % frames[0]))
	var tile_h := int(round(first.get_height() * tile_w / float(first.get_width())))
	var rows := int(ceil(frames.size() / float(cols)))
	var sheet := Image.create(cols * tile_w, rows * tile_h, false, Image.FORMAT_RGB8)
	sheet.fill(Color(0.05, 0.05, 0.05))
	for i in frames.size():
		var img := Image.load_from_file(dir.path_join("f_%05d.png" % frames[i]))
		if img == null:
			continue
		img.convert(Image.FORMAT_RGB8)
		img.resize(tile_w, tile_h, Image.INTERPOLATE_BILINEAR)
		var at := Vector2i((i % cols) * tile_w, (i / cols) * tile_h)
		sheet.blit_rect(img, Rect2i(Vector2i.ZERO, img.get_size()), at)
		_stamp(sheet, at + Vector2i(3, 3), str(frames[i]))
	sheet.save_png(out)
	print("CONTACT_SHEET %s (%d frames, %dx%d)" % [out, frames.size(), sheet.get_width(), sheet.get_height()])
	quit(0)

func _stamp(sheet: Image, at: Vector2i, text: String) -> void:
	var scale := 2
	var width := text.length() * 4 * scale + scale
	sheet.fill_rect(Rect2i(at, Vector2i(width, 6 * scale)), Color(0, 0, 0))
	for c in text.length():
		var glyph: Array = DIGITS.get(text[c], DIGITS["0"])
		for y in 5:
			for x in 3:
				if glyph[y][x] == "1":
					sheet.fill_rect(Rect2i(at + Vector2i((c * 4 + x) * scale + scale, y * scale + scale / 2), Vector2i(scale, scale)), Color(1, 1, 0.2))
