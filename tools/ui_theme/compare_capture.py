"""Compares a UI capture with a mockup render or a spec crop (TG-THEME, Temporal Glass rebuild 2026-10-27).

Run with the py launcher (Pillow is installed for it):

    py tools/ui_theme/compare_capture.py <capture.png> <reference.png> [<reference.png> ...] [--crop x,y,w,h] [--diff]

For each reference it writes, beside the capture:

    <capture>__vs__<reference>__side.png      the two side by side at 1:1 (capture left, reference right)
    <capture>__vs__<reference>__overlay.png   the reference blended 50 % over the capture at 1:1
    <capture>__vs__<reference>__diff.png      (with --diff) the absolute difference, brightened 3x

The capture and a full render (``screens/1080p/rec_*.png``) are both 1920 x 1080 and are compared whole. A spec crop
(``glass_spec_assets/crops/<name>__from_NN_X_Y.png``) is matched to the same region of the capture: the crop's file
name carries its top-left corner (``__from_04_20_14`` is rec_04 at x 20, y 14) and its size is the crop's own.
``--crop x,y,w,h`` picks the capture's region explicitly (a component on a specimen page sits elsewhere than in the
mock); a full-size reference is cut at the same region, a crop is used whole. When the two sizes differ, both are cut
to the common top-left size. Nothing is resized: the point is a 1:1 look.

The crops and renders include the browser's backdrop blur, which the build cannot have (UI_SPEC_GLASS 1.4); read the
fills accordingly.
"""
import argparse
import os
import re
import sys

from PIL import Image, ImageChops, ImageDraw

CROP_ORIGIN = re.compile(r"__from_(\d+)_(\d+)_(\d+)$")
GAP = 16
LABEL_BAND = 26


def capture_box(capture_size, reference_path, reference_size, explicit):
    """The capture's region to compare: explicit, from a crop's file name, or the reference's size from the top left."""
    if explicit:
        x, y, w, h = explicit
        return (x, y, x + w, y + h)
    stem = os.path.splitext(os.path.basename(reference_path))[0]
    match = CROP_ORIGIN.search(stem)
    w, h = reference_size
    if match:
        x, y = int(match.group(2)), int(match.group(3))
        return (x, y, x + w, y + h)
    return (0, 0, min(w, capture_size[0]), min(h, capture_size[1]))


def labelled(image, text):
    """The image with a dark caption band on top."""
    out = Image.new("RGBA", (image.width, image.height + LABEL_BAND), (12, 14, 24, 255))
    out.paste(image, (0, LABEL_BAND))
    ImageDraw.Draw(out).text((6, 6), text, fill=(235, 230, 215, 255))
    return out


def compare(capture_path, reference_path, explicit_crop, write_diff):
    capture = Image.open(capture_path).convert("RGBA")
    reference = Image.open(reference_path).convert("RGBA")
    box = capture_box(capture.size, reference_path, reference.size, explicit_crop)
    region = capture.crop(box)
    if reference.size == capture.size:
        reference = reference.crop(box)          # a full render: the same region
    if region.size != reference.size:
        common = (min(region.width, reference.width), min(region.height, reference.height))
        region = region.crop((0, 0) + common)
        reference = reference.crop((0, 0) + common)

    base = os.path.splitext(capture_path)[0]
    tag = os.path.splitext(os.path.basename(reference_path))[0]
    stem = "%s__vs__%s" % (base, tag)

    left = labelled(region, "capture %s  %s" % (os.path.basename(capture_path), box))
    right = labelled(reference, "reference %s" % os.path.basename(reference_path))
    side = Image.new("RGBA", (left.width + GAP + right.width, max(left.height, right.height)), (12, 14, 24, 255))
    side.paste(left, (0, 0))
    side.paste(right, (left.width + GAP, 0))
    side.save(stem + "__side.png")

    overlay = Image.blend(region, reference, 0.5)
    overlay.save(stem + "__overlay.png")
    written = [stem + "__side.png", stem + "__overlay.png"]

    if write_diff:
        diff = ImageChops.difference(region.convert("RGB"), reference.convert("RGB"))
        diff = diff.point(lambda v: min(255, v * 3))
        diff.save(stem + "__diff.png")
        written.append(stem + "__diff.png")
    return written


def parse_crop(text):
    parts = [int(p) for p in text.split(",")]
    if len(parts) != 4 or parts[2] <= 0 or parts[3] <= 0:
        raise argparse.ArgumentTypeError("--crop wants x,y,w,h")
    return tuple(parts)


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("capture")
    parser.add_argument("references", nargs="+")
    parser.add_argument("--crop", type=parse_crop, default=None)
    parser.add_argument("--diff", action="store_true")
    args = parser.parse_args(argv)
    for reference in args.references:
        for path in compare(args.capture, reference, args.crop, args.diff):
            print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
