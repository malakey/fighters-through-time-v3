// TG-WIDGETS. The main menu title's vertical gradient (UI_SPEC_GLASS 6.5, T01): #FFFFFF at the line box's top, #DFE8FF at
// 42 %, #8D9BC6 at its bottom, multiplied into the label's own colour (the label draws white under it), so the incoming
// modulate is kept and the black halo and shadow stay black. One ShaderMaterial per title Label (no instance uniforms);
// TitleGradient sets line_top and line_height from the label's font metrics and the UI scale. TG-CLOSE: a wrapped title
// repeats the gradient on each of its line_count lines, line_pitch apart (it spanned the first line box only, so the
// second line drew flat in the last colour); a pixel belongs to the nearest line box, the boundary halfway across the gap.
shader_type canvas_item;

uniform vec4 top_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 mid_color : source_color = vec4(0.875, 0.910, 1.0, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.553, 0.608, 0.776, 1.0);
uniform float mid_stop = 0.42;
uniform float line_top = 0.0;
uniform float line_height = 105.28;
uniform float line_pitch = 0.0;
uniform int line_count = 1;

varying float local_y;

void vertex() {
	local_y = VERTEX.y;
}

void fragment() {
	float d = local_y - line_top;
	float line = 0.0;
	if (line_count > 1 && line_pitch > 0.001) {
		line = clamp(floor((d + (line_pitch - line_height) * 0.5) / line_pitch), 0.0, float(line_count - 1));
	}
	float t = clamp((d - line * line_pitch) / max(line_height, 0.001), 0.0, 1.0);
	vec4 g = t < mid_stop ? mix(top_color, mid_color, t / mid_stop)
		: mix(mid_color, bottom_color, (t - mid_stop) / (1.0 - mid_stop));
	COLOR.rgb *= g.rgb;
}
