// Unified outline & glow shader (design-godot.md "Unified Outline & Glow Shader System").
//
// TWO INDEPENDENT LAYERS since Package 11 A8 (F24 Option A):
//
//   * the SECONDARY EFFECT layer — outline_color / outline_thickness /
//     glow_intensity / pulse_speed — is the arbitrated status / armor / spawn
//     treatment. It is composited BEHIND the sprite, and alpha 0 disables it.
//     Story entities may keep using it as their only outline.
//
//   * the OWNERSHIP EDGE — owner_outline_color / owner_outline_thickness /
//     owner_outline_enabled — is a Fighter player-slot identity layer (P1 cyan
//     #00f0ff, P2 red #ff3366) at a 1 px reference thickness and a static
//     intensity. It is composited AFTER (over) the effect edge, and nothing in
//     the effect layer can recolor, pulse, hide or overwrite it. HUD_CONTRACT:
//     "damage, statuses, armor, invulnerability, ability decoys and an effect
//     expiring cannot recolor, pulse, disable or replace it."
//
// The edge is drawn ON the silhouette's outermost texels rather than outside
// them: the effect glow already occupies the transparent padding, and painting a
// second opaque ring there would be exactly the "second opaque effect-coloured
// outline over it" the contract forbids — only in reverse.
//
// Alpha-based edge detection: the silhouette is found by sampling the sprite
// texture's alpha channel at 8-directional UV offsets, across three rings of
// increasing radius. Outer rings contribute at decreasing opacity so the aura
// falls off softly. The glow is composited BEHIND the sprite's own colour, so it
// surrounds the character without hiding sprite detail.
//
// gl_compatibility constraints this shader is written around:
//   1. Canvas shaders have no `instance uniform` support, so the design's
//      "one shared material for every entity" is not literally achievable.
//      FTT.Combat.GlowPresentationController duplicates this material once per
//      entity instead (Package 8 plan section 2.2).
//   2. The built-in TEXTURE sampler cannot be passed as a `sampler2D` function
//      argument — Godot's shader compiler aborts with
//      `!actions.custom_samplers.has(function->arguments[j].tex_builtin)`.
//      Every sample below is therefore written inline rather than factored into
//      a ring helper.
//   3. A CanvasItem shader cannot draw outside its own quad, so the outline is
//      clipped to the sprite rect: sprite art needs a few texels of transparent
//      padding for a thick outline to be fully visible.
shader_type canvas_item;
render_mode blend_mix;

// RGB + base opacity of the aura. Alpha 0 disables the effect entirely.
uniform vec4 outline_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
// Aura width in texels. 0 disables the effect entirely.
uniform float outline_thickness : hint_range(0.0, 5.0) = 0.0;
// Brightness multiplier applied to outline_color.
uniform float glow_intensity : hint_range(0.5, 3.0) = 1.0;
// Sinusoidal oscillation speed. 0 = static; the pulse modulates the effective
// intensity between 60% and 100% of glow_intensity.
uniform float pulse_speed : hint_range(0.0, 8.0) = 0.0;

// --- F24 ownership edge: an independent, persistent layer -------------------
// Slot-derived colour. Never written by a status/armor/spawn source.
uniform vec4 owner_outline_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
// Edge width in texels, independent of outline_thickness and of any pulse.
uniform float owner_outline_thickness : hint_range(0.0, 4.0) = 1.0;
// Mode/actor-derived. An effect expiring must never clear this.
uniform bool owner_outline_enabled = false;

const float DIAGONAL = 0.70710678;
const float TAU_CONST = 6.28318530;

void fragment() {
	// COLOR arrives as texture * modulate; keep it as the sprite's own pass.
	vec4 base = COLOR;
	if (outline_color.a <= 0.0 || outline_thickness <= 0.0) {
		COLOR = base;
	} else {
		float pulse = 1.0;
		if (pulse_speed > 0.0) {
			pulse = 0.8 + 0.2 * sin(TIME * pulse_speed * TAU_CONST);
		}
		float intensity = glow_intensity * pulse;

		vec2 texel = TEXTURE_PIXEL_SIZE;
		float r1 = outline_thickness * 0.4;
		float r2 = outline_thickness * 0.75;
		float r3 = outline_thickness;
		float d1 = r1 * DIAGONAL;
		float d2 = r2 * DIAGONAL;
		float d3 = r3 * DIAGONAL;

		// Inner ring, full weight.
		float edge = texture(TEXTURE, UV + vec2(r1, 0.0) * texel).a;
		edge = max(edge, texture(TEXTURE, UV + vec2(-r1, 0.0) * texel).a);
		edge = max(edge, texture(TEXTURE, UV + vec2(0.0, r1) * texel).a);
		edge = max(edge, texture(TEXTURE, UV + vec2(0.0, -r1) * texel).a);
		edge = max(edge, texture(TEXTURE, UV + vec2(d1, d1) * texel).a);
		edge = max(edge, texture(TEXTURE, UV + vec2(d1, -d1) * texel).a);
		edge = max(edge, texture(TEXTURE, UV + vec2(-d1, d1) * texel).a);
		edge = max(edge, texture(TEXTURE, UV + vec2(-d1, -d1) * texel).a);

		// Middle ring, reduced weight.
		float mid = texture(TEXTURE, UV + vec2(r2, 0.0) * texel).a;
		mid = max(mid, texture(TEXTURE, UV + vec2(-r2, 0.0) * texel).a);
		mid = max(mid, texture(TEXTURE, UV + vec2(0.0, r2) * texel).a);
		mid = max(mid, texture(TEXTURE, UV + vec2(0.0, -r2) * texel).a);
		mid = max(mid, texture(TEXTURE, UV + vec2(d2, d2) * texel).a);
		mid = max(mid, texture(TEXTURE, UV + vec2(d2, -d2) * texel).a);
		mid = max(mid, texture(TEXTURE, UV + vec2(-d2, d2) * texel).a);
		mid = max(mid, texture(TEXTURE, UV + vec2(-d2, -d2) * texel).a);
		edge = max(edge, mid * 0.65);

		// Outer ring, faintest weight — this is the soft radial falloff.
		float outer = texture(TEXTURE, UV + vec2(r3, 0.0) * texel).a;
		outer = max(outer, texture(TEXTURE, UV + vec2(-r3, 0.0) * texel).a);
		outer = max(outer, texture(TEXTURE, UV + vec2(0.0, r3) * texel).a);
		outer = max(outer, texture(TEXTURE, UV + vec2(0.0, -r3) * texel).a);
		outer = max(outer, texture(TEXTURE, UV + vec2(d3, d3) * texel).a);
		outer = max(outer, texture(TEXTURE, UV + vec2(d3, -d3) * texel).a);
		outer = max(outer, texture(TEXTURE, UV + vec2(-d3, d3) * texel).a);
		outer = max(outer, texture(TEXTURE, UV + vec2(-d3, -d3) * texel).a);
		edge = max(edge, outer * 0.35);

		float glow_alpha = clamp(outline_color.a * edge * intensity, 0.0, 1.0);
		vec3 glow_rgb = clamp(outline_color.rgb * intensity, vec3(0.0), vec3(1.0));

		// Sprite over glow: the aura only shows where the sprite is transparent.
		COLOR = vec4(mix(glow_rgb, base.rgb, base.a), max(base.a, glow_alpha));
	}

	// --- F24 ownership edge, composited LAST -------------------------------
	// Deliberately outside the branch above: the effect layer being absent,
	// expiring, or set to alpha 0 must leave this untouched. Static — no pulse
	// term, no glow_intensity term — so no effect can make it throb or dim.
	if (owner_outline_enabled && owner_outline_color.a > 0.0 && owner_outline_thickness > 0.0) {
		vec2 owner_texel = TEXTURE_PIXEL_SIZE;
		float ro = owner_outline_thickness;
		float dobl = ro * DIAGONAL;

		// The silhouette's outermost texels: opaque here, transparent one step
		// out in some direction. Drawing ON the rim rather than outside it keeps
		// the edge inside the sprite quad (canvas shaders cannot draw beyond it)
		// and keeps it legible over the effect aura in the padding.
		float neighbour = texture(TEXTURE, UV + vec2(ro, 0.0) * owner_texel).a;
		neighbour = min(neighbour, texture(TEXTURE, UV + vec2(-ro, 0.0) * owner_texel).a);
		neighbour = min(neighbour, texture(TEXTURE, UV + vec2(0.0, ro) * owner_texel).a);
		neighbour = min(neighbour, texture(TEXTURE, UV + vec2(0.0, -ro) * owner_texel).a);
		neighbour = min(neighbour, texture(TEXTURE, UV + vec2(dobl, dobl) * owner_texel).a);
		neighbour = min(neighbour, texture(TEXTURE, UV + vec2(dobl, -dobl) * owner_texel).a);
		neighbour = min(neighbour, texture(TEXTURE, UV + vec2(-dobl, dobl) * owner_texel).a);
		neighbour = min(neighbour, texture(TEXTURE, UV + vec2(-dobl, -dobl) * owner_texel).a);

		float own_texel_alpha = texture(TEXTURE, UV).a;
		float rim = clamp(own_texel_alpha - neighbour, 0.0, 1.0);
		float owner_alpha = clamp(owner_outline_color.a * rim, 0.0, 1.0);
		COLOR = vec4(mix(COLOR.rgb, owner_outline_color.rgb, owner_alpha),
			max(COLOR.a, owner_alpha));
	}
}
