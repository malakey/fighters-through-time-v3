using System;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-MENU. The cover heroes' "ink" treatment (UI_SPEC 3.7): a solid black outline and a hard black
    /// offset shadow around a baked pose, built once from the pose's own pixels so the three roster atlases
    /// (about 45 MB each) are never loaded. The pose PNGs ship without either.
    /// </summary>
    public static class MainMenuCoverInk {

        /// <summary>The ink colour of the outline and the shadow (#0A080E).</summary>
        public static readonly Color InkColor = new(10f / 255f, 8f / 255f, 14f / 255f);

        /// <summary>
        /// Builds the inked pose. The result is larger than the source by <paramref name="pad"/> pixels on every
        /// side, so the outline and the shadow are not clipped.
        /// </summary>
        public static ImageTexture Build(Texture2D source, int outlineRadius, Vector2I shadowOffset, out int pad) {
            pad = outlineRadius + Math.Max(Math.Abs(shadowOffset.X), Math.Abs(shadowOffset.Y)) + 1;
            Image image = source.GetImage();
            if (image.IsCompressed()) image.Decompress();
            image.Convert(Image.Format.Rgba8);
            int width = image.GetWidth();
            int height = image.GetHeight();
            byte[] src = image.GetData();
            int outWidth = width + 2 * pad;
            int outHeight = height + 2 * pad;
            byte[] result = Compose(src, width, height, pad, outlineRadius, shadowOffset);
            Image output = Image.CreateFromData(outWidth, outHeight, false, Image.Format.Rgba8, result);
            output.GenerateMipmaps();
            return ImageTexture.CreateFromImage(output);
        }

        /// <summary>The pure compositor over RGBA8 buffers (separated so the maths can be tested without a texture).</summary>
        public static byte[] Compose(byte[] src, int width, int height, int pad, int radius, Vector2I shadowOffset) {
            int outWidth = width + 2 * pad;
            int outHeight = height + 2 * pad;
            var mask = new bool[outWidth * outHeight];
            var shadow = new bool[outWidth * outHeight];
            var offsets = new System.Collections.Generic.List<(int X, int Y)>();
            for (int y = -radius; y <= radius; y++) {
                for (int x = -radius; x <= radius; x++) {
                    if (x * x + y * y <= radius * radius + radius) offsets.Add((x, y));
                }
            }
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    if (src[(y * width + x) * 4 + 3] < 128) continue;
                    foreach ((int dx, int dy) in offsets) {
                        int ox = x + pad + dx;
                        int oy = y + pad + dy;
                        mask[oy * outWidth + ox] = true;
                        int sx = ox + shadowOffset.X;
                        int sy = oy + shadowOffset.Y;
                        if (sx >= 0 && sx < outWidth && sy >= 0 && sy < outHeight) shadow[sy * outWidth + sx] = true;
                    }
                }
            }
            var result = new byte[outWidth * outHeight * 4];
            byte inkR = (byte)Math.Round(InkColor.R * 255f);
            byte inkG = (byte)Math.Round(InkColor.G * 255f);
            byte inkB = (byte)Math.Round(InkColor.B * 255f);
            for (int index = 0; index < outWidth * outHeight; index++) {
                if (!mask[index] && !shadow[index]) continue;
                result[index * 4] = inkR;
                result[index * 4 + 1] = inkG;
                result[index * 4 + 2] = inkB;
                result[index * 4 + 3] = 255;
            }
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int s = (y * width + x) * 4;
                    int a = src[s + 3];
                    if (a == 0) continue;
                    int d = ((y + pad) * outWidth + (x + pad)) * 4;
                    int dstA = result[d + 3];
                    // Source over the ink (straight alpha).
                    int outA = a + dstA * (255 - a) / 255;
                    if (outA <= 0) continue;
                    for (int c = 0; c < 3; c++) {
                        int blended = (src[s + c] * a * 255 + result[d + c] * dstA * (255 - a)) / (outA * 255);
                        result[d + c] = (byte)Math.Clamp(blended, 0, 255);
                    }
                    result[d + 3] = (byte)Math.Clamp(outA, 0, 255);
                }
            }
            return result;
        }
    }
}
