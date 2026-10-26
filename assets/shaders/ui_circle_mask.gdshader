shader_type canvas_item;

// UR-WIDGETS. Clips a textured rect to a circle given in the rect's own UV space (the portrait roundel:
// the art is drawn at 142 % of the diameter, off-centre, and shows through the disc only).
// gl_compatibility: plain uniforms only.

uniform vec2 circle_center = vec2(0.5, 0.5);
uniform float circle_radius = 0.5;
uniform vec2 uv_to_pixels = vec2(100.0, 100.0);

void fragment() {
	vec2 d = (UV - circle_center) * uv_to_pixels;
	float radius_px = circle_radius * uv_to_pixels.x;
	float edge = 1.0 - smoothstep(radius_px - 1.0, radius_px, length(d));
	COLOR.a *= edge;
}
