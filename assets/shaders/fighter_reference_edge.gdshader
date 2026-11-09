shader_type canvas_item;

uniform float edge_blend : hint_range(0.0, 1.0) = 0.0;
uniform vec2 edge_width = vec2(0.05);

void fragment() {
    vec2 edge = smoothstep(vec2(0.0), edge_width, UV)
        * smoothstep(vec2(0.0), edge_width, vec2(1.0) - UV);
    COLOR.a *= mix(1.0, edge.x * edge.y, edge_blend);
}
