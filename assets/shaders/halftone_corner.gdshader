shader_type canvas_item;

// UR-WIDGETS. PNL-B halftone corner (UI_SPEC 3.1): a pitch-10 dot grid in the bottom-right 42 % x 52 % of the
// layer, faded along the 315 degree diagonal. gl_compatibility: plain uniforms only, no instance uniforms.

uniform vec2 rect_size = vec2(100.0, 100.0);
uniform vec2 region_frac = vec2(0.42, 0.52);
uniform float pitch = 10.0;
uniform float solid_radius = 1.5;
uniform float fade_radius = 2.0;
uniform vec4 dot_color = vec4(1.0, 1.0, 1.0, 0.17);

void fragment() {
	vec2 p = UV * rect_size;
	vec2 region = rect_size * region_frac;
	vec2 q = p - (rect_size - region);
	float inside = step(0.0, q.x) * step(0.0, q.y);
	vec2 cell = mod(q, vec2(pitch)) - vec2(pitch * 0.5);
	float coverage = 1.0 - smoothstep(solid_radius, fade_radius, length(cell));
	float t = dot(q - region * 0.5, vec2(-0.7071, -0.7071)) / (0.7071 * (region.x + region.y)) + 0.5;
	float mask = 1.0 - clamp(t / 0.82, 0.0, 1.0);
	COLOR = vec4(dot_color.rgb, dot_color.a * coverage * mask * inside);
}
