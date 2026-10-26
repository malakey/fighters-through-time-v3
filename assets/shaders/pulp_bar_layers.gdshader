shader_type canvas_item;

// UR-WIDGETS. GAUGE-B bar layers (UI_SPEC 3.3.1 and 3.5), drawn over a ProgressBar whose fill is a skewed
// StyleBoxFlat: the damage tracker, the Rally echo band, the hatch and highlight on the fill, the decile
// ticks and the boss phase notches. gl_compatibility: plain uniforms only.
//
// Coordinates: p is the pixel inside the inner area (top-left origin). A skewed fill of fraction f has its
// right edge at x = f * W - skew_off * (p.y / H); c is the "canonical" x that makes every fraction edge
// vertical: c = x + skew_off * y / H (c = f * W at that edge, the base edge is c = skew_off). A right-filling
// bar is the mirror image about the same slant.

uniform vec2 rect_size = vec2(100.0, 30.0);
uniform vec2 inner_pos = vec2(3.0, 3.0);
uniform vec2 inner_size = vec2(94.0, 24.0);
uniform float skew_off = 0.0;
uniform float reverse = 0.0;
uniform float v = 1.0;
uniform float lag = 0.0;
uniform float echo_to = 0.0;
uniform vec4 lag_color = vec4(1.0, 0.353, 0.353, 1.0);
uniform vec4 echo_color = vec4(1.0, 0.969, 0.855, 1.0);
uniform float echo_inset = 0.22;
uniform float hatch_dark = 5.0;
uniform float hatch_clear = 6.0;
uniform float hatch_alpha = 0.22;
uniform float hl_top = 3.0;
uniform float hl_height = 5.0;
uniform float hl_alpha = 0.60;
uniform float ticks_on = 1.0;
uniform float tick_width = 2.0;
uniform vec4 notches = vec4(-1.0, -1.0, -1.0, -1.0);
uniform float notch_width = 6.0;

vec4 over(vec4 top, vec4 bottom) {
	float a = top.a + bottom.a * (1.0 - top.a);
	if (a <= 0.0) {
		return vec4(0.0);
	}
	return vec4((top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / a, a);
}

void fragment() {
	vec2 p = UV * rect_size - inner_pos;
	float W = inner_size.x;
	float H = inner_size.y;
	float yy = p.y / H;
	float c = mix(p.x + skew_off * yy, W - p.x + skew_off * (1.0 - yy), reverse);
	float inside = step(0.0, p.y) * step(p.y, H) * step(skew_off, c) * step(c, W);
	vec4 col = vec4(0.0);

	float fill_edge = v * W;
	float on_fill = step(c, fill_edge);

	// Tracker: from the fill end to lag.
	float in_lag = (1.0 - on_fill) * step(c, lag * W);
	col = over(vec4(lag_color.rgb, lag_color.a * in_lag), col);

	// Rally echo band: from v to echo_to, inset top and bottom, 2 px black edges.
	float band_top = echo_inset * H;
	float band_bottom = (1.0 - echo_inset) * H;
	float in_echo = (1.0 - on_fill) * step(c, echo_to * W) * step(band_top, p.y) * step(p.y, band_bottom);
	float echo_edge = step(p.y, band_top + 2.0) + step(band_bottom - 2.0, p.y);
	vec4 echo = mix(echo_color, vec4(0.0, 0.0, 0.0, 1.0), clamp(echo_edge, 0.0, 1.0));
	col = over(vec4(echo.rgb, echo.a * in_echo), col);

	// Hatch on the fill: 135 degree stripes, dark then clear, sheared with the track.
	float period = hatch_dark + hatch_clear;
	float phase = mod((c + p.y) * 0.7071, period);
	float hatch = step(phase, hatch_dark) * on_fill;
	col = over(vec4(0.0, 0.0, 0.0, hatch_alpha * hatch), col);

	// Highlight band across the fill.
	float hl = step(hl_top, p.y) * step(p.y, hl_top + hl_height) * on_fill;
	col = over(vec4(1.0, 1.0, 1.0, hl_alpha * hl), col);

	// Decile ticks: 2 px black at the end of every 10 %.
	float step_w = 0.1 * W;
	float t = mod(c, step_w);
	float tick = ticks_on * step(step_w - tick_width, t);
	col = over(vec4(0.0, 0.0, 0.0, tick), col);

	// Phase notches (boss bar): 6 px black, ending at the threshold.
	float notch = 0.0;
	for (int i = 0; i < 4; i++) {
		float n = notches[i];
		if (n >= 0.0) {
			float edge = n * W;
			notch = max(notch, step(edge - notch_width * 0.5, c) * step(c, edge + notch_width * 0.5));
		}
	}
	col = over(vec4(0.0, 0.0, 0.0, notch), col);

	// COLOR arrives as the draw colour times the canvas modulate (HUD opacity, panel fades): keep it.
	COLOR = vec4(col.rgb * COLOR.rgb, col.a * inside * COLOR.a);
}
