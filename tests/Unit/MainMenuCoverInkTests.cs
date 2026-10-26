using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-MENU. The cover heroes' ink treatment on a plain RGBA buffer: a black outline around the opaque pixels,
/// a hard offset shadow under it, the original pixels on top, and nothing outside.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuCoverInkTests {

    private static byte[] OnePixelFigure(int size, int at) {
        var src = new byte[size * size * 4];
        int i = (at * size + at) * 4;
        src[i] = 200; src[i + 1] = 30; src[i + 2] = 40; src[i + 3] = 255;
        return src;
    }

    private static byte[] Pixel(byte[] buffer, int width, int x, int y) {
        int i = (y * width + x) * 4;
        return new[] { buffer[i], buffer[i + 1], buffer[i + 2], buffer[i + 3] };
    }

    [TestCase]
    public void ThePixelKeepsItsColourWithAnInkOutlineAndAShadowAround() {
        const int pad = 6;
        byte[] result = MainMenuCoverInk.Compose(OnePixelFigure(3, 1), 3, 3, pad, 2, new Vector2I(2, 3));
        int outWidth = 3 + 2 * pad;
        // The figure's own pixel is untouched.
        AssertThat(Pixel(result, outWidth, 1 + pad, 1 + pad)).IsEqual(new byte[] { 200, 30, 40, 255 });
        // Inside the outline radius: opaque ink, not the figure colour.
        byte[] ring = Pixel(result, outWidth, 1 + pad + 2, 1 + pad);
        AssertThat(ring[3]).IsEqual((byte)255);
        AssertThat(ring[0]).IsEqual((byte)System.Math.Round(MainMenuCoverInk.InkColor.R * 255f));
        // The shadow reaches further down and right than the outline does on the opposite side.
        AssertThat(Pixel(result, outWidth, 1 + pad + 2 + 2, 1 + pad + 3)[3]).IsEqual((byte)255);
        AssertThat(Pixel(result, outWidth, 1 + pad - 3, 1 + pad)[3]).IsEqual((byte)0);
        // Far corners stay transparent.
        AssertThat(Pixel(result, outWidth, 0, 0)[3]).IsEqual((byte)0);
    }

    [TestCase]
    public void AHalfTransparentEdgeBlendsOverTheInkAndTransparentPixelsAddNothing() {
        const int pad = 4;
        var src = new byte[2 * 1 * 4];
        src[0] = 255; src[1] = 255; src[2] = 255; src[3] = 255;
        src[4] = 255; src[5] = 255; src[6] = 255; src[7] = 100;
        byte[] result = MainMenuCoverInk.Compose(src, 2, 1, pad, 1, new Vector2I(1, 1));
        int outWidth = 2 + 2 * pad;
        byte[] edge = Pixel(result, outWidth, 1 + pad, pad);
        AssertThat(edge[3]).IsEqual((byte)255);
        // Mixed with the near-black ink behind it: darker than the white source, lighter than the ink.
        AssertThat(edge[0] > 60 && edge[0] < 255).OverrideFailureMessage("edge red " + edge[0]).IsTrue();
    }
}
