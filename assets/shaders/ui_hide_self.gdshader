shader_type canvas_item;

// TG-WIDGETS (Temporal Glass, step 2): hides a ProgressBar's own drawing under its glass tube (PulpBarDecor).
// The bar stays the value carrier, and its SelfModulate stays the HUDs' flash channel (the tube follows it).
// Children do not use their parent's material, so the tube, the value labels and the not-yet frame draw as usual.
// TG-CLOSE's empty bar styles make this redundant.
void fragment() {
	COLOR.a = 0.0;
}
