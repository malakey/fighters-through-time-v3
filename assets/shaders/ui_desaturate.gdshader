shader_type canvas_item;

// UR-WIDGETS. Saturation and brightness for UI art (ability slots: cooling saturate(.5) brightness(.72),
// charging .55 / .62, suppressed greyscale at .5). gl_compatibility: plain uniforms only.

uniform float saturation = 1.0;
uniform float brightness = 1.0;

void fragment() {
	float grey = dot(COLOR.rgb, vec3(0.299, 0.587, 0.114));
	COLOR.rgb = mix(vec3(grey), COLOR.rgb, saturation) * brightness;
}
